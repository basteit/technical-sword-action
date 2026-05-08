using UnityEngine;

public enum PlayerState
{
    Idle,
    Move,
    Jump,
    Fall,
    Dash,
    Attack,
    Hit
}

public class PlayerStateMachine : MonoBehaviour
{
    [SerializeField] private PlayerMotor2D motor;
    [SerializeField] private PlayerAttack2D attack;
    [SerializeField] private PlayerDamageReceiver2D damageReceiver;
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

        if (motor.IsDashing)
        {
            return PlayerState.Dash;
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
