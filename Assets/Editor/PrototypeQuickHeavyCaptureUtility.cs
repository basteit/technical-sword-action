using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TechnicalSwordAction.PlayerState;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Records real prototype AI/inputs through the unchanged gameplay camera for rough review.</summary>
public static class PrototypeQuickHeavyCaptureUtility
{
    private static bool prepared, preparedManual;
    public static string Prepare(bool heavy = false)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("PlayMode required.");
        var clock = UnityEngine.Object.FindFirstObjectByType<CombatTimeController>();
        if (!prepared) preparedManual = clock.ManualAdvanceOnly;
        prepared = true; clock.ManualAdvanceOnly = true;
        var arena = UnityEngine.Object.FindFirstObjectByType<PrototypeCombatArena>();
        if (heavy) arena.ResetTrainingEncounter(true); else arena.ResetArena(false);
        return "Prepared; capture after the next editor frame.";
    }
    public static string Capture(bool heavy, bool performCounter = true)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("PlayMode required.");
        var arena = UnityEngine.Object.FindFirstObjectByType<PrototypeCombatArena>();
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerStateMachine>();
        var clock = UnityEngine.Object.FindFirstObjectByType<CombatTimeController>();
        if (!player.CanCollectGameplayInput) throw new InvalidOperationException("Wait one editor frame after Prepare.");
        var counter = player.GetComponent<PlayerParryCounter2D>();
        var presentation = player.GetComponent<PrototypeCounterPresentation>();
        var feedback = player.GetComponent<CombatFeedback2D>();
        var parry = player.GetComponent<PlayerParry2D>(); var motor = player.GetComponent<PlayerMotor2D>();
        var receiver = player.GetComponent<PlayerDamageReceiver2D>(); var body = player.GetComponent<Rigidbody2D>();
        var camera = Camera.main; bool manual = prepared ? preparedManual : clock.ManualAdvanceOnly;
        float volume = feedback.Settings.masterVolume;
        var render = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); render.Create();
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var oldActive = RenderTexture.active;
        string folder = Path.GetFullPath(".uloop/outputs/quick-heavy-tuning-20261117-v2/" + (heavy ? "heavy" : "quick") + (performCounter ? "" : "-idle"));
        Directory.CreateDirectory(folder);
        var rows = new List<string> { "tick,seconds,playerSprite,enemySprite,enemyState,enemyHp,enemyHits,parryWeight,parryPhase,counterPhase,hitstop,slowScale,playerX,playerY,playerHp,counterElapsed,sheatheFrame,hitFrame,actorOffsetX,playerAction" };
        int saved = 0, ticks = 0, counterEndedAt = -1;
        try
        {
            clock.ManualAdvanceOnly = true; feedback.Settings.masterVolume = 0;
            receiver.ResetHealth();
            // Prepare selected/reset the encounter on the previous editor frame.
            counter.TrySetQuickCounter(true);
            var enemies = player.gameObject.scene.GetRootGameObjects().Select(root => root.GetComponent<EnemyMeleeChaser2D>()).Where(enemy => enemy != null).ToArray();
            var target = heavy ? enemies.Single(enemy => enemy.GetComponent<Damageable2D>().HasInfiniteHealth) : enemies.Single(enemy => enemy.name == "Melee_1");
            foreach (var other in enemies.Where(enemy => enemy != target)) other.gameObject.SetActive(false);
            body.position = new Vector2(0, .81f); player.transform.position = body.position; body.linearVelocity = Vector2.zero;
            motor.SetFacing(1); motor.SetMoveInput(Vector2.zero);
            var health = target.GetComponent<Damageable2D>(); health.ResetForCapture(new Vector3(1.15f, .81f), 120);
            target.ResetForCapture(player.transform, false); Physics2D.SyncTransforms();
            int initialParries = parry.SuccessCount, initialStarts = counter.StartCount, initialHits = counter.HitCount;
            bool requestedParry = false, requestedCounter = false;
            for (; ticks < 300; ticks++)
            {
                if (!requestedParry && target.IsWindingUp && target.WindupRemainingFrames <= 3)
                {
                    if (player.RequestAction(PlayerActionRequest.Parry)) requestedParry = true;
                }
                if (performCounter && !requestedCounter && parry.SuccessCount > initialParries && player.ActionState == PlayerActionState.Neutral)
                {
                    if (player.RequestAction(PlayerActionRequest.Attack)) requestedCounter = true;
                }
                clock.AdvanceFrame(1d / 60); presentation.AdvanceVisuals(1f / 60); feedback.AdvanceVisuals(1f / 60);
                player.GetComponent<PrototypePlayerArtPresentation>().RefreshVisual();
                var enemyDrawing = target.GetComponent<PrototypeEnemyArtPresentation>();
                rows.Add(FormattableString.Invariant($"{ticks},{ticks / 60d},{player.GetComponent<SpriteRenderer>().sprite.name},{enemyDrawing?.Artwork.sprite.name},{target.CurrentStateName},{health.CurrentHp},{health.HitCount},{parry.LastReactionWeight},{presentation.ParryPresentationPhase},{counter.CinematicPhase},{CombatTimeController.HitstopRemainingTicks},{CombatTimeController.SlowScale},{body.position.x},{body.position.y},{receiver.CurrentHp},{counter.ElapsedFrames},{counter.QuickProfile.sheatheFrame},{counter.HitFrame},{presentation.ParryActorJitterX},{player.ActionState}"));
                if (ticks % 2 == 0)
                {
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
                    RenderTexture.active = render; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0, false); image.Apply(false);
                    File.WriteAllBytes(Path.Combine(folder, $"frame-{saved++:D4}.png"), image.EncodeToPNG());
                }
                if (counter.StartCount > initialStarts && !counter.IsActive && counterEndedAt < 0) counterEndedAt = ticks;
                if (!performCounter && parry.SuccessCount > initialParries && !presentation.IsParrySequenceActive &&
                    player.ActionState == PlayerActionState.Neutral && counterEndedAt < 0) counterEndedAt = ticks;
                if (counterEndedAt >= 0 && ticks >= counterEndedAt + 24) break;
            }
            File.WriteAllText(Path.Combine(folder, "trace.csv"), string.Join("\n", rows));
            if (!requestedParry || (performCounter && !requestedCounter) || counter.HitCount != initialHits + (performCounter ? 1 : 0) ||
                receiver.CurrentHp != receiver.MaxHp || counter.IsActive || counterEndedAt < 0)
                throw new InvalidOperationException($"Review recording incomplete: parry={requestedParry}, counter={requestedCounter}, hits={counter.HitCount-initialHits}, playerHp={receiver.CurrentHp}, active={counter.IsActive}; inspect trace.csv.");
            return folder;
        }
        finally
        {
            RenderTexture.active = oldActive; UnityEngine.Object.DestroyImmediate(image); render.Release(); UnityEngine.Object.DestroyImmediate(render);
            arena.ResetActionsOnly(); feedback.Settings.masterVolume = volume; clock.ManualAdvanceOnly = manual; prepared = false;
        }
    }
}
