using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TechnicalSwordAction.PlayerState;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Real normal-enemy AI; old/new presentation only, with source-frame/timing evidence.</summary>
public static class AdaptiveParryCaptureUtility
{
    public static string Prepare() => PrototypeImpactCaptureUtility.Prepare();
    public static string Capture(bool legacy = false, string revision = "20261102-orange-v1")
    {
        Check(Application.isPlaying, "PlayMode required.");
        Check(!string.IsNullOrEmpty(revision) && revision.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-'), "Use an ASCII revision.");
        string folder = Path.GetFullPath(".uloop/outputs/video/adaptive-parry-" + revision + (legacy ? "/legacy" : "/adaptive"));
        Check(!Directory.Exists(folder), "Preserve earlier captures; use a new revision.");
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerStateMachine>();
        Check(player != null && player.gameObject.scene.path == PrototypeCombatSceneBuilder.ScenePath && player.CanCollectGameplayInput, "Open the prototype and call Prepare one Editor frame before Capture.");
        var arena = UnityEngine.Object.FindFirstObjectByType<PrototypeCombatArena>();
        var clock = UnityEngine.Object.FindFirstObjectByType<CombatTimeController>();
        var body = player.GetComponent<Rigidbody2D>(); var motor = player.GetComponent<PlayerMotor2D>();
        var parry = player.GetComponent<PlayerParry2D>(); var presentation = player.GetComponent<PrototypeCounterPresentation>();
        var feedback = player.GetComponent<CombatFeedback2D>(); var counter = player.GetComponent<PlayerParryCounter2D>();
        var profile = counter.CinematicProfile; bool savedTuning = profile.synchronizedParryImpact, manual = clock.ManualAdvanceOnly;
        float volume = feedback.Settings.masterVolume, shake = feedback.Settings.shake, flash = feedback.Settings.flash, vfx = feedback.Settings.vfx;
        var enemy = player.gameObject.scene.GetRootGameObjects().Single(r => r.name == "Melee_1").GetComponent<Damageable2D>();
        var ai = enemy.GetComponent<EnemyMeleeChaser2D>(); var art = enemy.GetComponent<PrototypeEnemyArtPresentation>();
        var camera = Camera.main; var previous = RenderTexture.active;
        var target = new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32); target.Create();
        var image = new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var report = new Report {legacy=legacy, cameraOrthographicSize=camera.orthographicSize, cameraAspect=camera.aspect,
            cameraPosition=camera.transform.position, configuredStopTicks=profile.parryTrialHitstopFrames,
            configuredApproachMaxFrames=profile.parryApproachMaxFrames, configuredBackgroundDim=profile.parryBackgroundDim,
            configuredActorLight=profile.parryActorLight, configuredLegacyBloom=profile.parryLegacyBloomStrength};
        var playerArt=player.GetComponent<PrototypePlayerArtPresentation>();
        var rows = new List<string>{"frame,tick,playerSprite,enemySprite,enemyMotion,successes,impactCount,phase,hitstop,backgroundAlpha,wave,enemyHp,playerHp,orangeSparks,orangeGlow,playerJitter,enemyJitter,playerRootX,enemyRootX"};
        int successes = parry.SuccessCount, impacts = presentation.ParryImpactCount;
        bool anchored=false;Vector2 playerAnchor=default,enemyAnchor=default;int lastJitterSign=0;
        void Advance() {clock.AdvanceFrame(1d/60);presentation.AdvanceVisuals(1f/60);feedback.AdvanceVisuals(1f/60);}
        try
        {
            clock.ManualAdvanceOnly = true; profile.synchronizedParryImpact = !legacy;
            feedback.Settings.masterVolume=0;feedback.Settings.shake=feedback.Settings.flash=feedback.Settings.vfx=1;
            body.position=new Vector2(0,.81f);player.transform.position=body.position;body.linearVelocity=Vector2.zero;
            motor.SetFacing(1);motor.SetMoveInput(Vector2.zero);
            enemy.ResetForCapture(new Vector3(1.15f,.81f,0),30);ai.ResetForCapture(player.transform,false);Physics2D.SyncTransforms();
            for(int warm=0;warm<180;warm++)
            {if(ai.IsWindingUp&&ai.WindupRemainingFrames<=18)break;Advance();}
            Check(ai.IsWindingUp, "Real enemy windup was not reached.");
            Directory.CreateDirectory(folder); bool requested=false;
            for(int frame=0;frame<report.frameCount;frame++)
            {
                if(!requested&&ai.IsWindingUp&&ai.WindupRemainingFrames<=2)
                {Check(player.RequestAction(PlayerActionRequest.Parry),"Parry input rejected.");requested=true;}
                Advance();
                if(report.successFrame<0&&parry.SuccessCount>successes)report.successFrame=frame;
                if(report.impactFrame<0&&presentation.ParryImpactCount>impacts)
                {report.impactFrame=frame;report.impactEnemySprite=art.Artwork.sprite.name;report.leadCombatFrames=legacy?0:presentation.ParryImpactDelayFrames;}
                if(report.successFrame>=0&&report.reactionFrame<0&&art.MotionName=="Parried")report.reactionFrame=frame;
                var light=camera.GetComponent<VideoParryBacklight>();
                report.maximumBackgroundAlpha=Mathf.Max(report.maximumBackgroundAlpha,light.BackgroundAlpha);
                report.maximumStopTicks=Mathf.Max(report.maximumStopTicks,CombatTimeController.HitstopRemainingTicks);
                if(presentation.IsOrangeParrySparkVisible)report.orangeSparkFrames++;
                if(presentation.IsOrangeParryGlowVisible)report.orangeGlowFrames++;
                report.legacyHaloSeen|=light.IsHaloVisible;
                report.maximumPlayerJitterPixels=Mathf.Max(report.maximumPlayerJitterPixels,Mathf.Abs(playerArt.ImpactOffsetX)*32);
                report.maximumEnemyJitterPixels=Mathf.Max(report.maximumEnemyJitterPixels,Mathf.Abs(art.ImpactOffsetX)*32);
                if(presentation.IsParryActorJitterActive)
                {
                    report.jitterFrames++;int sign=playerArt.ImpactOffsetX>0?1:playerArt.ImpactOffsetX<0?-1:0;
                    if(sign!=0){if(lastJitterSign!=0&&sign!=lastJitterSign)report.jitterSignChanges++;lastJitterSign=sign;}
                }
                if(CombatTimeController.IsHitStopped)
                {
                    if(!anchored){playerAnchor=body.position;enemyAnchor=enemy.transform.position;anchored=true;}
                    report.maximumStoppedPlayerRootDrift=Mathf.Max(report.maximumStoppedPlayerRootDrift,Vector2.Distance(playerAnchor,body.position));
                    report.maximumStoppedEnemyRootDrift=Mathf.Max(report.maximumStoppedEnemyRootDrift,Vector2.Distance(enemyAnchor,enemy.transform.position));
                }
                Check(Mathf.Abs(camera.orthographicSize-report.cameraOrthographicSize)<.0001f,"Parry preview must keep the real gameplay camera size.");
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1920,1080),0,0,false);image.Apply(false);
                File.WriteAllBytes(Path.Combine(folder,$"frame-{frame:D4}.png"),image.EncodeToPNG());
                rows.Add(FormattableString.Invariant($"{frame},{CombatTimeController.TickCount},{player.GetComponent<SpriteRenderer>().sprite.name},{art.Artwork.sprite.name},{art.MotionName},{parry.SuccessCount-successes},{presentation.ParryImpactCount-impacts},{presentation.ParryPresentationPhase},{CombatTimeController.HitstopRemainingTicks},{light.BackgroundAlpha},{feedback.IsParryShockwaveVisible},{enemy.CurrentHp},{player.GetComponent<PlayerDamageReceiver2D>().CurrentHp},{presentation.IsOrangeParrySparkVisible},{presentation.IsOrangeParryGlowVisible},{playerArt.ImpactOffsetX},{art.ImpactOffsetX},{body.position.x},{enemy.transform.position.x}"));
            }
            Check(parry.SuccessCount==successes+1&&presentation.ParryImpactCount==impacts+1&&enemy.HitCount==0&&enemy.CurrentHp==30&&player.GetComponent<PlayerDamageReceiver2D>().CurrentHp==100,"Expected one real parry without damage/counter.");
            Check(!presentation.IsParrySequenceActive&&!presentation.IsParryBacklit&&!feedback.IsParryShockwaveVisible&&!CombatTimeController.IsHitStopped,"Presentation residue at capture end.");
            Check(!presentation.IsParryActorJitterActive&&presentation.OrangeParryEffectCount==0&&playerArt.ImpactOffsetX==0&&art.ImpactOffsetX==0,"Additional effect/drawing-offset residue at capture end.");
            Check(legacy || report.impactEnemySprite=="EnemySample_attack_14"&&report.maximumStopTicks==profile.parryTrialHitstopFrames&&
                report.leadCombatFrames<=profile.parryApproachMaxFrames&&report.reactionFrame>report.impactFrame&&
                Mathf.Abs(report.maximumBackgroundAlpha-profile.parryBackgroundDim)<.001f,"Adaptive peak/stop/lighting invariant failed.");
            Check(legacy || !profile.parryOrangeAccent || report.orangeSparkFrames>0&&report.orangeGlowFrames>0&&
                report.jitterSignChanges>0&&report.maximumStoppedPlayerRootDrift==0&&report.maximumStoppedEnemyRootDrift==0,"Additional accent/jitter invariant failed.");
            File.WriteAllText(Path.Combine(folder,"trace.csv"),string.Join("\n",rows));
            File.WriteAllText(Path.Combine(folder,"events.json"),JsonUtility.ToJson(report,true));
            return folder;
        }
        finally
        {
            profile.synchronizedParryImpact=savedTuning;arena.ResetActionsOnly();
            feedback.Settings.masterVolume=volume;feedback.Settings.shake=shake;feedback.Settings.flash=flash;feedback.Settings.vfx=vfx;
            clock.ManualAdvanceOnly=manual;RenderTexture.active=previous;
            UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    [Serializable] private sealed class Report
    {
        public bool legacy; public int frameCount=180,fps=60,successFrame=-1,impactFrame=-1,reactionFrame=-1,leadCombatFrames,maximumStopTicks;
        public int configuredStopTicks,configuredApproachMaxFrames;
        public float duration=3,maximumBackgroundAlpha,configuredBackgroundDim,cameraOrthographicSize,cameraAspect;
        public Vector3 cameraPosition; public string impactEnemySprite;
        public int orangeSparkFrames,orangeGlowFrames,jitterFrames,jitterSignChanges;
        public bool legacyHaloSeen;
        public float configuredActorLight,configuredLegacyBloom,maximumPlayerJitterPixels,maximumEnemyJitterPixels;
        public float maximumStoppedPlayerRootDrift,maximumStoppedEnemyRootDrift;
    }
}
