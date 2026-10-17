using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Damageable2D : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    private enum EnemyToughnessPreset
    {
        MeleeMob,
        RangedMob,
        Boss
    }

    [SerializeField] private int maxHp = 5;
    [SerializeField] private string individualId;
    [SerializeField] private EnemyCombatRank combatRank;
    [SerializeField] private EnemyCombatDefinition combatDefinition;
    [SerializeField] private bool overrideMaxHp;
    [SerializeField, Min(1)] private int instanceMaxHp = 30;
    [SerializeField] private bool overrideCounterMultiplier;
    [SerializeField, Min(.01f)] private float instanceCounterMultiplier = 3f;
    [SerializeField, Min(0f)] private float deathDisplayDuration;
    [SerializeField, Min(0f)] private float lightHitStunDuration;
    [SerializeField] private bool blockContactPushFromPlayer = true;
    [SerializeField] private LayerMask playerBodyLayers;
    [SerializeField] private float knockbackPower = 5.5f;
    [SerializeField] private float knockbackDamping = 18f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.7f, 0.7f, 1f);
    [SerializeField] private float hitFlashDuration = 0.08f;

    [Header("Parry Stun")]
    [SerializeField] private float normalParryStunDuration = 0.22f;
    [SerializeField] private float justParryStunDuration = 0.34f;

    [Header("Break / Stagger")]
    [SerializeField] private EnemyToughnessPreset toughnessPreset = EnemyToughnessPreset.MeleeMob;
    [SerializeField] private bool applyPresetOnAwake = true;
    [SerializeField] private float breakThreshold = 100f;
    [SerializeField] private float breakDecayPerSecond = 20f;
    [SerializeField] private float hitBreakMultiplier = 18f;
    [SerializeField] private float parryNormalBreakGain = 32f;
    [SerializeField] private float parryJustBreakGain = 48f;
    [SerializeField] private float breakStunDuration = 0.35f;
    [SerializeField] private float breakResistAfterStun = 0.18f;

    private int currentHp;
    private float flashTimer;
    private Color defaultColor = Color.white;
    private Vector2 knockbackVelocity;
    private float stunTimer;
    private float currentBreak;
    private float breakResistTimer;
    private float deathTimer;
    private Collider2D[] bodyColliders;
    private bool[] initialColliderStates;
    private readonly Dictionary<object,float> stunHolds=new();
    private readonly List<object> stunHoldOwners=new();
    private bool participatingInCombat = true;
    public bool ParticipatingInCombat => IsAlive && participatingInCombat;
    public EnemyCombatRank CombatRank => combatRank;
    public EnemyCombatDefinition Definition => combatDefinition;
    public int ConfiguredMaxHp => overrideMaxHp ? Mathf.Max(1, instanceMaxHp) : combatDefinition != null ? combatDefinition.maxHp : maxHp;
    public float CounterMultiplier => overrideCounterMultiplier ? Mathf.Max(.01f, instanceCounterMultiplier) :
        combatDefinition != null ? combatDefinition.CounterMultiplier : EnemyCombatDefinition.DefaultCounterMultiplier(combatRank);
    public string LastHitReaction { get; private set; } = "None";
    private float lightResistanceTimer;
    private CombatFeedback2D feedbackOwner;
    private bool HasExternalFeedback => feedbackOwner != null && feedbackOwner.IsReady;
    public void SetFeedbackOwner(CombatFeedback2D owner) => feedbackOwner = owner;
    public void ReleaseFeedbackOwner(CombatFeedback2D owner) { if (feedbackOwner == owner) feedbackOwner = null; }
    public int LastDamageTaken { get; private set; }
    public event Action<Damageable2D> LeftCombat;
    public void SetCombatParticipation(bool participating)
    {
        if (participatingInCombat && !participating) LeftCombat?.Invoke(this);
        participatingInCombat = participating;
    }
    public void ConfigureCombat(EnemyCombatRank rank, int hp)
    {
        combatDefinition = null;
        combatRank = rank;
        maxHp = Mathf.Max(1, hp);
        currentHp = maxHp;
        participatingInCombat = true;
    }
    public void ConfigureDefinition(EnemyCombatDefinition definition)
    {
        if (definition == null) return;
        combatDefinition = definition;
        combatRank = definition.rank;
        maxHp = ConfiguredMaxHp;
        currentHp = maxHp;
        participatingInCombat = true;
    }
    public void ConfigureInstanceOverrides(int hp, float multiplier)
    {
        overrideMaxHp = hp > 0; instanceMaxHp = Mathf.Max(1, hp);
        overrideCounterMultiplier = float.IsFinite(multiplier) && multiplier > 0;
        instanceCounterMultiplier = overrideCounterMultiplier ? Mathf.Max(.01f, multiplier) : 3f;
        maxHp = ConfiguredMaxHp; currentHp = maxHp;
    }

    public string IndividualId => string.IsNullOrEmpty(individualId) ? GetInstanceID().ToString() : individualId;
    public bool IsAlive => isActiveAndEnabled && currentHp > 0;
    public float StunRemaining { get { float n=Mathf.Max(0f,stunTimer);foreach(float value in stunHolds.Values) n=Mathf.Max(n,value);return n; } }
    public int HitCount { get; private set; }
    public int DeathCount { get; private set; }
    public event Action<Damageable2D, int, Vector2> HitReceived;
    public event Action<Damageable2D, ParryResult> Parried;
    public event Action<Damageable2D, Vector2> Died;
    public event Action<Damageable2D> ResetPerformed;

    public bool IsStunned => StunRemaining > 0f;
    public float CurrentBreak => currentBreak;
    public float BreakThreshold => breakThreshold;
    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public float HpNormalized => maxHp > 0 ? (float)currentHp / maxHp : 0f;
    public int CombatTickOrder => 190;

    private void OnEnable()
    {
        CombatTimeController.Register(this);
    }

    private void OnDisable()
    {
        SetCombatParticipation(false);
        CombatTimeController.Unregister(this);
        CombatTimeController.ReleaseOwner(this);
        flashTimer = 0f;
        stunTimer = 0f;
        breakResistTimer = 0f;
        currentBreak = 0f;
        lightResistanceTimer = 0;
        stunHolds.Clear();
        knockbackVelocity = Vector2.zero;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = defaultColor;
        }
    }

    private void Awake()
    {
        if (applyPresetOnAwake)
        {
            ApplyPresetValues();
        }

        if (combatDefinition != null) ConfigureDefinition(combatDefinition);
        currentHp = maxHp;
        bodyColliders = GetComponentsInChildren<Collider2D>(true);
        initialColliderStates = new bool[bodyColliders.Length];
        for (int i = 0; i < bodyColliders.Length; i++) initialColliderStates[i] = bodyColliders[i].enabled;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            defaultColor = spriteRenderer.color;
        }
    }

    public void CombatTick()
    {
        if (knockbackVelocity.sqrMagnitude > 0.0001f)
        {
            transform.position += (Vector3)(knockbackVelocity * CombatTimeController.StepSeconds);
        }
    }

    public void CombatTickTimers()
    {
        lightResistanceTimer = CombatTimeController.AdvanceTimer(lightResistanceTimer);
        stunHoldOwners.Clear();stunHoldOwners.AddRange(stunHolds.Keys);
        foreach(object owner in stunHoldOwners)
        { float value=CombatTimeController.AdvanceTimer(stunHolds[owner]);if(value<=0) stunHolds.Remove(owner);else stunHolds[owner]=value; }
        if (currentHp <= 0)
        {
            deathTimer = CombatTimeController.AdvanceTimer(deathTimer);
            if (deathTimer <= 0f) gameObject.SetActive(false);
            return;
        }
        if (breakResistTimer > 0f)
        {
            breakResistTimer = CombatTimeController.AdvanceTimer(breakResistTimer);
        }

        if (stunTimer <= 0f && currentBreak > 0f)
        {
            currentBreak = Mathf.Max(0f, currentBreak - breakDecayPerSecond * CombatTimeController.StepSeconds);
        }

        if (stunTimer > 0f)
        {
            stunTimer = CombatTimeController.AdvanceTimer(stunTimer);
        }

        if (flashTimer > 0f)
        {
            flashTimer = CombatTimeController.AdvanceTimer(flashTimer);
            if (flashTimer <= 0f && spriteRenderer != null)
            {
                spriteRenderer.color = defaultColor;
            }
        }

        if (knockbackVelocity.sqrMagnitude > 0.0001f)
        {
            knockbackVelocity = Vector2.Lerp(knockbackVelocity, Vector2.zero, knockbackDamping * CombatTimeController.StepSeconds);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryIgnorePlayerBodyCollision(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryIgnorePlayerBodyCollision(collision.collider);
    }

    public void TakeHit(int damage, Vector2 direction)
    {
        TryTakeHit(damage, direction);
    }

    public bool TryTakeHit(int damage, Vector2 direction)
    {
        return TryTakeCombatHit(damage, direction, EnemyHitStrength.Light);
    }

    public bool TryTakeCombatHit(int damage, Vector2 direction, EnemyHitStrength strength)
    {
        if (damage <= 0 || !isActiveAndEnabled || currentHp <= 0 ||
            (CombatTimeController.IsSuspended && !CombatTimeController.IsExecutingTick))
        {
            return false;
        }

        LastDamageTaken = Mathf.Min(currentHp, damage);
        currentHp = Mathf.Max(0, currentHp - damage);
        HitCount++;
        if (combatDefinition == null)
        {
            AddBreak(damage * hitBreakMultiplier);
            stunTimer = Mathf.Max(stunTimer, lightHitStunDuration);
        }
        else
        {
            bool armored = TryGetComponent<EnemyMeleeChaser2D>(out var melee) && melee.ResistsHitStun;
            int frames = strength == EnemyHitStrength.Strong ? combatDefinition.strongHitStunFrames : combatDefinition.lightHitStunFrames;
            bool resistsLight = strength == EnemyHitStrength.Light && lightResistanceTimer > 0;
            if (!armored && !resistsLight && frames > 0)
            {
                stunTimer = Mathf.Max(stunTimer, frames / 60f);
                lightResistanceTimer = (frames + combatDefinition.lightStunResistanceFrames) / 60f;
                LastHitReaction = strength + "Stun";
            }
            else LastHitReaction = armored ? "AttackArmor" : resistsLight ? "LightResistance" : "NoStun";
        }

        Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        knockbackVelocity = dir * (combatDefinition != null ? combatDefinition.hitKnockback : knockbackPower);

        if (!HasExternalFeedback && audioSource != null && hitClip != null)
        {
            audioSource.PlayOneShot(hitClip, 0.9f);
        }

        if (!HasExternalFeedback && spriteRenderer != null)
        {
            spriteRenderer.color = hitFlashColor;
            flashTimer = hitFlashDuration;
        }

        if (!HasExternalFeedback) CombatCameraFeedback2D.PlayHitShake();
        HitReceived?.Invoke(this, LastDamageTaken, dir);

        if (currentHp <= 0)
        {
            DeathCount++;
            deathTimer = deathDisplayDuration;
            foreach (Collider2D body in bodyColliders) if (body != null) body.enabled = false;
            Died?.Invoke(this, dir);
            if (deathTimer <= 0f) gameObject.SetActive(false);
        }
        return true;
    }

    public void ApplyParryStun(ParryResult result)
    {
        if (!isActiveAndEnabled || currentHp <= 0 ||
            (CombatTimeController.IsSuspended && !CombatTimeController.IsExecutingTick))
        {
            return;
        }

        if (result == ParryResult.Just)
        {
            stunTimer = combatDefinition != null ? combatDefinition.justParryStunFrames / 60f : justParryStunDuration;
            if (combatDefinition == null) AddBreak(parryJustBreakGain);
        }
        else if (result == ParryResult.Normal)
        {
            stunTimer = combatDefinition != null ? combatDefinition.normalParryStunFrames / 60f : normalParryStunDuration;
            if (combatDefinition == null) AddBreak(parryNormalBreakGain);
        }
        if (result != ParryResult.None) Parried?.Invoke(this, result);
    }

    public void ExtendStun(float combatSeconds)
    {
        if (IsAlive) stunTimer = Mathf.Max(stunTimer, combatSeconds);
    }
    public void AcquireStun(object owner,float combatSeconds)
    { if(owner!=null&&IsAlive) stunHolds[owner]=Mathf.Max(0,combatSeconds); }
    public void ReleaseStun(object owner) { if(owner!=null) stunHolds.Remove(owner); }

    public void ResetForCapture(Vector3 position, int hp)
    {
        gameObject.SetActive(true);
        transform.position = position;
        maxHp = Mathf.Max(1, hp);
        currentHp = maxHp;
        participatingInCombat = true;
        LastDamageTaken = 0;
        flashTimer = stunTimer = deathTimer = currentBreak = breakResistTimer = 0f;
        lightResistanceTimer = 0; LastHitReaction = "None";
        stunHolds.Clear();
        HitCount = DeathCount = 0;
        knockbackVelocity = Vector2.zero;
        if (spriteRenderer != null) spriteRenderer.color = defaultColor;
        if (bodyColliders != null)
            for (int i = 0; i < bodyColliders.Length; i++) if (bodyColliders[i] != null) bodyColliders[i].enabled = initialColliderStates[i];
        if (TryGetComponent(out Rigidbody2D body)) { body.linearVelocity = Vector2.zero; body.angularVelocity = 0; }
        CombatTimeController.ReleaseOwner(this);
        ResetPerformed?.Invoke(this);
    }

    private void AddBreak(float value)
    {
        if (value <= 0f || breakThreshold <= 0f || breakResistTimer > 0f)
        {
            return;
        }

        currentBreak = Mathf.Min(breakThreshold, currentBreak + value);
        if (currentBreak < breakThreshold)
        {
            return;
        }

        currentBreak = 0f;
        stunTimer = Mathf.Max(stunTimer, breakStunDuration);
        breakResistTimer = breakResistAfterStun;
    }

    private void ApplyPresetValues()
    {
        switch (toughnessPreset)
        {
            case EnemyToughnessPreset.MeleeMob:
                breakThreshold = 90f;
                breakDecayPerSecond = 20f;
                hitBreakMultiplier = 18f;
                parryNormalBreakGain = 32f;
                parryJustBreakGain = 48f;
                breakStunDuration = 0.35f;
                breakResistAfterStun = 0.16f;
                break;
            case EnemyToughnessPreset.RangedMob:
                breakThreshold = 72f;
                breakDecayPerSecond = 24f;
                hitBreakMultiplier = 20f;
                parryNormalBreakGain = 34f;
                parryJustBreakGain = 52f;
                breakStunDuration = 0.4f;
                breakResistAfterStun = 0.14f;
                break;
            case EnemyToughnessPreset.Boss:
                breakThreshold = 180f;
                breakDecayPerSecond = 14f;
                hitBreakMultiplier = 10f;
                parryNormalBreakGain = 22f;
                parryJustBreakGain = 34f;
                breakStunDuration = 0.28f;
                breakResistAfterStun = 0.22f;
                break;
        }
    }

    private void TryIgnorePlayerBodyCollision(Collider2D other)
    {
        if (!blockContactPushFromPlayer)
        {
            return;
        }

        if (other == null)
        {
            return;
        }

        if ((playerBodyLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        Collider2D own = GetComponent<Collider2D>();
        if (own != null)
        {
            Physics2D.IgnoreCollision(own, other, true);
        }
    }
}

