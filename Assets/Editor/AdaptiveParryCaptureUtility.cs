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
    public static string CaptureCounterSequence() => Capture(false, "20261103-hitstop-return-v1", true);
    public static string Capture(bool legacy = false, string revision = "20261103-hitstop-return-v1", bool counterSequence = false)
    {
        Check(Application.isPlaying, "PlayMode required.");
        Check(!string.IsNullOrEmpty(revision) && revision.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-'), "Use an ASCII revision.");
        Check(!legacy || !counterSequence, "Counter recording uses the current synchronized presentation.");
        string folder = Path.GetFullPath(".uloop/outputs/video/adaptive-parry-" + revision + (counterSequence ? "/counter" : legacy ? "/legacy" : "/adaptive"));
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
        var launch = enemy.GetComponent<EnemyCounterLaunch2D>();
        var camera = Camera.main; var previous = RenderTexture.active;
        var target = new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32); target.Create();
        var image = new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var report = new Report {legacy=legacy,counterSequence=counterSequence,frameCount=counterSequence?300:180,duration=counterSequence?5:3,
            cameraOrthographicSize=camera.orthographicSize, cameraAspect=camera.aspect,
            cameraPosition=camera.transform.position, configuredStopTicks=profile.parryTrialHitstopFrames,
            configuredApproachMaxFrames=profile.parryApproachMaxFrames, configuredBackgroundDim=profile.parryBackgroundDim,
            configuredActorLight=profile.parryActorLight, configuredLegacyBloom=profile.parryLegacyBloomStrength,
            minimumCameraSize=camera.orthographicSize, configuredGlowGain=profile.parryGlowGain,
            configuredGlowAlpha=profile.parryGlowColor.a, configuredGlowRadius=profile.parryGlowRadius,
            configuredExpressiveCamera=profile.parryExpressiveCamera,configuredReferenceAccent=profile.parryReferenceAccent,
            configuredArcHeight=profile.parryCameraArcHeight,configuredPositionLeadFrames=profile.parryCameraPositionLeadFrames,
            configuredZoomDelayFrames=profile.parryCameraZoomDelayFrames,configuredFlareLength=profile.parryFlareLength,
            configuredReferenceSparkRadius=profile.parryReferenceSparkRadius,configuredFlareSeconds=profile.parryFlareSeconds,
            configuredReturnOnHitstopEnd=profile.parryCameraReturnOnHitstopEnd};
        var playerArt=player.GetComponent<PrototypePlayerArtPresentation>();
        var rows = new List<string>{"frame,tick,playerSprite,enemySprite,enemyMotion,successes,impactCount,phase,hitstop,backgroundAlpha,wave,enemyHp,playerHp,orangeSparks,orangeGlow,playerJitter,enemyJitter,playerRootX,enemyRootX,cameraSize,cameraX,cameraY,cameraPhase,referenceFlare,particles,cameraUnshakenX,cameraUnshakenY,counterPhase,counterFrame,counterLight,enemyLaunch,enemyTravel,slowScale,playerAction,enemyHits"};
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
            Directory.CreateDirectory(folder); bool requested=false,requestedCounter=false;
            for(int frame=0;frame<report.frameCount;frame++)
            {
                if(!requested&&ai.IsWindingUp&&ai.WindupRemainingFrames<=2)
                {Check(player.RequestAction(PlayerActionRequest.Parry),"Parry input rejected.");requested=true;}
                if(counterSequence&&!requestedCounter&&report.successFrame>=0&&!presentation.IsParrySequenceActive&&player.CanCollectGameplayInput)
                {Check(player.RequestAction(PlayerActionRequest.Attack),"Counter input rejected.");requestedCounter=true;report.counterInputFrame=frame;}
                Advance();
                if(report.successFrame<0&&parry.SuccessCount>successes)report.successFrame=frame;
                if(report.impactFrame<0&&presentation.ParryImpactCount>impacts)
                {report.impactFrame=frame;report.impactEnemySprite=art.Artwork.sprite.name;report.leadCombatFrames=legacy?0:presentation.ParryImpactDelayFrames;report.impactCameraSize=camera.orthographicSize;}
                if(report.successFrame>=0&&report.reactionFrame<0&&art.MotionName=="Parried")report.reactionFrame=frame;
                if(report.impactFrame>=0&&report.firstHitstopReleaseFrame<0&&!CombatTimeController.IsHitStopped)report.firstHitstopReleaseFrame=frame;
                if(counter.IsActive&&report.counterStartFrame<0)
                {report.counterStartFrame=frame;report.counterInheritedCamera=feedback.IsCameraFocusOwnedBy(presentation);report.counterDamage=counter.SelectedDamage;}
                if(presentation.IsBodyHidden&&report.counterBlinkFrame<0)report.counterBlinkFrame=frame;
                if(presentation.IsCharged&&report.counterChargeFrame<0)report.counterChargeFrame=frame;
                if(counter.IsActive&&counter.ElapsedFrames>=profile.releaseFrame&&report.counterReleaseFrame<0)report.counterReleaseFrame=frame;
                if(enemy.HitCount>0&&report.counterHitFrame<0)report.counterHitFrame=frame;
                if(report.counterStartFrame>=0&&!counter.IsActive&&report.counterFinishedFrame<0)report.counterFinishedFrame=frame;
                if(report.counterHitFrame>=0)
                {
                    report.maximumCounterStopTicks=Mathf.Max(report.maximumCounterStopTicks,CombatTimeController.HitstopRemainingTicks);
                    report.counterLaunchTravel=Mathf.Max(report.counterLaunchTravel,launch.TravelDistance);
                    if(!launch.IsActive&&report.counterLaunchEndFrame<0)report.counterLaunchEndFrame=frame;
                }
                var light=camera.GetComponent<VideoParryBacklight>();
                report.maximumBackgroundAlpha=Mathf.Max(report.maximumBackgroundAlpha,light.BackgroundAlpha);
                report.maximumStopTicks=Mathf.Max(report.maximumStopTicks,CombatTimeController.HitstopRemainingTicks);
                if(presentation.IsOrangeParrySparkVisible)report.orangeSparkFrames++;
                if(presentation.IsOrangeParryGlowVisible)report.orangeGlowFrames++;
                if(presentation.IsParryFlareVisible)report.referenceFlareFrames++;
                report.peakParryParticles=Mathf.Max(report.peakParryParticles,presentation.ParryParticleCount);
                report.legacyHaloSeen|=light.IsHaloVisible;
                report.maximumPlayerJitterPixels=Mathf.Max(report.maximumPlayerJitterPixels,Mathf.Abs(playerArt.ImpactOffsetX)*32);
                report.maximumEnemyJitterPixels=Mathf.Max(report.maximumEnemyJitterPixels,Mathf.Abs(art.ImpactOffsetX)*32);
                if(presentation.IsParryActorJitterActive)
                {
                    report.jitterFrames++;int sign=playerArt.ImpactOffsetX>0?1:playerArt.ImpactOffsetX<0?-1:0;
                    if(sign!=0){if(lastJitterSign!=0&&sign!=lastJitterSign)report.jitterSignChanges++;lastJitterSign=sign;}
                }
                if(CombatTimeController.IsHitStopped&&report.firstHitstopReleaseFrame<0)
                {
                    if(!anchored){playerAnchor=body.position;enemyAnchor=enemy.transform.position;anchored=true;}
                    report.maximumStoppedPlayerRootDrift=Mathf.Max(report.maximumStoppedPlayerRootDrift,Vector2.Distance(playerAnchor,body.position));
                    report.maximumStoppedEnemyRootDrift=Mathf.Max(report.maximumStoppedEnemyRootDrift,Vector2.Distance(enemyAnchor,enemy.transform.position));
                }
                report.minimumCameraSize=Mathf.Min(report.minimumCameraSize,camera.orthographicSize);
                report.maximumCameraShake=Mathf.Max(report.maximumCameraShake,feedback.ShakeOffset.magnitude);
                if(report.cameraPreparationFrame<0&&presentation.ParryCameraPhase=="Preparation")report.cameraPreparationFrame=frame;
                if(report.cameraHoldFrame<0&&presentation.ParryCameraPhase=="Hold")report.cameraHoldFrame=frame;
                if(report.cameraReturnFrame<0&&presentation.ParryCameraPhase=="Returning")report.cameraReturnFrame=frame;
                if(report.cameraRestoredFrame<0&&report.cameraReturnFrame>=0&&!presentation.IsParryCameraActive&&!feedback.HasCameraFocus)report.cameraRestoredFrame=frame;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1920,1080),0,0,false);image.Apply(false);
                File.WriteAllBytes(Path.Combine(folder,$"frame-{frame:D4}.png"),image.EncodeToPNG());
                rows.Add(FormattableString.Invariant($"{frame},{CombatTimeController.TickCount},{player.GetComponent<SpriteRenderer>().sprite.name},{art.Artwork.sprite.name},{art.MotionName},{parry.SuccessCount-successes},{presentation.ParryImpactCount-impacts},{presentation.ParryPresentationPhase},{CombatTimeController.HitstopRemainingTicks},{light.BackgroundAlpha},{feedback.IsParryShockwaveVisible},{enemy.CurrentHp},{player.GetComponent<PlayerDamageReceiver2D>().CurrentHp},{presentation.IsOrangeParrySparkVisible},{presentation.IsOrangeParryGlowVisible},{playerArt.ImpactOffsetX},{art.ImpactOffsetX},{body.position.x},{enemy.transform.position.x},{camera.orthographicSize},{camera.transform.position.x},{camera.transform.position.y},{presentation.ParryCameraPhase},{presentation.IsParryFlareVisible},{presentation.ParryParticleCount},{camera.transform.position.x-feedback.ShakeOffset.x},{camera.transform.position.y-feedback.ShakeOffset.y},{counter.CinematicPhase},{counter.ElapsedFrames},{presentation.CounterLightPhase},{launch.IsActive},{launch.TravelDistance},{CombatTimeController.SlowScale},{player.ActionState},{enemy.HitCount}"));
            }
            report.finalEnemyHp=enemy.CurrentHp;report.finalPlayerHp=player.GetComponent<PlayerDamageReceiver2D>().CurrentHp;report.enemyDamageHits=enemy.HitCount;
            Check(parry.SuccessCount==successes+1&&presentation.ParryImpactCount==impacts+1&&report.finalPlayerHp==100,"Expected one real parry without player damage.");
            Check(counterSequence?requestedCounter&&report.counterDamage==90&&report.enemyDamageHits==1&&report.finalEnemyHp==0:
                report.enemyDamageHits==0&&report.finalEnemyHp==30,"Unexpected counter damage or duplicate hit.");
            Check(!presentation.IsParrySequenceActive&&!presentation.IsParryBacklit&&!feedback.IsParryShockwaveVisible&&!CombatTimeController.IsHitStopped,"Presentation residue at capture end.");
            Check(!presentation.IsParryActorJitterActive&&presentation.OrangeParryEffectCount==0&&playerArt.ImpactOffsetX==0&&art.ImpactOffsetX==0,"Additional effect/drawing-offset residue at capture end.");
            Check(!presentation.IsParryCameraActive&&!feedback.HasCameraFocus&&Mathf.Abs(camera.orthographicSize-report.cameraOrthographicSize)<.0001f,"Camera residue at capture end.");
            Check(!counter.IsActive&&!launch.IsActive&&CombatTimeController.SlowScale==1,"Counter/launch/time residue at capture end.");
            Check(legacy||!profile.parryCameraReturnOnHitstopEnd||report.cameraReturnFrame==report.firstHitstopReleaseFrame,"Camera must return on the first hitstop release frame.");
            Check(!counterSequence||report.counterInheritedCamera&&report.counterHitFrame>report.counterChargeFrame&&report.counterLaunchEndFrame>report.counterHitFrame&&report.counterFinishedFrame>report.counterHitFrame,"Counter sequence or camera handoff missing.");
            report.playerSortingOrder=playerArt.Artwork.sortingOrder;report.enemySortingOrder=art.Artwork.sortingOrder;
            Check(report.playerSortingOrder>report.enemySortingOrder,"Player must render ahead of the enemy body.");
            Check(legacy || report.impactEnemySprite=="EnemySample_attack_14"&&report.maximumStopTicks==profile.parryTrialHitstopFrames&&
                report.leadCombatFrames<=profile.parryApproachMaxFrames&&report.reactionFrame>report.impactFrame&&
                Mathf.Abs(report.maximumBackgroundAlpha-profile.parryBackgroundDim)<.001f,"Adaptive peak/stop/lighting invariant failed.");
            Check(legacy || !profile.parryOrangeAccent || report.orangeSparkFrames>0&&report.orangeGlowFrames>0&&
                report.jitterSignChanges>0&&report.maximumStoppedPlayerRootDrift==0&&report.maximumStoppedEnemyRootDrift==0,"Additional accent/jitter invariant failed.");
            Check(legacy || !profile.parryReferenceAccent || report.referenceFlareFrames>0&&report.peakParryParticles==32,"Reference accent invariant failed.");
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
        public int cameraPreparationFrame=-1,cameraHoldFrame=-1,cameraReturnFrame=-1,cameraRestoredFrame=-1,playerSortingOrder,enemySortingOrder;
        public float minimumCameraSize,impactCameraSize,maximumCameraShake,configuredGlowGain,configuredGlowAlpha,configuredGlowRadius;
        public bool configuredExpressiveCamera,configuredReferenceAccent;
        public int referenceFlareFrames,peakParryParticles,configuredPositionLeadFrames,configuredZoomDelayFrames;
        public float configuredArcHeight,configuredFlareLength,configuredReferenceSparkRadius,configuredFlareSeconds;
        public bool counterSequence,configuredReturnOnHitstopEnd,counterInheritedCamera;
        public int firstHitstopReleaseFrame=-1,counterInputFrame=-1,counterStartFrame=-1,counterBlinkFrame=-1,counterChargeFrame=-1,
            counterReleaseFrame=-1,counterHitFrame=-1,counterLaunchEndFrame=-1,counterFinishedFrame=-1,counterDamage,maximumCounterStopTicks,
            finalEnemyHp,finalPlayerHp,enemyDamageHits;
        public float counterLaunchTravel;
    }
}
