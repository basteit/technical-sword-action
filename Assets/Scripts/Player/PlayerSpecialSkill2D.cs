using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSpecialSkill2D : MonoBehaviour
{
    [Header("Gauge")]
    [SerializeField] private PlayerSpecialGauge specialGauge;
    [SerializeField] private float requiredGauge = 60f;

    [Header("Skill")]
    [SerializeField] private Transform skillPoint;
    [SerializeField] private float skillRadius = 1.8f;
    [SerializeField] private int skillDamage = 7;
    [SerializeField] private LayerMask targetLayers;

    [Header("Risk")]
    [SerializeField] private float startupLock = 0.24f;
    [SerializeField] private float recoveryLock = 0.55f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip castClip;
    [SerializeField] private AudioClip hitClip;

    [Header("Debug Draw")]
    [SerializeField] private bool showHitboxAlways = true;
    [SerializeField] private Color idleHitboxColor = new Color(1f, 0.95f, 0.2f, 0.55f);
    [SerializeField] private Color activeHitboxColor = new Color(1f, 0.85f, 0.05f, 0.95f);
    [SerializeField] private Color activeFillColor = new Color(1f, 0.85f, 0.05f, 0.22f);

    private bool isUsingSkill;
    private float lockTimer;
    private bool damageApplied;

    public bool IsUsingSkill => isUsingSkill;
    public float LockRemaining => Mathf.Max(0f, lockTimer);

    private void Awake()
    {
        if (specialGauge == null)
        {
            specialGauge = GetComponent<PlayerSpecialGauge>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void Update()
    {
        if (isUsingSkill)
        {
            UpdateSkillLock();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
        {
            TryCastSkill();
        }
    }

    private void TryCastSkill()
    {
        if (specialGauge == null)
        {
            return;
        }

        if (!specialGauge.Consume(requiredGauge))
        {
            return;
        }

        isUsingSkill = true;
        lockTimer = startupLock + recoveryLock;
        damageApplied = false;

        PlayClip(castClip, 1f);
    }

    private void UpdateSkillLock()
    {
        lockTimer -= Time.deltaTime;

        if (!damageApplied && lockTimer <= recoveryLock)
        {
            ApplySkillDamage();
            damageApplied = true;
        }

        if (lockTimer <= 0f)
        {
            isUsingSkill = false;
            lockTimer = 0f;
        }
    }

    private void ApplySkillDamage()
    {
        if (skillPoint == null)
        {
            return;
        }

        bool hitSomething = false;
        Collider2D[] hits = Physics2D.OverlapCircleAll(skillPoint.position, skillRadius, targetLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].TryGetComponent(out Damageable2D damageable))
            {
                Vector2 dir = (hits[i].transform.position - transform.position).normalized;
                if (dir.sqrMagnitude < 0.01f)
                {
                    dir = transform.right;
                }

                damageable.TakeHit(skillDamage, dir);
                hitSomething = true;
            }
        }

        if (hitSomething)
        {
            PlayClip(hitClip, 1f);
        }
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip, volume);
    }

    private void DrawSkillGizmo()
    {
        if (skillPoint == null)
        {
            return;
        }

        bool active = isUsingSkill && !damageApplied;
        if (active)
        {
            Gizmos.color = activeFillColor;
            Gizmos.DrawSphere(skillPoint.position, skillRadius);
            Gizmos.color = activeHitboxColor;
        }
        else
        {
            Gizmos.color = idleHitboxColor;
        }

        Gizmos.DrawWireSphere(skillPoint.position, skillRadius);
    }

    private void OnDrawGizmos()
    {
        if (!showHitboxAlways)
        {
            return;
        }

        DrawSkillGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        DrawSkillGizmo();
    }
}
