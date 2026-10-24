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
    public static string ValidateResetRuns()=>ValidateRuns(false);
    public static string ValidateDefenseCancelResetRuns()=>ValidateRuns(true);
    private static string ValidateRuns(bool defenseCancel)
    {
        Check(Application.isPlaying,"PlayMode required.");
        var director=UnityEngine.Object.FindFirstObjectByType<CombatVideoDirector>();
        Check(director.DefenseCancelTake==defenseCancel,"Open the matching capture scene before validation.");
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
                Check(director.FrontEnemy.HitCount==stages+(defenseCancel?1:0)&&director.FrontEnemy.DeathCount==1,"Front enemy duplicate/missing hit or death.");
                Check(director.RearEnemy.HitCount==1&&director.RearEnemy.DeathCount==1&&counter.HitCount-before==1,"Counter did not hit exactly once.");
                Check(director.Marks.Single(m=>m.kind=="Parry").target==director.Marks.Single(m=>m.kind=="Kick").target,"Counter target differs from parried source.");
                if(state.GetComponent<VideoPlayerAnimationDriver>().SkipAttackRecovery)
                    Check(!director.Marks.Any(m=>new[]{"VerticalSlash","HorizontalSlash","RapidSheathe","IaiSlash","AirRoundhouse"}.Any(name=>m.kind=="RecoveryStart:PlayerVideo_Recovery_"+name)),"Attack return animation was not skipped.");
                if(defenseCancel) Check(director.AttackWasCancelledForParry&&director.ReturnDashObserved,"Attack cancel or return dash did not occur.");
                Check(state.ActionState==PlayerActionState.Neutral&&!counter.IsCounterInvincible,"Player remained locked/invincible.");
                Check(state.GetComponent<PlayerDamageReceiver2D>().CurrentHp==state.GetComponent<PlayerDamageReceiver2D>().MaxHp,"Unexpected player damage.");
                Check(CombatTimeController.SlowOwnerCount==0&&CombatTimeController.HitstopOwnerCount==0&&Mathf.Approximately(Time.timeScale,1),"Time request remained.");
                Check(presentation.Zoom==0&&Mathf.Approximately(camera.orthographicSize,VideoCombatPresentation.DefaultNormalSize),"Zoom remained.");
                Check(presentation.DimAlpha==0&&!presentation.IsDimVisible,"Slow dim overlay remained.");
                Check(presentation.ActiveShakeCount==0&&presentation.ShakeOffset==Vector2.zero&&presentation.FlashAlpha==0,"Shake/flash remained.");
                Check(!state.GetComponent<PlayerVisualRecovery2D>().IsPlaying,"Recovery animation remained.");
                Check(!presentation.IsSpotlightVisible&&!presentation.AreEyesVisible,"Subject lift or eye light remained.");
                Check(presentation.IsPlayerBodyVisible&&!presentation.IsTransitShadowVisible,"Blink visibility remained.");
                Check(fx.ActiveCount==0,"VFX remained active.");
                Check(presentation.ChargeRealElapsed>=.98f&&presentation.ChargeRealElapsed<=1.05f,"Preparation duration outside .98–1.05 seconds.");
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
                Check(state.transform.position==director.InitialPlayerPosition&&director.FrontEnemy.transform.position==director.InitialFrontPosition&&director.RearEnemy.transform.position==director.InitialRearPosition,"Actor positions not reset.");
            }
            return JsonUtility.ToJson(new ValidationReport{runsPassed=records.Count,runs=records.ToArray()},true);
        }
        finally { director.MirrorLayout=false;director.ResetTake(2);clock.ManualAdvanceOnly=previousManual; }
    }

    public static string CapturePreview(int stages=2,bool hybrid=true)=>Capture(stages,hybrid,false);
    public static string CaptureDefenseCancelPreview(int stages=2,bool hybrid=true)=>Capture(stages,hybrid,true);
    public static string CaptureAtDistance(float normalSize,float closeSize,int stages=2,bool hybrid=true)
    {
        Check(Application.isPlaying,"PlayMode required.");
        Check(float.IsFinite(normalSize)&&float.IsFinite(closeSize)&&closeSize>0&&normalSize>=closeSize,"Invalid camera distances.");
        var presentation=UnityEngine.Object.FindFirstObjectByType<VideoCombatPresentation>();
        var settings=new SerializedObject(presentation);
        float savedNormal=settings.FindProperty("normalSize").floatValue,savedClose=settings.FindProperty("closeSize").floatValue;
        try
        {
            settings.FindProperty("normalSize").floatValue=normalSize;settings.FindProperty("closeSize").floatValue=closeSize;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return Capture(stages,hybrid,false);
        }
        finally
        {
            settings.Update();settings.FindProperty("normalSize").floatValue=savedNormal;settings.FindProperty("closeSize").floatValue=savedClose;
            settings.ApplyModifiedPropertiesWithoutUndo();presentation.ResetPresentation();
        }
    }
    private static string Capture(int stages,bool hybrid,bool defenseCancel)
    {
        Check(Application.isPlaying,"PlayMode required.");
        var director=UnityEngine.Object.FindFirstObjectByType<CombatVideoDirector>();var clock=UnityEngine.Object.FindFirstObjectByType<CombatTimeController>();
        Check(director.DefenseCancelTake==defenseCancel,"Open the matching scene; original previews must be preserved.");
        using var backdrop=CaptureBackdrop.Create();
        string folder=Path.GetFullPath($".uloop/outputs/video/{(defenseCancel?"defense-cancel-":"")}{stages}stage-{(hybrid?"hybrid":"pixel")}");Directory.CreateDirectory(folder);
        bool previous=clock.ManualAdvanceOnly;clock.ManualAdvanceOnly=true;
        director.MirrorLayout=false;director.StartTake(stages,hybrid);
        Vector3 initialPlayerPosition=director.Player.transform.position,initialFrontPosition=director.FrontEnemy.transform.position,initialRearPosition=director.RearEnemy.transform.position;
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
            var counter=director.Player.GetComponent<PlayerParryCounter2D>();
            var cameraSettings=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<VideoCombatPresentation>());
            File.WriteAllText(Path.Combine(folder,"events.json"),JsonUtility.ToJson(new CaptureReport{frameCount=frame,fps=60,duration=frame/60d,normalCameraSize=cameraSettings.FindProperty("normalSize").floatValue,closeCameraSize=cameraSettings.FindProperty("closeSize").floatValue,playerArtworkIsRough=defenseCancel||(stages==4?art.fourStageArtworkIsRough:art.playerArtworkIsRough),takeKind=defenseCancel?"defense-cancel":"sequential",counterAction=counter.AttacksFromBehind?"RearSubmittedKick":counter.IsOverheadHeelDrop?"OverheadHeelDrop":"AirRoundhouse",provisionalActions=art.motions.Where(m=>m.provisional).Select(m=>m.name).ToArray(),skipAttackRecovery=director.Player.GetComponent<VideoPlayerAnimationDriver>().SkipAttackRecovery,spacingApproaches=director.UsesSpacingApproaches,rearEntrance=director.UsesRearEntrance,initialPlayerPosition=initialPlayerPosition,initialFrontPosition=initialFrontPosition,initialRearPosition=initialRearPosition,events=director.Marks.ToArray()},true));
            return folder;
        }
        finally
        {
            RenderTexture.active=previousActive;UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
            director.ResetTake(stages);clock.ManualAdvanceOnly=previous;
        }
    }
    // Extend the original pixels at their existing pitch. A wide comparison must
    // not expose the finite backdrop's edges or stretch its grid to fill the frame.
    private sealed class CaptureBackdrop : IDisposable
    {
        private GameObject root;
        private Sprite grid, floor;
        public static CaptureBackdrop Create()
        {
            var original=GameObject.Find("TrainingGrid")?.GetComponent<SpriteRenderer>();
            if(original==null||original.sprite==null)return null;
            var result=new CaptureBackdrop();
            try {result.Build(original);return result;}
            catch {result.Dispose();throw;}
        }
        private void Build(SpriteRenderer original)
        {
            var source=original.sprite;var rect=source.rect;
            Check(rect.width>=64&&rect.height>=124,"Unexpected training backdrop layout.");
            grid=Sprite.Create(source.texture,new Rect(rect.x,rect.y+60,64,64),Vector2.one*.5f,source.pixelsPerUnit,0,SpriteMeshType.FullRect);
            floor=Sprite.Create(source.texture,new Rect(rect.x+1,rect.y+1,1,1),Vector2.one*.5f,1,0,SpriteMeshType.FullRect);
            grid.hideFlags=floor.hideFlags=HideFlags.DontSave;
            root=new GameObject("CaptureBackdropExtension"){hideFlags=HideFlags.DontSave};
            root.transform.SetParent(original.transform,false);
            float ground=(60-source.pivot.y)/source.pixelsPerUnit;
            var gridObject=new GameObject("ExtendedGrid");gridObject.transform.SetParent(root.transform,false);
            gridObject.transform.localPosition=new Vector3(0,ground+16,0);
            var gridRenderer=gridObject.AddComponent<SpriteRenderer>();
            gridRenderer.sprite=grid;gridRenderer.drawMode=SpriteDrawMode.Tiled;gridRenderer.size=new Vector2(48,32);
            gridRenderer.sharedMaterial=original.sharedMaterial;gridRenderer.sortingLayerID=original.sortingLayerID;gridRenderer.sortingOrder=original.sortingOrder-1;
            var floorObject=new GameObject("ExtendedSolidFloor");floorObject.transform.SetParent(root.transform,false);
            floorObject.transform.localPosition=new Vector3(0,ground-16,0);floorObject.transform.localScale=new Vector3(48,32,1);
            var floorRenderer=floorObject.AddComponent<SpriteRenderer>();floorRenderer.sprite=floor;
            floorRenderer.sharedMaterial=original.sharedMaterial;floorRenderer.sortingLayerID=original.sortingLayerID;floorRenderer.sortingOrder=original.sortingOrder-1;
        }
        public void Dispose()
        {
            if(root!=null)UnityEngine.Object.DestroyImmediate(root);
            if(grid!=null)UnityEngine.Object.DestroyImmediate(grid);
            if(floor!=null)UnityEngine.Object.DestroyImmediate(floor);
        }
    }
    [Serializable] private sealed class CaptureReport { public int frameCount,fps;public float normalCameraSize,closeCameraSize;public double duration;public bool playerArtworkIsRough,skipAttackRecovery,spacingApproaches,rearEntrance;public Vector3 initialPlayerPosition,initialFrontPosition,initialRearPosition;public string takeKind,counterAction;public string[] provisionalActions;public CombatVideoDirector.Mark[] events; }
}
