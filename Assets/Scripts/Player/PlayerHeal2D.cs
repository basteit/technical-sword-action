using TechnicalSwordAction.PlayerState;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerHeal2D : MonoBehaviour, IPlayerActionStateHandler, ICombatTickListener, ICombatTimerListener
{
    [SerializeField, Min(1)] private int healAmount = 2;
    [SerializeField, Min(1)] private int applyFrame = 30;
    [SerializeField, Min(1)] private int endFrame = 48;
    private PlayerStateMachine state;
    private PlayerDamageReceiver2D health;
    private int elapsed;
    private bool applied;
    public int RemainingUses { get; private set; } = 3;
    public bool IsHealing { get; private set; }
    public PlayerActionState ActionState => PlayerActionState.Heal;
    public int CombatTickOrder => 200;
    public float LockRemaining => IsHealing ? Mathf.Max(0, Mathf.Max(endFrame, applyFrame) - elapsed) * CombatTimeController.StepSeconds : 0f;
    public bool CanStartAction => isActiveAndEnabled && !IsHealing && RemainingUses > 0 &&
        health != null && health.isActiveAndEnabled && health.CurrentHp > 0 && health.CurrentHp < health.MaxHp &&
        state != null && state.LifeState == PlayerLifeState.Alive &&
        state.ActionState == PlayerActionState.Neutral && state.LocomotionState == PlayerLocomotionState.Grounded;

    private void OnEnable()
    {
        state = GetComponent<PlayerStateMachine>();
        health = GetComponent<PlayerDamageReceiver2D>();
        state?.RegisterActionHandler(this);
        CombatTimeController.Register(this);
    }
    public bool TryStartAction()
    {
        if (!CanStartAction) return false;
        IsHealing = true;
        elapsed = 0;
        applied = false;
        return true;
    }
    public void CombatTick() { }
    public void CombatTickTimers()
    {
        if (!IsHealing) return;
        if (state.ActionState != ActionState || state.LocomotionState != PlayerLocomotionState.Grounded)
        {
            CancelAction();
            state.CompleteAction(ActionState, "HealInterrupted");
            return;
        }
        elapsed++;
        if (!applied && elapsed >= Mathf.Max(1, applyFrame))
        {
            applied = true;
            if (health.RestoreHealth(healAmount)) RemainingUses--;
        }
        if (elapsed >= Mathf.Max(endFrame, applyFrame))
        {
            CancelAction();
            state.CompleteAction(ActionState, "HealComplete");
        }
    }
    public void CancelAction() { IsHealing = false; elapsed = 0; applied = false; }
    public void Refill() { CancelAction(); RemainingUses = 3; }
    private void OnDisable()
    {
        CancelAction();
        CombatTimeController.Unregister(this);
        state?.CompleteAction(ActionState, "HealDisabled");
        state?.UnregisterActionHandler(this);
    }
}
