using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerDamageReceiver2D : MonoBehaviour, IDamageReceiver2D
{
    [Header("Health")]
    [SerializeField] private int maxHp = 5;

    [Header("Invincibility")]
    [SerializeField] private float invincibleDuration = 0.45f;

    [Header("Hit Reaction")]
    [SerializeField] private float hitStopMoveDuration = 0.16f;
    [SerializeField] private float minKnockbackForce = 5.5f;

    [Header("Parry Feedback")]
    [SerializeField] private float normalParryHitStop = 0.035f;
    [SerializeField] private float justParryHitStop = 0.06f;

    [Header("Collision Ghost During Invincible")]
    [SerializeField] private LayerMask ignoreCollisionLayersWhileInvincible;

    [Header("References")]
    [SerializeField] private PlayerParry2D parry;
    [SerializeField] private PlayerSpecialGauge specialGauge;

    [Header("Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.45f, 0.45f, 1f);
    [SerializeField] private float hitFlashDuration = 0.08f;

    private Rigidbody2D rb;
    private Collider2D ownCollider;
    private PlayerMotor2D motor;

    private int currentHp;
    private float invincibleTimer;
    private float hitLockTimer;
    private float flashTimer;
    private float hitStopTimer;
    private Color defaultColor = Color.white;
    private readonly HashSet<Collider2D> ignoredColliders = new();

    public bool IsInvincible => invincibleTimer > 0f;
    public bool IsHitLocked => hitLockTimer > 0f;
    public int CurrentHp => currentHp;
    public ParryResult LastParryResult { get; private set; } = ParryResult.None;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ownCollider = GetComponent<Collider2D>();
        motor = GetComponent<PlayerMotor2D>();
        currentHp = maxHp;

        if (parry == null)
        {
            parry = GetComponent<PlayerParry2D>();
        }

        if (specialGauge == null)
        {
            specialGauge = GetComponent<PlayerSpecialGauge>();
        }

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
        if (hitStopTimer > 0f)
        {
            hitStopTimer -= Time.unscaledDeltaTime;
            if (hitStopTimer <= 0f)
            {
                Time.timeScale = 1f;
            }
        }

        if (invincibleTimer > 0f)
        {
            invincibleTimer -= Time.deltaTime;
            if (invincibleTimer <= 0f)
            {
                RestoreIgnoredCollisions();
            }
        }

        if (hitLockTimer > 0f)
        {
            hitLockTimer -= Time.deltaTime;
        }

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && spriteRenderer != null)
            {
                spriteRenderer.color = defaultColor;
            }
        }
    }

    public bool TryReceiveHit(int damage, Vector2 sourcePosition, float knockbackForce)
    {
        LastParryResult = ParryResult.None;

        if (parry != null && parry.TryResolveParry(out ParryResult parryResult))
        {
            LastParryResult = parryResult;
            ApplyParryEffects(sourcePosition, parryResult);

            if (specialGauge != null)
            {
                specialGauge.AddOnParry(parryResult);
            }

            return false;
        }

        if (IsInvincible)
        {
            return false;
        }

        if (motor != null && motor.IsDashing)
        {
            return false;
        }

        currentHp = Mathf.Max(0, currentHp - damage);
        invincibleTimer = invincibleDuration;
        hitLockTimer = hitStopMoveDuration;

        if (specialGauge != null)
        {
            specialGauge.AddOnDamaged();
        }

        Vector2 direction = ((Vector2)transform.position - sourcePosition).normalized;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector2.right;
        }

        float finalKnockback = Mathf.Max(knockbackForce, minKnockbackForce);
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * finalKnockback, ForceMode2D.Impulse);

        if (audioSource != null && hitClip != null)
        {
            audioSource.PlayOneShot(hitClip, 1f);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = hitFlashColor;
            flashTimer = hitFlashDuration;
        }

        IgnoreCurrentOverlaps();

        if (currentHp <= 0)
        {
            gameObject.SetActive(false);
        }

        return true;
    }

    private void ApplyParryEffects(Vector2 sourcePosition, ParryResult result)
    {
        EnemyProjectile2D projectile = FindClosestProjectile(sourcePosition, 1.2f);
        if (projectile != null && projectile.Owner != null)
        {
            if (projectile.Owner.TryGetComponent(out Damageable2D ownerDamageable))
            {
                ownerDamageable.ApplyParryStun(result);
            }
        }
        else
        {
            float radius = 1.8f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(sourcePosition, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].TryGetComponent(out Damageable2D damageable))
                {
                    damageable.ApplyParryStun(result);
                }
            }
        }

        float stop = result == ParryResult.Just ? justParryHitStop : normalParryHitStop;
        if (stop > 0f)
        {
            Time.timeScale = 0f;
            hitStopTimer = stop;
        }
    }

    private EnemyProjectile2D FindClosestProjectile(Vector2 center, float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        float bestDist = float.MaxValue;
        EnemyProjectile2D best = null;

        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].TryGetComponent(out EnemyProjectile2D p))
            {
                continue;
            }

            float d = Vector2.Distance(center, p.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = p;
            }
        }

        return best;
    }

    private void IgnoreCurrentOverlaps()
    {
        if (ownCollider == null)
        {
            return;
        }

        Collider2D[] all = Physics2D.OverlapCircleAll(transform.position, 1.5f);
        for (int i = 0; i < all.Length; i++)
        {
            Collider2D other = all[i];
            if (other == null || other == ownCollider)
            {
                continue;
            }

            if ((ignoreCollisionLayersWhileInvincible.value & (1 << other.gameObject.layer)) == 0)
            {
                continue;
            }

            Physics2D.IgnoreCollision(ownCollider, other, true);
            ignoredColliders.Add(other);
        }
    }

    private void RestoreIgnoredCollisions()
    {
        if (ownCollider == null)
        {
            ignoredColliders.Clear();
            return;
        }

        foreach (Collider2D c in ignoredColliders)
        {
            if (c != null)
            {
                Physics2D.IgnoreCollision(ownCollider, c, false);
            }
        }

        ignoredColliders.Clear();
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        RestoreIgnoredCollisions();
    }
}

