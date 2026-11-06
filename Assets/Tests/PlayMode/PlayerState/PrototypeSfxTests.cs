using System;
using System.Collections;
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
    public sealed class PrototypeSfxTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private Component clock, player, motor, attack, counter, presentation, feedback, sfx, arena;
        private Rigidbody2D body;
        private bool savedManual;
        private ScriptableObject audioProfileOverride;
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
        private static V Get<V>(object obj, string name) => (V)(obj.GetType().GetProperty(name, Flags)?.GetValue(obj) ?? obj.GetType().GetField(name, Flags).GetValue(obj));
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
        private static void Static(string name, params object[] args) => T("CombatTimeController").GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        private int Count(string name) => (int)Call(sfx, "Count", Enum.Parse(T("PrototypeSfxCue"), name));
        private void Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                Call(clock, "AdvanceFrame", 1d/60);
                Call(presentation, "AdvanceVisuals", 1f/60); Call(feedback, "AdvanceVisuals", 1f/60);
                Call(sfx,"AdvanceAudio",1f/60);
            }
        }
        private void Until(Func<bool> condition, int limit = 360)
        {
            while (!condition() && --limit > 0) Frames(1);
            Assert.That(limit, Is.GreaterThan(0), "Timed out waiting for gameplay/audio condition.");
        }
        private void Request(PlayerActionRequest action) { Assert.That(Call(player, "RequestAction", action), Is.True); Frames(1); }
        private AudioSource[] Sources => player.GetComponentsInChildren<AudioSource>(true).Where(a => (bool)Call(sfx,"OwnsAudioSource",a)).ToArray();
        private Component Enemy(string name = "StrongMelee", float x = 1.15f, int hp = 120)
        {
            var go = player.gameObject.scene.GetRootGameObjects().Single(r => r.name == name);
            var health = go.GetComponent(T("Damageable2D"));
            Call(health, "ResetForCapture", new Vector3(x,.81f), hp);
            Call(go.GetComponent(T("EnemyMeleeChaser2D")), "ResetForCapture", player.transform, true);
            Physics2D.SyncTransforms(); return health;
        }
        private void BeginCounter(bool enableAccent = true)
        {
            if (enableAccent)
            {
                // Exercise the optional path without changing the shared default-OFF asset.
                audioProfileOverride = Object.Instantiate((ScriptableObject)Get<object>(sfx,"Profile"));
                Set(audioProfileOverride,"counterAccentEnabled",true);
                Set(sfx,"profile",audioProfileOverride);
            }
            var enemy = Enemy(); Call(counter, "RegisterParriedEnemy", enemy);
            Request(PlayerActionRequest.Attack); Assert.That(Get<bool>(counter,"IsActive"), Is.True);
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            audioProfileOverride = null;
            clock = (Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            savedManual = Get<bool>(clock,"ManualAdvanceOnly"); clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock,true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player = (Component)Object.FindFirstObjectByType(T("PlayerStateMachine"));
            motor=player.GetComponent(T("PlayerMotor2D")); attack=player.GetComponent(T("PlayerAttack2D")); counter=player.GetComponent(T("PlayerParryCounter2D"));
            presentation=player.GetComponent(T("PrototypeCounterPresentation")); feedback=player.GetComponent(T("CombatFeedback2D"));
            sfx=player.GetComponent(T("PrototypeSfx2D")); arena=(Component)Object.FindFirstObjectByType(T("PrototypeCombatArena"));
            Assert.That(sfx,Is.Not.Null); Assert.That(Get<bool>(sfx,"IsReady"),Is.True);
            ((Behaviour)player.GetComponent(T("PlayerInputRouter"))).enabled=false;
            body=player.GetComponent<Rigidbody2D>(); body.position=new Vector2(0,.81f); player.transform.position=body.position;
            body.linearVelocity=Vector2.zero; Call(motor,"SetFacing",1); Call(motor,"SetMoveInput",Vector2.zero);
            var settings=Get<object>(feedback,"Settings"); Set(settings,"masterVolume",1f); Set(settings,"sfxVolume",1f); Set(settings,"shake",0f);
            Frames(5); Call(sfx,"ResetAudio"); Frames(2); yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            Static("SetPaused",false);
            if (player != null && !player.gameObject.activeSelf) player.gameObject.SetActive(true);
            if (arena != null) Call(arena,"ResetActionsOnly");
            if (audioProfileOverride != null) Object.Destroy(audioProfileOverride);
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock,savedManual); yield return null;
        }
        [Test] public void SpawnAndIdleAreSilent()
        { Frames(40); Assert.That(Get<int>(sfx,"PlayedCount"),Is.Zero); }
        [TestCase(1)] [TestCase(-1)]
        public void WalkingAlternatesQuietFootstepsButStandingAndAirborneDoNot(int direction)
        {
            Call(motor,"SetMoveInput",Vector2.right*direction); Frames(70);
            Assert.That(Count("Footstep"),Is.GreaterThanOrEqualTo(2));
            Call(motor,"SetMoveInput",Vector2.zero); int n=Count("Footstep"); Frames(20); Assert.That(Count("Footstep"),Is.EqualTo(n));
            Request(PlayerActionRequest.Jump); Call(motor,"SetJumpHeld",true); Call(motor,"SetMoveInput",Vector2.right*direction); Frames(12);
            Assert.That(Count("Footstep"),Is.EqualTo(n)); Assert.That(Count("Jump"),Is.EqualTo(1));
        }
        [Test] public void JumpAndLandingAreSingleCues()
        {
            Request(PlayerActionRequest.Jump); Call(motor,"SetJumpHeld",true); Frames(12); Call(motor,"SetJumpHeld",false);
            Until(()=>Get<bool>(motor,"IsGrounded")); Frames(3);
            Assert.That(Count("Jump"),Is.EqualTo(1)); Assert.That(Count("Land"),Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)]
        public void DashSoundsOnlyAtAcceptedGroundOrAirStart(bool airborne)
        {
            if(airborne) { Request(PlayerActionRequest.Jump); Frames(4); }
            Request(PlayerActionRequest.Dash); Frames(10); Assert.That(Count("Dash"),Is.EqualTo(1));
            Assert.That(Sources.Any(a=>a.isPlaying&&a.clip!=null&&a.clip.name=="Dash"&&!a.loop),Is.True);
            Assert.That(Count("LightHit")+Count("HeavyHit")+Count("CounterHit"),Is.Zero);
            if(airborne) { Request(PlayerActionRequest.Dash); Assert.That(Count("Dash"),Is.EqualTo(1)); }
        }
        [TestCase(false)] [TestCase(true)]
        public void FourStepWhiffsHaveOneTimedSwingPerStepAndOnlyOneFinalSheathe(bool airborne)
        {
            if(airborne) { Request(PlayerActionRequest.Jump); Call(motor,"SetJumpHeld",true); Frames(5); }
            Request(PlayerActionRequest.Attack);
            for(int step=1;step<=4;step++)
            {
                Assert.That(Count("Swing"+step),Is.Zero,"Do not play a swing at the initial button press.");
                Until(()=>Count("Swing"+step)==1);
                if(step<4) { Until(()=>Get<object>(attack,"Phase").ToString()=="Cancel"); Request(PlayerActionRequest.Attack); }
            }
            Until(()=>!Get<bool>(attack,"IsAttacking")&&!Get<bool>(attack,"IsRecovering")); Frames(2);
            for(int step=1;step<=4;step++) Assert.That(Count("Swing"+step),Is.EqualTo(1));
            Assert.That(Count("LightHit"),Is.Zero); Assert.That(Count("HeavyHit"),Is.Zero); Assert.That(Count("Sheathe"),Is.EqualTo(1));
        }
        [Test] public void MultipleCollidersAndSimultaneousEnemiesDoNotStackHitAudio()
        {
            var a=Enemy("Melee_1",1.15f,1000); var b=Enemy("Melee_2",1.3f,1000);
            a.gameObject.AddComponent<BoxCollider2D>().isTrigger=true; Physics2D.SyncTransforms();
            Request(PlayerActionRequest.Attack); Until(()=>Get<int>(a,"HitCount")>0); Frames(3);
            Assert.That(Get<int>(a,"HitCount"),Is.EqualTo(1)); Assert.That(Get<int>(b,"HitCount"),Is.EqualTo(1));
            Assert.That(Count("LightHit"),Is.EqualTo(1)); Assert.That(Count("Swing1"),Is.EqualTo(1));
        }
        [Test] public void InterruptedWindupDoesNotPlayItsPendingSwingOrSheathe()
        {
            Request(PlayerActionRequest.Attack);
            Call(player.GetComponent(T("PlayerDamageReceiver2D")),"TryReceiveHit",1,new Vector2(2,.81f),0f);
            Frames(60); Assert.That(Count("Swing1"),Is.Zero); Assert.That(Count("Sheathe"),Is.Zero); Assert.That(Count("PlayerHit"),Is.EqualTo(1));
        }
        [TestCase(1,"JustParry")] [TestCase(10,"Parry")]
        public void ParryClangWaitsForTheSynchronizedVisualImpact(int frame,string cue)
        {
            var target=Enemy("Melee_1"); var parry=player.GetComponent(T("PlayerParry2D"));
            var ai=target.GetComponent(T("EnemyMeleeChaser2D")); Assert.That(Call(ai,"BeginCaptureAttack"),Is.True);
            Until(()=>Get<int>(ai,"WindupRemainingFrames")<=Mathf.Max(2,frame));
            Request(PlayerActionRequest.Parry); Until(()=>Get<int>(parry,"SuccessCount")>0);
            Assert.That(Count(cue),Is.Zero); Assert.That(Count("ParryReady"),Is.EqualTo(1));
            Until(()=>Get<int>(presentation,"ParryImpactCount")==1);
            Assert.That(Count(cue),Is.EqualTo(1)); Assert.That(Get<long>(sfx,"LastCueTick"),Is.EqualTo(Get<long>(presentation,"ParryImpactTick")));
            Frames(35); Assert.That(Count(cue),Is.EqualTo(1)); Assert.That(Count("ParryMiss"),Is.Zero);
        }
        [Test] public void FailedParryHasOnlyTheQuietMissCue()
        {
            Request(PlayerActionRequest.Parry); Frames(50);
            Assert.That(Count("ParryMiss"),Is.EqualTo(1)); Assert.That(Count("Parry"),Is.Zero); Assert.That(Count("JustParry"),Is.Zero);
        }
        [Test] public void ParryReadySparklesBeforeAnySuccessfulContact()
        {
            Request(PlayerActionRequest.Parry);
            Assert.That(Count("ParryReady"),Is.EqualTo(1));
            Assert.That(Sources.Any(a=>a.isPlaying&&a.clip!=null&&a.clip.name=="ParryReady"&&!a.loop),Is.True);
            Assert.That(Count("Parry")+Count("JustParry"),Is.Zero);
            Frames(50); Assert.That(Count("ParryReady"),Is.EqualTo(1));
        }
        [Test] public void CounterElectricityIsOffByDefaultButTheKickStillHits()
        {
            Assert.That(Get<bool>(Get<object>(sfx,"Profile"),"counterAccentEnabled"),Is.False);
            BeginCounter(false); Until(()=>Get<int>(counter,"HitCount")==1);
            Assert.That(Count("CounterCharge"),Is.Zero);
            Assert.That(Get<bool>(sfx,"ChargeAccentActive"),Is.False);
            Assert.That(Sources.Any(a=>a.isPlaying&&a.clip!=null&&a.clip.name=="CounterCharge"),Is.False);
            Assert.That(Count("CounterHit"),Is.EqualTo(1));
            Assert.That(Get<int>(counter,"SelectedDamage"),Is.EqualTo(90));
        }
        [TestCase(false)] [TestCase(true)]
        public void DisablingTheOptionalAccentCancelsPendingOrPlayingAudio(bool afterPeak)
        {
            BeginCounter();
            if(afterPeak) Until(()=>Count("CounterCharge")==1);
            else Until(()=>Get<int>(counter,"ElapsedFrames")==Get<int>(counter,"ChargeFrame"));
            int count=Count("CounterCharge");
            Set(audioProfileOverride,"counterAccentEnabled",false); Call(sfx,"RefreshVolumes");
            Assert.That(Get<bool>(sfx,"ChargeAccentActive"),Is.False);
            Until(()=>Get<int>(counter,"HitCount")==1);
            Assert.That(Count("CounterCharge"),Is.EqualTo(count));
            Assert.That(Count("CounterHit"),Is.EqualTo(1));
        }
        [Test] public void CounterTransferChargeReleaseAndImpactFollowTheRealPhases()
        {
            BeginCounter(); Until(()=>Get<bool>(sfx,"ChargeAccentActive"));
            Assert.That(Count("CounterTransfer"),Is.EqualTo(1)); Assert.That(Count("CounterCharge"),Is.EqualTo(1));
            Assert.That(Sources.Any(a=>a.isPlaying&&!a.loop&&a.clip!=null&&a.clip.name=="CounterCharge"),Is.True);
            Assert.That(Get<int>(counter,"ElapsedFrames"),Is.EqualTo(Get<int>(counter,"ChargeFrame")+
                Get<int>(Get<object>(sfx,"Profile"),"counterAccentOffsetFrames")));
            Assert.That(Get<bool>(sfx,"ChargeLoopActive"),Is.False);
            Assert.That(Count("CounterHit"),Is.Zero);
            Until(()=>Get<int>(counter,"HitCount")==1);
            Assert.That(Count("CounterRelease"),Is.EqualTo(1)); Assert.That(Count("CounterHit"),Is.EqualTo(1));
            Assert.That(Get<bool>(sfx,"ChargeAccentActive"),Is.False);
            Assert.That(Sources.Any(a=>a.isPlaying&&a.clip!=null&&a.clip.name=="CounterImpact"&&!a.loop),Is.True);
            Until(()=>!Get<bool>(counter,"IsActive")); Frames(20); Assert.That(Count("CounterHit"),Is.EqualTo(1));
            Assert.That(Count("Land"),Is.EqualTo(1));
        }
        [Test] public void ElectricAccentEndsBeforeTheLongHoldAndNeverRepeats()
        {
            BeginCounter(); Until(()=>Get<int>(counter,"ElapsedFrames")==Get<int>(counter,"ChargeFrame"));
            Assert.That(Count("CounterCharge"),Is.Zero);
            Until(()=>Count("CounterCharge")==1);
            Call(sfx,"AdvanceAudio",.3f);
            Assert.That(Get<bool>(counter,"IsActive"),Is.True);
            Assert.That(Get<bool>(counter,"HasReleasedCharge"),Is.False);
            Assert.That(Get<bool>(sfx,"ChargeAccentActive"),Is.False);
            Assert.That(Get<bool>(sfx,"ChargeLoopActive"),Is.False);
            Assert.That(Count("CounterCharge"),Is.EqualTo(1));
            Assert.That(Count("CounterHit"),Is.Zero);
            Frames(1); Assert.That(Count("CounterCharge"),Is.EqualTo(1));
            Until(()=>Get<int>(counter,"HitCount")==1);
            Assert.That(Count("CounterCharge"),Is.EqualTo(1));
        }
        [TestCase("Reset")] [TestCase("Disable")] [TestCase("TargetLost")]
        public void CancellingBeforeTheLightPeakDiscardsThePendingElectricAccent(string interruption)
        {
            BeginCounter(); Until(()=>Get<int>(counter,"ElapsedFrames")==Get<int>(counter,"ChargeFrame"));
            Assert.That(Count("CounterCharge"),Is.Zero);
            if(interruption=="Reset") Call(arena,"ResetActionsOnly");
            else if(interruption=="Disable") ((Behaviour)sfx).enabled=false;
            else ((Component)Get<object>(counter,"SelectedTarget")).gameObject.SetActive(false);
            Frames(120); Assert.That(Count("CounterCharge"),Is.Zero);
        }
        [Test] public void PausingBeforeThePeakDoesNotStartOrLoseThePendingAccent()
        {
            BeginCounter(); Until(()=>Get<int>(counter,"ElapsedFrames")==Get<int>(counter,"ChargeFrame"));
            Static("SetPaused",true); Frames(60); Assert.That(Count("CounterCharge"),Is.Zero);
            Static("SetPaused",false); Until(()=>Count("CounterCharge")==1);
            Assert.That(Get<int>(counter,"ElapsedFrames"),Is.EqualTo(Get<int>(counter,"ChargeFrame")+
                Get<int>(Get<object>(sfx,"Profile"),"counterAccentOffsetFrames")));
        }
        [TestCase("Reset")] [TestCase("Disable")] [TestCase("TargetLost")]
        public void CounterChargeCannotLeakAfterInterruption(string interruption)
        {
            BeginCounter(); Until(()=>Get<bool>(sfx,"ChargeAccentActive"));
            if(interruption=="Reset") Call(arena,"ResetActionsOnly");
            else if(interruption=="Disable") ((Behaviour)sfx).enabled=false;
            else { ((Component)Get<object>(counter,"SelectedTarget")).gameObject.SetActive(false); Frames(2); }
            Assert.That(Get<bool>(sfx,"ChargeAccentActive"),Is.False);
            if(interruption!="TargetLost") Assert.That(Get<int>(sfx,"ActiveVoiceCount"),Is.Zero);
            Frames(20); Assert.That(Count("CounterHit"),Is.Zero);
        }
        [UnityTest] public IEnumerator PauseSuspendsAndResumesTheChargeWithoutRetriggeringIt()
        {
            BeginCounter(); Until(()=>Get<bool>(sfx,"ChargeAccentActive")); Static("SetPaused",true);
            Assert.That(Sources.All(a=>!a.isPlaying),Is.True); Frames(60); yield return null;
            Assert.That(Count("CounterCharge"),Is.EqualTo(1)); Static("SetPaused",false); yield return null;
            Assert.That(Get<bool>(sfx,"ChargeAccentActive"),Is.True); Assert.That(Count("CounterCharge"),Is.EqualTo(1));
            Call(sfx,"AdvanceAudio",.3f); Assert.That(Get<bool>(sfx,"ChargeAccentActive"),Is.False);
            Static("SetPaused",true); Static("SetPaused",false);
            Assert.That(Get<bool>(sfx,"ChargeAccentActive"),Is.False); Assert.That(Count("CounterCharge"),Is.EqualTo(1));
        }
        [TestCase("masterVolume")] [TestCase("sfxVolume")]
        public void MuteUpdatesAllVoicesAndNeverChangesCounterGameplay(string setting)
        {
            BeginCounter(); Until(()=>Get<bool>(sfx,"ChargeAccentActive")); Set(Get<object>(feedback,"Settings"),setting,0f); Call(sfx,"RefreshVolumes");
            Assert.That(Sources.All(a=>a.volume==0),Is.True);
            Until(()=>Get<int>(counter,"HitCount")==1); Assert.That(Count("CounterHit"),Is.Zero);
            Assert.That(Get<int>(counter,"SelectedDamage"),Is.EqualTo(90)); Assert.That(Sources.All(a=>a.volume==0),Is.True);
        }
        [Test] public void DeathCueIsNotStoppedAgainOnEveryDeadTickAndRespawnIsSilent()
        {
            Call(player.GetComponent(T("PlayerDamageReceiver2D")),"TryReceiveHit",1000,new Vector2(2,.81f),0f);
            Frames(5); Assert.That(Count("PlayerDeath"),Is.EqualTo(1));
            Assert.That(Sources.Any(a=>a.isPlaying&&a.clip!=null&&a.clip.name=="Fall"),Is.True);
            Call(player.GetComponent(T("PlayerRespawn2D")),"Respawn"); Frames(5);
            Assert.That(Count("Land"),Is.Zero); Assert.That(Count("PlayerDeath"),Is.EqualTo(1));
        }
        [Test] public void CommonFeedbackDoesNotOverwriteManagedVoiceVolumes()
        {
            Request(PlayerActionRequest.Dash); Call(sfx,"RefreshVolumes");
            var registered=Get<object>(feedback,"audioVolumes");
            var contains=registered.GetType().GetMethod("ContainsKey");
            foreach(var source in Sources) Assert.That(contains.Invoke(registered,new object[]{source}),Is.False);
            Assert.That(Sources.Any(a=>a.isPlaying&&a.volume>0),Is.True);
        }
        [Test] public void DroppingThroughAPlatformDoesNotPlayAJumpSound()
        {
            var platform=Object.FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None).First(p=>p.useOneWay&&p.GetComponent<BoxCollider2D>()!=null);
            var shape=platform.GetComponent<BoxCollider2D>();
            body.position=new Vector2(shape.bounds.center.x,shape.bounds.max.y+.805f); player.transform.position=body.position;
            body.linearVelocity=Vector2.zero; Physics2D.SyncTransforms(); Frames(4); Call(sfx,"ResetAudio"); Frames(2);
            Assert.That(Get<bool>(motor,"IsGrounded"),Is.True);
            Call(motor,"SetJumpDownInput",true); Request(PlayerActionRequest.Jump);
            Assert.That(Get<bool>(motor,"IsDroppingThrough"),Is.True); Assert.That(Count("Jump"),Is.Zero);
            Until(()=>Get<bool>(motor,"IsGrounded")); Frames(3);
            Assert.That(Count("Land"),Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)]
        public void HealApplySoundsOnlyAfterTheActualHealAndNotAfterInterruption(bool interrupted)
        {
            var health=player.GetComponent(T("PlayerDamageReceiver2D"));
            Call(health,"TryReceiveHit",10,new Vector2(2,.81f),0f); Frames(60);
            Request(PlayerActionRequest.Heal); Assert.That(Count("HealStart"),Is.EqualTo(1)); Assert.That(Count("HealApply"),Is.Zero);
            if(interrupted) { Call(health,"TryReceiveHit",1,new Vector2(2,.81f),0f); Frames(70); Assert.That(Count("HealApply"),Is.Zero); }
            else { Until(()=>Count("HealApply")==1); Frames(60); Assert.That(Count("HealApply"),Is.EqualTo(1)); }
        }
        [Test] public void SpecialCastHasNoHitSoundWhenItWhiffs()
        {
            Request(PlayerActionRequest.Special); Frames(120);
            Assert.That(Count("SpecialCast"),Is.EqualTo(1)); Assert.That(Count("SpecialHit"),Is.Zero);
        }
        [Test] public void InteractHasOneCueAndDoesNotCreateDashAudio()
        {
            PrototypeDialogueFixture.Create(player);
            var target=Object.FindFirstObjectByType(T("DialogueInteractable2D")) as Component;
            body.position=new Vector2(target.transform.position.x,.81f); player.transform.position=body.position;
            Physics2D.SyncTransforms(); Call(player.GetComponent(T("PlayerInteractor2D")),"RefreshAfterTeleport"); Frames(2);
            Request(PlayerActionRequest.Interact); Frames(10);
            Assert.That(Count("Interact"),Is.EqualTo(1)); Assert.That(Count("Dash"),Is.Zero);
            T("DialogueController").GetMethod("InterruptActive",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
        }
        [TestCase(false)] [TestCase(true)]
        public void EnemyWindupAttackAndDangerUseTheNewBusWithoutLegacyOverlap(bool dangerous)
        {
            var health=Enemy(); var ai=health.GetComponent(T("EnemyMeleeChaser2D"));
            ScriptableObject copy=null;
            try
            {
                if(dangerous)
                {
                    copy=Object.Instantiate((ScriptableObject)Get<object>(ai,"Definition"));
                    var field=copy.GetType().GetField("attacks"); var original=(Array)field.GetValue(copy);
                    var selected=Array.CreateInstance(original.GetType().GetElementType(),1); selected.SetValue(original.GetValue(1),0);
                    field.SetValue(copy,selected); Call(ai,"ConfigureDefinition",copy);
                }
                Assert.That(Call(ai,"BeginCaptureAttack"),Is.True); Assert.That(Count("EnemyWindup"),Is.EqualTo(1));
                Until(()=>Count("EnemyAttack")==1);
                Assert.That(Count("EnemyDanger"),Is.EqualTo(dangerous?1:0));
                foreach(var old in health.GetComponents<AudioSource>()) Assert.That(old.isPlaying,Is.False);
            }
            finally { if(copy!=null) Object.DestroyImmediate(copy); }
        }
    }
}
