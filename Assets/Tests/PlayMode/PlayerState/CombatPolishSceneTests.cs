using System;
using System.Collections;
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
            Call(arena, "ResetStrongEncounter", false);
            enemy = GameObject.Find("StrongMelee").GetComponent(T("EnemyMeleeChaser2D")); enemyHealth = enemy.GetComponent(T("Damageable2D"));
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
            Frames(10); Request(PlayerActionRequest.Attack); Frames(32);
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
        public void TenCombinedResetsRestoreBothEnemiesAndClearTimeAndPresentation()
        {
            for (int i = 0; i < 10; i++)
            {
                Call(arena, "ResetStrongEncounter", true);
                Assert.That(Get<int>(enemyHealth, "CurrentHp"), Is.EqualTo(120));
                Assert.That(Get<int>(Find("EnemyAttackCoordinator2D"), "ActiveAttackers"), Is.Zero);
                Assert.That(Get<int>(feedback, "ActiveEffectCount"), Is.Zero);
                Assert.That(Get<int>(counter, "StockCount"), Is.Zero);
                Assert.That(Time.timeScale, Is.EqualTo(1));
                Frames(90);
                Assert.That(Get<int>(Find("EnemyAttackCoordinator2D"), "ActiveAttackers"), Is.LessThanOrEqualTo(1));
            }
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
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static Component Find(string type) => (Component)Object.FindFirstObjectByType(T(type));
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
        private static TObj Get<TObj>(object target, string name) => (TObj)target.GetType().GetProperty(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static void Prop(object target, string name, object value) => target.GetType().GetProperty(name, Flags).SetValue(target, value);
    }
}
