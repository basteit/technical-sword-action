using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TechnicalSwordAction.PlayerState;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PlayerInputRouterTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject player, target, floor;
        private Component state, motor, router, attack, parry, gauge, interactor, counter, heal;
        private Component clock;
        private Type clockType;
        private bool savedManual;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad pad;
        private InputActionAsset actions;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            clockType = Type.GetType("CombatTimeController, Assembly-CSharp", true);
            clock = (Component)Object.FindFirstObjectByType(clockType);
            savedManual = Get<bool>(clock, "ManualAdvanceOnly");
            clockType.GetProperty("ManualAdvanceOnly").SetValue(clock, true);
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            pad = InputSystem.AddDevice<Gamepad>();
            player = new GameObject("InputRouterTestPlayer");
            player.SetActive(false);
            player.AddComponent<Rigidbody2D>().gravityScale = 0;
            player.AddComponent<BoxCollider2D>().isTrigger = true;
            state = Add(player, "PlayerStateMachine");
            motor = Add(player, "PlayerMotor2D");
            attack = Add(player, "PlayerAttack2D");
            parry = Add(player, "PlayerParry2D");
            gauge = Add(player, "PlayerSpecialGauge");
            Add(player, "PlayerSpecialSkill2D");
            interactor = Add(player, "PlayerInteractor2D");
            heal = Add(player, "TechnicalSwordAction.Tests.StateContractActionAdapter");
            floor = new GameObject("InputRouterGround");
            floor.layer = 29;
            floor.transform.position = new Vector3(0, -0.6f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            Field(motor, "groundCheck", player.transform);
            Field(motor, "groundLayer", (LayerMask)(1 << 29));
            target = new GameObject("InputRouterCounter");
            target.transform.position = new Vector3(20, 0);
            target.AddComponent<BoxCollider2D>().isTrigger = true;
            counter = Add(target, "TechnicalSwordAction.Tests.StateContractInteractable");
            player.SetActive(true);
            router = player.GetComponent(Type.GetType("PlayerInputRouter, Assembly-CSharp", true));
            RestrictDevices();
            yield return null;
            Reset();
            InputSystem.Update();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(floor);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(pad);
            clockType.GetProperty("ManualAdvanceOnly").SetValue(clock, savedManual);
            yield return null;
        }

        [Test]
        public void EveryStandardButtonBindingRoutesThroughActions()
        {
            var bindings = new (Key key, PlayerActionRequest request)[] {
                (Key.Space, PlayerActionRequest.Jump), (Key.J, PlayerActionRequest.Attack),
                (Key.LeftShift, PlayerActionRequest.Dash), (Key.RightShift, PlayerActionRequest.Dash),
                (Key.K, PlayerActionRequest.Parry), (Key.Q, PlayerActionRequest.Special),
                (Key.H, PlayerActionRequest.Heal), (Key.E, PlayerActionRequest.Interact) };
            foreach (var binding in bindings)
            {
                Release(); Reset(); Include();
                Keys(binding.key); Resolve();
                Assert.That(Accepted, Is.EqualTo(binding.request), binding.key.ToString());
            }
            var padBindings = new (GamepadButton button, PlayerActionRequest request)[] {
                (GamepadButton.South, PlayerActionRequest.Jump), (GamepadButton.West, PlayerActionRequest.Attack),
                (GamepadButton.LeftShoulder, PlayerActionRequest.Parry), (GamepadButton.RightShoulder, PlayerActionRequest.Special),
                (GamepadButton.North, PlayerActionRequest.Heal) };
            foreach (var binding in padBindings)
            {
                Release(); Reset(); Pad(binding.button); Resolve();
                Assert.That(Accepted, Is.EqualTo(binding.request), binding.button.ToString());
                Assert.That(Get<string>(router, "LastUsedControlScheme"), Is.EqualTo("Gamepad"));
            }
            for (int button = 0; button < 2; button++)
            {
                Release(); Reset();
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)(1 << button) }); Pump(); Resolve();
                Assert.That(Accepted, Is.EqualTo(button == 0 ? PlayerActionRequest.Attack : PlayerActionRequest.Parry));
                Assert.That(Get<string>(router, "LastUsedControlScheme"), Is.EqualTo("Keyboard&Mouse"));
            }
        }

        [Test]
        public void BWithAndWithoutTargetTenPressesEachNeverDoubleFires()
        {
            int startCount = Get<int>(counter, "Count");
            int sharedBefore = Get<int>(router, "SharedPressCount");
            for (int i = 0; i < 10; i++)
            {
                Reset(); Include(); Pad(GamepadButton.East); Resolve();
                Assert.That(Accepted, Is.EqualTo(PlayerActionRequest.Interact));
                Assert.That(Get<int>(counter, "Count"), Is.EqualTo(startCount + i + 1));
                Assert.That(Get<bool>(motor, "IsDashing"), Is.False);
                Release(); Reset(); Exclude(); Pad(GamepadButton.East); Resolve();
                Assert.That(Accepted, Is.EqualTo(PlayerActionRequest.Dash));
                Assert.That(Get<int>(counter, "Count"), Is.EqualTo(startCount + i + 1));
                Release();
            }
            Assert.That(Get<int>(router, "SharedPressCount"), Is.EqualTo(sharedBefore + 20));
        }

        [Test]
        public void RejectedVanishedAndExitedTargetsNeverBecomeDelayedDash()
        {
            for (int scenario = 0; scenario < 5; scenario++)
            {
                Release(); Reset(); Include();
                if (scenario == 0) { Keys(Key.J); Resolve(); Keys(); }
                if (scenario == 1) Field(motor, "isGrounded", false);
                Pad(GamepadButton.East);
                if (scenario == 2) target.SetActive(false);
                if (scenario == 3) Exclude();
                if (scenario == 4) target.GetComponent<Collider2D>().enabled = false;
                for (int tick = 0; tick < 15; tick++) { Resolve(); Call(state, "CombatTickTimers"); }
                Assert.That(Get<PlayerActionRequest>(router, "LastSharedResolution"), Is.EqualTo(PlayerActionRequest.Interact));
                Assert.That(Get<bool>(motor, "IsDashing"), Is.False);
                Assert.That(Get<int>(counter, "Count"), Is.Zero);
                Call(state, "CompleteAction", PlayerActionState.Attack, "TestUnlock");
                Resolve();
                Assert.That(Get<bool>(motor, "IsDashing"), Is.False);
                target.SetActive(true);
                target.GetComponent<Collider2D>().enabled = true;
            }
        }

        [Test]
        public void EAndShiftRemainIndependentIncludingWhenBIsHeld()
        {
            Exclude(); Keys(Key.E); Resolve();
            Assert.That(Accepted, Is.EqualTo(PlayerActionRequest.None));
            Assert.That(Get<PlayerActionRequest>(state, "PendingRequests") & PlayerActionRequest.Dash, Is.EqualTo(PlayerActionRequest.None));
            Release(); Reset(); Include(); Keys(Key.LeftShift); Resolve();
            Assert.That(Accepted, Is.EqualTo(PlayerActionRequest.Dash));
            Assert.That(Get<int>(counter, "Count"), Is.Zero);
            Release(); Reset(); Include(); Pad(GamepadButton.East); Resolve();
            Call(state, "CompleteAction", PlayerActionState.Interact, "TestComplete");
            Keys(Key.LeftShift); Resolve();
            Assert.That(Accepted, Is.EqualTo(PlayerActionRequest.Dash));
            Release(); Reset(); Include();
            // Even with E and Shift held, the independent physical B edge is observed once.
            Keys(Key.E, Key.LeftShift); Resolve();
            int before = Get<int>(router, "SharedPressCount");
            Pad(GamepadButton.East);
            Assert.That(Get<int>(router, "SharedPressCount"), Is.EqualTo(before + 1));
        }

        [Test]
        public void KeyboardMouseChordsAndHoldProduceOneEdgePerAction()
        {
            for (int kind = 0; kind < 2; kind++)
            {
                Release(); Reset();
                int before = Get<int>(router, "RoutedPressCount");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(kind == 0 ? Key.J : Key.K));
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)(1 << kind) }); Pump(); Resolve();
                for (int i = 0; i < 10; i++) Pump();
                Assert.That(Get<int>(router, "RoutedPressCount"), Is.EqualTo(before + 1));
                // Releasing one binding while the other remains held must not re-arm.
                Keys();
                Keys(kind == 0 ? Key.J : Key.K);
                Assert.That(Get<int>(router, "RoutedPressCount"), Is.EqualTo(before + 1));
                if (kind == 0) Assert.That(Get<float>(attack, "InputBufferRemaining"), Is.Zero);
            }
        }

        [Test]
        public void PressAndReleaseBeforeCollectionStillLatchesOnce()
        {
            Include();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
            InputSystem.QueueStateEvent(pad, new GamepadState()); Pump();
            Assert.That((bool)Call(router, "IsHeld", "SharedDashInteract"), Is.False);
            Assert.That(Get<PlayerActionRequest>(state, "PendingRequests"), Is.EqualTo(PlayerActionRequest.Interact));
            Resolve(); Resolve();
            Assert.That(Get<int>(counter, "Count"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisableEnableTenTimesRequiresReleaseAndDoesNotDuplicateSubscriptions()
        {
            for (int i = 0; i < 10; i++)
            {
                Reset(); Include(); Pad(GamepadButton.East); Resolve();
                int before = Get<int>(counter, "Count");
                ((Behaviour)router).enabled = false;
                ((Behaviour)router).enabled = true;
                RestrictDevices();
                yield return null;
                Pump(); Resolve();
                Assert.That(Get<int>(counter, "Count"), Is.EqualTo(before));
                Release(); Reset(); Include(); Pad(GamepadButton.East); Resolve();
                Assert.That(Get<int>(counter, "Count"), Is.EqualTo(before + 1));
                Release();
            }
        }

        [UnityTest]
        public IEnumerator PauseWinsRegardlessOfEventOrderAndResumeDiscardsGameplay()
        {
            Keys(Key.J); int buffered = (int)Call(state, "GetInputBufferFramesRemaining", PlayerActionRequest.Attack);
            Keys(Key.Escape, Key.K);
            Assert.That(Static<bool>("IsPaused"), Is.True);
            Assert.That((int)Call(state, "GetInputBufferFramesRemaining", PlayerActionRequest.Attack), Is.EqualTo(buffered));
            Assert.That(Get<PlayerActionRequest>(state, "PendingRequests") & PlayerActionRequest.Parry, Is.EqualTo(PlayerActionRequest.None));
            Keys(); yield return null;
            Keys(Key.Escape, Key.Space);
            Assert.That(Static<bool>("IsPaused"), Is.False);
            Assert.That(Get<PlayerActionRequest>(state, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None));
            yield return null;
            Pump(); Resolve();
            Assert.That(Get<PlayerActionState>(state, "ActionState"), Is.EqualTo(PlayerActionState.Neutral));
            Assert.That(Get<PlayerActionRequest>(state, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None));
            Release(); yield return null;
            Pad(GamepadButton.Start);
            Assert.That(Static<bool>("IsPaused"), Is.True);
            Pad(); yield return null;
            Pad(GamepadButton.Start);
            Assert.That(Static<bool>("IsPaused"), Is.False);
        }

        [Test]
        public void HitstopCollectsAndFreezesTheSelectedBContext()
        {
            Include(); TimeCall("RequestHitstop", this, 4f / 60f);
            Pad(GamepadButton.East);
            int count = Get<int>(counter, "Count");
            Call(clock, "AdvanceFrame", 3d / 60d);
            Assert.That(Get<int>(counter, "Count"), Is.EqualTo(count));
            Assert.That((int)Call(state, "GetInputBufferFramesRemaining", PlayerActionRequest.Interact), Is.EqualTo(1));
            Exclude(); TimeCall("ReleaseOwner", this);
            Call(clock, "AdvanceFrame", 10d / 60d);
            Assert.That(Get<bool>(motor, "IsDashing"), Is.False);
            Assert.That(Get<int>(counter, "Count"), Is.EqualTo(count));
            Release(); Reset(); Include(); TimeCall("RequestHitstop", this, 4f / 60f);
            Pad(GamepadButton.East); Call(clock, "AdvanceFrame", 4d / 60d);
            Assert.That(Get<int>(counter, "Count"), Is.EqualTo(count));
            Call(clock, "AdvanceFrame", 1d / 60d);
            Assert.That(Get<int>(counter, "Count"), Is.EqualTo(count + 1));
        }

        [Test]
        public void MoveThresholdOpposingDirectionsAndDownJumpSampleAreActionDriven()
        {
            Keys(Key.A); Assert.That(Get<float>(motor, "MoveInput"), Is.EqualTo(-1));
            Keys(Key.A, Key.D); Assert.That(Get<float>(motor, "MoveInput"), Is.Zero);
            Keys(Key.D); Assert.That(Get<float>(motor, "MoveInput"), Is.EqualTo(1));
            Keys();
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.19f, 0) }); Pump();
            Assert.That(Get<Vector2>(router, "MoveValue"), Is.EqualTo(Vector2.zero));
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.21f, 0) }); Pump();
            Assert.That(Get<Vector2>(router, "MoveValue").x, Is.GreaterThan(0));
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.59f, 0) }); Pump();
            Assert.That(Get<float>(motor, "MoveInput"), Is.Zero);
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.61f, 0) }); Pump();
            Assert.That(Get<float>(motor, "MoveInput"), Is.EqualTo(1));
            Pad(GamepadButton.DpadLeft); Assert.That(Get<float>(motor, "MoveInput"), Is.EqualTo(-1));
            Pad();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.Space));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Pump();
            Assert.That(Get<bool>(router, "LastJumpWasDown"), Is.True);
            Assert.That(Get<bool>(motor, "JumpDownInput"), Is.True);
            Assert.That(Get<bool>(motor, "DownInput"), Is.False);
        }

        [Test]
        public void All127KeyboardRequestCombinationsFollowCentralPriority()
        {
            Key[] keys = { Key.LeftShift, Key.K, Key.Q, Key.J, Key.H, Key.Space, Key.E };
            for (int mask = 1; mask <= 127; mask++)
            {
                Release(); Reset(); Include();
                var pressed = new List<Key>();
                for (int bit = 0; bit < keys.Length; bit++) if ((mask & (1 << bit)) != 0) pressed.Add(keys[bit]);
                Keys(pressed.ToArray()); Resolve();
                Assert.That(Accepted, Is.EqualTo((PlayerActionRequest)(mask & -mask)), "mask " + mask);
                Assert.That(Get<PlayerActionRequest>(state, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None));
            }
        }

        [Test]
        public void RealTriggerBoundaryAndTargetSwitchUseInteractorSelection()
        {
            for (int i = 0; i < 10; i++)
            {
                Reset();
                target.transform.position = new Vector3(0.99f, 0);
                Call(clock, "AdvanceFrame", 1d / 60d);
                Pad(GamepadButton.East); Resolve();
                Assert.That(Accepted, Is.EqualTo(PlayerActionRequest.Interact), "Inside boundary");
                Release(); Reset();
                target.transform.position = new Vector3(1.05f, 0); // beyond Physics2D contact skin
                Call(clock, "AdvanceFrame", 1d / 60d);
                Pad(GamepadButton.East); Resolve();
                Assert.That(Accepted, Is.EqualTo(PlayerActionRequest.Dash), "Outside boundary");
                Release();
            }
            GameObject other = new GameObject("CloserCounter");
            try
            {
                other.transform.position = new Vector3(0.1f, 0);
                var collider = other.AddComponent<BoxCollider2D>(); collider.isTrigger = true;
                Component otherCounter = Add(other, "TechnicalSwordAction.Tests.StateContractInteractable");
                Reset(); Include(); Call(interactor, "OnTriggerEnter2D", collider);
                Pad(GamepadButton.East);
                // The nearest target at collection stays selected even if priorities by distance change.
                other.transform.position = new Vector3(0.9f, 0);
                target.transform.position = new Vector3(0.01f, 0);
                Call(interactor, "SelectCurrentInteractable");
                Resolve();
                Assert.That(Get<int>(otherCounter, "Count"), Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(other); }
        }

        private PlayerActionRequest Accepted => Get<PlayerActionRequest>(state, "LastAcceptedRequest");
        private void RestrictDevices() { actions = Get<InputActionAsset>(router, "Actions"); actions.devices = new InputDevice[] { keyboard, mouse, pad }; }
        private void Reset()
        {
            Call(state, "ResetToSafeState", "InputTest", true);
            player.transform.position = Vector3.zero;
            player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            Field(motor, "isGrounded", true);
            Call(gauge, "AddGauge", 100f);
            Physics2D.SyncTransforms();
        }
        private void Include() { Call(interactor, "OnTriggerEnter2D", target.GetComponent<Collider2D>()); Call(interactor, "SelectCurrentInteractable"); }
        private void Exclude() => Call(interactor, "OnTriggerExit2D", target.GetComponent<Collider2D>());
        private void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); Pump(); }
        private void Pad(params GamepadButton[] buttons)
        {
            var value = new GamepadState();
            foreach (var button in buttons) value = value.WithButton(button);
            InputSystem.QueueStateEvent(pad, value); Pump();
        }
        private void Release()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(pad, new GamepadState());
            InputSystem.QueueStateEvent(mouse, new MouseState()); Pump();
        }
        private void Pump() { InputSystem.Update(); Call(router, "CollectInput"); }
        private void Resolve() => Call(state, "CombatTick");
        private void TimeCall(string name, params object[] args) => clockType.GetMethod(name).Invoke(null, args);
        private T Static<T>(string name) => (T)clockType.GetProperty(name).GetValue(null);
        private static Component Add(GameObject item, string name) => item.AddComponent(Type.GetType(name + ", Assembly-CSharp", true));
        private static T Get<T>(object item, string name) => (T)item.GetType().GetProperty(name, Flags).GetValue(item);
        private static void Field(object item, string name, object value) => item.GetType().GetField(name, Flags).SetValue(item, value);
        private static object Call(object item, string name, params object[] args) => item.GetType().GetMethod(name, Flags).Invoke(item, args);
    }
}
