using System.Collections.Generic;
using TechnicalSwordAction.PlayerState;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerParryCounter2D : MonoBehaviour, IPlayerActionStateHandler, ICombatTickListener, ICombatTimerListener
{
    [SerializeField, Min(1)] private int hitFrame = 6;
    [SerializeField, Min(1)] private int endFrame = 24;
    [SerializeField, Min(1)] private int damage = 3;
    [SerializeField] private float radius = 1.1f;
    private PlayerStateMachine state;
    private PlayerMotor2D motor;
    private int elapsed;
    private bool active;
    private readonly HashSet<Damageable2D> targets = new();
    public PlayerActionState ActionState => PlayerActionState.ParryCounter;
    public int CombatTickOrder => 200;
    public float LockRemaining => active ? Mathf.Max(0, Mathf.Max(endFrame, hitFrame) - elapsed) * CombatTimeController.StepSeconds : 0f;
    public bool CanStartAction => isActiveAndEnabled && !active && state != null &&
        state.LifeState == PlayerLifeState.Alive && state.ActionState == PlayerActionState.ParrySuccess;
    private void OnEnable()
    {
        state = GetComponent<PlayerStateMachine>();
        motor = GetComponent<PlayerMotor2D>();
        state?.RegisterActionHandler(this);
        CombatTimeController.Register(this);
    }
    public bool TryStartAction()
    {
        if (!CanStartAction) return false;
        active = true;
        elapsed = 0;
        targets.Clear();
        return true;
    }
    public void CombatTick() { }
    public void CombatTickTimers()
    {
        if (!active) return;
        elapsed++;
        if (elapsed == Mathf.Max(1, hitFrame))
        {
            Vector2 direction = Vector2.right * (motor != null ? motor.FacingSign : 1);
            Vector2 center = (Vector2)transform.position + direction * 0.7f;
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, radius))
            {
                Damageable2D target = hit.GetComponentInParent<Damageable2D>();
                if (target != null && targets.Add(target)) target.TakeHit(damage, direction);
            }
        }
        if (elapsed >= Mathf.Max(endFrame, hitFrame))
        {
            CancelAction();
            state.CompleteAction(ActionState, "ParryCounterComplete");
        }
    }
    public void CancelAction() { active = false; elapsed = 0; targets.Clear(); }
    private void OnDisable()
    {
        CancelAction();
        CombatTimeController.Unregister(this);
        state?.CompleteAction(ActionState, "ParryCounterDisabled");
        state?.UnregisterActionHandler(this);
    }
}
