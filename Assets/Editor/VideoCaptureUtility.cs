using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TechnicalSwordAction.PlayerState;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class VideoCaptureUtility
{
    [Serializable] private sealed class RunRecord { public int stages;public bool mirrored;public double duration;public int frontHits,rearHits;public float chargeSeconds; }
    [Serializable] private sealed class ValidationReport { public int runsPassed;public RunRecord[] runs; }
    private static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    public static string ValidateResetRuns()
    {
        Check(Application.isPlaying,"PlayMode required.");
        var director=UnityEngine.Object.FindFirstObjectByType<CombatVideoDirector>();
        var clock=UnityEngine.Object.FindFirstObjectByType<CombatTimeController>();
        var camera=Camera.main;var fx=UnityEngine.Object.FindFirstObjectByType<VideoVfxRenderer>();
        var presentation=UnityEngine.Object.FindFirstObjectByType<VideoCombatPresentation>();
        var state=director.Player;var counter=state.GetComponent<PlayerParryCounter2D>();
        bool previousManual=clock.ManualAdvanceOnly;clock.ManualAdvanceOnly=true;
        var records=new List<RunRecord>();
        try
        {
            foreach(int stages in new[]{2,4}) foreach(bool mirrored in new[]{false,true}) for(int run=0;run<10;run++)
            {
                director.MirrorLayout=mirrored;int before=counter.HitCount;
                director.StartTake(stages,run%2==0);
                for(int frame=0;frame<360&&!director.Completed;frame++) director.StepCapture(1d/60d);
                Check(director.Completed,$"{stages} stages, mirror {mirrored}, run {run}: take did not finish. " + string.Join(",",director.Marks.Select(m=>m.kind+":"+m.target)));
                Check(director.RealElapsed>=3&&director.RealElapsed<=6,"Duration outside 3–6 seconds.");
                Check(director.FrontEnemy.HitCount==stages&&director.FrontEnemy.DeathCount==1,"Front enemy duplicate/missing hit or death.");
                Check(director.RearEnemy.HitCount==1&&director.RearEnemy.DeathCount==1&&counter.HitCount-before==1,"Counter did not hit exactly once.");
                Check(director.Marks.Single(m=>m.kind=="Parry").target==director.Marks.Single(m=>m.kind=="Kick").target,"Counter target differs from parried source.");
                Check(state.ActionState==PlayerActionState.Neutral&&!counter.IsCounterInvincible,"Player remained locked/invincible.");
                Check(state.GetComponent<PlayerDamageReceiver2D>().CurrentHp==state.GetComponent<PlayerDamageReceiver2D>().MaxHp,"Unexpected player damage.");
                Check(CombatTimeController.SlowOwnerCount==0&&CombatTimeController.HitstopOwnerCount==0&&Mathf.Approximately(Time.timeScale,1),"Time request remained.");
                Check(presentation.Zoom==0&&Mathf.Approximately(camera.orthographicSize,2.8125f),"Zoom remained.");
                Check(presentation.DimAlpha==0&&!presentation.IsDimVisible,"Slow dim overlay remained.");
                Check(presentation.ActiveShakeCount==0&&presentation.ShakeOffset==Vector2.zero&&presentation.FlashAlpha==0,"Shake/flash remained.");
                Check(!state.GetComponent<PlayerVisualRecovery2D>().IsPlaying,"Recovery animation remained.");
                Check(!presentation.IsSpotlightVisible&&!presentation.AreEyesVisible,"Subject lift or eye light remained.");
                Check(presentation.IsPlayerBodyVisible&&!presentation.IsTransitShadowVisible,"Blink visibility remained.");
                Check(fx.ActiveCount==0,"VFX remained active.");
                Check(presentation.ChargeRealElapsed>=.5f&&presentation.ChargeRealElapsed<=1f,"Charge duration outside .5–1 seconds.");
                records.Add(new RunRecord{stages=stages,mirrored=mirrored,duration=director.RealElapsed,frontHits=director.FrontEnemy.HitCount,rearHits=director.RearEnemy.HitCount,chargeSeconds=presentation.ChargeRealElapsed});
                director.ResetTake(stages);
                Check(presentation.DimAlpha==0&&!presentation.IsDimVisible,"Slow dim overlay not reset.");
                Check(!state.GetComponent<PlayerVisualRecovery2D>().IsPlaying&&presentation.ActiveShakeCount==0&&presentation.FlashAlpha==0,"Recovery/impact feedback not reset.");
                Check(!presentation.IsSpotlightVisible&&!presentation.AreEyesVisible,"Subject/eye overlay not reset.");
                Check(presentation.IsPlayerBodyVisible&&!presentation.IsTransitShadowVisible,"Blink visibility not reset.");
                Check(director.FrontEnemy.CurrentHp==director.FrontEnemy.MaxHp&&director.RearEnemy.CurrentHp==3,"HP not reset.");
                Check(counter.StockCount==0&&fx.ActiveCount==0,"Stock or VFX not reset.");
                Check(director.FrontEnemy.GetComponentsInChildren<Collider2D>().All(c=>c.enabled)&&director.RearEnemy.GetComponentsInChildren<Collider2D>().All(c=>c.enabled),"Collider not reset.");
                foreach(var enemy in new[]{director.FrontEnemy,director.RearEnemy})
                {
                    var renderer=enemy.GetComponentInChildren<SpriteRenderer>();
                    Check(renderer.transform.localPosition==Vector3.zero,"Enemy visual origin drifted.");
                    Check(renderer.transform.localRotation==Quaternion.identity,"Enemy launch rotation not reset.");
                    Check(renderer.color==Color.white,"Enemy tint/alpha not reset.");
                    Check(enemy.GetComponent<VideoEnemyAnimationDriver>().MotionName=="Idle"&&enemy.GetComponent<EnemyMeleeChaser2D>().CurrentStateName=="Idle","Enemy motion/AI not reset.");
                    Check(!enemy.GetComponent<VideoEnemyAnimationDriver>().IsHitBacking,"Enemy hitback not reset.");
                    Check(!enemy.GetComponent<VideoEnemyAnimationDriver>().IsLaunched&&enemy.GetComponent<VideoEnemyAnimationDriver>().LastLaunchHeight==0,"Enemy launch not reset.");
                    Check(enemy.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash("EnemyVideo_Idle"),"Enemy Animator not reset.");
                }
                Check(state.GetComponent<Rigidbody2D>().gravityScale==3,"Gravity not restored.");
                Check(state.transform.position==new Vector3(0,1,0)&&Mathf.Approximately(director.FrontEnemy.transform.position.x,mirrored?-1.1f:1.1f)&&Mathf.Approximately(director.RearEnemy.transform.position.x,mirrored?.85f:-.85f),"Actor positions not reset.");
            }
            return JsonUtility.ToJson(new ValidationReport{runsPassed=records.Count,runs=records.ToArray()},true);
        }
        finally { director.MirrorLayout=false;director.ResetTake(2);clock.ManualAdvanceOnly=previousManual; }
    }

    public static string CapturePreview(int stages=2,bool hybrid=true)
    {
        Check(Application.isPlaying,"PlayMode required.");
        var director=UnityEngine.Object.FindFirstObjectByType<CombatVideoDirector>();var clock=UnityEngine.Object.FindFirstObjectByType<CombatTimeController>();
        string folder=Path.GetFullPath($".uloop/outputs/video/{stages}stage-{(hybrid?"hybrid":"pixel")}");Directory.CreateDirectory(folder);
        bool previous=clock.ManualAdvanceOnly;clock.ManualAdvanceOnly=true;
        director.MirrorLayout=false;director.StartTake(stages,hybrid);
        var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);target.Create();
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var camera=Camera.main;int frame=0;var previousActive=RenderTexture.active;
        try
        {
            while(!director.Completed&&frame<360)
            {
                director.StepCapture(1d/60d);
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1920,1080),0,0,false);image.Apply(false);
                File.WriteAllBytes(Path.Combine(folder,$"frame-{frame:D4}.png"),image.EncodeToPNG());frame++;
            }
            Check(director.Completed,"Capture did not complete.");
            var art=AssetDatabase.LoadAssetAtPath<VideoArtLibrary>(CombatVideoArtBuilder.LibraryPath);
            File.WriteAllText(Path.Combine(folder,"events.json"),JsonUtility.ToJson(new CaptureReport{frameCount=frame,fps=60,duration=frame/60d,playerArtworkIsRough=stages==4?art.fourStageArtworkIsRough:art.playerArtworkIsRough,events=director.Marks.ToArray()},true));
            return folder;
        }
        finally
        {
            RenderTexture.active=previousActive;UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
            director.ResetTake(stages);clock.ManualAdvanceOnly=previous;
        }
    }
    [Serializable] private sealed class CaptureReport { public int frameCount,fps;public double duration;public bool playerArtworkIsRough;public CombatVideoDirector.Mark[] events; }
}
