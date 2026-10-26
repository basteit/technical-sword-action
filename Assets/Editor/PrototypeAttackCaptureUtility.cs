using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TechnicalSwordAction.PlayerState;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Review-only recording of the actual prototype attack clock; no gameplay/asset edits.</summary>
public static class PrototypeAttackCaptureUtility
{
    private static bool prepared, savedManual, savedPaused, withTarget;
    private static int direction;
    private static PrototypeAttackTempo savedTempo, takeTempo;

    public static string Prepare(PrototypeAttackTempo tempo, bool target, int facing)
    {
        Check(Application.isPlaying && !prepared, "Open the prototype in PlayMode; finish/cancel the previous capture first.");
        var arena = UnityEngine.Object.FindFirstObjectByType<PrototypeCombatArena>();
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerStateMachine>();
        Check(arena != null && player.gameObject.scene.path == PrototypeCombatSceneBuilder.ScenePath, "Open PrototypeCombatValidation.");
        var trial = arena.GetComponent<PrototypeCombatTrial>();
        var clock = UnityEngine.Object.FindFirstObjectByType<CombatTimeController>();
        savedManual = clock.ManualAdvanceOnly; savedPaused = EditorApplication.isPaused; savedTempo = trial.AttackTempo;
        takeTempo = tempo; withTarget = target; direction = facing < 0 ? -1 : 1; prepared = true;
        try
        {
            clock.ManualAdvanceOnly = true;
            trial.SetAttackTempo(tempo); arena.ResetActionsOnly();
            var body = player.GetComponent<Rigidbody2D>();
            body.position = new Vector2(0, .81f); player.transform.position = body.position; body.linearVelocity = Vector2.zero;
            var motor = player.GetComponent<PlayerMotor2D>(); motor.SetMoveInput(Vector2.zero); motor.SetFacing(direction);
            if (target)
            {
                var health = player.gameObject.scene.GetRootGameObjects().Single(root => root.name == "Melee_1").GetComponent<Damageable2D>();
                health.ResetForCapture(new Vector3(direction * 1.8f, .8f), 1000);
                health.GetComponent<EnemyMeleeChaser2D>().ResetForCapture(player.transform, true);
                health.AcquireMotionControl(arena); // Stationary review target only; original HP/AI data are untouched.
            }
            Physics2D.SyncTransforms();
            for (int frame = 0; frame < 3; frame++) clock.AdvanceFrame(1d / 60);
            EditorApplication.isPaused = true; EditorApplication.Step();
            return "Prepared " + tempo + (target ? " hit" : " whiff") + "; capture after the next rendered frame.";
        }
        catch { Cancel(); throw; }
    }

    public static string Capture()
    {
        Check(Application.isPlaying && prepared, "Call Prepare first.");
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerStateMachine>();
        Check(player.CanCollectGameplayInput, "Wait one rendered frame after Prepare; do not bypass the reset input guard.");
        var clock = UnityEngine.Object.FindFirstObjectByType<CombatTimeController>();
        var attack = player.GetComponent<PlayerAttack2D>(); var motor = player.GetComponent<PlayerMotor2D>();
        var feedback = player.GetComponent<CombatFeedback2D>(); var art = player.GetComponent<PrototypePlayerArtPresentation>();
        var health = withTarget ? GameObject.Find("Melee_1").GetComponent<Damageable2D>() : null;
        string key = (takeTempo == PrototypeAttackTempo.SnappyFirstTwo ? "snappy" : "previous-fast") +
            (withTarget ? "-hit" : "-whiff") + (direction < 0 ? "-left" : "-right");
        string folder = Path.GetFullPath(".uloop/outputs/video/prototype-attack-review-20261026/" + key);
        Check(!Directory.Exists(folder), "Preserve existing takes; use a fresh output folder.");
        Directory.CreateDirectory(folder);
        var render = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); render.Create();
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        var previousActive = RenderTexture.active;
        bool previousAudioPause = AudioListener.pause; AudioListener.pause = true;
        var marks = new List<Mark>(); int pairs = 0, chains = 0; bool chainQueued = false;
        var rows = new List<string> { "frame,step,attackFrame,phase,playerX,targetX,targetHp,targetHits,sprite,hitstop" };
        try
        {
            for (int frame = 0; frame < 180; frame++)
            {
                // Two ordinary attack press pairs, identical schedule in the old/new comparisons.
                if (frame == 18 || frame == 102)
                {
                    motor.SetFacing(direction);
                    Check(player.RequestAction(PlayerActionRequest.Attack), "First attack request rejected.");
                    pairs++; chainQueued = false; marks.Add(new Mark { frame = frame, kind = "Attack1Input" });
                }
                if (!chainQueued && attack.ComboStep == 1 && attack.Phase == AttackPhase.Cancel)
                {
                    Check(player.RequestAction(PlayerActionRequest.Attack), "Second attack request rejected.");
                    chainQueued = true; chains++; marks.Add(new Mark { frame = frame, kind = "Attack2Input" });
                }
                clock.AdvanceFrame(1d / 60);
                feedback.AdvanceVisuals(1f / 60); art.RefreshVisual();
                RenderPipeline.SubmitRenderRequest(Camera.main, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false); image.Apply(false);
                File.WriteAllBytes(Path.Combine(folder, $"frame-{frame:D4}.png"), image.EncodeToPNG());
                rows.Add(FormattableString.Invariant($"{frame},{attack.ComboStep},{attack.ElapsedFrames},{attack.Phase},{player.transform.position.x},{(health != null ? health.transform.position.x : 0)},{(health != null ? health.CurrentHp : 0)},{(health != null ? health.HitCount : 0)},{art.Artwork.sprite.name},{CombatTimeController.HitstopRemainingTicks}"));
            }
            Check(pairs == 2 && chains == 2 && !attack.IsAttacking && !attack.IsRecovering, "Expected two finished 1/2 attack pairs.");
            Check(!withTarget || (health.HitCount == 4 && health.CurrentHp == 960), "Expected four ordinary 10-damage hits.");
            File.WriteAllText(Path.Combine(folder, "trace.csv"), string.Join("\n", rows));
            File.WriteAllText(Path.Combine(folder, "events.json"), JsonUtility.ToJson(new Report
            {
                mode = takeTempo.ToString(), target = withTarget, facing = direction, attack1 = attack.Definition.steps[0].activeStart,
                attack2 = attack.Definition.steps[1].activeStart, targetHits = health != null ? health.HitCount : 0,
                targetHp = health != null ? health.CurrentHp : 0, events = marks.ToArray()
            }, true));
            return folder;
        }
        finally
        {
            RenderTexture.active = previousActive; AudioListener.pause = previousAudioPause;
            UnityEngine.Object.DestroyImmediate(image); render.Release(); UnityEngine.Object.DestroyImmediate(render);
            Cancel();
        }
    }

    public static void Cancel()
    {
        if (!prepared) return;
        prepared = false;
        if (Application.isPlaying)
        {
            var arena = UnityEngine.Object.FindFirstObjectByType<PrototypeCombatArena>();
            arena.GetComponent<PrototypeCombatTrial>().SetAttackTempo(savedTempo); arena.ResetActionsOnly();
            UnityEngine.Object.FindFirstObjectByType<CombatTimeController>().ManualAdvanceOnly = savedManual;
        }
        EditorApplication.isPaused = savedPaused;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Mark { public int frame; public string kind; }
    [Serializable] private sealed class Report
    {
        public int frameCount = 180, fps = 60, facing, attack1, attack2, targetHits, targetHp;
        public double duration = 3;
        public string mode, note = "Real prototype 1/2 attacks at 1x speed; stationary high-HP review target, no parry/counter.";
        public bool target;
        public Mark[] events;
    }
}
