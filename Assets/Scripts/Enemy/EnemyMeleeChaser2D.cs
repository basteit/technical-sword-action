using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Damageable2D))]
public class EnemyMeleeChaser2D : MonoBehaviour, ICombatTickListener, ICombatHitListener, ICombatTimerListener
{
    private enum State
    {
        Idle,
        Approach,
        AttackWindup,
        Active,
        Recovery,
        Cooldown
    }

    [Header("Target")]
    [SerializeField] private EnemyCombatDefinition combatDefinition;
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
    private long attackSequence;
    private bool captureHeld;
    private readonly HashSet<int> hitReceivers = new();
    private readonly RaycastHit2D[] movementHits = new RaycastHit2D[8];
    private Collider2D ownCollider;
    private int activeRemaining;
    public EnemyCombatDefinition Definition => combatDefinition;
    public string AttackKind => combatDefinition == null || combatDefinition.parryable ? "Parryable" : "Unparryable";
    public int WindupRemainingFrames => Mathf.CeilToInt(WindupRemaining * 60f - .0001f);
    public int ActiveRemainingFrames => currentState == State.Active ? activeRemaining : 0;
    public Vector2 Facing => facing;
    public string CurrentStateName => damageable != null && !damageable.IsAlive ? "Dead" :
        damageable != null && damageable.IsStunned ? "Stunned" : currentState.ToString();
    public bool IsWindingUp => currentState == State.AttackWindup;
    public float WindupRemaining => IsWindingUp ? stateTimer : 0f;
    public long AttackSequence => attackSequence;
    public event Action WindupStarted;
    public event Action AttackStarted;

    /// <summary>Explicit training override; normal scenes keep their serialized combat values.</summary>
    public void ConfigureForTraining(Transform playerTarget, float windup, float interval, int damage, float speed)
    {
        target = playerTarget;
        attackWindupDuration = Mathf.Clamp(windup, .1f, 10f);
        cooldownDuration = Mathf.Clamp(interval, .1f, 30f);
        attackDamage = Mathf.Max(0, damage);
        moveSpeed = Mathf.Clamp(speed, 0, 10f);
    }

    public void ResetForCapture(Transform playerTarget, bool held = true)
    {
        target = playerTarget;
        captureHeld = held;
        currentState = State.Idle;
        stateTimer = 0f;
        attackApplied = advanceStateTimer = pendingMeleeHit = false;
        hitReceivers.Clear();
        facing = target != null ? ((Vector2)(target.position - transform.position)).normalized : Vector2.right;
    }

    public bool BeginCaptureAttack()
    {
        if (damageable == null || !damageable.IsAlive || damageable.IsStunned || target == null) return false;
        captureHeld = false;
        StartWindup();
        return true;
    }

    private void StartWindup()
    {
        currentState = State.AttackWindup;
        stateTimer = attackWindupDuration;
        attackApplied = false;
        attackSequence++;
        hitReceivers.Clear();
        WindupStarted?.Invoke();
    }

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
        ownCollider = GetComponent<Collider2D>();
        if (combatDefinition != null)
        {
            if (!combatDefinition.IsValid) Debug.LogError("Invalid enemy combat definition", this);
            attackWindupDuration = combatDefinition.windupFrames / 60f;
            recoveryDuration = combatDefinition.recoveryFrames / 60f;
            cooldownDuration = combatDefinition.cooldownFrames / 60f;
            attackDamage = combatDefinition.damage;
            attackKnockback = combatDefinition.knockback;
            attackRange = combatDefinition.attackRange;
            preferredMaxDistance = attackRange;
            preferredMinDistance = attackRange * .65f;
            moveSpeed = combatDefinition.moveSpeed;
            attackTargetLayers = combatDefinition.targetLayers;
        }
        if (disableContactDamageSources)
        {
            DisablePassiveDamageSources();
        }
    }

    private void Start()
    {
        if (combatDefinition != null) damageable.ConfigureCombat(combatDefinition.rank, combatDefinition.maxHp);
        TryFindPlayerTarget();
    }

    public void CombatTick()
    {
        advanceStateTimer = false;
        pendingMeleeHit = false;
        if (captureHeld || damageable == null || !damageable.IsAlive) return;
        if (target == null)
        {
            TryFindPlayerTarget();
            return;
        }

        if (damageable != null && damageable.IsStunned)
        {
            // Never resume an interrupted windup/active hit after stun ends.
            currentState = State.Recovery;
            stateTimer = recoveryDuration;
            activeRemaining = 0;
            return;
        }

        Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
        float distance = toTarget.magnitude;

        if (distance > loseTargetRange)
        {
            damageable.SetCombatParticipation(false);
            currentState = State.Idle;
            return;
        }
        damageable.SetCombatParticipation(true);

        if (currentState != State.AttackWindup && currentState != State.Active && toTarget.sqrMagnitude > 0.0001f)
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
            case State.Active:
                pendingMeleeHit = true;
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
        if (pendingMeleeHit && damageable != null && damageable.IsAlive && !damageable.IsStunned)
        {
            pendingMeleeHit = false;
            TryApplyMeleeHit();
        }
    }

    public void CombatTickTimers()
    {
        if (currentState == State.Active && --activeRemaining <= 0)
        {
            currentState = State.Recovery;
            stateTimer = recoveryDuration;
            advanceStateTimer = false;
        }
        if (advanceStateTimer)
        {
            stateTimer = CombatTimeController.AdvanceTimer(stateTimer);
        }
    }

    private void UpdateApproach(float distance, Vector2 toTarget)
    {
        if (distance >= preferredMinDistance && distance <= attackRange && IsTargetInFront() && CanBeginVisibleAttack())
        {
            StartWindup();
            return;
        }

        if (distance < preferredMinDistance)
        {
            Vector2 retreat = -toTarget.normalized * (moveSpeed * CombatTimeController.StepSeconds);
            MoveSafely(retreat);
            return;
        }

        if (distance > preferredMaxDistance)
        {
            Vector2 advance = toTarget.normalized * (moveSpeed * CombatTimeController.StepSeconds);
            MoveSafely(advance);
        }
    }

    private void UpdateAttackWindup(float distance)
    {
        if (!attackApplied && CombatTimeController.AdvanceTimer(stateTimer) <= 0f)
        {
            attackApplied = true;
            if (combatDefinition != null)
            {
                currentState = State.Active;
                activeRemaining = combatDefinition.activeFrames;
                pendingMeleeHit = true;
                advanceStateTimer = false;
                AttackStarted?.Invoke();
                return;
            }
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
        if (combatDefinition == null) AttackStarted?.Invoke();
        Collider2D[] hits = combatDefinition == null
            ? Physics2D.OverlapCircleAll(transform.position, attackRange)
            : Physics2D.OverlapBoxAll((Vector2)transform.position + new Vector2(facing.x < 0 ? -combatDefinition.hitOffset.x : combatDefinition.hitOffset.x, combatDefinition.hitOffset.y), combatDefinition.hitSize, 0, attackTargetLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D other = hits[i];
            if ((attackTargetLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                continue;
            }

            IDamageReceiver2D receiver = other.GetComponentInParent<IDamageReceiver2D>();
            if (combatDefinition != null && Physics2D.Linecast(transform.position, other.bounds.center, combatDefinition.obstacleLayers).collider != null) continue;
            if (receiver == null || !hitReceivers.Add(((Component)receiver).GetInstanceID()))
            {
                continue;
            }

            if (receiver is IContextDamageReceiver2D contextual)
                contextual.TryReceiveContextHit(attackDamage, transform.position, attackKnockback, new CombatHitContext2D(damageable, attackSequence, combatDefinition == null || combatDefinition.parryable));
            else receiver.TryReceiveHit(attackDamage, transform.position, attackKnockback);
        }
    }

    private bool IsTargetInFront()
    {
        Vector2 toTarget = ((Vector2)target.position - (Vector2)transform.position).normalized;
        return Vector2.Dot(facing, toTarget) > 0.1f;
    }

    private bool CanBeginVisibleAttack()
    {
        if (combatDefinition == null) return true;
        if (Physics2D.Linecast(transform.position, target.position, combatDefinition.obstacleLayers).collider != null) return false;
        if (combatDefinition.allowOffscreenStart) return true;
        Camera camera = Camera.main;
        if (camera == null) return false;
        Vector3 viewport = camera.WorldToViewportPoint(transform.position);
        return viewport.z > 0 && viewport.x >= .04f && viewport.x <= .96f && viewport.y >= 0 && viewport.y <= 1;
    }

    private void MoveSafely(Vector2 displacement)
    {
        if (combatDefinition != null)
        {
            displacement.y = 0;
            if (ownCollider != null && displacement.sqrMagnitude > .000001f)
            {
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = combatDefinition.obstacleLayers, useTriggers = false };
                int count = ownCollider.Cast(displacement.normalized, filter, movementHits, displacement.magnitude);
                float distance = displacement.magnitude;
                for (int i = 0; i < count; i++)
                    if (Vector2.Dot(displacement.normalized, movementHits[i].normal) < -.001f)
                        distance = Mathf.Min(distance, Mathf.Max(0, movementHits[i].distance - .01f));
                displacement = displacement.normalized * distance;
            }
        }
        transform.position += (Vector3)displacement;
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
