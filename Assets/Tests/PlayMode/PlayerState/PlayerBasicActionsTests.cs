using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PlayerBasicActionsTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject player, floor;
        private Component state, motor, health, heal, respawn, counter, parry, attack, gauge, special;
        private Rigidbody2D body;
        private Component clock;
        private Type clockType;
        private bool previousManual;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            clockType = Type.GetType("CombatTimeController, Assembly-CSharp", true);
            clock = (Component)Object.FindFirstObjectByType(clockType);
            previousManual = Get<bool>(clock, "ManualAdvanceOnly");
            clockType.GetProperty("ManualAdvanceOnly").SetValue(clock, true);
            player = new GameObject("BasicActionsPlayer");
            player.SetActive(false);
            player.transform.position = new Vector3(100, 1, 0);
            body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            player.AddComponent<BoxCollider2D>();
            state = Add("PlayerStateMachine");
            motor = Add("PlayerMotor2D");
            attack = Add("PlayerAttack2D");
            gauge = Add("PlayerSpecialGauge");
            Field(gauge, "startGauge", 100f);
            special = Add("PlayerSpecialSkill2D");
            health = Add("PlayerDamageReceiver2D");
            parry = Add("PlayerParry2D");
            heal = Add("PlayerHeal2D");
            respawn = Add("PlayerRespawn2D");
            counter = Add("PlayerParryCounter2D");
            var feet = new GameObject("Feet");
            feet.transform.SetParent(player.transform, false);
            feet.transform.localPosition = new Vector3(0, -0.5f, 0);
            Field(motor, "groundCheck", feet.transform);
            Field(motor, "groundLayer", (LayerMask)1);
            floor = new GameObject("OneWayFloor");
            floor.transform.position = new Vector3(100, 0.4f, 0);
            var collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(5, 0.2f);
            floor.AddComponent<PlatformEffector2D>().useOneWay = true;
            collider.usedByEffector = true;
            player.SetActive(true);
            yield return null;
            Call(state, "ResetToSafeState", "TestSetup", true);
            Field(motor, "isGrounded", true);
            Field(health, "currentHp", 1);
            Physics2D.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(floor);
            clockType.GetProperty("ManualAdvanceOnly").SetValue(clock, previousManual);
            yield return null;
        }

        [Test]
        public void HealInterruptedBeforeApplicationKeepsAllUsesTenTimes()
        {
            for (int i = 0; i < 10; i++)
            {
                Reset(); Start(PlayerActionRequest.Heal); HealFrames(29);
                Call(state, "ForceHit", "BeforeHeal");
                HealFrames(50);
                Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(1));
                Assert.That(Get<int>(heal, "RemainingUses"), Is.EqualTo(3));
                Assert.That(Get<bool>(heal, "IsHealing"), Is.False);
            }
        }

        [Test]
        public void HealAppliedOnceAndPostApplicationInterruptKeepsConsumption()
        {
            for (int i = 0; i < 10; i++)
            {
                Reset(); Start(PlayerActionRequest.Heal); HealFrames(30);
                Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(3));
                Assert.That(Get<int>(heal, "RemainingUses"), Is.EqualTo(2));
                Call(state, "ForceHit", "AfterHeal"); HealFrames(50);
                Assert.That(Get<int>(heal, "RemainingUses"), Is.EqualTo(2));
            }
        }

        [Test]
        public void ThreeHealsCompleteAndFourthCannotStart()
        {
            for (int i = 0; i < 3; i++)
            {
                Field(health, "currentHp", 1);
                Start(PlayerActionRequest.Heal); HealFrames(48);
                Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
                Assert.That(Get<int>(heal, "RemainingUses"), Is.EqualTo(2 - i));
            }
            Start(PlayerActionRequest.Heal);
            Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
        }

        [Test]
        public void HealRequiresGroundedNeutralAndMissingHealth()
        {
            Field(motor, "isGrounded", false); Start(PlayerActionRequest.Heal);
            Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
            Reset(); Field(health, "currentHp", Get<int>(health, "MaxHp")); Start(PlayerActionRequest.Heal);
            Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
            Reset(); Start(PlayerActionRequest.Dash); Start(PlayerActionRequest.Heal);
            Assert.That(Action, Is.EqualTo(PlayerActionState.Dash));
        }

        [Test]
        public void DownJumpIgnoresOnlyOneWayFloorAndHitRestoresCollisionTenTimes()
        {
            for (int i = 0; i < 10; i++)
            {
                Reset(); Call(motor, "SetJumpDownInput", true); Start(PlayerActionRequest.Jump);
                Assert.That(body.linearVelocity.y, Is.LessThan(0));
                Assert.That(Get<bool>(motor, "IsDroppingThrough"), Is.True);
                Assert.That(Physics2D.GetIgnoreCollision(player.GetComponent<Collider2D>(), floor.GetComponent<Collider2D>()), Is.True);
                Call(state, "ForceHit", "DropInterrupted");
                Assert.That(Physics2D.GetIgnoreCollision(player.GetComponent<Collider2D>(), floor.GetComponent<Collider2D>()), Is.False);
            }
        }

        [Test]
        public void SolidFloorDownJumpJumpsAndReleasedJumpCutsRiseOnlyOnce()
        {
            floor.GetComponent<BoxCollider2D>().usedByEffector = false;
            Call(motor, "SetJumpDownInput", true); Call(motor, "SetJumpHeld", true);
            Start(PlayerActionRequest.Jump);
            float rise = body.linearVelocity.y;
            Assert.That(rise, Is.GreaterThan(0));
            Call(motor, "SetJumpHeld", false); Call(motor, "CombatTick");
            Assert.That(body.linearVelocity.y, Is.EqualTo(rise * 0.5f).Within(0.001f));
            Call(motor, "CombatTick");
            Assert.That(body.linearVelocity.y, Is.EqualTo(rise * 0.5f).Within(0.001f));
        }

        [Test]
        public void AirDashCannotRepeatUntilActualGroundContact()
        {
            Field(motor, "isGrounded", false); Start(PlayerActionRequest.Dash);
            Assert.That(Get<bool>(motor, "AirDashUsed"), Is.True);
            Call(motor, "CancelDashFromStateMachine", true);
            Call(state, "CompleteAction", PlayerActionState.Dash, "TestEnd");
            Assert.That(Get<bool>(motor, "CanStartDash"), Is.False);
            Call(motor, "CombatTick");
            Assert.That(Get<bool>(motor, "IsGrounded"), Is.True);
            Assert.That(Get<bool>(motor, "CanStartDash"), Is.True);
        }

        [Test]
        public void PauseFreezesHealAndDropsPendingGameplayOnResume()
        {
            Start(PlayerActionRequest.Heal);
            Call(state, "RequestPause");
            float remaining = Get<float>(heal, "LockRemaining");
            Call(clock, "AdvanceFrame", 1d);
            Assert.That(Get<float>(heal, "LockRemaining"), Is.EqualTo(remaining));
            Assert.That((bool)Call(state, "RequestAction", PlayerActionRequest.Attack), Is.False);
            Call(state, "RequestPause");
            Assert.That(Get<PlayerActionRequest>(state, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None));
        }

        [Test]
        public void FatalDamageKeepsPlayerActiveThenRespawnRestoresResourcesTenTimes()
        {
            for (int i = 0; i < 10; i++)
            {
                Reset(); Start(PlayerActionRequest.Heal); HealFrames(30);
                Assert.That((bool)Call(health, "TryReceiveHit", 100, (Vector2)player.transform.position + Vector2.left, 0f), Is.True);
                Assert.That(player.activeSelf, Is.True);
                Assert.That(Get<PlayerLifeState>(state, "LifeState"), Is.EqualTo(PlayerLifeState.Dead));
                for (int frame = 0; frame < 90; frame++) Call(respawn, "CombatTickTimers");
                Assert.That(Get<PlayerLifeState>(state, "LifeState"), Is.EqualTo(PlayerLifeState.Alive));
                Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(Get<int>(health, "MaxHp")));
                Assert.That(Get<int>(heal, "RemainingUses"), Is.EqualTo(3));
                Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
                Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            }
        }

        [Test]
        public void ParrySuccessAttackStartsDedicatedCounterAndCompletesTenTimes()
        {
            for (int i = 0; i < 10; i++)
            {
                Reset(); Start(PlayerActionRequest.Parry);
                Call(state, "ChangeActionPhase", PlayerActionState.Parry, PlayerActionState.ParrySuccess, "TestSuccess");
                Start(PlayerActionRequest.Attack);
                Assert.That(Action, Is.EqualTo(PlayerActionState.ParryCounter));
                for (int frame = 0; frame < 24; frame++) Call(counter, "CombatTickTimers");
                Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
            }
        }

        private PlayerActionState Action => Get<PlayerActionState>(state, "ActionState");
        [Test]
        public void GroundAndAirFourStepAttackDashParryAndSpecialFinishTenTimes()
        {
            foreach (bool grounded in new[] { true, false })
            for (int i = 0; i < 10; i++)
            {
                Reset(); Field(motor, "isGrounded", grounded); Start(PlayerActionRequest.Attack);
                for (int step = 1; step <= 4; step++)
                {
                    Assert.That(Get<int>(attack, "ComboStep"), Is.EqualTo(step));
                    if (step < 4)
                    {
                        Call(attack, "RequestAttack");
                        Call(attack, "OnComboWindowOpen", step);
                    }
                    Call(attack, "OnAttackEnd", step);
                }
                Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
                Reset(); Field(motor, "isGrounded", grounded); Start(PlayerActionRequest.Dash);
                Assert.That(Action, Is.EqualTo(PlayerActionState.Dash));
                for (int frame = 0; frame < 12; frame++) Call(motor, "CombatTickTimers");
                Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
                Reset(); Field(motor, "isGrounded", grounded); Start(PlayerActionRequest.Parry);
                Assert.That(Action, Is.EqualTo(PlayerActionState.Parry));
                for (int frame = 0; frame < 60; frame++) Call(parry, "CombatTickTimers");
                Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
                Reset(); Field(motor, "isGrounded", grounded); Call(gauge, "ResetGauge"); Start(PlayerActionRequest.Special);
                Assert.That(Action, Is.EqualTo(PlayerActionState.Special));
                for (int frame = 0; frame < 60; frame++)
                {
                    Call(special, "ResolveCombatHits"); Call(special, "CombatTickTimers");
                }
                Assert.That(Get<float>(gauge, "CurrentGauge"), Is.EqualTo(40f));
                Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
            }
        }

        [Test]
        public void CounterDealsDamageOnlyOnceToEnemyWithMultipleColliders()
        {
            var enemy = new GameObject("CounterTarget");
            enemy.SetActive(false);
            enemy.transform.position = player.transform.position + Vector3.right * 0.7f;
            enemy.AddComponent<BoxCollider2D>().isTrigger = true;
            enemy.AddComponent<CircleCollider2D>().isTrigger = true;
            var damageable = enemy.AddComponent(Type.GetType("Damageable2D, Assembly-CSharp", true));
            Field(damageable, "maxHp", 20);
            enemy.SetActive(true);
            Physics2D.SyncTransforms();
            try
            {
                Start(PlayerActionRequest.Parry);
                Call(state, "ChangeActionPhase", PlayerActionState.Parry, PlayerActionState.ParrySuccess, "TestSuccess");
                Start(PlayerActionRequest.Attack);
                for (int frame = 0; frame < 24; frame++) Call(counter, "CombatTickTimers");
                Assert.That(Get<int>(damageable, "CurrentHp"), Is.EqualTo(17));
            }
            finally { Object.DestroyImmediate(enemy); }
        }

        [Test]
        public void RealParrySuccessBuffersCounterThroughHitstopThenReturnsToNeutral()
        {
            Start(PlayerActionRequest.Parry);
            Assert.That((bool)Call(health, "TryReceiveHit", 1, (Vector2)player.transform.position + Vector2.left * 0.1f, 0f), Is.False);
            Assert.That(Action, Is.EqualTo(PlayerActionState.ParrySuccess));
            Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(1));
            Call(state, "RequestAction", PlayerActionRequest.Attack);
            for (int i = 0; i < 40; i++) Call(clock, "AdvanceFrame", 1d / 60d);
            Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
            Assert.That(Get<float>(counter, "LockRemaining"), Is.Zero);
            Assert.That(Get<PlayerActionRequest>(state, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None));
        }

        [Test]
        public void HealDisableAndFloorDisableLeaveNoExecutionOrCollisionTenTimes()
        {
            for (int i = 0; i < 10; i++)
            {
                Reset(); Start(PlayerActionRequest.Heal); HealFrames(29);
                ((Behaviour)heal).enabled = false;
                Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
                Assert.That(Get<int>(heal, "RemainingUses"), Is.EqualTo(3));
                ((Behaviour)heal).enabled = true;
                Reset(); Call(motor, "SetJumpDownInput", true); Start(PlayerActionRequest.Jump);
                ((Behaviour)motor).enabled = false;
                Assert.That(Get<bool>(motor, "IsDroppingThrough"), Is.False);
                Assert.That(Physics2D.GetIgnoreCollision(player.GetComponent<Collider2D>(), floor.GetComponent<Collider2D>()), Is.False);
                ((Behaviour)motor).enabled = true;
            }
        }

        [Test]
        public void CounterHitAndDisableAlwaysReleaseItsLockTenTimes()
        {
            for (int i = 0; i < 10; i++)
            {
                Reset(); Start(PlayerActionRequest.Parry);
                Call(state, "ChangeActionPhase", PlayerActionState.Parry, PlayerActionState.ParrySuccess, "TestSuccess");
                Start(PlayerActionRequest.Attack); Call(state, "ForceHit", "CounterInterrupted");
                Assert.That(Get<float>(counter, "LockRemaining"), Is.Zero);
                Reset(); Start(PlayerActionRequest.Parry);
                Call(state, "ChangeActionPhase", PlayerActionState.Parry, PlayerActionState.ParrySuccess, "TestSuccess");
                Start(PlayerActionRequest.Attack); ((Behaviour)counter).enabled = false;
                Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral));
                Assert.That(Get<float>(counter, "LockRemaining"), Is.Zero);
                ((Behaviour)counter).enabled = true;
            }
        }

        private void Reset()
        {
            Call(state, "ResetToSafeState", "Repeat", true);
            Call(heal, "Refill"); Field(health, "currentHp", 1); Field(motor, "isGrounded", true);
            Field(state, "suppressGameplayInputFrame", -1);
        }
        private void Start(PlayerActionRequest request) { Call(state, "RequestAction", request); Call(state, "CombatTick"); }
        private void HealFrames(int count) { for (int i = 0; i < count; i++) Call(heal, "CombatTickTimers"); }
        private Component Add(string name) => player.AddComponent(Type.GetType(name + ", Assembly-CSharp", true));
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetProperty(name, Flags).GetValue(target);
        private static void Field(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    }
}
