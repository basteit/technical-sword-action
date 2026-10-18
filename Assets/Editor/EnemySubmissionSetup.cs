using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Apply received enemy artwork without rebuilding player clips or scenes.</summary>
public static class EnemySubmissionSetup
{
    public const string Folder="Assets/Art/Enemy/Submitted/20261019";
    [Serializable] private sealed class Manifest { public Motion[] motions; }
    [Serializable] private sealed class Motion { public string name;public int width,height;public Frame[] frames; }
    [Serializable] private sealed class Frame { public int durationMs; }
    [MenuItem("Tools/Technical Sword Action/Video/Apply Submitted Enemy")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit PlayMode before applying enemy art.");
        if(!File.Exists(Folder+"/manifest.json"))return;
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder+"/manifest.json"));
        var library=AssetDatabase.LoadAssetAtPath<VideoArtLibrary>(CombatVideoArtBuilder.LibraryPath);
        foreach(var source in manifest.motions)
        for(int i=1;i<=source.frames.Length;i++)
        {
            string path=$"{Folder}/EnemySample_{source.name}_{i:00}.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(32f/source.width,38f/source.height);
            importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
        VideoArtLibrary.Motion Set(string state,string source,int first,int count,int total)
        {
            var input=manifest.motions.Single(m=>m.name==source);
            var frames=Enumerable.Range(first,count).Select(i=>AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/EnemySample_{source}_{i:00}.png")).ToArray();
            int sum=input.frames.Skip(first-1).Take(count).Sum(f=>f.durationMs),elapsed=0;
            int cumulative=0;var times=new int[count];
            for(int i=0;i<count;i++){cumulative+=input.frames[first-1+i].durationMs;int next=Mathf.RoundToInt(cumulative*total/(float)sum);times[i]=next-elapsed;elapsed=next;}
            var motion=new VideoArtLibrary.Motion{name="Enemy_"+state,frames=frames,durations=times};
            int index=Array.FindIndex(library.motions,m=>m.name==motion.name);
            if(index<0)throw new InvalidOperationException("Missing enemy state "+state);
            library.motions[index]=motion;
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>($"{CombatVideoArtBuilder.Folder}/EnemyVideo_{state}.anim");
            var keys=new ObjectReferenceKeyframe[count+1];elapsed=0;
            for(int i=0;i<count;i++){keys[i]=new ObjectReferenceKeyframe{time=elapsed/60f,value=frames[i]};elapsed+=times[i];}
            keys[count]=new ObjectReferenceKeyframe{time=elapsed/60f,value=frames[count-1]};
            AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),keys);
            var clipSettings=AnimationUtility.GetAnimationClipSettings(clip);clipSettings.loopTime=state=="Idle"||state=="Move";
            AnimationUtility.SetAnimationClipSettings(clip,clipSettings);EditorUtility.SetDirty(clip);return motion;
        }
        Set("Idle","idle",1,10,60);Set("Move","walk",1,10,60);
        Set("Windup","attack",1,7,30);Set("Attack","attack",8,7,16);
        Set("Hit","chance",1,1,6);Set("Parried","chance",1,1,10);Set("Death","down",1,1,39);
        EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
    }
}
