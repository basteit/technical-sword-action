using UnityEngine;

public class EnemyRangedShooter2D : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float detectRange = 12f;
    [SerializeField] private float loseTargetRange = 16f;

    [Header("Spacing")]
    [SerializeField] private float moveSpeed = 2.6f;
    [SerializeField] private float preferredMinDistance = 4.5f;
    [SerializeField] private float preferredMaxDistance = 7.2f;
    [SerializeField] private float nearStopDistance = 0.75f;

    [Header("Shoot")]
    [SerializeField] private EnemyProjectile2D projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float shootInterval = 1.2f;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private float projectileKnockback = 4f;
    [SerializeField] private LayerMask projectileTargetLayers;
    [SerializeField] private LayerMask reflectedHitLayers;

    private float shootTimer;
    private bool advanceShootTimer;

    public int CombatTickOrder => 200;

    private void OnEnable()
    {
        shootTimer = shootInterval;
        CombatTimeController.Register(this);
    }

    private void OnDisable()
    {
        CombatTimeController.Unregister(this);
        CombatTimeController.ReleaseOwner(this);
        shootTimer = 0f;
        advanceShootTimer = false;
    }

    private void Start()
    {
        shootTimer = shootInterval;
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    public void CombatTick()
    {
        advanceShootTimer = false;
        if (target == null || projectilePrefab == null || muzzle == null)
        {
            return;
        }

        float dist = Vector2.Distance(transform.position, target.position);
        if (dist > loseTargetRange)
        {
            return;
        }

        if (dist <= detectRange)
        {
            UpdateSpacing(dist);
        }

        if (dist < preferredMinDistance || dist > preferredMaxDistance)
        {
            return;
        }

        advanceShootTimer = true;
        if (CombatTimeController.AdvanceTimer(shootTimer) > 0f)
        {
            return;
        }

        shootTimer = shootInterval;
        advanceShootTimer = false;
        ShootAtTarget();
    }

    public void CombatTickTimers()
    {
        if (advanceShootTimer)
        {
            shootTimer = CombatTimeController.AdvanceTimer(shootTimer);
        }
    }

    private void UpdateSpacing(float dist)
    {
        Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 dir = toTarget.normalized;
        if (dist < preferredMinDistance)
        {
            if (dist > nearStopDistance)
            {
                transform.position += (Vector3)(-dir * moveSpeed * CombatTimeController.StepSeconds);
            }

            return;
        }

        if (dist > preferredMaxDistance)
        {
            transform.position += (Vector3)(dir * moveSpeed * CombatTimeController.StepSeconds);
        }
    }

    private void ShootAtTarget()
    {
        Vector2 dir = ((Vector2)target.position - (Vector2)muzzle.position).normalized;
        if (dir.sqrMagnitude < 0.001f)
        {
            dir = Vector2.right;
        }

        EnemyProjectile2D proj = Instantiate(projectilePrefab, muzzle.position, Quaternion.identity);
        proj.Initialize(dir, projectileSpeed, projectileDamage, projectileKnockback, projectileTargetLayers, reflectedHitLayers, transform);
    }
}
