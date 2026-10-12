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
    public sealed class CombatVideoIntegrationTests
    {
        private Component director,clock,state,counter,health,presentation;
        private Type clockType;
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        [UnitySetUp] public IEnumerator Setup()
        {
            clockType=Type.GetType("CombatTimeController, Assembly-CSharp",true);
            clock=(Component)Object.FindFirstObjectByType(clockType);Set(clock,"ManualAdvanceOnly",true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/CombatVideo.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            BindScene();
        }
        private void BindScene()
        {
            director=Find("CombatVideoDirector");state=(Component)Get<object>(director,"Player");
            counter=state.GetComponent(Type.GetType("PlayerParryCounter2D, Assembly-CSharp",true));
            health=state.GetComponent(Type.GetType("PlayerDamageReceiver2D, Assembly-CSharp",true));
            presentation=Find("VideoCombatPresentation");
        }
        [UnityTearDown] public IEnumerator Teardown()
        { Call(director,"ResetTake",2);Set(clock,"ManualAdvanceOnly",false);yield return null; }
        [Test] public void TwoAndFourStagesAndBothDirectionsResetTenTimesEach()
        {
            var utility=Type.GetType("VideoCaptureUtility, Assembly-CSharp-Editor",true);
            string report=(string)utility.GetMethod("ValidateResetRuns").Invoke(null,null);
            Assert.That(report,Does.Contain("\"runsPassed\": 40"));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ActualScenePreservesHitsTargetsAndTimeAtEveryRenderRate(int fps)
        {
            Call(director,"StartTake",2,true);
            for(int frame=0;frame<fps*6&&!Get<bool>(director,"Completed");frame++) Call(director,"StepCapture",1d/fps);
            Assert.That(Get<bool>(director,"Completed"),Is.True);
            Assert.That(Get<int>((Component)Get<object>(director,"FrontEnemy"),"HitCount"),Is.EqualTo(2));
            Assert.That(Get<int>((Component)Get<object>(director,"RearEnemy"),"HitCount"),Is.EqualTo(1));
            Assert.That(Get<double>(director,"RealElapsed"),Is.InRange(3.80d,3.88d));
            Assert.That(Get<float>(presentation,"ChargeRealElapsed"),Is.InRange(.63f,.71f));
        }
        [TestCase("TargetLost")] [TestCase("CounterDisabled")] [TestCase("PlayerDead")] [TestCase("ForcedHit")]
        public void ChargeInterruptionReleasesTimeGravityInvulnerabilityAndCamera(string interruption)
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(presentation,"IsCharged");i++) Step();
            Assert.That(Get<bool>(presentation,"IsCharged"),Is.True);
            for(int i=0;i<5;i++) Step();
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.GreaterThan(0));
            int hits=Get<int>(counter,"HitCount");
            if(interruption=="TargetLost") ((Component)Get<object>(counter,"SelectedTarget")).gameObject.SetActive(false);
            if(interruption=="CounterDisabled") ((Behaviour)counter).enabled=false;
            if(interruption=="PlayerDead") Call(state,"SetDead","VideoTestDeath");
            if(interruption=="ForcedHit") Call(state,"ForceHit","VideoTestInterrupt");
            for(int i=0;i<30;i++) Step();
            Assert.That(Get<int>(counter,"HitCount"),Is.EqualTo(hits));
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
            Assert.That(Get<float>(presentation,"Zoom"),Is.Zero);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.False);
            Assert.That(state.GetComponent<Rigidbody2D>().gravityScale,Is.EqualTo(interruption=="PlayerDead"?0:3));
            ((Behaviour)counter).enabled=true;
            Call(director,"ResetTake",2);Assert.That(state.GetComponent<Rigidbody2D>().gravityScale,Is.EqualTo(3));
        }
        [Test] public void KickInvulnerabilityRejectsAnotherAttackerAndEndsOnReset()
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(counter,"IsActive");i++) Step();
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.True);
            int hp=Get<int>(health,"CurrentHp");
            Assert.That((bool)Call(health,"TryReceiveHit",1,(Vector2)state.transform.position+Vector2.right,0f),Is.False);
            Assert.That(Get<int>(health,"CurrentHp"),Is.EqualTo(hp));
            Call(director,"ResetTake",2);
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
            Assert.That((bool)Call(health,"TryReceiveHit",1,(Vector2)state.transform.position+Vector2.right,0f),Is.True);
        }
        [Test] public void PauseFreezesChargeAndResumesWithoutResidualOwners()
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(presentation,"IsCharged");i++) Step();
            int elapsed=Get<int>(counter,"ElapsedFrames");float charge=Get<float>(presentation,"ChargeRealElapsed");
            float dim=Get<float>(presentation,"DimAlpha");
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});
            for(int i=0;i<120;i++) Step();
            Assert.That(Get<int>(counter,"ElapsedFrames"),Is.EqualTo(elapsed));
            Assert.That(Get<float>(presentation,"ChargeRealElapsed"),Is.EqualTo(charge));
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.EqualTo(dim));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            for(int i=0;i<180&&!Get<bool>(director,"Completed");i++) Step();
            Assert.That(Get<bool>(director,"Completed"),Is.True);
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void SlowDimCoversChargeAndClearsImmediatelyOnResetAtEveryRenderRate(int fps)
        {
            Call(director,"StartTake",2,true);
            for(int frame=0;frame<fps*5&&!Get<bool>(presentation,"IsCharged");frame++) Call(director,"StepCapture",1d/fps);
            for(int frame=0;frame<Mathf.CeilToInt(fps*.12f);frame++) Call(director,"StepCapture",1d/fps);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.EqualTo(.14f).Within(.001f));
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.True);
            Assert.That(Camera.main.transform.Find("VideoSlowDimmer"),Is.Not.Null);
            Call(director,"ResetTake",2);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.False);
        }
        [UnityTest] public IEnumerator SceneReloadDuringChargeReleasesAllRequestsAndReferences()
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(presentation,"IsCharged");i++) Step();
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.True);
            var previousDimmer=Camera.main.transform.Find("VideoSlowDimmer");
            Assert.That(previousDimmer,Is.Not.Null);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/CombatVideo.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;BindScene();
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
            Assert.That((int)clockType.GetProperty("HitstopOwnerCount").GetValue(null),Is.Zero);
            Assert.That(Get<float>(presentation,"Zoom"),Is.Zero);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.False);
            Assert.That(previousDimmer==null,Is.True,"Previous scene's overlay survived unload.");
            Assert.That(Camera.main.GetComponentsInChildren<SpriteRenderer>(true).Count(r=>r.name=="VideoSlowDimmer"),Is.EqualTo(1));
        }
        [Test] public void PresentationDisableClearsDimAndReenableReusesItsOverlay()
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(presentation,"IsCharged");i++) Step();
            for(int i=0;i<6;i++) Step();
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.GreaterThan(0));
            var dimmer=Camera.main.transform.Find("VideoSlowDimmer");
            ((Behaviour)presentation).enabled=false;
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.False);
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
            ((Behaviour)presentation).enabled=true;
            Assert.That(Camera.main.transform.Find("VideoSlowDimmer"),Is.SameAs(dimmer));
            for(int i=0;i<180&&!Get<bool>(director,"Completed");i++) Step();
            Assert.That(Get<bool>(director,"Completed"),Is.True);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.False);
        }
        [Test] public void SettingDimStrengthToZeroDuringChargeLeavesNoDarknessAfterwards()
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(presentation,"IsCharged");i++) Step();
            for(int i=0;i<6;i++) Step();
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.GreaterThan(0));
            presentation.GetType().GetField("slowDimStrength",Flags).SetValue(presentation,0f);Step();
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.False);
            for(int i=0;i<180&&!Get<bool>(director,"Completed");i++) Step();
            Assert.That(Get<bool>(director,"Completed"),Is.True);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            presentation.GetType().GetField("slowDimStrength",Flags).SetValue(presentation,.14f);
            Call(director,"ResetTake",2);
        }
        [TestCase(-1f,0f,true,true)] [TestCase(1f,0f,true,true)]
        [TestCase(-2.2f,0f,true,false)] [TestCase(-1f,1.3f,true,false)] [TestCase(-1f,0f,false,false)]
        public void SourceParryUsesBothDirectionsRangeHeightAndParryableFlag(float dx,float dy,bool parryable,bool expected)
        {
            Call(director,"ResetTake",2);
            var source=(Component)Get<object>(director,"RearEnemy");source.transform.position=state.transform.position+new Vector3(dx,dy,0);
            var parry=state.GetComponent(Type.GetType("PlayerParry2D, Assembly-CSharp",true));
            Call(state,"RequestAction",PlayerActionRequest.Parry);Step();
            var context=Activator.CreateInstance(Type.GetType("CombatHitContext2D, Assembly-CSharp",true),source,51L,parryable);
            Call(health,"TryReceiveContextHit",1,(Vector2)source.transform.position,0f,context);
            Assert.That(Get<object>(parry,"LastParriedEnemy"),expected?Is.SameAs(source):Is.Null);
            var motor=state.GetComponent(Type.GetType("PlayerMotor2D, Assembly-CSharp",true));
            Assert.That(Get<int>(motor,"FacingSign"),Is.EqualTo(expected&&dx<0?-1:1));
            int count=Get<int>(parry,"SuccessCount");
            Call(health,"TryReceiveContextHit",1,(Vector2)source.transform.position,0f,context);
            Assert.That(Get<int>(parry,"SuccessCount"),Is.EqualTo(count));
            var other=(Component)Get<object>(director,"FrontEnemy");
            Assert.That(Get<bool>(other,"IsStunned"),Is.False);
            Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(expected?1:0));
        }
        [Test] public void EmptySlashProducesNoHitstopOrHitEffects()
        {
            Call(director,"ResetTake",2);
            ((Component)Get<object>(director,"FrontEnemy")).transform.position+=Vector3.right*6;
            Physics2D.SyncTransforms();Call(state,"RequestAction",PlayerActionRequest.Attack);
            for(int frame=0;frame<36;frame++) Step();
            Assert.That((int)clockType.GetProperty("HitstopOwnerCount").GetValue(null),Is.Zero);
            Assert.That(Get<int>(Find("VideoVfxRenderer"),"EmissionCount"),Is.Zero);
            Assert.That(Get<PlayerActionState>(state,"ActionState"),Is.EqualTo(PlayerActionState.Neutral));
        }
        [Test] public void SkippedOldStockIsPreservedAndOverlappingOtherEnemyIsNeverKicked()
        {
            Call(director,"ResetTake",2);
            var a=(Component)Get<object>(director,"FrontEnemy");var b=(Component)Get<object>(director,"RearEnemy");
            a.transform.position=state.transform.position+Vector3.right*5;b.transform.position=state.transform.position+Vector3.right;
            Call(counter,"RegisterParriedEnemy",a);Call(counter,"RegisterParriedEnemy",a);Call(counter,"RegisterParriedEnemy",b);
            Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(2));
            Call(state,"RequestAction",PlayerActionRequest.Attack);Step();
            Assert.That(Get<object>(counter,"SelectedTarget"),Is.SameAs(b));
            Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));
            a.transform.position=b.transform.position;Physics2D.SyncTransforms();int hp=Get<int>(a,"CurrentHp");
            for(int i=0;i<100&&Get<bool>(counter,"IsActive");i++) Step();
            Assert.That(Get<int>(a,"CurrentHp"),Is.EqualTo(hp));
            Assert.That(Get<int>(b,"CurrentHp"),Is.Zero);
            Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));
        }
        private void Step()=>Call(director,"StepCapture",1d/60d);
        [TestCase(false)] [TestCase(true)]
        public void WallBlocksStartOrImpactWithoutWrongDamageOrResidualOwnership(bool afterStart)
        {
            Call(director,"ResetTake",2);var a=(Component)Get<object>(director,"FrontEnemy");Call(counter,"RegisterParriedEnemy",a);
            if(afterStart) { Call(state,"RequestAction",PlayerActionRequest.Attack);Step();Assert.That(Get<bool>(counter,"IsActive"),Is.True); }
            var wall=new GameObject("VideoCounterWall");wall.layer=LayerMask.NameToLayer("Ground");wall.transform.position=new Vector3(.55f,1.2f,0);
            wall.AddComponent<BoxCollider2D>().size=new Vector2(.1f,3.4f);Physics2D.SyncTransforms();
            try
            {
                if(!afterStart)
                {
                    Assert.That(Get<bool>(counter,"CanStartAction"),Is.False);
                    Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));
                }
                else
                {
                    for(int i=0;i<180&&Get<bool>(counter,"IsActive");i++) Step();
                    Assert.That(Get<int>(a,"CurrentHp"),Is.EqualTo(2));
                    Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
                    Assert.That(Get<bool>(a,"IsStunned"),Is.False);
                    Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
                    Assert.That(Get<float>(presentation,"Zoom"),Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(wall); }
        }
        private static Component Find(string name)=>(Component)Object.FindFirstObjectByType(Type.GetType(name+", Assembly-CSharp",true));
        private static T Get<T>(object target,string name)=>(T)target.GetType().GetProperty(name,Flags).GetValue(target);
        private static void Set(object target,string name,object value)=>target.GetType().GetProperty(name,Flags).SetValue(target,value);
        private static object Call(object target,string name,params object[] values)
        { return target.GetType().GetMethods(Flags).Single(m=>m.Name==name&&m.GetParameters().Length==values.Length).Invoke(target,values); }
    }
}
