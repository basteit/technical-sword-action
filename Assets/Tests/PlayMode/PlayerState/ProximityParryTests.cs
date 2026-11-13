using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class ProximityParryTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private Component clock, player, arena, motor, parry, counter, health, ai, enemy;
        private Rigidbody2D body;
        private bool manual;
        private float gravity;
        private GameObject wall;
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name,Flags).Invoke(obj,args);
        private static V Get<V>(object obj,string name) => (V)(obj.GetType().GetProperty(name,Flags)?.GetValue(obj) ?? obj.GetType().GetField(name,Flags).GetValue(obj));
        private static void Set(object obj,string name,object value) => obj.GetType().GetField(name,Flags).SetValue(obj,value);
        private void Frames(int count) { for(int i=0;i<count;i++) Call(clock,"AdvanceFrame",1d/60); }
        private void Until(Func<bool> done)
        { int limit=600; while(!done()&&--limit>0)Frames(1); Assert.That(done(),Is.True,"Timed out waiting for attack/parry."); }
        private Component AI(string name) => player.gameObject.scene.GetRootGameObjects().Single(go=>go.name==name).GetComponent(T("EnemyMeleeChaser2D"));
        private void Begin(Component attacker,Vector2 position,int direction)
        {
            Call(attacker.GetComponent(T("Damageable2D")),"ResetForCapture",(Vector3)position,1000);
            Call(attacker,"ResetForCapture",player.transform,true);
            Assert.That(Call(attacker,"BeginCaptureAttack"),Is.True);
            Set(attacker,"facing",Vector2.right*direction); Physics2D.SyncTransforms();
        }
        private void ReadyParry()
        {
            Until(()=>Get<int>(ai,"WindupRemainingFrames")<=2);
            Assert.That(Get<int>(parry,"SuccessCount"),Is.Zero,"Windup alone cannot be parried.");
            Assert.That(Call(player,"RequestAction",PlayerActionRequest.Parry),Is.True);
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            clock=(Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            manual=Get<bool>(clock,"ManualAdvanceOnly");clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock,true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player=(Component)Object.FindFirstObjectByType(T("PlayerStateMachine"));arena=(Component)Object.FindFirstObjectByType(T("PrototypeCombatArena"));
            motor=player.GetComponent(T("PlayerMotor2D"));parry=player.GetComponent(T("PlayerParry2D"));
            counter=player.GetComponent(T("PlayerParryCounter2D"));health=player.GetComponent(T("PlayerDamageReceiver2D"));
            body=player.GetComponent<Rigidbody2D>();gravity=body.gravityScale;
            ((Behaviour)player.GetComponent(T("PlayerInputRouter"))).enabled=false;
            body.position=new Vector2(0,.81f);player.transform.position=body.position;body.linearVelocity=Vector2.zero;
            Call(motor,"SetMoveInput",Vector2.zero);Call(motor,"SetFacing",1);
            ai=AI("Melee_1");enemy=ai.GetComponent(T("Damageable2D"));
            Physics2D.SyncTransforms();Frames(3);yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            T("CombatTimeController").GetMethod("SetPaused").Invoke(null,new object[]{false});
            if(wall!=null)Object.DestroyImmediate(wall);
            body.gravityScale=gravity;Call(arena,"ResetActionsOnly");
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock,manual);yield return null;
        }
        private static IEnumerable<TestCaseData> Directions()
        {
            foreach(int side in new[]{-1,1}) foreach(int attackDirection in new[]{-1,1})
                foreach(int playerDirection in new[]{-1,1}) foreach(bool airborne in new[]{false,true})
                    yield return new TestCaseData(side,attackDirection,playerDirection,airborne);
        }
        [TestCaseSource(nameof(Directions))]
        public void NearbyActiveAttackParriesRegardlessOfBothFacings(int side,int attackDirection,int playerDirection,bool airborne)
        {
            if(airborne){body.position=new Vector2(0,5);player.transform.position=body.position;body.gravityScale=0;}
            Call(motor,"SetFacing",playerDirection);
            Begin(ai,body.position+Vector2.right*(side*1.6f),attackDirection);ReadyParry();
            Until(()=>Get<int>(parry,"SuccessCount")==1);
            Assert.That(Get<object>(parry,"LastParriedEnemy"),Is.SameAs(enemy));
            Assert.That(Get<int>(motor,"FacingSign"),Is.EqualTo(side));
            Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));
            Assert.That(Get<bool>(enemy,"IsParryStaggered"),Is.True);
            Assert.That(Get<int>(health,"CurrentHp"),Is.EqualTo(100));
            Frames(100);Assert.That(Get<int>(parry,"SuccessCount"),Is.EqualTo(1));
            Assert.That(Get<int>(health,"BlockedByParry"),Is.EqualTo(1));
        }
        [TestCase("Distance")] [TestCase("Height")] [TestCase("Unparryable")]
        [TestCase("TooEarly")] [TestCase("NoInput")] [TestCase("Wall")]
        public void ProximityDoesNotBypassOtherAcceptanceRules(string reason)
        {
            Vector2 offset=reason=="Distance"?new Vector2(2.6f,0):reason=="Height"?new Vector2(.5f,1.6f):new Vector2(1.6f,0);
            Begin(ai,body.position+offset,1);
            if(reason=="Unparryable")Set(ai,"configuredParryable",false);
            if(reason=="Wall")
            {
                int mask=Get<LayerMask>(Get<object>(ai,"Definition"),"obstacleLayers").value;
                wall=new GameObject("ProximityParryWall");wall.layer=Enumerable.Range(0,32).First(i=>(mask&(1<<i))!=0);
                wall.transform.position=new Vector3(.8f,.81f);wall.AddComponent<BoxCollider2D>().size=new Vector2(.1f,3);
                Physics2D.SyncTransforms();
            }
            if(reason=="TooEarly")Assert.That(Call(player,"RequestAction",PlayerActionRequest.Parry),Is.True);
            else if(reason!="NoInput")ReadyParry();
            Frames(130);
            Assert.That(Get<int>(parry,"SuccessCount"),Is.Zero);
            Assert.That(Get<int>(counter,"StockCount"),Is.Zero);
            Assert.That(Get<int>(health,"BlockedByParry"),Is.Zero);
        }
        [TestCase(2.5f,0f,true)] [TestCase(-2.5f,0f,true)]
        [TestCase(1.5f,1.5f,true)] [TestCase(-1.5f,-1.5f,true)]
        [TestCase(2.51f,0f,false)] [TestCase(.5f,1.51f,false)]
        public void ExpandedSpatialBoundaryParriesAndCanStartItsCounter(float x,float y,bool accepted)
        {
            Assert.That(Get<float>(parry,"SourceRange"),Is.EqualTo(2.5f));
            Assert.That(Get<float>(parry,"MaxSourceHeightDifference"),Is.EqualTo(1.5f));
            Assert.That(Get<int>(parry,"AcceptanceStartFrame"),Is.Zero);
            Assert.That(Get<int>(parry,"AcceptanceEndFrame"),Is.EqualTo(13));
            // Keep the lower-left counter route above the upper platform, including the enemy's parry retreat.
            body.position=new Vector2(0,10);player.transform.position=body.position;body.gravityScale=0;
            Begin(ai,body.position+new Vector2(x,y),x>0?1:-1); // Swing away: do not rely on the damage hitbox.
            ReadyParry();
            if(!accepted)
            {
                Frames(130);Assert.That(Get<int>(parry,"SuccessCount"),Is.Zero);
                Assert.That(Get<int>(counter,"StockCount"),Is.Zero);return;
            }
            Until(()=>Get<int>(parry,"SuccessCount")==1);
            Assert.That(Get<int>(health,"CurrentHp"),Is.EqualTo(100));
            Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));
            Assert.That(Call(counter,"EligibilityReason",enemy),Is.EqualTo("Eligible"),
                $"Counter path from {player.transform.position} to {enemy.transform.position}");
            Assert.That(Call(player,"RequestAction",PlayerActionRequest.Attack),Is.True);
            Until(()=>Get<bool>(counter,"IsActive"));
            Assert.That(Get<object>(counter,"SelectedTarget"),Is.SameAs(enemy));
            Until(()=>Get<int>(counter,"HitCount")==1);
            Assert.That(Get<int>(enemy,"CurrentHp"),Is.EqualTo(910));
        }

        [Test]
        public void CounterReadsLiveParryRangeAndHeightInsteadOfAnIndependentFixedLimit()
        {
            Begin(ai,body.position+Vector2.right*2.8f,1);
            Call(counter,"RegisterParriedEnemy",enemy);
            Assert.That(Call(counter,"EligibilityReason",enemy),Is.EqualTo("OutOfRange"));
            Set(parry,"parrySnapDistance",3f);
            Assert.That(Call(counter,"EligibilityReason",enemy),Is.EqualTo("Eligible"));
            enemy.transform.position=player.transform.position+new Vector3(1.5f,1.4f);
            Set(parry,"maxParryHeightDifference",1.3f);Physics2D.SyncTransforms();
            Assert.That(Call(counter,"EligibilityReason",enemy),Is.EqualTo("Height"));
            Set(parry,"maxParryHeightDifference",1.5f);
            Assert.That(Call(counter,"EligibilityReason",enemy),Is.EqualTo("Eligible"));
        }

        [Test] public void AFailedProximityProbeDoesNotEatTheOrdinaryDamageHit()
        {
            Begin(ai,body.position+Vector2.right, -1);Frames(130);
            Assert.That(Get<int>(health,"TotalHitsTaken"),Is.EqualTo(1));
            Assert.That(Get<int>(health,"CurrentHp"),Is.EqualTo(90));
            Assert.That(Get<int>(parry,"SuccessCount"),Is.Zero);
        }
        [Test] public void TwoAwayFacingSourcesGiveTwoStocksButOnePrimarySuccess()
        {
            var second=AI("Melee_2");
            Set(second,"attackCoordinator",null); // Isolate same-tick resolution from the scene's attack-role cap.
            Begin(ai,body.position+Vector2.right*1.6f,1);
            Begin(second,body.position+Vector2.left*1.6f,-1);ReadyParry();
            Until(()=>Get<int>(counter,"StockCount")==2);
            Assert.That(Get<int>(parry,"SuccessCount"),Is.EqualTo(1));
            Assert.That(Get<int>(health,"BlockedByParry"),Is.EqualTo(2));
            Assert.That(Get<int>(health,"CurrentHp"),Is.EqualTo(100));
            Assert.That(Get<bool>(second.GetComponent(T("Damageable2D")),"IsParryStaggered"),Is.True);
        }
        [Test] public void LegacyWhiffStillAllowsTheExistingParryRangeWithoutGrowingDamage()
        {
            Set(ai,"combatDefinition",null);Call(ai,"ConfigureForTraining",player.transform,.05f,.55f,10,0f);
            Begin(ai,body.position+Vector2.right*1.7f,1);
            Assert.That(Call(player,"RequestAction",PlayerActionRequest.Parry),Is.True);
            Until(()=>Get<int>(parry,"SuccessCount")==1);
            Assert.That(Get<int>(health,"CurrentHp"),Is.EqualTo(100));
        }
    }
}
