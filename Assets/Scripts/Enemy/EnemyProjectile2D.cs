using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile2D : MonoBehaviour
{
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private float reflectedSpeedMultiplier = 1.15f;

    private Vector2 direction;
    private float speed;
    private int damage;
    private float knockback;
    private LayerMask targetLayers;
    private LayerMask reflectedTargetLayers;
    private Transform owner;

    private float lifeTimer;
    private bool initialized;
    private bool reflected;

    public Transform Owner => owner;
    public bool IsReflected => reflected;

    public void Initialize(
        Vector2 dir,
        float moveSpeed,
        int hitDamage,
        float knockbackForce,
        LayerMask targets,
        LayerMask reflectedTargets,
        Transform projectileOwner)
    {
        direction = dir.normalized;
        speed = moveSpeed;
        damage = hitDamage;
        knockback = knockbackForce;
        targetLayers = targets;
        reflectedTargetLayers = reflectedTargets;
        owner = projectileOwner;
        lifeTimer = lifeTime;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        transform.position += (Vector3)(direction * speed * Time.deltaTime);

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized)
        {
            return;
        }

        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        if (other.TryGetComponent(out IDamageReceiver2D receiver))
        {
            bool applied = receiver.TryReceiveHit(damage, transform.position, knockback);
            if (!applied && !reflected && other.TryGetComponent(out PlayerDamageReceiver2D playerReceiver) && playerReceiver.LastParryResult != ParryResult.None)
            {
                ReflectFromParry(other.transform);
                return;
            }

            Destroy(gameObject);
            return;
        }

        if (other.TryGetComponent(out Damageable2D damageable))
        {
            Vector2 hitDir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            damageable.TakeHit(damage, hitDir);
            Destroy(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private void ReflectFromParry(Transform player)
    {
        reflected = true;

        if (owner != null)
        {
            direction = ((Vector2)owner.position - (Vector2)transform.position).normalized;
        }
        else
        {
            direction = -direction;
        }

        speed *= reflectedSpeedMultiplier;
        targetLayers = reflectedTargetLayers;

        Collider2D playerCol = player != null ? player.GetComponent<Collider2D>() : null;
        Collider2D ownCol = GetComponent<Collider2D>();
        if (playerCol != null && ownCol != null)
        {
            Physics2D.IgnoreCollision(ownCol, playerCol, true);
        }
    }
}
