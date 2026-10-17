using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CombatVideoArtBuilder
{
    public const string Folder="Assets/Animations/Video";
    public const string ControllerPath=Folder+"/CombatVideoPlayer.controller";
    public const string EnemyControllerPath=Folder+"/CombatVideoEnemy.controller";
    public const string LibraryPath="Assets/Settings/CombatVideoArt.asset";
    [Serializable] private sealed class Manifest { public ActionData[] actions; }
    [Serializable] private sealed class ActionData { public string name;public int hitFrame,branchFrame,totalFrames,chargeFrame,releaseFrame,landingFrame,recoveryFrame,canvasWidth,canvasHeight,pivotX,pivotY;public bool provisional,includesSlashTrail;public FrameData[] frames; }
    [Serializable] private sealed class FrameData { public int frames,start; }
    [Serializable] private sealed class EyeManifest { public EyeFrame[] frames; }
    [Serializable] private sealed class EyeFrame { public string file;public int pixels; }
    [MenuItem("Tools/Technical Sword Action/Video/Build Art and Clips")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Art/Player/VideoRough/timing-manifest.json"));
        const string showcaseFolder="Assets/Art/Player/VideoShowcase";
        bool hasShowcase=File.Exists(showcaseFolder+"/timing-manifest.json");
        if(hasShowcase)
            manifest.actions=manifest.actions.Concat(JsonUtility.FromJson<Manifest>(File.ReadAllText(showcaseFolder+"/timing-manifest.json")).actions).ToArray();
        const string heelFolder="Assets/Art/Player/VideoHeelDrop";
        bool hasHeel=File.Exists(heelFolder+"/timing-manifest.json");
        if(hasHeel)
        {
            var heel=JsonUtility.FromJson<Manifest>(File.ReadAllText(heelFolder+"/timing-manifest.json"));
            for(int i=0;i<manifest.actions.Length;i++) manifest.actions[i]=heel.actions.FirstOrDefault(a=>a.name==manifest.actions[i].name)??manifest.actions[i];
        }
        const string submittedFolder="Assets/Art/Player/VideoSubmitted";
        bool hasSubmitted=File.Exists(submittedFolder+"/timing-manifest.json");
        if(hasSubmitted)
        {
            var submitted=JsonUtility.FromJson<Manifest>(File.ReadAllText(submittedFolder+"/timing-manifest.json"));
            for(int i=0;i<manifest.actions.Length;i++) manifest.actions[i]=submitted.actions.FirstOrDefault(a=>a.name==manifest.actions[i].name)??manifest.actions[i];
        }
        const string cleanFolder="Assets/Art/Player/VideoClean";
        bool hasClean=File.Exists(cleanFolder+"/timing-manifest.json");
        if(hasClean)
        {
            var final=JsonUtility.FromJson<Manifest>(File.ReadAllText(cleanFolder+"/timing-manifest.json"));
            for(int i=0;i<manifest.actions.Length;i++) manifest.actions[i]=final.actions.FirstOrDefault(a=>a.name==manifest.actions[i].name)??manifest.actions[i];
            // Split parry tags are additions rather than replacements of ParryDeflect.
            manifest.actions=manifest.actions.Concat(final.actions.Where(a=>!manifest.actions.Any(old=>old.name==a.name))).ToArray();
        }
        var motions=new List<VideoArtLibrary.Motion>();
        var paths=SelectCleanFrames("Assets/Art/Player/VideoRough").Concat(Directory.GetFiles("Assets/Art/Player/VideoRough","*Reference_??.png")).Concat(Directory.GetFiles("Assets/Art/Video","*.png"));
        if(hasShowcase) paths=paths.Concat(SelectCleanFrames(showcaseFolder));
        if(hasHeel) paths=paths.Concat(SelectCleanFrames(heelFolder));
        if(hasSubmitted) paths=paths.Concat(SelectCleanFrames(submittedFolder));
        if(hasClean) paths=paths.Concat(SelectCleanFrames(cleanFolder));
        var groups=paths.Select(p=>p.Replace('\\','/')).Where(p=>System.Text.RegularExpressions.Regex.IsMatch(p,@"_\d\d\.png$")).GroupBy(p=>Path.GetFileNameWithoutExtension(p)[..^3]);
        var frames=groups.SelectMany(g=>g.Any(p=>p.StartsWith(cleanFolder+"/"))?g.Where(p=>p.StartsWith(cleanFolder+"/")):
            g.Any(p=>p.StartsWith(submittedFolder+"/"))?g.Where(p=>p.StartsWith(submittedFolder+"/")):
            g.Any(p=>p.StartsWith(heelFolder+"/"))?g.Where(p=>p.StartsWith(heelFolder+"/")):g).ToArray();
        var eyeCounts=new Dictionary<string,int>();
        foreach(string folder in new[]{"Assets/Art/Player/VideoRough",showcaseFolder,heelFolder,submittedFolder,cleanFolder})
        {
            string file=folder+"/eye-mask-manifest.json";if(!File.Exists(file)) continue;
            foreach(var frame in JsonUtility.FromJson<EyeManifest>(File.ReadAllText(file)).frames)
                eyeCounts[folder+"/EyeMasks/"+frame.file+".png"]=frame.pixels;
        }
        foreach(string path in frames.Concat(eyeCounts.Keys.Where(p=>frames.Contains(p.Replace("/EyeMasks/","/")))))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            var textureSettings=new TextureImporterSettings();importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteAlignment=(int)SpriteAlignment.Custom;
            string motionName=Path.GetFileNameWithoutExtension(path)[..^3];
            var action=manifest.actions.FirstOrDefault(a=>a.name==motionName);
            textureSettings.spritePivot=action!=null&&action.canvasWidth>0?
                new Vector2(action.pivotX/(float)action.canvasWidth,action.pivotY/(float)action.canvasHeight):
                motionName=="Fx_HeelLightning"?new Vector2(.5f,64f/192):
                path.Contains("/Player/")||Path.GetFileName(path).StartsWith("Enemy_")?new Vector2(.5f,38f/64):new Vector2(.5f,.5f);
            importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();
        }
        foreach(var group in frames.GroupBy(p=>Path.GetFileNameWithoutExtension(p)[..^3]))
        {
            string name=group.Key;int[] durations;
            var action=manifest.actions.FirstOrDefault(a=>a.name==name);
            if(action!=null) durations=action.frames.Select(f=>f.frames).ToArray();
            else if(name=="Fx_Kick") durations=new[]{1,1,2,3,3,3,3,3};
            else if(name=="Fx_HeelLightning") durations=new[]{1,1,2,2,3,3,4,4};
            else if(name=="Fx_Parry") durations=new[]{2,1,1,1,1,2,2,2};
            else if(name=="Fx_Slash") durations=new[]{1,1,1,1,2,2,2,2};
            else durations=Enumerable.Repeat(name=="IdleReference"?6:name=="WalkReference"?5:name=="Enemy_Idle"?8:name=="Enemy_Windup"?5:name=="Fx_Vanish"?3:4,group.Count()).ToArray();
            var ordered=group.OrderBy(p=>p).ToArray();
            var eyes=ordered.Select(p=>Path.GetDirectoryName(p).Replace('\\','/')+"/EyeMasks/"+Path.GetFileName(p)).Select(p=>eyeCounts.TryGetValue(p,out int count)&&count>0?AssetDatabase.LoadAssetAtPath<Sprite>(p):null).ToArray();
            motions.Add(new VideoArtLibrary.Motion {name=name,provisional=action?.provisional??false,includesSlashTrail=action?.includesSlashTrail??false,frames=ordered.Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p)).ToArray(),eyeFrames=eyes,durations=durations,hitFrame=action?.hitFrame??0,branchFrame=action?.branchFrame??0,chargeFrame=action?.chargeFrame??0,releaseFrame=action?.releaseFrame??0,landingFrame=action?.landingFrame??0,recoveryFrame=action?.recoveryFrame??0});
        }
        string backgroundPath="Assets/Art/Video/CaptureTrainingGrid.png";
        var bg=(TextureImporter)AssetImporter.GetAtPath(backgroundPath);bg.textureType=TextureImporterType.Sprite;bg.spritePixelsPerUnit=32;bg.filterMode=FilterMode.Point;bg.textureCompression=TextureImporterCompression.Uncompressed;bg.mipmapEnabled=false;bg.SaveAndReimport();
        var library=AssetDatabase.LoadAssetAtPath<VideoArtLibrary>(LibraryPath);
        if(library==null) { library=ScriptableObject.CreateInstance<VideoArtLibrary>();AssetDatabase.CreateAsset(library,LibraryPath); }
        library.motions=motions.ToArray();library.background=AssetDatabase.LoadAssetAtPath<Sprite>(backgroundPath);EditorUtility.SetDirty(library);
        bool CleanExists(string name)=>frames.Any(p=>p.StartsWith(cleanFolder)&&Path.GetFileNameWithoutExtension(p).StartsWith(name+"_"));
        bool cleanParry=CleanExists("ParryReady")&&CleanExists("ParrySuccessLight")&&CleanExists("Recovery_ParrySuccessLight")||
            CleanExists("ParryDeflect")&&CleanExists("Recovery_ParryDeflect");
        library.playerArtworkIsRough=!cleanParry||!new[]{"VerticalSlash","HorizontalSlash","AirRoundhouse","Recovery_VerticalSlash","Recovery_HorizontalSlash","Recovery_AirRoundhouse"}.All(CleanExists);
        library.fourStageArtworkIsRough=library.playerArtworkIsRough||!new[]{"RapidSheathe","IaiSlash","Recovery_RapidSheathe","Recovery_IaiSlash"}.All(CleanExists);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach(var parameter in controller.parameters) controller.RemoveParameter(parameter);
        controller.AddParameter("ComboStep",AnimatorControllerParameterType.Int);controller.AddParameter("AttackTrigger",AnimatorControllerParameterType.Trigger);
        var machine=controller.layers[0].stateMachine;foreach(var state in machine.states) machine.RemoveState(state.state);
        // Idle has already been supplied; unfinished actions must not select a rough substitute for it.
        var idleMotion=library.Find("IdleReference")??library.Find("IdleRough");
        var moveMotion=library.playerArtworkIsRough?library.Find("WalkRough")??library.Find("WalkReference"):library.Find("WalkReference");
        var idle=Clip("Idle",idleMotion,true);var idleState=machine.AddState("PlayerVideo_Idle");idleState.motion=idle;machine.defaultState=idleState;
        int stage=1;
        foreach(var action in manifest.actions.Take(4))
        {
            var clip=Clip("Attack_"+stage,library.Find(action.name),false);
            var events=new List<AnimationEvent>();
            void Event(int frame,string name,int parameter=0)=>events.Add(new AnimationEvent{time=frame/60f,functionName=name,intParameter=parameter});
            int recoveryFrame=action.recoveryFrame>0?action.recoveryFrame:action.branchFrame;
            Event(action.hitFrame,"OnAttackHit",stage);
            if(stage<4) { Event(action.hitFrame+4,"OnComboWindowOpen",stage);Event(action.branchFrame-1,"OnComboWindowClose",stage); }
            if(stage<4&&action.branchFrame<recoveryFrame) Event(action.branchFrame,"OnComboBranch",stage);
            Event(action.hitFrame+4,"OnDefenseCancelWindowOpen");Event(recoveryFrame-2,"OnLateCancelWindowOpen");
            Event(recoveryFrame,"OnAttackRecovery",stage);Event(action.totalFrames,"OnAttackEnd",stage);
            AnimationUtility.SetAnimationEvents(clip,events.OrderBy(e=>e.time).ToArray());EditorUtility.SetDirty(clip);
            machine.AddState("PlayerVideo_Attack_"+stage).motion=clip;stage++;
        }
        var parry=library.Find("ParryDeflect");
        var parryReady=library.Find("ParryReady")??new VideoArtLibrary.Motion{name="Ready",frames=new[]{parry.frames[0]},durations=new[]{12}};
        machine.AddState("PlayerVideo_Parry").motion=Clip("Parry",parryReady,false);
        var lightParry=library.Find("ParrySuccessLight")??new VideoArtLibrary.Motion{name="Deflect",frames=parry.frames.Skip(2).ToArray(),durations=parry.durations.Skip(2).ToArray()};
        machine.AddState("PlayerVideo_ParrySuccess").motion=Clip("ParrySuccess",lightParry,false);
        foreach(string reaction in new[]{"ParrySuccessLight","ParrySuccessHeavy"})
        {
            var motion=library.Find(reaction);
            if(motion!=null) machine.AddState("PlayerVideo_"+reaction).motion=Clip(reaction,motion,false);
        }
        machine.AddState("PlayerVideo_Counter").motion=Clip("Counter",library.Find("AirRoundhouse"),false);
        machine.AddState("PlayerVideo_Move").motion=Clip("Move",moveMotion,true);
        var dash=library.Find("Dash");
        if(dash!=null) machine.AddState("PlayerVideo_Dash").motion=Clip("Dash",dash,false);
        machine.AddState("PlayerVideo_ParryFail").motion=Clip("ParryFail",new VideoArtLibrary.Motion{name="Miss",frames=new[]{parryReady.frames[^1]},durations=new[]{18}},false);
        var kick=library.Find("AirRoundhouse");
        machine.AddState("PlayerVideo_Jump").motion=Clip("Jump",new VideoArtLibrary.Motion{name="Jump",frames=new[]{kick.frames[1]},durations=new[]{8}},false);
        machine.AddState("PlayerVideo_Fall").motion=Clip("Fall",new VideoArtLibrary.Motion{name="Fall",frames=new[]{kick.frames[^1]},durations=new[]{8}},false);
        foreach(var motion in motions.Where(m=>m.name.StartsWith("Recovery_")))
            machine.AddState("PlayerVideo_"+motion.name).motion=Clip(motion.name,motion,false);
        var enemyController=AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
        if(enemyController==null) enemyController=AnimatorController.CreateAnimatorControllerAtPath(EnemyControllerPath);
        var enemyMachine=enemyController.layers[0].stateMachine;foreach(var state in enemyMachine.states) enemyMachine.RemoveState(state.state);
        foreach(string name in new[]{"Idle","Move","Windup","Attack","Hit","Parried","Death"})
        {
            var motion=library.Find("Enemy_"+name);string path=Folder+"/EnemyVideo_"+name+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null) { clip=new AnimationClip{name="EnemyVideo_"+name,frameRate=60};AssetDatabase.CreateAsset(clip,path); }
            SetSpriteKeys(clip,motion,name=="Idle"||name=="Move");
            var state=enemyMachine.AddState("EnemyVideo_"+name);state.motion=clip;if(name=="Idle") enemyMachine.defaultState=state;
        }
        AssetDatabase.SaveAssets();
    }
    private static string[] SelectCleanFrames(string folder)
    {
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(folder+"/timing-manifest.json"));
        var expected=new HashSet<string>(manifest.actions.SelectMany(a=>Enumerable.Range(1,a.frames.Length).Select(i=>$"{a.name}_{i:D2}.png")));
        var frames=Directory.GetFiles(folder,"*.png").Where(p=>expected.Contains(Path.GetFileName(p))).ToArray();
        if(frames.Length!=expected.Count) throw new InvalidOperationException("Cleanup PNGs do not match their timing manifest.");
        return frames;
    }
    private static AnimationClip Clip(string name,VideoArtLibrary.Motion motion,bool loop)
    {
        string path=Folder+"/PlayerVideo_"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(clip==null) { clip=new AnimationClip{name="PlayerVideo_"+name,frameRate=60};AssetDatabase.CreateAsset(clip,path); }
        SetSpriteKeys(clip,motion,loop);return clip;
    }
    private static void SetSpriteKeys(AnimationClip clip,VideoArtLibrary.Motion motion,bool loop)
    {
        var keys=new List<ObjectReferenceKeyframe>();int elapsed=0;
        for(int i=0;i<motion.frames.Length;i++) { keys.Add(new ObjectReferenceKeyframe{time=elapsed/60f,value=motion.frames[i]});elapsed+=motion.durations[i]; }
        keys.Add(new ObjectReferenceKeyframe{time=elapsed/60f,value=motion.frames[^1]});
        AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),keys.ToArray());
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
    }
}
