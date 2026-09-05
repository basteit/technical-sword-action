using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class PlayerAnimationDriver2D : MonoBehaviour, ICombatTickListener
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerDamageReceiver2D damageReceiver;

    [Header("Parry Feedback")]
    [SerializeField, Min(0f)] private float parrySuccessPoseDuration = 0.16f;

    private static readonly int IdleState = Animator.StringToHash("PlayerPrototype_Idle");
    private static readonly int MoveState = Animator.StringToHash("PlayerPrototype_Move");
    private static readonly int JumpState = Animator.StringToHash("PlayerPrototype_Jump");
    private static readonly int FallState = Animator.StringToHash("PlayerPrototype_Fall");
    private static readonly int DashState = Animator.StringToHash("PlayerPrototype_Dash");
    private static readonly int ParryState = Animator.StringToHash("PlayerPrototype_Parry");
    private static readonly int ParryFailState = Animator.StringToHash("PlayerPrototype_ParryFail");
    private static readonly int ParrySuccessState = Animator.StringToHash("PlayerPrototype_ParrySuccess");
    private static readonly int SpecialState = Animator.StringToHash("PlayerPrototype_Special");
    private static readonly int HitState = Animator.StringToHash("PlayerPrototype_Hit");

    private PlayerState lastDrivenState = (PlayerState)(-1);
    private int observedParryCount;
    private float parrySuccessTimer;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CombatTimeController.Register(this);
        CombatTimeController.RegisterAnimator(animator, this);
        observedParryCount = damageReceiver != null ? damageReceiver.BlockedByParry : 0;
        parrySuccessTimer = 0f;
        lastDrivenState = (PlayerState)(-1);
    }

    public int CombatTickOrder => 300;

    public void CombatTick()
    {
        if (animator == null || stateMachine == null)
        {
            return;
        }

        if (TryPlayNewParrySuccess())
        {
            return;
        }

        PlayerState state = stateMachine.CurrentState;
        if (state == PlayerState.Attack)
        {
            // PlayerAttack2D owns the existing AttackTrigger/ComboStep contract.
            parrySuccessTimer = 0f;
            lastDrivenState = PlayerState.Attack;
            return;
        }

        if (parrySuccessTimer > 0f && (state == PlayerState.Idle || state == PlayerState.Move))
        {
            parrySuccessTimer = Mathf.Max(0f, parrySuccessTimer - CombatTimeController.StepSeconds);
            return;
        }

        parrySuccessTimer = 0f;
        if (state == lastDrivenState)
        {
            return;
        }

        animator.Play(GetStateHash(state), 0, 0f);
        lastDrivenState = state;
    }

    private bool TryPlayNewParrySuccess()
    {
        if (damageReceiver == null)
        {
            return false;
        }

        int parryCount = damageReceiver.BlockedByParry;
        if (parryCount == observedParryCount)
        {
            return false;
        }

        observedParryCount = parryCount;
        parrySuccessTimer = parrySuccessPoseDuration;
        animator.Play(ParrySuccessState, 0, 0f);
        lastDrivenState = (PlayerState)(-1);
        return true;
    }

    private static int GetStateHash(PlayerState state)
    {
        return state switch
        {
            PlayerState.Move => MoveState,
            PlayerState.Jump => JumpState,
            PlayerState.Fall => FallState,
            PlayerState.Dash => DashState,
            PlayerState.Parry => ParryState,
            PlayerState.ParryFail => ParryFailState,
            PlayerState.Special => SpecialState,
            PlayerState.Hit => HitState,
            _ => IdleState
        };
    }

    private void ResolveReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (stateMachine == null)
        {
            stateMachine = GetComponent<PlayerStateMachine>();
        }

        if (damageReceiver == null)
        {
            damageReceiver = GetComponent<PlayerDamageReceiver2D>();
        }
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        CombatTimeController.Unregister(this);
        CombatTimeController.UnregisterAnimator(animator, this);
    }
}
