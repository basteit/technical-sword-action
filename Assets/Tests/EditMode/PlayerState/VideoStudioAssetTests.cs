using System;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class VideoStudioAssetTests
    {
        private static Type T(string name)=>Type.GetType(name+", Assembly-CSharp",true);
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name).SetValue(target,value);
        private static object Get(object target,string name)=>target.GetType().GetField(name).GetValue(target);
        private static int Frames(double seconds,int scheduled)=>(int)Type.GetType("VideoStudioCapture, Assembly-CSharp-Editor",true).GetMethod("FramesDue").Invoke(null,new object[]{seconds,scheduled});
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void LiveCapturePreservesOneRealSecondAcrossDifferentRenderCadences(int fps)
        {
            int frames=0;double elapsed=0;
            for(int i=0;i<fps;i++){elapsed+=1d/fps;frames+=Frames(elapsed,frames);}
            Assert.That(frames,Is.EqualTo(60));
        }
        [Test] public void SlowAndHitstopPresentationStillHaveTheirFullRealDurationInTheClip()
        {
            int frames=0;double elapsed=0;
            // 0.5 s normal + 2/3 s charge + 8/60 s hitstop + a slow-rendered 0.5 s.
            foreach(double seconds in new[]{.5,2d/3,8d/60,.5}){elapsed+=seconds;frames+=Frames(elapsed,frames);}
            Assert.That(frames,Is.EqualTo(108));Assert.That(Frames(elapsed,frames),Is.Zero);
        }
        [Test] public void ACustomSetPersistsEnemyAndGimmickSettingsAndReloadsIndependently()
        {
            string path="Assets/Settings/VideoStudio/StudioTest-"+Guid.NewGuid().ToString("N")+".asset";
            var preset=ScriptableObject.CreateInstance(T("StudioPreset"));
            Set(preset,"cameraCenter",new Vector2(2,1.5f));Set(preset,"followCamera",false);
            var enemy=Activator.CreateInstance(T("StudioEnemySetup"));Set(enemy,"position",new Vector2(-1.4f,1));Set(enemy,"hp",8);
            Set(enemy,"windupSeconds",.8f);Set(enemy,"mode",Enum.ToObject(T("StudioEnemyMode"),2));
            var enemies=Array.CreateInstance(T("StudioEnemySetup"),1);enemies.SetValue(enemy,0);Set(preset,"enemies",enemies);
            var gimmick=Activator.CreateInstance(T("StudioGimmickSetup"));Set(gimmick,"travel",new Vector2(3,1));Set(gimmick,"periodSeconds",4f);
            var gimmicks=Array.CreateInstance(T("StudioGimmickSetup"),1);gimmicks.SetValue(gimmick,0);Set(preset,"gimmicks",gimmicks);
            try
            {
                AssetDatabase.CreateAsset(preset,path);AssetDatabase.SaveAssetIfDirty(preset);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                var loaded=AssetDatabase.LoadAssetAtPath(path,T("StudioPreset"));
                Assert.That((Vector2)Get(loaded,"cameraCenter"),Is.EqualTo(new Vector2(2,1.5f)));
                Assert.That((bool)Get(loaded,"followCamera"),Is.False);
                object reloaded=((Array)Get(loaded,"enemies")).GetValue(0);
                Assert.That((Vector2)Get(reloaded,"position"),Is.EqualTo(new Vector2(-1.4f,1)));
                Assert.That((int)Get(reloaded,"hp"),Is.EqualTo(8));Assert.That((float)Get(reloaded,"windupSeconds"),Is.EqualTo(.8f));
                Assert.That(Get(reloaded,"mode").ToString(),Is.EqualTo("RepeatAttack"));
                object prop=((Array)Get(loaded,"gimmicks")).GetValue(0);
                Assert.That((Vector2)Get(prop,"travel"),Is.EqualTo(new Vector2(3,1)));Assert.That((float)Get(prop,"periodSeconds"),Is.EqualTo(4));
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
        [Test] public void PrefabBasedSceneOverridesSurviveSavingAndReopeningTheStage()
        {
            string path="Assets/Scenes/StudioTest-"+Guid.NewGuid().ToString("N")+".unity";
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Assert.That(AssetDatabase.CopyAsset("Assets/Scenes/CombatVideoStudio.unity",path),Is.True);
                var scene=EditorSceneManager.OpenScene(path);
                var studio=UnityEngine.Object.FindFirstObjectByType(T("CombatVideoStudio"));
                var setup=Activator.CreateInstance(T("StudioEnemySetup"));Set(setup,"hp",11);Set(setup,"position",new Vector2(-1.6f,1));
                var enemy=(Component)studio.GetType().GetMethod("AddEnemy").Invoke(studio,new[]{setup});
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(enemy),Is.True);
                Assert.That(UnityEngine.Object.FindObjectsByType(T("StudioEnemyActor"),FindObjectsSortMode.None).Select(o=>o.name).Distinct().Count(),Is.EqualTo(2));
                string name=enemy.name;
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);EditorSceneManager.OpenScene(path);
                var reloaded=GameObject.Find(name).GetComponent(T("StudioEnemyActor"));
                var settings=reloaded.GetType().GetProperty("Setup").GetValue(reloaded);
                Assert.That((int)Get(settings,"hp"),Is.EqualTo(11));
                Assert.That((Vector2)Get(settings,"position"),Is.EqualTo(new Vector2(-1.6f,1)));
                Assert.That(reloaded.transform.position,Is.EqualTo(new Vector3(-1.6f,1,0)));
            }
            finally
            {
                try
                {
                    if(Array.Exists(previous,s=>s.isLoaded&&s.isActive&&!string.IsNullOrEmpty(s.path)))
                        EditorSceneManager.RestoreSceneManagerSetup(previous);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                }
                finally { AssetDatabase.DeleteAsset(path); }
            }
        }
    }
}
