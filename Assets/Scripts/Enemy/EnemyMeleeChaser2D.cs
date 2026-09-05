using UnityEngine;

[RequireComponent(typeof(Damageable2D))]
public class EnemyMeleeChaser2D : MonoBehaviour, ICombatTickListener, ICombatHitListener, ICombatTimerListener
{
    private enum State
    {
        Idle,
        Approach,
        AttackWindup,
        Recovery,
        Cooldown
    }

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float detectRange = 10f;
    [SerializeField] private float loseTargetRange = 14f;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 3.2f;
    [SerializeField] private float preferredMinDistance = 1.05f;
    [SerializeField] private float preferredMaxDistance = 1.7f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.25f;
    [SerializeField] private float attackWindupDuration = 0.18f;
    [SerializeField] private float recoveryDuration = 0.45f;
    [SerializeField] private float cooldownDuration = 0.55f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackKnockback = 4.5f;
    [SerializeField] private LayerMask attackTargetLayers;
    [SerializeField] private bool disableContactDamageSources = true;

    private Damageable2D damageable;
    private State currentState = State.Idle;
    private float stateTimer;
    private bool attackApplied;
    private Vector2 facing = Vector2.right;
    private bool advanceStateTimer;
    private bool pendingMeleeHit;

    public int CombatTickOrder => 200;

    private void OnEnable()
    {
        CombatTimeController.Register(this);
    }

    private void OnDisable()
    {
        CombatTimeController.Unregister(this);
        CombatTimeController.ReleaseOwner(this);
        currentState = State.Idle;
        stateTimer = 0f;
        attackApplied = false;
        advanceStateTimer = false;
        pendingMeleeHit = false;
    }

    private void Awake()
    {
        damageable = GetComponent<Damageable2D>();
        if (disableContactDamageSources)
        {
            DisablePassiveDamageSources();
        }
    }

    private void Start()
    {
        TryFindPlayerTarget();
    }

    public void CombatTick()
    {
        advanceStateTimer = false;
        pendingMeleeHit = false;
        if (target == null)
        {
            TryFindPlayerTarget();
            return;
        }

        if (damageable != null && damageable.IsStunned)
        {
            return;
        }

        Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
        float distance = toTarget.magnitude;

        if (distance > loseTargetRange)
        {
            currentState = State.Idle;
            return;
        }

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            facing = toTarget.normalized;
        }

        switch (currentState)
        {
            case State.Idle:
                if (distance <= detectRange)
                {
                    currentState = State.Approach;
                }
                break;
            case State.Approach:
                UpdateApproach(distance, toTarget);
                break;
            case State.AttackWindup:
                advanceStateTimer = true;
                UpdateAttackWindup(distance);
                break;
            case State.Recovery:
            case State.Cooldown:
                advanceStateTimer = true;
                UpdateTimedState();
                break;
        }
    }

    public void ResolveCombatHits()
    {
        if (pendingMeleeHit)
        {
            pendingMeleeHit = false;
            TryApplyMeleeHit();
        }
    }

    public void CombatTickTimers()
    {
        if (advanceStateTimer)
        {
            stateTimer = CombatTimeController.AdvanceTimer(stateTimer);
        }
    }

    private void UpdateApproach(float distance, Vector2 toTarget)
    {
        if (distance >= preferredMinDistance && distance <= attackRange && IsTargetInFront())
        {
            currentState = State.AttackWindup;
            stateTimer = attackWindupDuration;
            attackApplied = false;
            return;
        }

        if (distance < preferredMinDistance)
        {
            Vector2 retreat = -toTarget.normalized * (moveSpeed * CombatTimeController.StepSeconds);
            transform.position += (Vector3)retreat;
            return;
        }

        if (distance > preferredMaxDistance)
        {
            Vector2 advance = toTarget.normalized * (moveSpeed * CombatTimeController.StepSeconds);
            transform.position += (Vector3)advance;
        }
    }

    private void UpdateAttackWindup(float distance)
    {
        if (!attackApplied && CombatTimeController.AdvanceTimer(stateTimer) <= 0f)
        {
            attackApplied = true;
            if (distance <= attackRange * 1.1f)
            {
                pendingMeleeHit = true;
            }

            currentState = State.Recovery;
            stateTimer = recoveryDuration;
            advanceStateTimer = false;
        }
    }

    private void UpdateTimedState()
    {
        if (CombatTimeController.AdvanceTimer(stateTimer) > 0f)
        {
            return;
        }

        if (currentState == State.Recovery)
        {
            currentState = State.Cooldown;
            stateTimer = cooldownDuration;
            advanceStateTimer = false;
            return;
        }

        currentState = State.Approach;
        advanceStateTimer = false;
    }

    private void TryApplyMeleeHit()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D other = hits[i];
            if ((attackTargetLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                continue;
            }

            if (!other.TryGetComponent(out IDamageReceiver2D receiver))
            {
                continue;
            }

            receiver.TryReceiveHit(attackDamage, transform.position, attackKnockback);
        }
    }

    private bool IsTargetInFront()
    {
        Vector2 toTarget = ((Vector2)target.position - (Vector2)transform.position).normalized;
        return Vector2.Dot(facing, toTarget) > 0.1f;
    }

    private void TryFindPlayerTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
        }
    }

    private void DisablePassiveDamageSources()
    {
        DamageSource2D[] sources = GetComponentsInChildren<DamageSource2D>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i].enabled = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
