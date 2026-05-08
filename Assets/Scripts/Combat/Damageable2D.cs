using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Damageable2D : MonoBehaviour
{
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

    private int currentHp;
    private float flashTimer;
    private Color defaultColor = Color.white;
    private Vector2 knockbackVelocity;

    private void Awake()
    {
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

        if (currentHp <= 0)
        {
            gameObject.SetActive(false);
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
