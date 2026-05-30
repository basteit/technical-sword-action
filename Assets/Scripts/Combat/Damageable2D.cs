using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Damageable2D : MonoBehaviour
{
    private enum EnemyToughnessPreset
    {
        MeleeMob,
        RangedMob,
        Boss
    }

    [SerializeField] private int maxHp = 5;
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

    public bool IsStunned => stunTimer > 0f;
    public float CurrentBreak => currentBreak;
    public float BreakThreshold => breakThreshold;
    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public float HpNormalized => maxHp > 0 ? (float)currentHp / maxHp : 0f;

    private void Awake()
    {
        if (applyPresetOnAwake)
        {
            ApplyPresetValues();
        }

        currentHp = maxHp;

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

    private void Update()
    {
        if (breakResistTimer > 0f)
        {
            breakResistTimer -= Time.deltaTime;
        }

        if (stunTimer <= 0f && currentBreak > 0f)
        {
            currentBreak = Mathf.Max(0f, currentBreak - breakDecayPerSecond * Time.deltaTime);
        }

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
        }

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && spriteRenderer != null)
            {
                spriteRenderer.color = defaultColor;
            }
        }

        if (knockbackVelocity.sqrMagnitude > 0.0001f)
        {
            transform.position += (Vector3)(knockbackVelocity * Time.deltaTime);
            knockbackVelocity = Vector2.Lerp(knockbackVelocity, Vector2.zero, knockbackDamping * Time.deltaTime);
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
        currentHp = Mathf.Max(0, currentHp - damage);
        AddBreak(damage * hitBreakMultiplier);

        Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        knockbackVelocity = dir * knockbackPower;

        if (audioSource != null && hitClip != null)
        {
            audioSource.PlayOneShot(hitClip, 0.9f);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = hitFlashColor;
            flashTimer = hitFlashDuration;
        }

        CombatCameraFeedback2D.PlayHitShake();

        if (currentHp <= 0)
        {
            gameObject.SetActive(false);
        }
    }

    public void ApplyParryStun(ParryResult result)
    {
        if (result == ParryResult.Just)
        {
            stunTimer = justParryStunDuration;
            AddBreak(parryJustBreakGain);
        }
        else if (result == ParryResult.Normal)
        {
            stunTimer = normalParryStunDuration;
            AddBreak(parryNormalBreakGain);
        }
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

