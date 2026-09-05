using System;
using System.Collections.Generic;
using TechnicalSwordAction.PlayerState;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Collects Dynamic Update input; the state machine owns combat-frame buffers.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class PlayerInputRouter : MonoBehaviour
{
    private sealed class Button
    {
        public InputAction Action;
        public bool Held;
        public bool Pressed;
        public Action<InputAction.CallbackContext> Performed;
        public Action<InputAction.CallbackContext> Canceled;
    }

    private readonly Dictionary<string, Button> buttons = new();
    private InputActionAsset actions;
    private InputAction move;
    private PlayerStateMachine state;
    private PlayerMotor2D motor;
    private PlayerAttack2D attack;
    private bool jumpDown;
    private int suppressFrame = -1;

    public Vector2 MoveValue { get; private set; }
    public bool DownHeld => MoveValue.y <= -0.5f;
    public bool LastJumpWasDown { get; private set; }
    public InputDevice LastUsedDevice { get; private set; }
    public string LastUsedControlScheme => LastUsedDevice is Gamepad ? "Gamepad" : "Keyboard&Mouse";
    public PlayerActionRequest LastSharedResolution => state != null
        ? state.LastSharedInputResolution : PlayerActionRequest.None;
    public int SharedPressCount { get; private set; }
    public int RoutedPressCount { get; private set; }
    public InputActionAsset Actions => actions;
    public event Action<PlayerActionRequest> RequestRouted;

    private void OnEnable()
    {
        state = GetComponent<PlayerStateMachine>();
        motor = GetComponent<PlayerMotor2D>();
        attack = GetComponent<PlayerAttack2D>();
        // Clone the project-wide asset: each component owns its subscriptions and lifetime.
        actions = Instantiate(InputSystem.actions);
        actions.Disable();
        move = actions.FindAction("Gameplay/Move", true);
        move.performed += ObserveDevice;
        foreach (string name in new[] { "Jump", "Attack", "Dash", "Parry", "Special", "Heal", "Interact", "Pause" })
            Subscribe(actions.FindAction("Gameplay/" + name, true));
        Subscribe(actions.FindAction("Context/SharedDashInteract", true));
        actions.FindActionMap("Gameplay", true).Enable();
        actions.FindActionMap("Context", true).Enable();
        CombatTimeController.PauseChanged += OnPauseChanged;
        InputSystem.onAfterUpdate += AfterInputUpdate;
    }

    private void Subscribe(InputAction action)
    {
        var button = new Button { Action = action };
        // An already held binding is not a fresh press when the component is enabled.
        foreach (InputControl control in action.controls)
            if (control.EvaluateMagnitude() >= InputSystem.settings.defaultButtonPressPoint) button.Held = true;
        button.Performed = context =>
        {
            ObserveDevice(context);
            if (!button.Held && (action.name == "Pause" || (state != null && state.CanCollectGameplayInput)))
            {
                button.Pressed = true;
                if (action.name == "Jump") jumpDown = move.ReadValue<Vector2>().y <= -0.5f;
            }
            button.Held = true;
        };
        button.Canceled = _ => button.Held = false;
        action.performed += button.Performed;
        action.canceled += button.Canceled;
        buttons.Add(action.name, button);
    }

    private void ObserveDevice(InputAction.CallbackContext context) => LastUsedDevice = context.control.device;
    public bool IsHeld(string actionName) => buttons.TryGetValue(actionName, out Button button) && button.Held;

    private void AfterInputUpdate()
    {
        // Snapshot shared context before gameplay Update can remove or switch targets.
        if (InputState.currentUpdateType == InputUpdateType.Dynamic) CollectInput();
    }

    /// <summary>Also used by Input System event injection tests, after a Dynamic input update.</summary>
    public void CollectInput()
    {
        if (!isActiveAndEnabled || actions == null) return;
        // Resolve Pause before any gameplay edge, regardless of input event order.
        bool pausePressed = TakePress("Pause");
        if (pausePressed && !DialogueController.GameplayInputBlocked)
        {
            if (state != null && state.RequestPause()) NotifyRequest(PlayerActionRequest.Pause);
        }

        bool blocked = state == null || !state.CanCollectGameplayInput;
        if (blocked || suppressFrame == Time.frameCount)
        {
            ClearGameplayInput();
            return;
        }

        MoveValue = move.ReadValue<Vector2>();
        motor?.SetMoveInput(MoveValue);
        if (TakePress("SharedDashInteract"))
        {
            if (state.RequestSharedDashInteract())
            {
                SharedPressCount++;
                NotifyRequest(state.LastSharedInputResolution);
            }
        }
        Route("Dash", PlayerActionRequest.Dash);
        Route("Parry", PlayerActionRequest.Parry);
        Route("Special", PlayerActionRequest.Special);
        if (TakePress("Attack") && attack != null && attack.RequestAttack()) NotifyRequest(PlayerActionRequest.Attack);
        Route("Heal", PlayerActionRequest.Heal);
        if (TakePress("Jump"))
        {
            LastJumpWasDown = jumpDown;
            motor?.SetJumpDownInput(jumpDown);
            if (state.RequestAction(PlayerActionRequest.Jump)) NotifyRequest(PlayerActionRequest.Jump);
        }
        Route("Interact", PlayerActionRequest.Interact);
    }

    private void Route(string name, PlayerActionRequest request)
    {
        if (TakePress(name) && state.RequestAction(request)) NotifyRequest(request);
    }

    private void NotifyRequest(PlayerActionRequest request)
    {
        RoutedPressCount++;
        RequestRouted?.Invoke(request);
    }

    private bool TakePress(string name)
    {
        Button button = buttons[name];
        bool pressed = button.Pressed;
        button.Pressed = false;
        return pressed;
    }

    public void ClearGameplayInput()
    {
        foreach (var pair in buttons)
            if (pair.Key != "Pause") pair.Value.Pressed = false;
        MoveValue = Vector2.zero;
        jumpDown = false;
        motor?.ClearSampledInput();
    }

    private void OnPauseChanged(bool paused)
    {
        ClearGameplayInput();
        suppressFrame = Time.frameCount;
    }

    private void OnDisable()
    {
        CombatTimeController.PauseChanged -= OnPauseChanged;
        InputSystem.onAfterUpdate -= AfterInputUpdate;
        ClearGameplayInput();
        state?.ClearBufferedGameplayInput();
        if (actions == null) return;
        move.performed -= ObserveDevice;
        foreach (Button button in buttons.Values)
        {
            button.Action.performed -= button.Performed;
            button.Action.canceled -= button.Canceled;
        }
        buttons.Clear();
        actions.Disable();
        Destroy(actions);
        actions = null;
    }
}
