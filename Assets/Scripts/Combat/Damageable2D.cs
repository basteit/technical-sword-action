using UnityEngine;

public class Damageable2D : MonoBehaviour
{
    [SerializeField] private int maxHp = 5;
    [SerializeField] private float knockbackPower = 3f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.7f, 0.7f, 1f);
    [SerializeField] private float hitFlashDuration = 0.08f;

    private int currentHp;
    private Rigidbody2D rb;
    private float flashTimer;
    private Color defaultColor = Color.white;

    private void Awake()
    {
        currentHp = maxHp;
        rb = GetComponent<Rigidbody2D>();

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
    }

    public void TakeHit(int damage, Vector2 direction)
    {
        currentHp = Mathf.Max(0, currentHp - damage);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(direction.normalized * knockbackPower, ForceMode2D.Impulse);
        }

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
}
