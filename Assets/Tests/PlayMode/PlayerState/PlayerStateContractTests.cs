using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TechnicalSwordAction.PlayerState;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PlayerStateContractTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<GameObject> objects = new();
        private readonly List<Scene> scenes = new();
        private GameObject player, target;
        private Component state, motor, attack, parry, special, damage, gauge, interactor, heal, counter, interactable;
        private Rigidbody2D body;
        private Scene originalScene;
        private float savedScale, savedFixedStep;
        private Type timeType;
        private Behaviour timeController;
        private bool savedManualAdvance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            savedScale = Time.timeScale;
            savedFixedStep = Time.fixedDeltaTime;
            timeType = Type.GetType("CombatTimeController, Assembly-CSharp", true);
            timeController = (Behaviour)Object.FindFirstObjectByType(timeType);
            savedManualAdvance = Get<bool>(timeController, "ManualAdvanceOnly");
            timeType.GetProperty("ManualAdvanceOnly").SetValue(timeController, true);
            player = NewObject("StateContractPlayer");
            player.SetActive(false);
            body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            player.AddComponent<BoxCollider2D>().isTrigger = true;
            state = Add(player, "PlayerStateMachine");
            motor = Add(player, "PlayerMotor2D");
            attack = Add(player, "PlayerAttack2D");
            parry = Add(player, "PlayerParry2D");
            gauge = Add(player, "PlayerSpecialGauge");
            special = Add(player, "PlayerSpecialSkill2D");
            damage = Add(player, "PlayerDamageReceiver2D");
            interactor = Add(player, "PlayerInteractor2D");
            heal = Add(player, "TechnicalSwordAction.Tests.StateContractActionAdapter");
            counter = Add(player, "TechnicalSwordAction.Tests.StateContractActionAdapter");
            Field(counter, "State", PlayerActionState.ParryCounter);
            Field(damage, "maxHp", 10000);
            Field(gauge, "startGauge", 100f);
            target = NewObject("StateContractTarget");
            target.AddComponent<BoxCollider2D>().isTrigger = true;
            interactable = Add(target, "TechnicalSwordAction.Tests.StateContractInteractable");
            player.SetActive(true);
            yield return null;
            Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in objects) if (item != null) Object.DestroyImmediate(item);
            objects.Clear();
            if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
            foreach (var scene in scenes) if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            scenes.Clear();
            timeType.GetProperty("ManualAdvanceOnly").SetValue(timeController, savedManualAdvance);
            Time.timeScale = savedScale;
            Time.fixedDeltaTime = savedFixedStep;
        }

        [Test]
        public void SharedBWithAndWithoutTargetEachStartsOnlyOneActionTenTimes()
        {
            for (int i = 0; i < 10; i++)
            {
                Reset();
                IncludeTarget();
                int before = Get<int>(interactable, "Count");
                Call(state, "RequestSharedDashInteract");
                Call(state, "RequestSharedDashInteract"); // same pending press group is latched once
                Resolve();
                Assert.That(Get<PlayerActionRequest>(state, "LastSharedInputResolution"), Is.EqualTo(PlayerActionRequest.Interact));
                Assert.That(Get<int>(interactable, "Count"), Is.EqualTo(before + 1));
                Assert.That(Get<bool>(motor, "IsDashing"), Is.False);
                Reset();
                ExcludeTarget();
                Call(state, "RequestSharedDashInteract");
                Resolve();
                Assert.That(Action, Is.EqualTo(PlayerActionState.Dash));
                Assert.That(Get<PlayerActionRequest>(state, "LastSharedInputResolution"), Is.EqualTo(PlayerActionRequest.Dash));
                Assert.That(Get<int>(interactable, "Count"), Is.EqualTo(before + 1));
                Call(state, "CompleteAction", PlayerActionState.Dash, "AlreadyConsumed");
                Resolve();
                Assert.That(Get<PlayerActionRequest>(state, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None));
            }
        }

        [Test]
        public void RejectedOrVanishedSharedTargetNeverFallsBackToDash()
        {
            for (int scenario = 0; scenario < 4; scenario++)
            {
                Reset(); IncludeTarget();
                if (scenario == 0) Start(PlayerActionRequest.Attack);
                if (scenario == 1) Field(motor, "isGrounded", false);
                Call(state, "RequestSharedDashInteract");
                if (scenario == 2) target.SetActive(false);
                if (scenario == 3) ExcludeTarget();
                Resolve();
                Assert.That(Get<PlayerActionRequest>(state, "LastSharedInputResolution"), Is.EqualTo(PlayerActionRequest.Interact));
                Assert.That(Get<bool>(motor, "IsDashing"), Is.False);
                Assert.That(Get<int>(interactable, "Count"), Is.Zero);
                Reset(); Resolve();
                Assert.That(Get<bool>(motor, "IsDashing"), Is.False);
                target.SetActive(true);
            }
        }

        [Test]
        public void EveryNeutralRequestCombinationStartsOnlyTheHighestLegalAction()
        {
            for (int mask = 1; mask <= (int)PlayerActionRequest.Gameplay; mask++)
            {
                Reset(); IncludeTarget();
                int healStarts = Get<int>(heal, "Starts");
                int attacks = Get<int>(attack, "ComboAttemptCount");
                var request = (PlayerActionRequest)mask;
                Start(request);
                int selected = mask & -mask;
                Assert.That(Get<PlayerActionRequest>(state, "LastAcceptedRequest"), Is.EqualTo((PlayerActionRequest)selected), $"mask {mask}");
                AssertExclusiveExecutors();
                Assert.That(Get<int>(heal, "Starts") - healStarts, Is.EqualTo(selected == (int)PlayerActionRequest.Heal ? 1 : 0));
                Assert.That(Get<int>(attack, "ComboAttemptCount") - attacks, Is.EqualTo(selected == (int)PlayerActionRequest.Attack ? 1 : 0));
                Assert.That(Get<PlayerActionRequest>(state, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None));
            }
        }

        [UnityTest]
        public IEnumerator SharedBRespectsPauseHitstopAndExpiresItsSelectedContext()
        {
            Reset(); IncludeTarget();
            TimeCall("SetPaused", true);
            Assert.That((bool)Call(state, "RequestSharedDashInteract"), Is.False);
            Assert.That(Get<PlayerActionRequest>(state, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None));
            TimeCall("SetPaused", false);
            yield return null;

            TimeCall("RequestHitstop", this, 4f / 60f);
            Assert.That((bool)Call(state, "RequestSharedDashInteract"), Is.True);
            ExcludeTarget();
            Assert.That((bool)Call(state, "RequestSharedDashInteract"), Is.True);
            Assert.That(Get<PlayerActionRequest>(state, "LastSharedInputResolution"), Is.EqualTo(PlayerActionRequest.Interact));
            Call(timeController, "AdvanceFrame", 3d / 60d);
            Assert.That((int)Call(state, "GetInputBufferFramesRemaining", PlayerActionRequest.Interact), Is.EqualTo(1));
            Assert.That(Get<int>(interactable, "Count"), Is.Zero);
            TimeCall("ReleaseOwner", this);
            Resolve(); Call(state, "CombatTickTimers");
            Assert.That(Get<bool>(motor, "IsDashing"), Is.False);
            Assert.That(Get<PlayerActionRequest>(state, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None));

            // Expired Interact must not keep the next distinct press latched.
            Assert.That((bool)Call(state, "RequestSharedDashInteract"), Is.True);
            Resolve();
            Assert.That(Action, Is.EqualTo(PlayerActionState.Dash));
            Assert.That(Get<PlayerActionRequest>(state, "LastSharedInputResolution"), Is.EqualTo(PlayerActionRequest.Dash));

            Reset(); IncludeTarget();
            TimeCall("RequestHitstop", this, 4f / 60f);
            Call(state, "RequestSharedDashInteract");
            Call(timeController, "AdvanceFrame", 3d / 60d);
            TimeCall("ReleaseOwner", this);
            Resolve(); Resolve();
            Assert.That(Get<int>(interactable, "Count"), Is.EqualTo(1));
        }

        [Test]
        public void DefenseAndLateCancelGatesAndResourceFallbackMatchCentralContract()
        {
            foreach (PlayerActionRequest request in new[] { PlayerActionRequest.Dash, PlayerActionRequest.Parry, PlayerActionRequest.Special, PlayerActionRequest.Jump })
            {
                Reset(); Start(PlayerActionRequest.Attack); Start(request);
                Assert.That(Action, Is.EqualTo(PlayerActionState.Attack), $"closed {request}");
                string gate = request == PlayerActionRequest.Dash || request == PlayerActionRequest.Parry ? "OnDefenseCancelWindowOpen" : "OnLateCancelWindowOpen";
                Call(attack, gate); Start(request);
                Assert.That(Get<PlayerActionRequest>(state, "LastAcceptedRequest"), Is.EqualTo(request));
            }
            Reset(); Start(PlayerActionRequest.Attack); Call(attack, "OnLateCancelWindowOpen");
            Call(gauge, "Consume", 100f); Start(PlayerActionRequest.Special | PlayerActionRequest.Jump);
            Assert.That(Get<PlayerActionRequest>(state, "LastAcceptedRequest"), Is.EqualTo(PlayerActionRequest.Jump));
            Reset(); Start(PlayerActionRequest.Attack); Call(attack, "OnLateCancelWindowOpen");
            Call(gauge, "Consume", 100f); Field(motor, "isGrounded", false);
            Start(PlayerActionRequest.Special | PlayerActionRequest.Jump);
            Assert.That(Action, Is.EqualTo(PlayerActionState.Attack));
            Assert.That(Get<float>(gauge, "CurrentGauge"), Is.Zero);
        }

        [Test]
        public void ConnectedActionsAndHealAdapterEachInterruptAndReleaseTenTimes()
        {
            foreach (var request in new[] { PlayerActionRequest.Attack, PlayerActionRequest.Dash, PlayerActionRequest.Parry,
                PlayerActionRequest.Special, PlayerActionRequest.Heal, PlayerActionRequest.Jump, PlayerActionRequest.Interact })
            {
                for (int i = 0; i < 10; i++)
                {
                    Reset(); IncludeTarget(); Start(request);
                    Assert.That(Get<PlayerActionRequest>(state, "LastAcceptedRequest"), Is.EqualTo(request), $"{request} {i}");
                    AssertExclusiveExecutors();
                    SeedCollisionLock();
                    Call(state, "ForceHit", "ContractInterrupt");
                    Assert.That(Action, Is.EqualTo(PlayerActionState.Hit));
                    Assert.That(Get<int>(state, "ActiveCollisionIgnoreCount"), Is.Zero);
                    Assert.That(Get<bool>(attack, "IsAttacking"), Is.False);
                    Assert.That(Get<bool>(motor, "IsDashing"), Is.False);
                    Assert.That(Get<bool>(heal, "Active"), Is.False);
                    Call(state, "ResetToSafeState", "ContractReset", true);
                    AssertClean($"{request} {i}");
                }
            }
        }

        [Test]
        public void ParrySuccessCounterAdapterAndFailEachReleaseTenTimes()
        {
            for (int i = 0; i < 10; i++)
            {
                Reset(); Start(PlayerActionRequest.Parry);
                Assert.That((bool)Call(damage, "TryReceiveHit", 1, Vector2.zero, 0f), Is.False);
                Assert.That(Action, Is.EqualTo(PlayerActionState.ParrySuccess));
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That((bool)Call(counter, "TryStartAction"), Is.True);
                Call(state, "ChangeActionPhase", PlayerActionState.ParrySuccess, PlayerActionState.ParryCounter, "ContractCounter");
                Assert.That(Action, Is.EqualTo(PlayerActionState.ParryCounter));
                Assert.That(Get<float>(parry, "SuccessLockRemaining"), Is.Zero);
                Call(state, "ResetToSafeState", "CounterReset", true); AssertClean($"counter {i}");
                Reset(); Start(PlayerActionRequest.Parry);
                Field(parry, "parryTimer", 0f); Call(parry, "CombatTickTimers");
                Assert.That(Action, Is.EqualTo(PlayerActionState.ParryFail));
                Call(state, "ResetToSafeState", "FailReset", true); AssertClean($"fail {i}");
            }
        }

        [Test]
        public void DisableAndDeathDuringHitstopClearAllOwnershipTenTimesEach()
        {
            for (int i = 0; i < 10; i++)
            {
                SeedParryStop(); player.SetActive(false); AssertClean($"disable {i}"); player.SetActive(true);
                SeedParryStop(); Call(state, "SetDead", "ContractDeath"); AssertClean($"death {i}");
                Assert.That(Get<PlayerLifeState>(state, "LifeState"), Is.EqualTo(PlayerLifeState.Dead));
                Call(state, "Revive", "ContractRevive");
            }
        }

        [UnityTest]
        public IEnumerator SceneTransitionsResetPersistentPlayerAndUnloadWithoutLeaksTenTimes()
        {
            for (int i = 0; i < 10; i++)
            {
                Scene scene = SceneManager.CreateScene($"StateContract_{i}"); scenes.Add(scene);
                SeedParryStop();
                SceneManager.SetActiveScene(scene);
                AssertClean($"active scene {i}");
                SceneManager.SetActiveScene(originalScene);
                yield return SceneManager.UnloadSceneAsync(scene);
                Assert.That(Time.timeScale, Is.EqualTo(savedScale));
            }
        }

        [UnityTest]
        public IEnumerator NaturalExecutorCompletionReturnsToNeutral()
        {
            foreach (var request in new[] { PlayerActionRequest.Attack, PlayerActionRequest.Dash, PlayerActionRequest.Parry, PlayerActionRequest.Special, PlayerActionRequest.Heal, PlayerActionRequest.Interact })
            {
                Reset(); IncludeTarget(); Start(request);
                Assert.That(Get<PlayerActionRequest>(state, "LastAcceptedRequest"), Is.EqualTo(request));
                if (request == PlayerActionRequest.Heal) Call(heal, "Finish");
                double deadline = Time.realtimeSinceStartupAsDouble + 3d;
                while (Action != PlayerActionState.Neutral && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    Call(timeController, "AdvanceFrame", 1d / 60d);
                    yield return null;
                }
                Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral), $"natural completion {request}");
            }
        }

        private PlayerActionState Action => Get<PlayerActionState>(state, "ActionState");
        private void Reset()
        {
            Call(state, "ResetToSafeState", "FixtureReset", true);
            Field(motor, "isGrounded", true);
            body.position = Vector2.zero; body.linearVelocity = Vector2.zero;
            Call(gauge, "AddOnAttackHit", 100f);
        }
        private void SeedParryStop()
        {
            Reset(); Start(PlayerActionRequest.Parry);
            Call(damage, "TryReceiveHit", 1, Vector2.zero, 0f);
            Assert.That(Time.timeScale, Is.Zero);
            SeedCollisionLock();
            Call(state, "RequestSharedDashInteract");
        }
        private void SeedCollisionLock() => Call(state, "AcquireCollisionIgnore", Action, player.GetComponent<Collider2D>(), target.GetComponent<Collider2D>());
        private void AssertExclusiveExecutors()
        {
            int active = 0;
            if (Get<bool>(attack, "IsAttacking")) active++;
            if (Get<bool>(motor, "IsDashing")) active++;
            if (Get<bool>(parry, "IsParryActive") || Get<bool>(parry, "IsFailLocked") || Get<bool>(parry, "IsSuccessLocked")) active++;
            if (Get<bool>(special, "IsUsingSkill")) active++;
            if (Get<bool>(heal, "Active")) active++;
            if (Get<bool>(counter, "Active")) active++;
            if (Action == PlayerActionState.Interact) active++;
            Assert.That(active, Is.LessThanOrEqualTo(1), "Concurrent action executors");
        }
        private void AssertClean(string context)
        {
            Assert.That(Action, Is.EqualTo(PlayerActionState.Neutral), context);
            Assert.That(Get<int>(state, "ActiveActionOwnerCount"), Is.Zero, context);
            Assert.That(Get<int>(state, "ActiveCollisionIgnoreCount"), Is.Zero, context);
            Assert.That(Get<PlayerActionRequest>(state, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None), context);
            Assert.That(Get<float>(state, "RemainingLock"), Is.Zero, context);
            Assert.That(Get<float>(attack, "InputBufferRemaining"), Is.Zero, context);
            Assert.That(Get<float>(motor, "DashRemaining"), Is.Zero, context);
            Assert.That(Get<float>(parry, "ParryRemaining"), Is.Zero, context);
            Assert.That(Get<bool>(special, "IsUsingSkill"), Is.False, context);
            Assert.That(Get<bool>(heal, "Active"), Is.False, context);
            Assert.That(Get<bool>(counter, "Active"), Is.False, context);
            Assert.That((float)damage.GetType().GetField("invincibleTimer", Flags).GetValue(damage), Is.Zero, context);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero), context);
            Assert.That(body.gravityScale, Is.Zero, context);
            Assert.That(Time.timeScale, Is.EqualTo(savedScale), context);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(savedFixedStep), context);
            Assert.That(Physics2D.GetIgnoreCollision(player.GetComponent<Collider2D>(), target.GetComponent<Collider2D>()), Is.False, context);
        }
        private void IncludeTarget()
        {
            Call(interactor, "OnTriggerEnter2D", target.GetComponent<Collider2D>());
            Call(interactor, "SelectCurrentInteractable");
        }
        private void ExcludeTarget() => Call(interactor, "OnTriggerExit2D", target.GetComponent<Collider2D>());
        private void Start(PlayerActionRequest request) { Call(state, "RequestAction", request); Resolve(); }
        private void Resolve() => Call(state, "CombatTick");
        private void TimeCall(string name, params object[] args) => timeType.GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        private GameObject NewObject(string name) { var item = new GameObject(name); objects.Add(item); return item; }
        private static Component Add(GameObject item, string name) => item.AddComponent(Type.GetType(name + ", Assembly-CSharp", true));
        private static T Get<T>(object item, string name) => (T)item.GetType().GetProperty(name, Flags).GetValue(item);
        private static void Field(object item, string name, object value) => item.GetType().GetField(name, Flags).SetValue(item, value);
        private static object Call(object item, string name, params object[] args)
        {
            foreach (var method in item.GetType().GetMethods(Flags))
            {
                if (method.Name != name || method.GetParameters().Length != args.Length) continue;
                var parameters = method.GetParameters(); bool match = true;
                for (int i = 0; i < args.Length; i++) if (args[i] != null && !parameters[i].ParameterType.IsInstanceOfType(args[i])) match = false;
                if (match) return method.Invoke(item, args);
            }
            throw new MissingMethodException(item.GetType().Name, name);
        }
    }
}
