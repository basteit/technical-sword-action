using UnityEngine;

[RequireComponent(typeof(Damageable2D))]
public class EnemyBossController2D : MonoBehaviour, ICombatTickListener, ICombatHitListener, ICombatTimerListener
{
    private enum BossAction
    {
        MeleeArc,
        TripleShot,
        ChargeStrike
    }

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float detectRange = 14f;
    [SerializeField] private float moveSpeed = 2.3f;
    [SerializeField] private float preferredMinDistance = 2.6f;
    [SerializeField] private float preferredMaxDistance = 5.8f;

    [Header("Phase")]
    [SerializeField] private float phase2Threshold = 0.5f;
    [SerializeField] private float phase2ActionIntervalMultiplier = 0.8f;
    [SerializeField] private float phase2DamageMultiplier = 1.2f;

    [Header("Melee Arc")]
    [SerializeField] private float meleeRange = 1.8f;
    [SerializeField] private int meleeDamage = 1;
    [SerializeField] private float meleeKnockback = 5f;
    [SerializeField] private LayerMask meleeTargetLayers;

    [Header("Triple Shot")]
    [SerializeField] private EnemyProjectile2D projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float projectileSpeed = 8.5f;
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private float projectileKnockback = 4.5f;
    [SerializeField] private float spreadAngle = 18f;
    [SerializeField] private LayerMask projectileTargetLayers;
    [SerializeField] private LayerMask reflectedHitLayers;

    [Header("Charge Strike")]
    [SerializeField] private float chargeDuration = 0.3f;
    [SerializeField] private float chargeSpeed = 8.5f;
    [SerializeField] private float chargeHitRadius = 1.45f;
    [SerializeField] private int chargeDamage = 2;
    [SerializeField] private float chargeKnockback = 7f;

    [Header("Timings")]
    [SerializeField] private float actionInterval = 1.05f;
    [SerializeField] private float postActionRecovery = 0.5f;

    private Damageable2D damageable;
    private BossAction nextAction = BossAction.MeleeArc;
    private float actionTimer;
    private float chargeTimer;
    private bool phase2;
    private bool isCharging;
    private Vector2 chargeDir;
    private bool advanceActionTimer;
    private bool advanceChargeTimer;
    private bool pendingMeleeHit;
    private bool pendingChargeHit;

    public int CombatTickOrder => 200;

    private void OnEnable()
    {
        actionTimer = actionInterval;
        CombatTimeController.Register(this);
    }

    private void OnDisable()
    {
        CombatTimeController.Unregister(this);
        CombatTimeController.ReleaseOwner(this);
        actionTimer = 0f;
        chargeTimer = 0f;
        isCharging = false;
        nextAction = BossAction.MeleeArc;
        advanceActionTimer = false;
        advanceChargeTimer = false;
        pendingMeleeHit = false;
        pendingChargeHit = false;
    }

    private void Awake()
    {
        damageable = GetComponent<Damageable2D>();
        actionTimer = actionInterval;
        DisablePassiveDamageSources();
    }

    private void Start()
    {
        TryFindPlayerTarget();
    }

    public void CombatTick()
    {
        advanceActionTimer = false;
        advanceChargeTimer = false;
        pendingMeleeHit = false;
        pendingChargeHit = false;
        if (target == null)
        {
            TryFindPlayerTarget();
            return;
        }

        if (damageable != null && damageable.IsStunned)
        {
            return;
        }

        if (!phase2 && damageable != null && damageable.HpNormalized <= phase2Threshold)
        {
            phase2 = true;
        }

        if (isCharging)
        {
            advanceChargeTimer = true;
            UpdateCharge();
            return;
        }

        float dist = Vector2.Distance(transform.position, target.position);
        if (dist > detectRange)
        {
            return;
        }

        UpdateSpacing(dist);

        advanceActionTimer = true;
        if (CombatTimeController.AdvanceTimer(actionTimer) > 0f)
        {
            return;
        }

        ExecuteAction(nextAction);
        nextAction = (BossAction)(((int)nextAction + 1) % 3);
        float interval = actionInterval + postActionRecovery;
        if (phase2)
        {
            interval *= phase2ActionIntervalMultiplier;
        }

        actionTimer = interval;
        advanceActionTimer = false;
    }

    public void ResolveCombatHits()
    {
        if (pendingMeleeHit)
        {
            pendingMeleeHit = false;
            DoMeleeArc();
        }

        if (pendingChargeHit)
        {
            pendingChargeHit = false;
            ResolveChargeHits();
        }
    }

    public void CombatTickTimers()
    {
        if (advanceActionTimer)
        {
            actionTimer = CombatTimeController.AdvanceTimer(actionTimer);
        }

        if (advanceChargeTimer)
        {
            chargeTimer = CombatTimeController.AdvanceTimer(chargeTimer);
            if (chargeTimer <= 0f)
            {
                isCharging = false;
            }
        }
    }

    private void UpdateSpacing(float dist)
    {
        Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
        if (toTarget.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector2 dir = toTarget.normalized;
        if (dist < preferredMinDistance)
        {
            transform.position += (Vector3)(-dir * moveSpeed * CombatTimeController.StepSeconds);
        }
        else if (dist > preferredMaxDistance)
        {
            transform.position += (Vector3)(dir * moveSpeed * CombatTimeController.StepSeconds);
        }
    }

    private void ExecuteAction(BossAction action)
    {
        switch (action)
        {
            case BossAction.MeleeArc:
                pendingMeleeHit = true;
                break;
            case BossAction.TripleShot:
                DoTripleShot();
                break;
            case BossAction.ChargeStrike:
                StartChargeStrike();
                break;
        }
    }

    private void DoMeleeArc()
    {
        int damage = ScaleDamage(meleeDamage);
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, meleeRange, meleeTargetLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].TryGetComponent(out IDamageReceiver2D receiver))
            {
                continue;
            }

            receiver.TryReceiveHit(damage, transform.position, meleeKnockback);
        }
    }

    private void DoTripleShot()
    {
        if (projectilePrefab == null || muzzle == null)
        {
            return;
        }

        Vector2 baseDir = ((Vector2)target.position - (Vector2)muzzle.position).normalized;
        if (baseDir.sqrMagnitude < 0.001f)
        {
            baseDir = Vector2.right;
        }

        FireShot(baseDir, 0f);
        FireShot(baseDir, spreadAngle);
        FireShot(baseDir, -spreadAngle);
    }

    private void FireShot(Vector2 baseDir, float angleOffset)
    {
        Vector2 dir = Quaternion.Euler(0f, 0f, angleOffset) * baseDir;
        EnemyProjectile2D proj = Instantiate(projectilePrefab, muzzle.position, Quaternion.identity);
        proj.Initialize(dir, projectileSpeed, ScaleDamage(projectileDamage), projectileKnockback, projectileTargetLayers, reflectedHitLayers, transform);
    }

    private void StartChargeStrike()
    {
        Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
        if (toTarget.sqrMagnitude < 0.001f)
        {
            toTarget = Vector2.right;
        }

        chargeDir = toTarget.normalized;
        chargeTimer = chargeDuration;
        isCharging = true;
    }

    private void UpdateCharge()
    {
        transform.position += (Vector3)(chargeDir * chargeSpeed * CombatTimeController.StepSeconds);
        pendingChargeHit = true;
    }

    private void ResolveChargeHits()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, chargeHitRadius, meleeTargetLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].TryGetComponent(out IDamageReceiver2D receiver))
            {
                continue;
            }

            receiver.TryReceiveHit(ScaleDamage(chargeDamage), transform.position, chargeKnockback);
        }
    }

    private int ScaleDamage(int baseDamage)
    {
        if (!phase2)
        {
            return baseDamage;
        }

        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * phase2DamageMultiplier));
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
}
