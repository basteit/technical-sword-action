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
    public sealed class VideoStudioTests
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private Component studio,clock,state,motor,counter,health,presentation;
        private Type clockType;
        private Rigidbody2D body;
        [UnitySetUp] public IEnumerator Setup()
        {
            clockType=T("CombatTimeController");clock=(Component)Object.FindFirstObjectByType(clockType);
            Set(clock,"ManualAdvanceOnly",true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/CombatVideoStudio.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            studio=Find("CombatVideoStudio");state=Get<Component>(studio,"Player");
            motor=state.GetComponent(T("PlayerMotor2D"));counter=state.GetComponent(T("PlayerParryCounter2D"));health=state.GetComponent(T("PlayerDamageReceiver2D"));
            presentation=Get<Component>(studio,"Presentation");body=state.GetComponent<Rigidbody2D>();
            state.GetComponent(T("PlayerInputRouter")).GetType().GetProperty("enabled").SetValue(state.GetComponent(T("PlayerInputRouter")),false);
            Call(state,"ResetToSafeState","StudioTestReady",true);Step(2);
        }
        [UnityTearDown] public IEnumerator Teardown()
        { Call(studio,"ResetStudio");Set(clock,"ManualAdvanceOnly",false);yield return null; }
        private static Type T(string name)=>Type.GetType(name+", Assembly-CSharp",true);
        private static Component Find(string name)=>(Component)Object.FindFirstObjectByType(T(name));
        private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
        private static U Get<U>(object target,string name)=>(U)target.GetType().GetProperty(name,Flags).GetValue(target);
        private static void Set(object target,string name,object value)=>target.GetType().GetProperty(name,Flags).SetValue(target,value);
        private static object Field(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
        private static void Field(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        private Component[] Enemies()=>Get<IEnumerable>(studio,"Enemies").Cast<Component>().ToArray();
        private Component[] Gimmicks()=>Get<IEnumerable>(studio,"Gimmicks").Cast<Component>().ToArray();
        private void Load(int index)
        {
            Call(studio,"ApplyPreset",((Array)Get<object>(studio,"Presets")).GetValue(index));
            ((Behaviour)state.GetComponent(T("PlayerInputRouter"))).enabled=false;
            Call(state,"ResetToSafeState","StudioTestReady",true);Step(2);
        }
        private void Step(int count=1,int fps=60) { for(int i=0;i<count;i++)Call(studio,"StepStudio",1d/fps); }
        private void Pause(bool value)=>clockType.GetMethod("SetPaused").Invoke(null,new object[]{value});

        [TestCase(0,0,0)] [TestCase(1,1,0)] [TestCase(2,1,0)] [TestCase(3,0,3)] [TestCase(4,2,0)]
        public void EverySetCanBeLoadedWithoutMissingArtworkOrSharingMutableSetup(int index,int enemies,int gimmicks)
        {
            Load(index);Assert.That(Enemies().Length,Is.EqualTo(enemies));Assert.That(Gimmicks().Length,Is.EqualTo(gimmicks));
            Assert.That(Field(Get<Component>(studio,"Vfx"),"library"),Is.Not.Null);
            Assert.That(Field(state.GetComponent(T("VideoPlayerAnimationDriver")),"library"),Is.Not.Null);
            foreach(var enemy in Enemies())
                Assert.That(Field(enemy.GetComponent(T("VideoEnemyAnimationDriver")),"library"),Is.Not.Null);
            var snapshot=(ScriptableObject)Call(studio,"SnapshotPreset");
            var original=(ScriptableObject)Get<object>(studio,"CurrentPreset");
            Assert.That(snapshot,Is.Not.SameAs(original));
            if(enemies>0)
            {
                object copied=((Array)Field(snapshot,"enemies")).GetValue(0),source=((Array)Field(original,"enemies")).GetValue(0);
                Assert.That(copied,Is.Not.SameAs(source));Field(copied,"hp",99);Assert.That((int)Field(source,"hp"),Is.Not.EqualTo(99));
            }
            Object.DestroyImmediate(snapshot);
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void RearParryPreviewUsesTheRealSourceAndCounterOnlyHitsThatEnemy(int fps,bool mirrored)
        {
            Load(2);if(mirrored){Call(studio,"MirrorLayout");Step(2);}var enemy=Enemies()[0];var target=Get<Component>(enemy,"Health");
            Assert.That(Call(studio,"BeginParryPreview",enemy,true),Is.True);
            bool charged=false;
            for(int i=0;i<fps*4;i++)
            {
                Step(1,fps);charged|=Get<bool>(presentation,"IsCharged");
                if(Get<bool>(counter,"IsActive"))Assert.That(Get<Component>(counter,"SelectedTarget"),Is.SameAs(target));
            }
            Assert.That(charged,Is.True);Assert.That(Get<int>(counter,"HitCount"),Is.EqualTo(1));
            Assert.That(Get<int>(target,"HitCount"),Is.EqualTo(1));Assert.That(Get<int>(target,"DeathCount"),Is.EqualTo(1));
            Assert.That(Get<int>(health,"CurrentHp"),Is.EqualTo(Get<int>(health,"MaxHp")));
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<float>(presentation,"Zoom"),Is.Zero);Assert.That(Time.timeScale,Is.EqualTo(1));
            Assert.That(Get<int>(Get<Component>(studio,"Vfx"),"ActiveCount"),Is.Zero);
        }
        [Test] public void SingleAttackRejectsRepeatedRequestsAndDoesNotTurnIntoAnAutomaticLoop()
        {
            Load(2);var enemy=Enemies()[0];var setup=Call(Get<object>(enemy,"Setup"),"Copy");
            Field(setup,"mode",Enum.ToObject(T("StudioEnemyMode"),1));Call(enemy,"Configure",setup,state.transform);
            Assert.That(Call(enemy,"AttackOnce"),Is.True);
            for(int i=0;i<10;i++)Assert.That(Call(enemy,"AttackOnce"),Is.False);
            Step(360);
            Assert.That(Get<int>(health,"TotalHitsTaken"),Is.EqualTo(1));
            Assert.That(Get<long>(Get<Component>(enemy,"AI"),"AttackSequence"),Is.EqualTo(1));
            Assert.That(Get<string>(Get<Component>(enemy,"AI"),"CurrentStateName"),Is.EqualTo("Idle"));
        }
        [Test] public void ComboPreviewCanBeStartedAfterLongFreePlayAndUsesFourRealHits()
        {
            Load(1);Step(180);Call(studio,"BeginComboPreview");Step(220);
            var target=Get<Component>(Enemies()[0],"Health");
            Assert.That(Get<int>(target,"HitCount"),Is.EqualTo(4));Assert.That(Get<int>(target,"CurrentHp"),Is.EqualTo(13));
            Assert.That(Get<object>(state,"ActionState").ToString(),Is.EqualTo("Neutral"));
        }
        [Test] public void FreePlayDashTravelsFasterThanWalking()
        {
            Load(0);Call(motor,"SetMoveInput",Vector2.right);Step(10);float walk=body.position.x;
            Call(studio,"ResetStudio");Step(2);Call(studio,"RequestAction",PlayerActionRequest.Dash);Step(10);
            Assert.That(body.position.x,Is.GreaterThan(walk*1.5f));
        }
        [Test] public void StartingFromAnAuthoredLayoutKeepsSceneOverridesInsteadOfReapplyingTheSourceAsset()
        {
            Load(1);var enemy=Enemies()[0];var setup=Call(Get<object>(enemy,"Setup"),"Copy");Field(setup,"hp",11);
            Call(enemy,"Configure",setup,state.transform);enemy.transform.position=new Vector3(2.5f,1,0);
            Call(studio,"Start");Call(studio,"ResetStudio");
            Assert.That(Get<int>(Get<Component>(enemy,"Health"),"CurrentHp"),Is.EqualTo(11));
            Assert.That(enemy.transform.position,Is.EqualTo(new Vector3(2.5f,1,0)));
            var source=((Array)Field(Get<object>(studio,"CurrentPreset"),"enemies")).GetValue(0);
            Assert.That((int)Field(source,"hp"),Is.EqualTo(20));
        }
        [Test] public void ChaseAndMovingPlatformsFreezeWithPauseAndUseSlowCombatTicks()
        {
            Load(4);var enemy=Enemies()[0];Vector3 start=enemy.transform.position;Step(30);
            Assert.That(enemy.transform.position.x,Is.LessThan(start.x));Vector3 paused=enemy.transform.position;
            Pause(true);Step(60);Assert.That(enemy.transform.position,Is.EqualTo(paused));Pause(false);
            Load(3);var platform=Gimmicks().Last();Step(30);int before=Get<int>(platform,"ElapsedTicks");Vector3 p=platform.transform.position;
            Pause(true);Step(60);Assert.That(platform.transform.position,Is.EqualTo(p));Pause(false);
            clockType.GetMethod("RequestSlow").Invoke(null,new object[]{this,.25f,1f});Step(60);
            Assert.That(Get<int>(platform,"ElapsedTicks")-before,Is.InRange(14,16));
            Call(studio,"ResetStudio");Assert.That(Get<int>(platform,"ElapsedTicks"),Is.Zero);
            Assert.That(platform.transform.position,Is.EqualTo((Vector3)(Vector2)Field(Get<object>(platform,"Setup"),"position")));
            Assert.That(Time.timeScale,Is.EqualTo(1));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void MovingPlatformCarriesStandingPlayerAndResetsWithoutPositionDrift(int fps)
        {
            Load(3);var platform=Gimmicks().Last();var collider=platform.GetComponent<BoxCollider2D>();
            body.position=new Vector2(platform.transform.position.x,collider.bounds.max.y+1.005f);body.linearVelocity=Vector2.zero;
            Physics2D.SyncTransforms();float offset=body.position.x-platform.transform.position.x;
            Step(fps/2,fps);
            Assert.That(platform.transform.position.x,Is.GreaterThan(4.1f));
            Assert.That(body.position.x-platform.transform.position.x,Is.EqualTo(offset).Within(.08f));
            Assert.That(Get<bool>(motor,"IsGrounded"),Is.True);
            for(int i=0;i<10;i++){Call(studio,"ResetStudio");Step(60);}
            Call(studio,"ResetStudio");Assert.That(platform.transform.position,Is.EqualTo(new Vector3(4,1.6f,0)));
            Assert.That(body.position,Is.EqualTo(Vector2.up));
        }
        [Test] public void FloorDropAndCollisionIgnoresAreRestoredByReset()
        {
            Load(3);var platform=Gimmicks()[1];var collider=platform.GetComponent<BoxCollider2D>();
            body.position=new Vector2(-2,collider.bounds.max.y+1.005f);body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();Step(2);
            Call(motor,"SetMoveInput",Vector2.down);Call(motor,"SetJumpDownInput",true);Call(studio,"RequestAction",PlayerActionRequest.Jump);Step(2);
            Assert.That(Get<bool>(motor,"IsDroppingThrough"),Is.True);
            Call(studio,"ResetStudio");
            Assert.That(Get<bool>(motor,"IsDroppingThrough"),Is.False);
            Assert.That(Physics2D.GetIgnoreCollision(state.GetComponent<Collider2D>(),collider),Is.False);
        }
        [TestCase("Attack")] [TestCase("Dash")] [TestCase("Charge")] [TestCase("Dead")]
        public void TenResetsDuringActionsRemoveTimeCameraAiEffectsAndInputResidue(string phase)
        {
            Load(phase=="Charge"?2:1);
            for(int repeat=0;repeat<10;repeat++)
            {
                Call(studio,"ResetStudio");Step(2);
                if(phase=="Charge")
                {
                    Call(studio,"BeginParryPreview",Enemies()[0],true);
                    for(int i=0;i<180&&!Get<bool>(presentation,"IsCharged");i++)Step();
                    Assert.That(Get<bool>(presentation,"IsCharged"),Is.True);
                }
                else if(phase=="Dead")Call(state,"SetDead","StudioTest");
                else Call(studio,"RequestAction",phase=="Dash"?PlayerActionRequest.Dash:PlayerActionRequest.Attack);
                Step(2);Pause(true);Call(studio,"ResetStudio");
                Assert.That(state.transform.position,Is.EqualTo(Vector3.up));Assert.That(body.gravityScale,Is.EqualTo(3));
                Assert.That(Get<int>(health,"CurrentHp"),Is.EqualTo(Get<int>(health,"MaxHp")));
                Assert.That(Get<object>(state,"ActionState").ToString(),Is.EqualTo("Neutral"));
                Assert.That(Get<object>(state,"LifeState").ToString(),Is.EqualTo("Alive"));
                Assert.That(Get<int>(counter,"StockCount"),Is.Zero);Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
                Assert.That(Get<float>(motor,"MoveInput"),Is.Zero);Assert.That(Get<float>(motor,"DashCooldownRemaining"),Is.Zero);
                Assert.That(Get<int>(state,"ActiveCollisionIgnoreCount"),Is.Zero);
                Assert.That(Get<float>(presentation,"Zoom"),Is.Zero);Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
                Assert.That(Get<int>(presentation,"ActiveShakeCount"),Is.Zero);Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.True);
                Assert.That(Get<bool>(presentation,"IsTransitShadowVisible"),Is.False);
                Assert.That(Get<int>(Get<Component>(studio,"Vfx"),"ActiveCount"),Is.Zero);
                Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);Assert.That(Time.timeScale,Is.EqualTo(1));
                foreach(var enemy in Enemies())Assert.That(Get<int>(Get<Component>(enemy,"Health"),"CurrentHp"),Is.EqualTo(Get<int>(Get<Component>(enemy,"Health"),"MaxHp")));
            }
        }
        [Test] public void SwitchingSetsDuringChargeSafelyReleasesTheOldCounterTarget()
        {
            Load(2);Call(studio,"BeginParryPreview",Enemies()[0],true);
            for(int i=0;i<180&&!Get<bool>(presentation,"IsCharged");i++)Step();
            Assert.That(Get<bool>(presentation,"IsCharged"),Is.True);Load(0);
            Assert.That(Enemies(),Is.Empty);Assert.That(Get<bool>(counter,"IsActive"),Is.False);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);Assert.That(Time.timeScale,Is.EqualTo(1));
            Call(studio,"RequestAction",PlayerActionRequest.Jump);Step(2);Assert.That(body.position.y,Is.GreaterThan(1.05f));
        }
        [Test] public void CustomContentSurvivesSetChangesAndRestoresItsCapturedLocalTransformAndPhysics()
        {
            var parent=Get<Transform>(studio,"CustomContent");parent.position=new Vector3(6,0,0);
            var prop=new GameObject("Custom reset test");prop.transform.SetParent(parent,false);prop.transform.localPosition=new Vector3(1,2,0);
            var rigid=prop.AddComponent<Rigidbody2D>();rigid.gravityScale=.4f;
            var anchor=prop.AddComponent(T("StudioResetAnchor"));Call(anchor,"Capture");
            prop.transform.localPosition=Vector3.one*9;rigid.gravityScale=3;rigid.linearVelocity=Vector2.one*4;
            Load(0);Call(studio,"ResetStudio");
            Assert.That(prop,Is.Not.Null);Assert.That(prop.transform.localPosition,Is.EqualTo(new Vector3(1,2,0)));
            Assert.That(rigid.linearVelocity,Is.EqualTo(Vector2.zero));Assert.That(rigid.gravityScale,Is.EqualTo(.4f));
            Object.DestroyImmediate(prop);
        }
        [Test] public void SpawnedEnemyIdsStayDistinctAndRemovedEnemiesCannotKeepAttacking()
        {
            Load(2);var first=Enemies()[0];var setup=Call(Get<object>(first,"Setup"),"Copy");Field(setup,"position",new Vector2(1.3f,1));
            var second=(Component)Call(studio,"AddEnemy",setup);
            Assert.That(Get<string>(Get<Component>(first,"Health"),"IndividualId"),Is.Not.EqualTo(Get<string>(Get<Component>(second,"Health"),"IndividualId")));
            Assert.That(Call(first,"AttackOnce"),Is.True);Call(studio,"RemoveEnemy",first);Step(90);
            Assert.That(first.gameObject.activeSelf,Is.False);Assert.That(Get<int>(health,"TotalHitsTaken"),Is.Zero);
            Assert.That(Enemies().Length,Is.EqualTo(1));
        }
    }
}
