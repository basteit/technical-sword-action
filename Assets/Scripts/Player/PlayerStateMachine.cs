using UnityEngine;

public enum PlayerState
{
    Idle,
    Move,
    Jump,
    Fall,
    Dash,
    Parry,
    ParryFail,
    Attack,
    Special,
    Hit
}

public class PlayerStateMachine : MonoBehaviour
{
    [SerializeField] private PlayerMotor2D motor;
    [SerializeField] private PlayerAttack2D attack;
    [SerializeField] private PlayerDamageReceiver2D damageReceiver;
    [SerializeField] private PlayerParry2D parry;
    [SerializeField] private PlayerSpecialSkill2D specialSkill;
    [SerializeField] private bool debugLogStateChange;

    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

    private void Awake()
    {
        if (motor == null)
        {
            motor = GetComponent<PlayerMotor2D>();
        }

        if (attack == null)
        {
            attack = GetComponent<PlayerAttack2D>();
        }

        if (damageReceiver == null)
        {
            damageReceiver = GetComponent<PlayerDamageReceiver2D>();
        }

        if (parry == null)
        {
            parry = GetComponent<PlayerParry2D>();
        }

        if (specialSkill == null)
        {
            specialSkill = GetComponent<PlayerSpecialSkill2D>();
        }
    }

    private void Update()
    {
        if (motor == null)
        {
            return;
        }

        PlayerState next = EvaluateState();
        if (next != CurrentState)
        {
            CurrentState = next;
            if (debugLogStateChange)
            {
                Debug.Log($"[PlayerState] => {CurrentState}", this);
            }
        }
    }

    private PlayerState EvaluateState()
    {
        if (damageReceiver != null && damageReceiver.IsHitLocked)
        {
            return PlayerState.Hit;
        }

        if (specialSkill != null && specialSkill.IsUsingSkill)
        {
            return PlayerState.Special;
        }

        if (parry != null && parry.IsFailLocked)
        {
            return PlayerState.ParryFail;
        }

        if (motor.IsDashing)
        {
            return PlayerState.Dash;
        }

        if (parry != null && parry.IsParryActive)
        {
            return PlayerState.Parry;
        }

        if (attack != null && attack.IsAttacking)
        {
            return PlayerState.Attack;
        }

        if (!motor.IsGrounded)
        {
            return motor.Velocity.y > 0.05f ? PlayerState.Jump : PlayerState.Fall;
        }

        return Mathf.Abs(motor.MoveInput) > 0.01f ? PlayerState.Move : PlayerState.Idle;
    }
}
