using UnityEngine;

public class Damageable2D : MonoBehaviour
{
    [SerializeField] private int maxHp = 5;
    [SerializeField] private float knockbackPower = 3f;

    private int currentHp;
    private Rigidbody2D rb;

    private void Awake()
    {
        currentHp = maxHp;
        rb = GetComponent<Rigidbody2D>();
    }

    public void TakeHit(int damage, Vector2 direction)
    {
        currentHp = Mathf.Max(0, currentHp - damage);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(direction.normalized * knockbackPower, ForceMode2D.Impulse);
        }

        if (currentHp <= 0)
        {
            gameObject.SetActive(false);
        }
    }
}
