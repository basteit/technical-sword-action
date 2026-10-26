using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TechnicalSwordAction.PlayerState;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Review recording of real enemy AI and gameplay actions, without a filming director.</summary>
public static class PrototypeImpactCaptureUtility
{
    public static string Prepare()
    {
        Check(Application.isPlaying,"Open the saved prototype in PlayMode.");
        UnityEngine.Object.FindFirstObjectByType<PrototypeCombatArena>().ResetActionsOnly();
        return "Prepared. Capture on the next editor frame, after the reset input guard.";
    }
    public static string Capture()
    {
        Check(Application.isPlaying,"PlayMode required.");
        var arena=UnityEngine.Object.FindFirstObjectByType<PrototypeCombatArena>();
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerStateMachine>();
        var clock=UnityEngine.Object.FindFirstObjectByType<CombatTimeController>();
        Check(arena!=null&&player!=null&&player.gameObject.scene.path==PrototypeCombatSceneBuilder.ScenePath,"Open PrototypeCombatValidation.");
        Check(player.CanCollectGameplayInput,"Call Prepare, then wait one editor frame before Capture.");
        var body=player.GetComponent<Rigidbody2D>();
        var motor=player.GetComponent<PlayerMotor2D>();var counter=player.GetComponent<PlayerParryCounter2D>();
        var parry=player.GetComponent<PlayerParry2D>();var receiver=player.GetComponent<PlayerDamageReceiver2D>();
        var presentation=player.GetComponent<PrototypeCounterPresentation>();var feedback=player.GetComponent<CombatFeedback2D>();
        var target=player.gameObject.scene.GetRootGameObjects().Single(root=>root.name=="StrongMelee").GetComponent<Damageable2D>();
        var ai=target.GetComponent<EnemyMeleeChaser2D>();var camera=Camera.main;
        bool manual=clock.ManualAdvanceOnly;
        float volume=feedback.Settings.masterVolume,shake=feedback.Settings.shake,flash=feedback.Settings.flash,vfx=feedback.Settings.vfx;
        var previousActive=RenderTexture.active;var render=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);render.Create();
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        string folder=Path.GetFullPath(".uloop/outputs/video/prototype-impact");Directory.CreateDirectory(folder);
        var rows=new List<string>{"frame,seconds,playerSprite,enemyState,enemyHp,enemyHits,parryImpacts,parryBacklit,counterPhase,counterLight,camera,shakeX,shakeY,hitstop,slowScale"};
        int frame=0;bool requestedParry=false,requestedCounter=false,started=false;int initialParries=parry.SuccessCount;
        try
        {
            // This synchronous render loop never runs an InputRouter.Update between samples.
            // Disabling that router here would intentionally suppress input for this editor frame.
            clock.ManualAdvanceOnly=true;
            feedback.Settings.masterVolume=0;feedback.Settings.shake=feedback.Settings.flash=feedback.Settings.vfx=1;
            body.position=new Vector2(0,.81f);player.transform.position=body.position;body.linearVelocity=Vector2.zero;
            motor.SetFacing(1);motor.SetMoveInput(Vector2.zero);
            target.ResetForCapture(new Vector3(1.15f,.81f,0),120);ai.ResetForCapture(player.transform,false);Physics2D.SyncTransforms();
            while(frame<330)
            {
                if(!requestedParry&&ai.IsWindingUp&&ai.AttackKind=="Parryable"&&ai.WindupRemainingFrames<=2)
                {Check(player.RequestAction(PlayerActionRequest.Parry),"Parry request rejected.");requestedParry=true;}
                if(!requestedCounter&&parry.SuccessCount>initialParries&&player.ActionState==PlayerActionState.Neutral&&player.CanCollectGameplayInput)
                {Check(player.RequestAction(PlayerActionRequest.Attack),"Counter request rejected.");requestedCounter=true;}
                clock.AdvanceFrame(1d/60);presentation.AdvanceVisuals(1f/60);feedback.AdvanceVisuals(1f/60);
                started|=counter.IsActive;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=render});
                RenderTexture.active=render;image.ReadPixels(new Rect(0,0,1920,1080),0,0,false);image.Apply(false);
                File.WriteAllBytes(Path.Combine(folder,$"frame-{frame:D4}.png"),image.EncodeToPNG());frame++;
                rows.Add(FormattableString.Invariant($"{frame},{frame/60d},{player.GetComponent<SpriteRenderer>().sprite.name},{ai.CurrentStateName},{target.CurrentHp},{target.HitCount},{presentation.ParryImpactCount},{presentation.IsParryBacklit},{counter.CinematicPhase},{presentation.CounterLightPhase},{camera.orthographicSize},{feedback.ShakeOffset.x},{feedback.ShakeOffset.y},{CombatTimeController.HitstopRemainingTicks},{CombatTimeController.SlowScale}"));
                if(frame>=210&&started&&!counter.IsActive&&presentation.CounterLightPhase=="None"&&!feedback.HasCameraFocus)break;
            }
            Check(started&&requestedCounter&&parry.SuccessCount==initialParries+1&&target.HitCount==1&&target.CurrentHp==30&&receiver.CurrentHp==receiver.MaxHp,"Expected one real parry and one 90-damage counter.");
            Check(!counter.IsActive&&!feedback.HasCameraFocus&&CombatTimeController.SlowScale==1,"Capture did not restore gameplay state.");
            File.WriteAllText(Path.Combine(folder,"trace.csv"),string.Join("\n",rows));
            File.WriteAllText(Path.Combine(folder,"events.json"),JsonUtility.ToJson(new Report{frameCount=frame,duration=frame/60d,enemyHp=target.CurrentHp,playerHp=receiver.CurrentHp},true));
            return folder;
        }
        finally
        {
            RenderTexture.active=previousActive;UnityEngine.Object.DestroyImmediate(image);render.Release();UnityEngine.Object.DestroyImmediate(render);
            arena.ResetActionsOnly();feedback.Settings.masterVolume=volume;feedback.Settings.shake=shake;feedback.Settings.flash=flash;feedback.Settings.vfx=vfx;
            clock.ManualAdvanceOnly=manual;
        }
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    [Serializable]private sealed class Report
    {public int frameCount,fps=60,enemyHp,playerHp;public double duration;public string takeKind="prototype-real-ai-parry-and-counter";}
}
