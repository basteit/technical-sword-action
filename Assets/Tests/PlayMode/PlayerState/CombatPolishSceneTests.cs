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
    public sealed class CombatPolishSceneTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private Component clock, arena, player, health, motor, counter, enemy, enemyHealth, feedback;
        private bool manual;
        [UnitySetUp] public IEnumerator Setup()
        {
            clock = (Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            manual = Get<bool>(clock, "ManualAdvanceOnly"); Prop(clock, "ManualAdvanceOnly", true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            arena = Find("PrototypeCombatArena"); player = Find("PlayerStateMachine");
            health = player.GetComponent(T("PlayerDamageReceiver2D")); motor = player.GetComponent(T("PlayerMotor2D"));
            counter = player.GetComponent(T("PlayerParryCounter2D")); feedback = player.GetComponent(T("CombatFeedback2D"));
            ((Behaviour)player.GetComponent(T("PlayerInputRouter"))).enabled = false;
            // The strong enemy still has gameplay regressions but is not a playable preset.
            Call(arena, "ResetActionsOnly");
            var reserved = Root("StrongMelee");
            enemy = reserved.GetComponent(T("EnemyMeleeChaser2D")); enemyHealth = reserved.GetComponent(T("Damageable2D"));
            Call(enemyHealth, "ResetForCapture", reserved.transform.position, Get<int>(enemyHealth, "ConfiguredMaxHp"));
            Call(enemy, "ResetForCapture", player.transform, false);
            player.transform.position = enemy.transform.position + Vector3.left * 1.15f;
            player.GetComponent<Rigidbody2D>().position = player.transform.position;
            Call(motor, "SetFacing", 1); Physics2D.SyncTransforms(); Frames(1);
            Set(Get<object>(feedback, "Settings"), "masterVolume", 0f);
            // Arena reset intentionally suppresses gameplay for its render frame, not for combat ticks.
            yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            if (arena != null) Call(arena, "ResetArena", false);
            Prop(clock, "ManualAdvanceOnly", manual); yield return null;
        }
        [Test]
        public void StrongEnemyParryAndCounterUseRealSceneAndLeaveThirtyHp()
        {
            Assert.That((bool)Call(enemy, "BeginCaptureAttack"), Is.True);
            Frames(30); Request(PlayerActionRequest.Parry); Frames(14);
            Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(100));
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
            Frames(10); Request(PlayerActionRequest.Attack);
            int limit = 180;
            while (Get<int>(counter, "HitCount") == 0 && --limit > 0) Frames(1);
            Assert.That(limit, Is.GreaterThan(0), "The authored charge uses real time; wait for the hit rather than the legacy 32F timeout.");
            Assert.That(Get<int>(enemyHealth, "CurrentHp"), Is.EqualTo(30));
            Assert.That(Get<bool>(enemyHealth, "IsAlive"), Is.True);
            Assert.That(Get<int>(counter, "HitCount"), Is.EqualTo(1));
            Assert.That(Get<int>(feedback, "EmissionCount"), Is.GreaterThanOrEqualTo(2));
        }
        [TestCase("Parry", 80)] [TestCase("Jump", 100)] [TestCase("Dash", 100)]
        public void LowSweepRejectsParryButCanBeAvoidedWithInitialActions(string action, int hp)
        {
            Set(enemy, "nextAttackIndex", 1); Call(enemy, "BeginCaptureAttack");
            Assert.That(Get<string>(enemy, "AttackKind"), Is.EqualTo("Unparryable"));
            Frames(action == "Dash" ? 49 : 42);
            if (action == "Jump") Call(motor, "SetJumpHeld", true);
            Request((PlayerActionRequest)Enum.Parse(typeof(PlayerActionRequest), action));
            Frames(action == "Dash" ? 12 : 19);
            Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(hp));
            Assert.That(Get<int>(counter, "StockCount"), Is.Zero);
        }
        [Test]
        public void TenCombinedResetsRestoreNormalAndUnlimitedDummyAndClearTimeAndPresentation()
        {
            for (int i = 0; i < 10; i++)
            {
                Call(arena, "ResetTrainingEncounter", true);
                Assert.That(Root("StrongMelee").activeSelf, Is.False);
                var dummy = Root("TrainingDummy"); var dummyHealth = dummy.GetComponent(T("Damageable2D"));
                Assert.That(dummy.activeSelf && Get<bool>(dummyHealth, "HasInfiniteHealth"), Is.True);
                Assert.That(Get<int>(dummyHealth, "CurrentHp"), Is.EqualTo(Get<int>(dummyHealth, "MaxHp")));
                Assert.That(Get<int>(Root("Melee_1").GetComponent(T("Damageable2D")), "CurrentHp"), Is.EqualTo(30));
                Assert.That(Get<int>(Find("EnemyAttackCoordinator2D"), "ActiveAttackers"), Is.Zero);
                Assert.That(Get<int>(feedback, "ActiveEffectCount"), Is.Zero);
                Assert.That(Get<int>(counter, "StockCount"), Is.Zero);
                Assert.That(Time.timeScale, Is.EqualTo(1));
                Frames(90);
                Assert.That(Get<int>(Find("EnemyAttackCoordinator2D"), "ActiveAttackers"), Is.LessThanOrEqualTo(1));
            }
        }
        [Test]
        public void UnlimitedPracticeHitsKeepHpWithoutFlinchingAndNormalEnemiesStillDie()
        {
            Call(arena, "ResetTrainingEncounter", true);
            var dummy = Root("TrainingDummy"); var target = dummy.GetComponent(T("Damageable2D"));
            var normal = Root("Melee_1").GetComponent(T("Damageable2D"));
            int hp = Get<int>(target, "CurrentHp");
            Vector3 position = dummy.transform.position;
            Assert.That(Get<bool>(target, "SuppressesOrdinaryHitReaction"), Is.True);
            Assert.That(Get<bool>(normal, "HasInfiniteHealth"), Is.False);
            Assert.That(Get<string>(normal, "IndividualId"), Is.Not.EqualTo(Get<string>(target, "IndividualId")));
            var strongHit = Enum.Parse(T("EnemyHitStrength"), "Strong");
            for (int i = 1; i <= 5; i++)
            {
                Assert.That(Call(target, "TryTakeCombatHit", 900, Vector2.right, strongHit), Is.True);
                Assert.That(Get<int>(target, "HitCount"), Is.EqualTo(i));
                Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(hp));
                Assert.That(Get<int>(target, "LastDamageTaken"), Is.Zero);
                Assert.That(Get<bool>(target, "IsStunned"), Is.False);
                Assert.That(dummy.transform.position, Is.EqualTo(position));
                Assert.That(Get<int>(target, "DeathCount"), Is.Zero);
                Assert.That(dummy.GetComponent<Collider2D>().enabled, Is.True);
            }
            Assert.That(Call(normal, "TryTakeCombatHit", 900, Vector2.right, strongHit), Is.True);
            Assert.That(Get<int>(normal, "CurrentHp"), Is.Zero);
            Assert.That(Get<int>(normal, "DeathCount"), Is.EqualTo(1));
            Assert.That(Get<bool>(target, "ParticipatingInCombat"), Is.True);
            Call(target, "ApplyParryStun", Enum.Parse(T("ParryResult"), "Just"));
            Assert.That(Get<bool>(target, "IsParryStaggered"), Is.True, "Parry still opens the counter opportunity.");
        }
        [UnityTest]
        public IEnumerator SameDummyCanBeParriedAndCounterKickedTwiceWithoutResettingHp()
        {
            Call(arena.GetComponent(T("PrototypeCombatTrial")), "SetEnemyTiming", false);
            Call(arena, "ResetTrainingEncounter", false);
            yield return null; // Release the reset render-frame input guard.
            var dummy = Root("TrainingDummy");
            var target = dummy.GetComponent(T("Damageable2D"));
            var ai = dummy.GetComponent(T("EnemyMeleeChaser2D"));
            var launch = dummy.GetComponent(T("EnemyCounterLaunch2D"));
            var body = player.GetComponent<Rigidbody2D>();
            int hp = Get<int>(target, "CurrentHp");
            for (int repeat = 1; repeat <= 2; repeat++)
            {
                // Arrange the next approach without respawning or refilling the dummy.
                body.position = new Vector2(0, .81f); player.transform.position = body.position;
                body.linearVelocity = Vector2.zero; dummy.transform.position = new Vector3(1.15f, .8f);
                Call(motor, "SetFacing", 1); Call(ai, "ResetForCapture", player.transform, true);
                Physics2D.SyncTransforms(); Frames(2);
                Assert.That(Call(ai, "BeginCaptureAttack"), Is.True);
                Frames(Get<int>(ai, "WindupRemainingFrames") - 5);
                Request(PlayerActionRequest.Parry);
                Until(() => Get<int>(counter, "StockCount") == 1);
                Assert.That(Get<bool>(target, "IsParryStaggered"), Is.True);
                Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(100));
                Frames(10);
                Request(PlayerActionRequest.Attack);
                Until(() => Get<int>(counter, "HitCount") == repeat);
                Assert.That(Get<int>(target, "HitCount"), Is.EqualTo(repeat));
                Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(hp));
                Assert.That(Get<int>(target, "DeathCount"), Is.Zero);
                Assert.That(Get<bool>(launch, "IsActive"), Is.True);
                Assert.That(Get<bool>(launch, "IsSurvivorRetreat"), Is.True);
                Assert.That(Get<string>(dummy.GetComponent(T("PrototypeEnemyArtPresentation")), "MotionName"), Is.EqualTo("CounterRetreat"));
                Assert.That(Get<object>(feedback, "LastKind").ToString(), Is.EqualTo("Counter"));
                Until(() => !Get<bool>(counter, "IsActive") && !Get<bool>(launch, "IsActive") && !Get<bool>(target, "IsStunned"));
            }
            Assert.That(dummy.activeSelf && Get<bool>(target, "ParticipatingInCombat"), Is.True);
        }
        [Test]
        public void InstallerIsPresentAndEssentialDefinitionsHaveNoMissingReference()
        {
            Type validator = Type.GetType("PrototypeCombatValidator, Assembly-CSharp-Editor", true);
            var errors = (string[])validator.GetMethod("Validate").Invoke(null, null);
            Assert.That(errors, Is.Empty);
            Assert.That(Get<bool>(feedback, "IsReady"), Is.True);
        }
        private void Request(PlayerActionRequest action)
        {
            Assert.That((bool)Call(player, "RequestAction", action), Is.True, "Input must be collectible after the reset render-frame guard.");
            Call(player, "CombatTick");
        }
        private void Frames(int count) { for (int i = 0; i < count; i++) Call(clock, "AdvanceFrame", 1d / 60d); }
        private void Until(Func<bool> predicate)
        {
            for (int i = 0; i < 360 && !predicate(); i++) Frames(1);
            Assert.That(predicate(), Is.True, "Combat did not reach the expected state.");
        }
        private GameObject Root(string name) => player.gameObject.scene.GetRootGameObjects().Single(go => go.name == name);
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static Component Find(string type) => (Component)Object.FindFirstObjectByType(T(type));
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
        private static TObj Get<TObj>(object target, string name) => (TObj)target.GetType().GetProperty(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static void Prop(object target, string name, object value) => target.GetType().GetProperty(name, Flags).SetValue(target, value);
    }
}
