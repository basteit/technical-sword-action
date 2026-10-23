using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Update only received counter sprites/clips and the gameplay art snapshot.</summary>
public static class CounterSplitSubmissionSetup
{
    public const string Source="Assets/Art/Player/VideoSubmitted";
    public const string Effects="Assets/Art/Video/Submitted/20261024";
    [Serializable] private sealed class Manifest { public Action[] actions; }
    [Serializable] private sealed class Action { public string name;public int canvasWidth,canvasHeight,pivotX,pivotY,hitFrame,releaseFrame,totalFrames;public Frame[] frames; }
    [Serializable] private sealed class Frame { public int frames; }
    [Serializable] private sealed class Eyes { public Eye[] frames; }
    [Serializable] private sealed class Eye { public string file;public int pixels; }
    [MenuItem("Tools/Technical Sword Action/Video/Apply Split Counter Artwork")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit PlayMode first.");
        var data=JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"/timing-manifest.json"));
        data.actions=data.actions.Concat(JsonUtility.FromJson<Manifest>(File.ReadAllText(Effects+"/timing-manifest.json")).actions).ToArray();
        var eyes=JsonUtility.FromJson<Eyes>(File.ReadAllText(Source+"/eye-mask-manifest.json"));
        var art=AssetDatabase.LoadAssetAtPath<VideoArtLibrary>(CombatVideoArtBuilder.LibraryPath);
        foreach(var action in data.actions.Where(a=>a.name=="CounterKick"||a.name=="CounterKickEffect"))
        {
            bool effect=action.name=="CounterKickEffect";string folder=effect?Effects:Source;
            var sprites=Enumerable.Range(1,action.frames.Length).Select(i=>
            {
                string path=$"{folder}/{action.name}_{i:00}.png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(action.pivotX/(float)action.canvasWidth,action.pivotY/(float)action.canvasHeight);
                importer.SetTextureSettings(settings);importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }).ToArray();
            var motion=new VideoArtLibrary.Motion{name=action.name,frames=sprites,durations=action.frames.Select(f=>f.frames).ToArray()};
            if(!effect)
            {
                motion.includesCounterTrail=true;motion.counterContactOffset=new Vector2(51/32f,4/32f);
                motion.chargeFrame=0;motion.releaseFrame=action.releaseFrame;motion.hitFrame=action.hitFrame;
                motion.landingFrame=motion.recoveryFrame=action.totalFrames;
                motion.eyeFrames=Enumerable.Range(1,sprites.Length).Select(i=>eyes.frames.Any(e=>e.file==$"CounterKick_{i:00}"&&e.pixels>0)?AssetDatabase.LoadAssetAtPath<Sprite>($"{Source}/EyeMasks/CounterKick_{i:00}.png"):null).ToArray();
            }
            int index=Array.FindIndex(art.motions,m=>m.name==motion.name);
            if(index>=0)art.motions[index]=motion;else art.motions=art.motions.Append(motion).ToArray();
            if(!effect)SetClip("Assets/Animations/Video/PlayerVideo_Counter.anim",motion);
        }
        EditorUtility.SetDirty(art);AssetDatabase.SaveAssetIfDirty(art);
        var snapshot=AssetDatabase.LoadAssetAtPath<VideoArtLibrary>(PrototypeCounterCinematicInstaller.ArtPath);
        if(snapshot!=null)
        {
            foreach(string name in new[]{"CounterKick","CounterKickEffect"})
            {
                var m=art.Find(name);var copy=new VideoArtLibrary.Motion{name=m.name,frames=(Sprite[])m.frames.Clone(),durations=(int[])m.durations.Clone(),eyeFrames=m.eyeFrames!=null?(Sprite[])m.eyeFrames.Clone():null,includesCounterTrail=m.includesCounterTrail,counterContactOffset=m.counterContactOffset,chargeFrame=m.chargeFrame,releaseFrame=m.releaseFrame,hitFrame=m.hitFrame,landingFrame=m.landingFrame,recoveryFrame=m.recoveryFrame};
                int i=Array.FindIndex(snapshot.motions,x=>x.name==name);
                if(i>=0)snapshot.motions[i]=copy;else snapshot.motions=snapshot.motions.Append(copy).ToArray();
            }
            EditorUtility.SetDirty(snapshot);AssetDatabase.SaveAssetIfDirty(snapshot);
            var profile=AssetDatabase.LoadAssetAtPath<CounterCinematicProfile>(PrototypeCounterCinematicInstaller.ProfilePath);
            var set=AssetDatabase.LoadAssetAtPath<PrototypePlayerArtSet>(PrototypePlayerArtInstaller.SetPath);
            PrototypeCounterCinematicInstaller.ApplyCounterArtwork(set,profile);
        }
    }
    private static void SetClip(string path,VideoArtLibrary.Motion motion)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);int elapsed=0;
        var keys=motion.frames.Select((sprite,i)=>{var key=new ObjectReferenceKeyframe{time=elapsed/60f,value=sprite};elapsed+=motion.durations[i];return key;}).Append(new ObjectReferenceKeyframe{time=motion.TotalFrames/60f,value=motion.frames[^1]}).ToArray();
        AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),keys);
        EditorUtility.SetDirty(clip);AssetDatabase.SaveAssetIfDirty(clip);
    }
}
