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

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip attackSwingClip;
    [SerializeField] private AudioClip hitConfirmClip;

    [Header("Debug Draw")]
    [SerializeField] private bool showHitboxAlways = true;
    [SerializeField] private Color idleHitboxColor = new Color(1f, 0.3f, 0.3f, 0.55f);
    [SerializeField] private Color activeHitboxColor = new Color(1f, 0.1f, 0.1f, 0.95f);
    [SerializeField] private Color activeFillColor = new Color(1f, 0.1f, 0.1f, 0.22f);

    [Header("Optional References")]
    [SerializeField] private PlayerParry2D parry;
    [SerializeField] private PlayerSpecialGauge specialGauge;
    [SerializeField] private PlayerSpecialSkill2D specialSkill;

    private int comboStep;
    private float comboTimer;
    private float attackTimer;
    private bool isAttacking;

    public bool IsAttacking => isAttacking;
    public int ComboStep => comboStep;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (parry == null)
        {
            parry = GetComponent<PlayerParry2D>();
        }

        if (specialGauge == null)
        {
            specialGauge = GetComponent<PlayerSpecialGauge>();
        }

        if (specialSkill == null)
        {
            specialSkill = GetComponent<PlayerSpecialSkill2D>();
        }
    }

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

        if ((parry != null && parry.IsFailLocked) || (specialSkill != null && specialSkill.IsUsingSkill))
        {
            return;
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

        PlayClip(attackSwingClip, 0.9f);

        int damage = step == 1 ? attack1Damage : attack2Damage;
        bool hitSomething = ApplyHit(damage);
        if (hitSomething)
        {
            PlayClip(hitConfirmClip, 1f);
        }
    }

    private bool ApplyHit(int damage)
    {
        if (hitPoint == null)
        {
            return false;
        }

        bool hitSomething = false;
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
                hitSomething = true;

                if (specialGauge != null)
                {
                    specialGauge.AddOnAttackHit();
                }
            }
        }

        return hitSomething;
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip, volume);
    }

    private void DrawHitGizmo()
    {
        if (hitPoint == null)
        {
            return;
        }

        if (isAttacking)
        {
            Gizmos.color = activeFillColor;
            Gizmos.DrawSphere(hitPoint.position, attackRadius);
            Gizmos.color = activeHitboxColor;
        }
        else
        {
            Gizmos.color = idleHitboxColor;
        }

        Gizmos.DrawWireSphere(hitPoint.position, attackRadius);
    }

    private void OnDrawGizmos()
    {
        if (!showHitboxAlways)
        {
            return;
        }

        DrawHitGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        DrawHitGizmo();
    }
}
