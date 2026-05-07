using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack2D : MonoBehaviour
{
    [Header("Combo")]
    [SerializeField] private float comboResetTime = 0.45f;
    [SerializeField] private float attack1Duration = 0.18f;
    [SerializeField] private float attack2Duration = 0.22f;

    [Header("Hit")]
    [SerializeField] private Transform hitPoint;
    [SerializeField] private float attackRadius = 0.65f;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private int attack1Damage = 1;
    [SerializeField] private int attack2Damage = 2;

    private int comboStep;
    private float comboTimer;
    private float attackTimer;
    private bool isAttacking;

    public bool IsAttacking => isAttacking;
    public int ComboStep => comboStep;

    private void Update()
    {
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                isAttacking = false;
            }
        }

        if (comboStep > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f)
            {
                comboStep = 0;
            }
        }

        if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        if (isAttacking)
        {
            return;
        }

        if (comboStep == 0)
        {
            PerformAttack(1);
        }
        else
        {
            PerformAttack(2);
        }
    }

    private void PerformAttack(int step)
    {
        comboStep = step;
        comboTimer = comboResetTime;
        isAttacking = true;
        attackTimer = step == 1 ? attack1Duration : attack2Duration;

        int damage = step == 1 ? attack1Damage : attack2Damage;
        ApplyHit(damage);
    }

    private void ApplyHit(int damage)
    {
        if (hitPoint == null)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(hitPoint.position, attackRadius, targetLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].TryGetComponent(out Damageable2D damageable))
            {
                Vector2 knockbackDir = (hits[i].transform.position - transform.position).normalized;
                if (knockbackDir.sqrMagnitude < 0.01f)
                {
                    knockbackDir = transform.right;
                }

                damageable.TakeHit(damage, knockbackDir);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (hitPoint == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(hitPoint.position, attackRadius);
    }
}
