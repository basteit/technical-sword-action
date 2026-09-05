using UnityEngine;

using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile2D : MonoBehaviour, ICombatTickListener, ICombatHitListener, ICombatTimerListener
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
    private readonly List<Collider2D> pendingContacts = new List<Collider2D>();
    private Collider2D reflectedPlayerCollider;
    private bool ownedReflectionCollisionIgnore;

    public Transform Owner => owner;
    public bool IsReflected => reflected;
    public int CombatTickOrder => 210;

    private void OnEnable()
    {
        CombatTimeController.Register(this);
    }

    private void OnDisable()
    {
        CombatTimeController.Unregister(this);
        CombatTimeController.ReleaseOwner(this);
        initialized = false;
        lifeTimer = 0f;
        pendingContacts.Clear();
        ClearReflectionCollision();
        reflected = false;
    }

    public void Initialize(
        Vector2 dir,
        float moveSpeed,
        int hitDamage,
        float knockbackForce,
        LayerMask targets,
        LayerMask reflectedTargets,
        Transform projectileOwner)
    {
        ClearReflectionCollision();
        reflected = false;
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

    public void CombatTick()
    {
        if (!initialized)
        {
            return;
        }

        transform.position += (Vector3)(direction * speed * CombatTimeController.StepSeconds);
    }

    public void CombatTickTimers()
    {
        if (!initialized)
        {
            return;
        }

        lifeTimer = CombatTimeController.AdvanceTimer(lifeTimer);
        if (lifeTimer <= 0f)
        {
            Retire();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || (CombatTimeController.IsSuspended && !CombatTimeController.IsExecutingTick))
        {
            return;
        }

        pendingContacts.Add(other);
    }

    public void ResolveCombatHits()
    {
        for (int i = 0; i < pendingContacts.Count && initialized; i++)
        {
            Collider2D other = pendingContacts[i];
            if (other != null && other.isActiveAndEnabled)
            {
                ResolveContact(other);
            }
        }

        pendingContacts.Clear();
    }

    private void ResolveContact(Collider2D other)
    {

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

            Retire();
            return;
        }

        if (other.TryGetComponent(out Damageable2D damageable))
        {
            Vector2 hitDir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            damageable.TakeHit(damage, hitDir);
            Retire();
            return;
        }

        Retire();
    }

    private void Retire()
    {
        // Destroy is deferred until the rendered frame ends. Retire immediately so
        // another combat tick in the same frame cannot move or hit with this projectile.
        gameObject.SetActive(false);
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
            reflectedPlayerCollider = playerCol;
            ownedReflectionCollisionIgnore = !Physics2D.GetIgnoreCollision(ownCol, playerCol);
            if (ownedReflectionCollisionIgnore)
            {
                Physics2D.IgnoreCollision(ownCol, playerCol, true);
            }
        }
    }

    private void ClearReflectionCollision()
    {
        if (ownedReflectionCollisionIgnore && reflectedPlayerCollider != null)
        {
            Collider2D ownCol = GetComponent<Collider2D>();
            if (ownCol != null)
            {
                Physics2D.IgnoreCollision(ownCol, reflectedPlayerCollider, false);
            }
        }

        reflectedPlayerCollider = null;
        ownedReflectionCollisionIgnore = false;
    }
}
