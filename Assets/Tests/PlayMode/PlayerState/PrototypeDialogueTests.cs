using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TechnicalSwordAction.PlayerState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PrototypeDialogueTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private Component clock, player, motor, router, interactor, controller, view, left, right, arena;
        private ScriptableObject sequence, profile;
        private Rigidbody2D body;
        private bool savedManual;
        private Keyboard keyboard;
        private Gamepad pad;
        private readonly List<InputDevice> nativeDevices = new();
        private readonly List<Object> owned = new();
        private InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;
        private InputSettings.BackgroundBehavior savedBackground;

        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
        private static V Get<V>(object obj, string name) => (V)(obj.GetType().GetProperty(name, Flags)?.GetValue(obj) ?? obj.GetType().GetField(name, Flags).GetValue(obj));
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
        private static void Pause(bool value) => T("CombatTimeController").GetMethod("SetPaused").Invoke(null, new object[] { value });
        private bool Open => Get<bool>(controller, "IsOpen");
        private bool Locked => Get<bool>(controller, "IsGameplayLockActive");
        private PlayerActionState State => Get<PlayerActionState>(player, "ActionState");
        private void Frames(int count = 1) { for (int i = 0; i < count; i++) Call(clock, "AdvanceFrame", 1d / 60d); }
        private void Visuals(float seconds) => Call(view, "AdvanceVisuals", seconds);
        private void Begin() => Assert.That((bool)Call(controller, "TryPlay", sequence), Is.True);
        private void Key(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        private void Pad(bool east) { InputSystem.QueueStateEvent(pad, east ? new GamepadState().WithButton(GamepadButton.East) : new GamepadState()); InputSystem.Update(); }
        private object Line(int index) => Get<IList>(sequence, "Lines")[index];

        [UnitySetUp]
        public IEnumerator Setup()
        {
            clock = (Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            savedManual = Get<bool>(clock, "ManualAdvanceOnly");
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player = (Component)Object.FindFirstObjectByType(T("PlayerStateMachine"));
            body = player.GetComponent<Rigidbody2D>();
            motor = player.GetComponent(T("PlayerMotor2D"));
            router = player.GetComponent(T("PlayerInputRouter"));
            interactor = player.GetComponent(T("PlayerInteractor2D"));
            arena = (Component)Object.FindFirstObjectByType(T("PrototypeCombatArena"));
            controller = (Component)Object.FindFirstObjectByType(T("DialogueController"));
            view = Get<Component>(controller, "view");
            ((Behaviour)view).enabled = false; // deterministic visual time, controller input still runs
            left = Get<Component>(view, "leftPortrait"); right = Get<Component>(view, "rightPortrait");
            sequence = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/PrototypeDialogue/Dialogue_PrototypeTest.asset");
            profile = Object.Instantiate(Get<ScriptableObject>(view, "presentation")); owned.Add(profile);
            Set(view, "presentation", profile);
            Set(controller, "releaseGuardDuration", 0f);
            savedEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (var device in InputSystem.devices)
                if (device.native && device.enabled) { InputSystem.DisableDevice(device); nativeDevices.Add(device); }
            keyboard = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<Gamepad>();
            Get<InputActionAsset>(router, "Actions").devices = new InputDevice[] { keyboard, pad };
            Frames(2);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (controller != null) { ((Behaviour)controller).enabled = true; Call(controller, "Interrupt"); }
            Pause(false);
            if (player != null) { player.gameObject.SetActive(true); Call(arena, "ResetActionsOnly"); }
            foreach (Object obj in owned) if (obj != null) Object.DestroyImmediate(obj);
            owned.Clear();
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (pad != null) InputSystem.RemoveDevice(pad);
            foreach (var device in nativeDevices) if (device.added) InputSystem.EnableDevice(device);
            nativeDevices.Clear();
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
            InputSystem.settings.backgroundBehavior = savedBackground;
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, savedManual);
            yield return null;
        }

        private void MoveIntoTrigger()
        {
            body.position = new Vector2(-6, .81f); player.transform.position = body.position;
            body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); Frames(3);
            Call(interactor, "RefreshAfterTeleport");
        }

        [Test]
        public void BoxFadesFromTransparentWithoutChangingCombatTime()
        {
            float scale = Time.timeScale;
            Begin(); Assert.That(Get<float>(view, "Opacity"), Is.Zero);
            Visuals(.175f); Assert.That(Get<float>(view, "Opacity"), Is.EqualTo(.5f).Within(.0001f));
            Visuals(.175f); Assert.That(Get<float>(view, "Opacity"), Is.EqualTo(1f));
            Assert.That(Time.timeScale, Is.EqualTo(scale));
        }

        [Test]
        public void FirstLeftPortraitSlidesFromLeftAndRightRemainsAbsentUntilItSpeaks()
        {
            Vector2 home = ((RectTransform)left.transform).anchoredPosition;
            Begin();
            Assert.That(Get<bool>(left, "HasCharacter"), Is.True);
            Assert.That(Get<bool>(right, "HasCharacter"), Is.False);
            Assert.That(((RectTransform)left.transform).anchoredPosition.x, Is.EqualTo(home.x - 80));
            Visuals(.15f); Assert.That(((RectTransform)left.transform).anchoredPosition.x, Is.InRange(home.x - 80, home.x));
            Visuals(.15f); Assert.That(((RectTransform)left.transform).anchoredPosition, Is.EqualTo(home));
        }

        [Test]
        public void RightSlidesFromRightAndLeftReentryDoesNotRepeat()
        {
            Vector2 home = ((RectTransform)right.transform).anchoredPosition;
            Begin(); Visuals(1); Call(controller, "Advance");
            Assert.That(((RectTransform)right.transform).anchoredPosition.x, Is.EqualTo(home.x + 80));
            Assert.That(Get<bool>(left, "HasCharacter"), Is.True);
            Visuals(1); Call(controller, "Advance");
            Assert.That(Get<int>(left, "EntranceCount"), Is.EqualTo(1));
            Assert.That(Get<int>(right, "EntranceCount"), Is.EqualTo(1));
            Assert.That(Get<bool>(left, "IsEntering"), Is.False);
        }

        [Test]
        public void ReplacingCharacterInSameSlotStartsOnlyThatSidesEntrance()
        {
            Begin(); Visuals(1);
            var speaker = ScriptableObject.CreateInstance(T("DialogueSpeakerDefinition")); owned.Add(speaker);
            Set(speaker, "portraitSide", Enum.Parse(T("DialoguePortraitSide"), "Left"));
            object line = Activator.CreateInstance(T("DialogueLine")); Set(line, "speaker", speaker); Set(line, "body", "Test");
            Call(view, "Show", line, true);
            Assert.That(Get<int>(left, "EntranceCount"), Is.EqualTo(2));
            Assert.That(Get<Object>(left, "DisplayedSpeaker"), Is.SameAs(speaker));
            Assert.That(Get<bool>(right, "HasCharacter"), Is.False);
        }

        [Test]
        public void PortraitAndBoxArtworkCanBeReplacedWithoutNewCodeOrEntrance()
        {
            Begin(); Visuals(1);
            var texture = new Texture2D(4, 4); owned.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f); owned.Add(sprite);
            object line = Activator.CreateInstance(T("DialogueLine"));
            Set(line, "speaker", Get<Object>(Line(0), "Speaker")); Set(line, "portraitOverride", sprite);
            Set(profile, "boxSprite", sprite);
            Call(view, "Show", line, true);
            Assert.That(Get<Object>(Get<Component>(left, "portraitImage"), "sprite"), Is.SameAs(sprite));
            Assert.That(Get<Object>(Get<Component>(view, "boxImage"), "sprite"), Is.SameAs(sprite));
            Assert.That(Get<GameObject>(left, "placeholder").activeSelf, Is.False);
            Assert.That(Get<int>(left, "EntranceCount"), Is.EqualTo(1));
        }

        [Test]
        public void ZeroDurationPresentationIsImmediateAndStillKeepsTwoSlots()
        {
            Set(profile, "fadeInSeconds", 0f); Set(profile, "portraitSlideSeconds", 0f);
            Begin();
            Assert.That(Get<float>(view, "Opacity"), Is.EqualTo(1));
            Assert.That(Get<bool>(left, "IsEntering"), Is.False);
            Assert.That(left.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Assert.That(right.GetComponent<CanvasGroup>().alpha, Is.Zero);
        }

        [Test]
        public void UIUsesItsOwnTimeEvenWhenCombatClockIsPaused()
        {
            Begin(); Pause(true); float scale = Time.timeScale;
            Visuals(.35f);
            Assert.That(Get<float>(view, "Opacity"), Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(scale));
        }

        [Test]
        public void EveryGameplayActionAndPauseIsBlockedWhileDialogueOwnsInput()
        {
            Begin(); float x = body.position.x;
            foreach (var request in new[] { PlayerActionRequest.Attack, PlayerActionRequest.Dash, PlayerActionRequest.Parry,
                PlayerActionRequest.Special, PlayerActionRequest.Heal, PlayerActionRequest.Jump, PlayerActionRequest.Interact, PlayerActionRequest.Pause })
                Assert.That((bool)Call(player, "RequestAction", request), Is.False, request.ToString());
            Call(motor, "SetMoveInput", Vector2.right); Frames(30);
            Assert.That(body.position.x, Is.EqualTo(x).Within(.001f));
            Assert.That(Get<bool>(player, "CanCollectGameplayInput"), Is.False);
        }

        [Test]
        public void RealTriggerAndUnifiedInteractionStartDialogueInsteadOfCounting()
        {
            MoveIntoTrigger();
            Assert.That(Get<object>(interactor, "CurrentInteractable").GetType().Name, Is.EqualTo("DialogueInteractable2D"));
            Assert.That((bool)Call(player, "RequestInteraction"), Is.True); Frames();
            Assert.That(Open, Is.True); Assert.That(State, Is.EqualTo(PlayerActionState.Interact));
            Assert.That(Get<string>(Get<Component>(view, "bodyLabel"), "text"), Is.EqualTo("Test"));
            Assert.That(player.gameObject.scene.GetRootGameObjects().Single(g => g.name == "InteractCounter").GetComponent(T("PrototypeInteractable2D")), Is.Null);
        }

        [UnityTest]
        public IEnumerator OpeningEIsNotAlsoAnAdvanceAndSpaceCloseDoesNotBecomeJump()
        {
            MoveIntoTrigger(); Key(UnityEngine.InputSystem.Key.E); Frames(); yield return null;
            Assert.That(Open, Is.True); Assert.That(Get<int>(controller, "CurrentLineIndex"), Is.Zero);
            Key(); yield return null;
            Call(controller, "Advance"); Call(controller, "Advance");
            Key(UnityEngine.InputSystem.Key.Space); yield return null;
            Assert.That(Open, Is.False); Assert.That(Locked, Is.True);
            Frames(4); Assert.That(State, Is.EqualTo(PlayerActionState.Interact));
            Key(); yield return null; yield return null; Frames(2);
            Assert.That(Locked, Is.False); Assert.That(State, Is.EqualTo(PlayerActionState.Neutral));
            Assert.That(body.linearVelocity.y, Is.LessThanOrEqualTo(.01f));
            Assert.That(Get<PlayerActionRequest>(player, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None));
        }

        [UnityTest]
        public IEnumerator SharedBStartsAndClosesDialogueWithoutDashAndWorksAgainAfterRelease()
        {
            MoveIntoTrigger(); Pad(true); Frames(); yield return null;
            Assert.That(Open, Is.True); Assert.That(Get<int>(controller, "CurrentLineIndex"), Is.Zero);
            Assert.That(Get<PlayerActionRequest>(player, "LastSharedInputResolution"), Is.EqualTo(PlayerActionRequest.Interact));
            Pad(false); yield return null;
            Call(controller, "Advance"); Call(controller, "Advance");
            Pad(true); yield return null;
            Assert.That(Open, Is.False); Assert.That(Locked, Is.True); Frames(4);
            Assert.That(State, Is.Not.EqualTo(PlayerActionState.Dash));
            Pad(false); yield return null; yield return null; Frames(2); yield return null;
            Pad(true); Frames(); Assert.That(Open, Is.True); Assert.That(State, Is.EqualTo(PlayerActionState.Interact));
        }

        [UnityTest]
        public IEnumerator EscapeSkipsWithoutAlsoOpeningPause()
        {
            Begin(); yield return null;
            Key(UnityEngine.InputSystem.Key.Escape); yield return null;
            Assert.That(Open, Is.False);
            Assert.That((bool)T("CombatTimeController").GetProperty("IsPaused").GetValue(null), Is.False);
            Key(); yield return null; yield return null;
            Assert.That(Locked, Is.False);
        }

        [TestCase("Hit")] [TestCase("Dead")] [TestCase("Reset")] [TestCase("Disable")] [TestCase("ViewDisable")]
        public void ForcedInterruptionClearsBothPortraitsBoxAndInputLockImmediately(string reason)
        {
            if (reason == "ViewDisable") ((Behaviour)view).enabled = true;
            Begin(); Visuals(1); Call(controller, "Advance"); Visuals(1);
            if (reason == "Hit") Call(player, "ForceHit", "DialogueTest");
            if (reason == "Dead") Call(player, "SetDead", "DialogueTest");
            if (reason == "Reset") Call(arena, "ResetActionsOnly");
            if (reason == "Disable") ((Behaviour)controller).enabled = false;
            if (reason == "ViewDisable") ((Behaviour)view).enabled = false;
            Assert.That(Open, Is.False); Assert.That(Locked, Is.False);
            Assert.That(Get<float>(view, "Opacity"), Is.Zero);
            Assert.That(Get<bool>(left, "HasCharacter"), Is.False);
            Assert.That(Get<bool>(right, "HasCharacter"), Is.False);
        }

        [Test]
        public void InterruptedDialogueCanRestartWithoutKeepingOldRightCharacter()
        {
            Begin(); Call(controller, "Advance"); Visuals(1); Call(controller, "Interrupt"); Begin();
            Assert.That(Get<int>(controller, "CurrentLineIndex"), Is.Zero);
            Assert.That(Get<bool>(left, "IsEntering"), Is.True);
            Assert.That(Get<bool>(right, "HasCharacter"), Is.False);
        }

        [UnityTest]
        public IEnumerator SceneReloadDoesNotRetainGlobalDialogueLock()
        {
            Begin(); Call(controller, "Advance"); Visuals(1);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            controller = (Component)Object.FindFirstObjectByType(T("DialogueController"));
            player = (Component)Object.FindFirstObjectByType(T("PlayerStateMachine"));
            arena = (Component)Object.FindFirstObjectByType(T("PrototypeCombatArena"));
            Assert.That(Open, Is.False); Assert.That(Locked, Is.False);
            Assert.That((bool)T("DialogueController").GetProperty("GameplayInputBlocked").GetValue(null), Is.False);
        }
    }
}
