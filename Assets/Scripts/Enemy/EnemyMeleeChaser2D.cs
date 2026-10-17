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
    [SerializeField] private EnemyAttackCoordinator2D attackCoordinator;
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
    private EnemyAttackDefinition currentAttack;
    private int nextAttackIndex;
    private Vector2 configuredHitOffset, configuredHitSize;
    private bool configuredParryable = true;
    private ParryReactionWeight configuredParryReaction = ParryReactionWeight.Light;
    private int configuredActiveFrames = 1;
    public string AttackId => currentAttack != null ? currentAttack.attackId : "legacy-melee";
    public bool PlayerIsInCounterResponse => target != null && target.TryGetComponent<PlayerStateMachine>(out var player) &&
        (player.ActionState == TechnicalSwordAction.PlayerState.PlayerActionState.ParrySuccess ||
         player.ActionState == TechnicalSwordAction.PlayerState.PlayerActionState.ParryCounter ||
         player.ActionState == TechnicalSwordAction.PlayerState.PlayerActionState.OpportunityStrike);
    public bool ResistsHitStun => currentAttack != null && currentAttack.resistHitStunWhileAttacking &&
        (currentState == State.AttackWindup || currentState == State.Active);
    public string AttackRoleReason { get; private set; } = "None";
    public EnemyCombatDefinition Definition => combatDefinition;
    public string AttackKind => combatDefinition == null || configuredParryable ? "Parryable" : "Unparryable";
    public ParryReactionWeight AttackParryReaction => configuredParryReaction;
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
        attackCoordinator?.Release(this);
        target = playerTarget;
        captureHeld = held;
        currentState = State.Idle;
        stateTimer = 0f;
        attackApplied = advanceStateTimer = pendingMeleeHit = false;
        hitReceivers.Clear();
        activeRemaining = 0; nextAttackIndex = 0; currentAttack = null; AttackRoleReason = "Reset";
        ApplyAttackSettings(null);
        facing = target != null ? ((Vector2)(target.position - transform.position)).normalized : Vector2.right;
    }

    public bool BeginCaptureAttack()
    {
        if (damageable == null || !damageable.IsAlive || damageable.IsStunned || target == null) return false;
        captureHeld = false;
        return StartWindup();
    }

    private bool StartWindup()
    {
        if (attackCoordinator != null && !attackCoordinator.TryAcquire(this))
        {
            AttackRoleReason = "WaitingForAttackRole";
            return false;
        }
        AttackRoleReason = attackCoordinator != null ? "AttackRoleAcquired" : "Independent";
        if (combatDefinition != null && combatDefinition.attacks != null && combatDefinition.attacks.Length > 0)
        {
            currentAttack = combatDefinition.attacks[nextAttackIndex % combatDefinition.attacks.Length];
            nextAttackIndex++;
        }
        ApplyAttackSettings(currentAttack);
        currentState = State.AttackWindup;
        stateTimer = attackWindupDuration;
        attackApplied = false;
        attackSequence++;
        hitReceivers.Clear();
        WindupStarted?.Invoke();
        if (currentAttack != null && currentAttack.windupClip != null && TryGetComponent<AudioSource>(out var audio))
            audio.PlayOneShot(currentAttack.windupClip);
        return true;
    }

    public int CombatTickOrder => 200;

    private void OnEnable()
    {
        CombatTimeController.Register(this);
    }

    private void OnDisable()
    {
        attackCoordinator?.Release(this);
        CombatTimeController.Unregister(this);
        CombatTimeController.ReleaseOwner(this);
        currentState = State.Idle;
        stateTimer = 0f;
        attackApplied = false;
        advanceStateTimer = false;
        pendingMeleeHit = false;
        activeRemaining = 0;
    }

    private void Awake()
    {
        damageable = GetComponent<Damageable2D>();
        ownCollider = GetComponent<Collider2D>();
        if (combatDefinition != null)
        {
            if (!combatDefinition.IsValid) Debug.LogError("Invalid enemy combat definition", this);
            ApplyAttackSettings(null);
            preferredMaxDistance = attackRange;
            preferredMinDistance = attackRange * .65f;
            moveSpeed = combatDefinition.moveSpeed;
            attackTargetLayers = combatDefinition.targetLayers;
            damageable.ConfigureDefinition(combatDefinition);
        }
        damageable.Died += OnDied;
        damageable.Parried += OnParried;
        if (disableContactDamageSources)
        {
            DisablePassiveDamageSources();
        }
    }

    private void Start()
    {
        TryFindPlayerTarget();
    }

    private void ApplyAttackSettings(EnemyAttackDefinition data)
    {
        if (combatDefinition == null) return;
        attackWindupDuration = (data != null ? data.windupFrames : combatDefinition.windupFrames) / 60f;
        recoveryDuration = (data != null ? data.recoveryFrames : combatDefinition.recoveryFrames) / 60f;
        cooldownDuration = (data != null ? data.cooldownFrames : combatDefinition.cooldownFrames) / 60f;
        attackDamage = data != null ? data.damage : combatDefinition.damage;
        attackKnockback = data != null ? data.knockback : combatDefinition.knockback;
        attackRange = data != null ? data.attackRange : combatDefinition.attackRange;
        configuredActiveFrames = data != null ? data.activeFrames : combatDefinition.activeFrames;
        configuredHitOffset = data != null ? data.hitOffset : combatDefinition.hitOffset;
        configuredHitSize = data != null ? data.hitSize : combatDefinition.hitSize;
        configuredParryable = data != null ? data.parryable : combatDefinition.parryable;
        configuredParryReaction = data != null ? data.parryReaction : combatDefinition.parryReaction;
    }

    private void InterruptAttack(string reason)
    {
        attackCoordinator?.Release(this);
        AttackRoleReason = reason;
        currentState = State.Recovery; stateTimer = recoveryDuration;
        pendingMeleeHit = false; activeRemaining = 0; advanceStateTimer = false;
    }
    private void OnDied(Damageable2D enemy, Vector2 direction) => InterruptAttack("Dead");
    private void OnParried(Damageable2D enemy, ParryResult result) => InterruptAttack("Parried");
    private void OnDestroy()
    {
        if (damageable == null) return;
        damageable.Died -= OnDied; damageable.Parried -= OnParried;
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
        if (!target.gameObject.activeInHierarchy || (target.TryGetComponent<PlayerStateMachine>(out var playerState) &&
            playerState.LifeState == TechnicalSwordAction.PlayerState.PlayerLifeState.Dead))
        {
            InterruptAttack("TargetUnavailable");
            return;
        }

        if (damageable != null && damageable.IsStunned)
        {
            // Never resume an interrupted windup/active hit after stun ends.
            InterruptAttack("Stunned");
            return;
        }

        Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
        float distance = toTarget.magnitude;

        if (distance > loseTargetRange)
        {
            damageable.SetCombatParticipation(false);
            attackCoordinator?.Release(this);
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
                activeRemaining = configuredActiveFrames;
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
            attackCoordinator?.Release(this);
            AttackRoleReason = "RecoveryComplete";
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
            : Physics2D.OverlapBoxAll((Vector2)transform.position + new Vector2(facing.x < 0 ? -configuredHitOffset.x : configuredHitOffset.x, configuredHitOffset.y), configuredHitSize, 0, attackTargetLayers);
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
                contextual.TryReceiveContextHit(attackDamage, transform.position, attackKnockback,
                    new CombatHitContext2D(damageable, attackSequence, combatDefinition == null || configuredParryable, configuredParryReaction));
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
