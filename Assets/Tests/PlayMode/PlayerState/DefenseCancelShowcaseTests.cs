using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class DefenseCancelShowcaseTests
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        private Component director,clock,state,counter,presentation;
        private Type clockType;
        [UnitySetUp] public IEnumerator Setup()
        {
            clockType=Type.GetType("CombatTimeController, Assembly-CSharp",true);
            clock=(Component)Object.FindFirstObjectByType(clockType);Set(clock,"ManualAdvanceOnly",true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/CombatVideoDefenseCancel.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            director=Find("CombatVideoDirector");state=(Component)Get<object>(director,"Player");
            counter=state.GetComponent(Type.GetType("PlayerParryCounter2D, Assembly-CSharp",true));
            presentation=Find("VideoCombatPresentation");
        }
        [UnityTearDown] public IEnumerator Teardown()
        { Call(director,"ResetTake",2);Set(clock,"ManualAdvanceOnly",false);yield return null; }
        [TestCase(2,30,false)] [TestCase(2,60,false)] [TestCase(2,120,false)]
        [TestCase(2,30,true)] [TestCase(2,60,true)] [TestCase(2,120,true)]
        [TestCase(4,30,false)] [TestCase(4,60,false)] [TestCase(4,120,false)]
        [TestCase(4,30,true)] [TestCase(4,60,true)] [TestCase(4,120,true)]
        public void AttackCancelCounterDashAndReturnComboUseTheActualStateAndTarget(int stages,int fps,bool mirror)
        {
            Set(director,"MirrorLayout",mirror);Call(director,"StartTake",stages,true);
            var front=(Component)Get<object>(director,"FrontEnemy");var rear=(Component)Get<object>(director,"RearEnemy");
            bool sawParry=false,sawDash=false;
            for(int i=0;i<fps*6&&!Get<bool>(director,"Completed");i++)
            {
                Call(director,"StepCapture",1d/fps);
                if(!sawParry&&Get<bool>(director,"AttackWasCancelledForParry"))
                { sawParry=true;Assert.That(Get<int>(front,"HitCount"),Is.EqualTo(1));Assert.That(Get<bool>(front,"IsAlive"),Is.True); }
                if(!sawDash&&Get<bool>(director,"ReturnDashObserved"))
                {
                    sawDash=true;Assert.That(Get<bool>(rear,"IsAlive"),Is.False);
                    Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
                    Assert.That(state.GetComponent<SpriteRenderer>().sprite.name,Does.StartWith("Dash_"));
                }
            }
            Assert.That(sawParry&&sawDash&&Get<bool>(director,"Completed"),Is.True);
            Assert.That(Get<int>(front,"HitCount"),Is.EqualTo(stages+1));
            Assert.That(Get<int>(rear,"HitCount"),Is.EqualTo(1));
            Assert.That(Get<int>(front,"DeathCount"),Is.EqualTo(1));Assert.That(Get<int>(rear,"DeathCount"),Is.EqualTo(1));
            Assert.That(Get<int>(counter,"StockCount"),Is.Zero);
            var health=state.GetComponent(Type.GetType("PlayerDamageReceiver2D, Assembly-CSharp",true));
            Assert.That(Get<int>(health,"CurrentHp"),Is.EqualTo(Get<int>(health,"MaxHp")));
            var marks=(IEnumerable)Get<object>(director,"Marks");
            var items=marks.Cast<object>().ToArray();
            string Kind(object m)=>(string)m.GetType().GetField("kind").GetValue(m);
            string Target(object m)=>(string)m.GetType().GetField("target").GetValue(m);
            Assert.That(Target(items.Single(m=>Kind(m)=="Parry")),Is.EqualTo(Target(items.Single(m=>Kind(m)=="Kick"))));
            Assert.That(Array.FindIndex(items,m=>Kind(m)=="Landing"),Is.LessThan(Array.FindIndex(items,m=>Kind(m)=="ReturnDash")));
            Assert.That(Get<double>(director,"RealElapsed"),Is.InRange(3d,6d));
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
        }
        [Test] public void BothEndingsAndBothDirectionsResetTenTimesEach()
        {
            var utility=Type.GetType("VideoCaptureUtility, Assembly-CSharp-Editor",true);
            string report=(string)utility.GetMethod("ValidateDefenseCancelResetRuns").Invoke(null,null);
            Assert.That(report,Does.Contain("\"runsPassed\": 40"));
        }
        [Test] public void PauseAtReturnDashFreezesPositionPoseAndEffectsThenResetRestoresControl()
        {
            Call(director,"StartTake",2,true);for(int i=0;i<250&&!Get<bool>(director,"ReturnDashObserved");i++)Step();
            Assert.That(Get<bool>(director,"ReturnDashObserved"),Is.True);
            Vector3 position=state.transform.position;var visual=state.GetComponent<SpriteRenderer>();string pose=visual.sprite.name;
            int effects=Get<int>(Find("VideoVfxRenderer"),"ActiveCount");
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});for(int i=0;i<120;i++)Step();
            Assert.That(state.transform.position,Is.EqualTo(position));Assert.That(visual.sprite.name,Is.EqualTo(pose));
            Assert.That(Get<int>(Find("VideoVfxRenderer"),"ActiveCount"),Is.EqualTo(effects));
            Call(director,"ResetTake",2);
            var motor=state.GetComponent(Type.GetType("PlayerMotor2D, Assembly-CSharp",true));
            Assert.That(Get<bool>(motor,"IsDashing"),Is.False);Assert.That(Get<float>(motor,"DashCooldownRemaining"),Is.Zero);
            Assert.That(Get<float>(motor,"MoveInput"),Is.Zero);Assert.That(Get<int>(Find("VideoVfxRenderer"),"ActiveCount"),Is.Zero);
            Assert.That(Get<int>(state,"ActiveCollisionIgnoreCount"),Is.Zero);
        }
        [TestCase("TargetLost")] [TestCase("CounterDisabled")] [TestCase("PlayerDead")]
        public void InterruptedCounterDoesNotStartAnAutomaticReturnDash(string interruption)
        {
            Call(director,"StartTake",2,true);for(int i=0;i<250&&!Get<bool>(presentation,"IsCharged");i++)Step();
            Assert.That(Get<bool>(presentation,"IsCharged"),Is.True);
            if(interruption=="TargetLost") ((Component)Get<object>(director,"RearEnemy")).gameObject.SetActive(false);
            if(interruption=="CounterDisabled") ((Behaviour)counter).enabled=false;
            if(interruption=="PlayerDead") Call(state,"SetDead","ShowcaseInterrupted");
            for(int i=0;i<120;i++)Step();
            Assert.That(Get<bool>(director,"Running"),Is.False);Assert.That(Get<bool>(director,"ReturnDashObserved"),Is.False);
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            ((Behaviour)counter).enabled=true;
        }
        private void Step()=>Call(director,"StepCapture",1d/60);
        private static Component Find(string name)=>(Component)Object.FindFirstObjectByType(Type.GetType(name+", Assembly-CSharp",true));
        private static T Get<T>(object o,string name)
        {
            var p=o.GetType().GetProperty(name,Flags);return (T)(p!=null?p.GetValue(o):o.GetType().GetField(name,Flags).GetValue(o));
        }
        private static void Set(object o,string name,object value)=>o.GetType().GetProperty(name,Flags).SetValue(o,value);
        private static object Call(object o,string name,params object[] args)=>o.GetType().GetMethods(Flags).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(o,args);
    }
}
