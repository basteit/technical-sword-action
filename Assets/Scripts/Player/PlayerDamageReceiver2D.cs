using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDamageReceiver2D : MonoBehaviour, IDamageReceiver2D
{
    [Header("Health")]
    [SerializeField] private int maxHp = 5;

    [Header("Invincibility")]
    [SerializeField] private float invincibleDuration = 0.45f;

    [Header("Hit Reaction")]
    [SerializeField] private float hitStopMoveDuration = 0.1f;

    [Header("Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.45f, 0.45f, 1f);
    [SerializeField] private float hitFlashDuration = 0.08f;

    private Rigidbody2D rb;
    private int currentHp;
    private float invincibleTimer;
    private float hitLockTimer;
    private float flashTimer;
    private Color defaultColor = Color.white;

    public bool IsInvincible => invincibleTimer > 0f;
    public bool IsHitLocked => hitLockTimer > 0f;
    public int CurrentHp => currentHp;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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
        if (invincibleTimer > 0f)
        {
            invincibleTimer -= Time.deltaTime;
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
        if (IsInvincible)
        {
            return false;
        }

        currentHp = Mathf.Max(0, currentHp - damage);
        invincibleTimer = invincibleDuration;
        hitLockTimer = hitStopMoveDuration;

        Vector2 direction = ((Vector2)transform.position - sourcePosition).normalized;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector2.right;
        }

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);

        if (audioSource != null && hitClip != null)
        {
            audioSource.PlayOneShot(hitClip, 1f);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = hitFlashColor;
            flashTimer = hitFlashDuration;
        }

        if (currentHp <= 0)
        {
            gameObject.SetActive(false);
        }

        return true;
    }
}
