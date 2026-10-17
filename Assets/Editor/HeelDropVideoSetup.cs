using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Updates the existing video scenes without rebuilding their authored layouts.</summary>
public static class HeelDropVideoSetup
{
    [MenuItem("Tools/Technical Sword Action/Video/Apply Submitted Attack and Heel Drop")]
    public static void Apply()
    {
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve loaded scene edits before applying video art.");
        string[] paths={CombatVideoSceneBuilder.ScenePath,CombatVideoSceneBuilder.DefenseCancelScenePath,"Assets/Scenes/CombatVideoStudio.unity"};
        string previous=SceneManager.GetActiveScene().path;
        if(!paths.Contains(previous)) throw new InvalidOperationException("Open a saved video scene before applying this update.");
        CombatVideoArtBuilder.Build();
        // Reimporting generated clips can mark their saved video scene dirty.
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        var library=AssetDatabase.LoadAssetAtPath<VideoArtLibrary>(CombatVideoArtBuilder.LibraryPath);
        foreach(string path in paths)
        {
            var scene=EditorSceneManager.OpenScene(path);
            var player=UnityEngine.Object.FindFirstObjectByType<PlayerParryCounter2D>();
            var data=new SerializedObject(player);
            data.FindProperty("overheadHeelDrop").boolValue=true;
            data.FindProperty("overheadHeight").floatValue=1.35f;
            data.FindProperty("heelImpactHeight").floatValue=1.15f;
            data.FindProperty("heelContactOffset").vector2Value=new Vector2(10f/32f,-22f/32f);
            data.FindProperty("heelTargetHeadOffsetX").floatValue=7/32f;
            data.ApplyModifiedPropertiesWithoutUndo();
            CombatVideoSceneBuilder.ConfigureAttack(player.GetComponent<PlayerAttack2D>(),library);
            CombatVideoSceneBuilder.Set(player.GetComponent<VideoPlayerAnimationDriver>(),"skipAttackRecovery",true);
            if(path==CombatVideoSceneBuilder.ScenePath)
                CombatVideoSceneBuilder.ConfigureSequentialLayout(UnityEngine.Object.FindFirstObjectByType<CombatVideoDirector>());
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        EditorSceneManager.OpenScene(previous);AssetDatabase.SaveAssets();
    }
}
