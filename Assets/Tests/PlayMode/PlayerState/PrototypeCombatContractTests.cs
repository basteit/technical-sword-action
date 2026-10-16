using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PrototypeCombatContractTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<Object> owned = new();
        private GameObject player;
        private Component state, motor, attack, counter, health, gauge, heal, parry, special, clock, telemetry;
        private ScriptableObject counterData, attackData, timing;
        private bool savedManual;
        private readonly List<Behaviour> mutedRouters = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            clock = (Component)Object.FindFirstObjectByType(TypeOf("CombatTimeController"));
            savedManual = Get<bool>(clock, "ManualAdvanceOnly"); Property(clock, "ManualAdvanceOnly", true);
            foreach (Behaviour router in Object.FindObjectsByType(TypeOf("PlayerInputRouter"), FindObjectsSortMode.None))
                if (router.enabled) { mutedRouters.Add(router); router.enabled = false; }
            player = Own(new GameObject("PrototypeContractPlayer")); player.SetActive(false);
            player.transform.position = new Vector3(100, 1);
            player.AddComponent<Rigidbody2D>().gravityScale = 0;
            player.AddComponent<BoxCollider2D>().size = new Vector2(.6f, 1);
            state = Add(player, "PlayerStateMachine"); motor = Add(player, "PlayerMotor2D");
            attack = Add(player, "PlayerAttack2D"); gauge = Add(player, "PlayerSpecialGauge");
            Set(gauge, "startGauge", 100f);
            special = Add(player, "PlayerSpecialSkill2D"); health = Add(player, "PlayerDamageReceiver2D");
            Set(health, "maxHp", 100); Set(health, "minKnockbackForce", 0f);
            parry = Add(player, "PlayerParry2D"); heal = Add(player, "PlayerHeal2D");
            Set(heal, "applyFrame", 36); Set(heal, "endFrame", 54); Set(heal, "healAmount", 40);
            Add(player, "PlayerRespawn2D"); counter = Add(player, "PlayerParryCounter2D");
            counterData = Own(ScriptableObject.CreateInstance(TypeOf("ParryCounterDefinition")));
            Set(counter, "definition", counterData);
            timing = Own(ScriptableObject.CreateInstance(TypeOf("ParryTimingDefinition"))); Set(parry, "timing", timing);
            telemetry = Add(player, "PlayerCombatTelemetry");
            var feet = new GameObject("Feet"); feet.transform.SetParent(player.transform, false); feet.transform.localPosition = Vector3.down * .5f;
            Set(motor, "groundCheck", feet.transform); Set(motor, "groundLayer", (LayerMask)(1 << 29));
            var floor = Own(new GameObject("ContractFloor")); floor.layer = 29; floor.transform.position = new Vector3(100, .4f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(40, .2f);
            var point = new GameObject("HitPoint"); point.transform.SetParent(player.transform, false); point.transform.localPosition = Vector3.right * .8f;
            Set(attack, "hitPoint", point.transform); Set(attack, "targetLayers", (LayerMask)1);
            Set(special, "skillPoint", point.transform); Set(special, "targetLayers", (LayerMask)1);
            Set(special, "skillDamage", 60);
            player.SetActive(true);
            yield return null;
            Call(state, "ResetToSafeState", "ContractSetup", true);
            Set(motor, "isGrounded", true); Physics2D.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            foreach (var router in mutedRouters) if (router != null) router.enabled = true;
            mutedRouters.Clear();
            Property(clock, "ManualAdvanceOnly", savedManual);
            yield return null;
        }

        [Test]
        public void StocksArePerIndividualAndRetainedAcrossTimeRangeAndOrdinaryHit()
        {
            var a = Enemy(.9f, 1000); var b = Enemy(1.1f, 1000);
            Grant(a); Grant(a); Grant(b);
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(2));
            a.transform.position += Vector3.right * 5;
            Frames(400);
            Call(health, "TryReceiveHit", 1, (Vector2)player.transform.position + Vector2.left, 0f);
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(2));
            Call(state, "CompleteAction", PlayerActionState.Hit, "TestHitEnd");
            Start(PlayerActionRequest.Attack);
            Assert.That(Get<object>(counter, "SelectedTarget"), Is.SameAs(b));
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
        }

        [Test]
        public void DeathDisableAndCombatExitPermanentlyRemoveOnlyThatStock()
        {
            var a = Enemy(.8f, 1000); var b = Enemy(1f, 1000);
            Grant(a); Grant(b); Call(a, "SetCombatParticipation", false); Call(a, "SetCombatParticipation", true);
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
            a.gameObject.SetActive(false); a.gameObject.SetActive(true);
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
            Call(b, "TryTakeHit", 10000, Vector2.right);
            Assert.That(Get<int>(counter, "StockCount"), Is.Zero);
            Call(a, "SetCombatParticipation", true); Grant(a);
            Call(state, "SetDead", "TestDeath");
            Assert.That(Get<int>(counter, "StockCount"), Is.Zero);
        }

        [TestCase("Normal", 90)]
        [TestCase("Variant", 90)]
        [TestCase("Elite", 60)]
        [TestCase("MidBoss", 45)]
        [TestCase("Boss", 45)]
        public void CounterAppliesRankMultiplierOnceAndOnlyHitsSelectedIndividual(string rank, int damage)
        {
            var a = Enemy(.9f, 1000); var b = Enemy(1.05f, 1000);
            Call(a, "ConfigureCombat", Enum.Parse(TypeOf("EnemyCombatRank"), rank), 1000);
            a.gameObject.AddComponent<CircleCollider2D>().isTrigger = true;
            Grant(a); Grant(b); Call(gauge, "Consume", 20f);
            Start(PlayerActionRequest.Attack); Frames(35);
            Assert.That(Get<int>(a, "CurrentHp"), Is.EqualTo(1000 - damage));
            Assert.That(Get<int>(b, "CurrentHp"), Is.EqualTo(1000));
            Assert.That(Get<int>(a, "HitCount"), Is.EqualTo(1));
            Assert.That(Get<float>(gauge, "CurrentGauge"), Is.EqualTo(83f));
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
            Assert.That(Get<PlayerActionState>(state, "ActionState"), Is.EqualTo(PlayerActionState.Neutral));
        }

        [TestCase("Normal", 30, false)]
        [TestCase("Elite", 120, true)]
        public void NormalDiesToOneCounterButEliteSurvives(string rank, int hp, bool alive)
        {
            var enemy = Enemy(.9f, hp); Call(enemy, "ConfigureCombat", Enum.Parse(TypeOf("EnemyCombatRank"), rank), hp);
            Grant(enemy); Start(PlayerActionRequest.Attack); Frames(35);
            Assert.That(Get<bool>(enemy, "IsAlive"), Is.EqualTo(alive));
        }

        [Test]
        public void OnePressConsumesOneStockAndLastSixFramesCanReserveExactlyOneNextCounter()
        {
            var a = Enemy(.85f, 1000); var b = Enemy(1f, 1000); Grant(a); Grant(b);
            Start(PlayerActionRequest.Attack);
            Assert.That((bool)Call(attack, "RequestAttack"), Is.False, "No dedicated reservation before final six frames");
            Frames(29);
            Assert.That((bool)Call(attack, "RequestAttack"), Is.True);
            Call(attack, "RequestAttack"); Frames(6);
            Assert.That(Get<int>(counter, "ConsumedCount"), Is.EqualTo(1));
            Frames(1); Assert.That(Get<int>(counter, "ConsumedCount"), Is.EqualTo(2));
            Frames(70); Assert.That(Get<int>(counter, "StartCount"), Is.EqualTo(2));
        }

        [Test]
        public void FailedEligibilityFallsBackToNormalAttackWithoutConsumingStock()
        {
            var a = Enemy(-.9f, 1000); Grant(a);
            Start(PlayerActionRequest.Attack);
            Assert.That(Get<PlayerActionState>(state, "ActionState"), Is.EqualTo(PlayerActionState.Attack));
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
            Assert.That(Get<int>(counter, "ConsumedCount"), Is.Zero);
        }

        [Test]
        public void CounterInterruptionConsumesOneReleasesTargetAndKeepsOtherStock()
        {
            var a = Enemy(.9f, 1000); var b = Enemy(1.1f, 1000); Grant(a); Grant(b);
            Start(PlayerActionRequest.Attack); Call(state, "ForceHit", "InterruptCounter");
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
            Assert.That(Get<object>(counter, "SelectedTarget"), Is.Null);
            Assert.That(Get<float>(a, "StunRemaining"), Is.EqualTo(.2f).Within(.001f));
            Assert.That(Get<int>(a, "CurrentHp"), Is.EqualTo(1000));
        }

        [TestCase(4, false)]
        [TestCase(5, true)]
        [TestCase(8, true)]
        [TestCase(18, true)]
        [TestCase(19, false)]
        public void ParryAcceptanceUsesInclusiveConfiguredBoundary(int frame, bool succeeds)
        {
            var enemy = Enemy(.9f, 1000); Start(PlayerActionRequest.Parry); Frames(frame);
            object context = Activator.CreateInstance(TypeOf("CombatHitContext2D"), enemy, 1L, true);
            Call(health, "TryReceiveContextHit", 10, (Vector2)enemy.transform.position, 0f, context);
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(succeeds ? 1 : 0));
            Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(succeeds ? 100 : 90));
        }

        [Test]
        public void HealMovesAtConfiguredFractionAndOnlyConsumesAtApplyFrame()
        {
            Set(health, "currentHp", 20); Start(PlayerActionRequest.Heal);
            Call(motor, "SetMoveInput", Vector2.right); Call(motor, "ApplyCombatVelocity");
            Assert.That(player.GetComponent<Rigidbody2D>().linearVelocity.x, Is.EqualTo(Get<float>(motor, "MoveSpeed") * .4f).Within(.001));
            for (int i = 0; i < 35; i++) Call(heal, "CombatTickTimers");
            Assert.That(Get<int>(heal, "RemainingUses"), Is.EqualTo(3));
            Call(heal, "CombatTickTimers");
            Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(60));
            Assert.That(Get<int>(heal, "RemainingUses"), Is.EqualTo(2));
        }

        [Test]
        public void SpecialHitsChildAndDuplicateCollidersExactlyOnce()
        {
            var enemy = Enemy(.9f, 1000); enemy.gameObject.AddComponent<CircleCollider2D>().isTrigger = true;
            var child = new GameObject("ChildHitbox"); child.transform.SetParent(enemy.transform, false); child.AddComponent<BoxCollider2D>().isTrigger = true;
            Physics2D.SyncTransforms(); Start(PlayerActionRequest.Special); Frames(60);
            Assert.That(Get<int>(enemy, "CurrentHp"), Is.EqualTo(940));
            Assert.That(Get<int>(enemy, "HitCount"), Is.EqualTo(1));
            Assert.That(Get<int>(telemetry, "TotalDamage"), Is.EqualTo(60));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void FourPhaseTimelineHasIdenticalDamageAndNeutralRecoveryAcrossRenderRates(int fps)
        {
            InstallAttackDefinition();
            var enemy = Enemy(.9f, 1000); enemy.gameObject.AddComponent<CircleCollider2D>().isTrigger = true;
            Call(gauge, "Consume", 20f);
            Start(PlayerActionRequest.Attack);
            for (int i = 0; i < fps; i++) Call(clock, "AdvanceFrame", 1d / fps);
            Assert.That(Get<int>(enemy, "CurrentHp"), Is.EqualTo(990));
            Assert.That(Get<int>(enemy, "HitCount"), Is.EqualTo(1));
            Assert.That(Get<PlayerActionState>(state, "ActionState"), Is.EqualTo(PlayerActionState.Neutral));
            Assert.That(Get<bool>(attack, "IsAttacking"), Is.False);
            Assert.That(Get<int>(attack, "TimeoutFallbackCount"), Is.Zero);
            Assert.That(Get<float>(gauge, "CurrentGauge"), Is.EqualTo(82f));
            Assert.That(player.transform.position.x, Is.EqualTo(100f).Within(.001f));
        }

        private static IEnumerable<TestCaseData> CancelBoundaries()
        {
            for (int step = 1; step <= 4; step++)
            foreach (var request in new[] { PlayerActionRequest.Dash, PlayerActionRequest.Parry, PlayerActionRequest.Special })
            foreach (int frame in new[] { 3, 4, 7, 8 })
                yield return new TestCaseData(step, request, frame);
        }

        [TestCaseSource(nameof(CancelBoundaries))]
        public void EveryStepAndDestinationHonorsCommonWindowAndFreeRecovery(int step, PlayerActionRequest request, int frame)
        {
            InstallAttackDefinition(); Start(PlayerActionRequest.Attack);
            for (int i = 1; i < step; i++) { Frames(4); Start(PlayerActionRequest.Attack); }
            Frames(frame);
            Start(request);
            bool shouldStart = frame >= 4;
            PlayerActionState expected = request == PlayerActionRequest.Dash ? PlayerActionState.Dash :
                request == PlayerActionRequest.Parry ? PlayerActionState.Parry : PlayerActionState.Special;
            Assert.That(Get<PlayerActionState>(state, "ActionState") == expected, Is.EqualTo(shouldStart));
        }

        [Test]
        public void DataDrivenMeleeCompletesTenRealAttackAndParryCyclesWithoutDuplicateHits()
        {
            var enemy = Enemy(1.1f, 1000); enemy.gameObject.SetActive(false);
            var data = Own(ScriptableObject.CreateInstance(TypeOf("EnemyCombatDefinition")));
            Set(data, "maxHp", 1000); Set(data, "targetLayers", (LayerMask)1);
            Set(data, "allowOffscreenStart", true); Set(data, "knockback", 0f);
            Component ai = Add(enemy.gameObject, "EnemyMeleeChaser2D");
            Set(ai, "combatDefinition", data); Set(ai, "target", player.transform);
            enemy.gameObject.SetActive(true); Call(ai, "Start");
            player.AddComponent<CircleCollider2D>().isTrigger = true;
            for (int run = 0; run < 10; run++)
            {
                Call(state, "ResetToSafeState", "MeleeCycle", true); Call(health, "ResetHealth");
                Call(enemy, "ResetForCapture", player.transform.position + Vector3.right * 1.1f, 1000);
                Call(ai, "ResetForCapture", player.transform, true);
                Assert.That((bool)Call(ai, "BeginCaptureAttack"), Is.True);
                Frames(35);
                Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(90), "One attack instance, multiple player colliders");
                Assert.That(Get<int>(ai, "ActiveRemainingFrames"), Is.Zero);
                Call(state, "ResetToSafeState", "MeleeParry", true); Call(health, "ResetHealth");
                Call(ai, "ResetForCapture", player.transform, true); Call(ai, "BeginCaptureAttack");
                Frames(20); Start(PlayerActionRequest.Parry); Frames(15);
                Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(100));
                Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
                Assert.That(Get<string>(ai, "CurrentStateName"), Is.EqualTo("Stunned"));
            }
        }

        [TestCase(3, false)]
        [TestCase(4, true)]
        [TestCase(7, true)]
        [TestCase(8, false)]
        public void CommonCancelBoundaryAllowsSpecialOnlyDuringWindow(int frame, bool allowed)
        {
            InstallAttackDefinition(); Start(PlayerActionRequest.Attack); Frames(frame);
            bool open = Get<PlayerAttackCancelWindow>(attack, "OpenCancelWindows") == PlayerAttackCancelWindow.Common;
            Assert.That(open, Is.EqualTo(allowed));
            Start(PlayerActionRequest.Special);
            if (frame == 8) Assert.That(Get<PlayerActionState>(state, "ActionState"), Is.EqualTo(PlayerActionState.Special), "Recovery is freely cancellable");
            else Assert.That(Get<PlayerActionState>(state, "ActionState") == PlayerActionState.Special, Is.EqualTo(allowed));
        }

        [Test]
        public void CommonWindowAttackImmediatelyAdvancesAndRecoveryRestartsAtStepOne()
        {
            InstallAttackDefinition(); Start(PlayerActionRequest.Attack); Frames(4); Start(PlayerActionRequest.Attack);
            Assert.That(Get<int>(attack, "ComboStep"), Is.EqualTo(2));
            Frames(8); Assert.That(Get<bool>(attack, "IsRecovering"), Is.True);
            Start(PlayerActionRequest.Attack); Assert.That(Get<int>(attack, "ComboStep"), Is.EqualTo(1));
        }

        [Test]
        public void OverlayReportsMissingComponentsInsteadOfFabricatedZeroes()
        {
            var obj = Own(new GameObject("MissingDebugDependencies"));
            Component overlay = Add(obj, "PlayerDebugOverlay");
            Assert.That((string)Call(overlay, "BuildSnapshot", 0), Does.Contain("N/A (PlayerStateMachine missing)"));
            Assert.That((string)Call(overlay, "BuildSnapshot", 1), Does.Contain("N/A (PlayerAttack2D missing)"));
        }

        private void InstallAttackDefinition()
        {
            attackData = Own(ScriptableObject.CreateInstance(TypeOf("PlayerAttackDefinition")));
            Type stepType = TypeOf("PlayerAttackDefinition").GetNestedType("Step");
            Array steps = Array.CreateInstance(stepType, 4);
            for (int i = 0; i < 4; i++)
            {
                object step = Activator.CreateInstance(stepType);
                Set(step, "clip", Own(new AnimationClip())); Set(step, "activeStart", 2); Set(step, "activeEnd", 3);
                Set(step, "cancelStart", 4); Set(step, "recoveryStart", 8); Set(step, "endFrame", 10);
                Set(step, "motionDistance", 0f); Set(step, "damage", i == 3 ? 25 : 10);
                steps.SetValue(step, i);
            }
            Set(attackData, "steps", steps); Set(attack, "definition", attackData);
        }
        private Component Enemy(float x, int hp)
        {
            var obj = Own(new GameObject("ContractEnemy")); obj.SetActive(false);
            obj.transform.position = player.transform.position + Vector3.right * x;
            obj.AddComponent<BoxCollider2D>().isTrigger = true;
            Component enemy = Add(obj, "Damageable2D"); Set(enemy, "maxHp", hp); Set(enemy, "knockbackPower", 0f);
            obj.SetActive(true); Physics2D.SyncTransforms(); return enemy;
        }
        private void Grant(Component enemy) => Call(counter, "RegisterParriedEnemy", enemy);
        private void Frames(int count) { for (int i = 0; i < count; i++) Call(clock, "AdvanceFrame", 1d / 60d); }
        private void Start(PlayerActionRequest request) { Call(state, "RequestAction", request); Call(state, "CombatTick"); }
        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static Type TypeOf(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static Component Add(GameObject obj, string type) => obj.AddComponent(TypeOf(type));
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetProperty(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name, Flags).SetValue(target, value);
    }
}
