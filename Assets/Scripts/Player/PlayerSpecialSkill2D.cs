using UnityEngine;
using UnityEngine.InputSystem;
using TechnicalSwordAction.PlayerState;

public class PlayerSpecialSkill2D : MonoBehaviour
{
    [Header("Gauge")]
    [SerializeField] private PlayerSpecialGauge specialGauge;
    [SerializeField] private float requiredGauge = 60f;

    [Header("State")]
    [SerializeField] private PlayerStateMachine stateMachine;

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
    private float reservedGauge;

    public bool IsUsingSkill => isUsingSkill;
    public float LockRemaining => Mathf.Max(0f, lockTimer);
    public bool CanStartSkill => isActiveAndEnabled &&
                                 !isUsingSkill &&
                                 specialGauge != null &&
                                 specialGauge.CanConsume(Mathf.Max(0f, requiredGauge));

    private void Awake()
    {
        if (specialGauge == null)
        {
            specialGauge = GetComponent<PlayerSpecialGauge>();
        }

        if (stateMachine == null)
        {
            stateMachine = GetComponent<PlayerStateMachine>();
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
            stateMachine?.RequestAction(PlayerActionRequest.Special);
        }
    }

    public bool TryStartSkillFromStateMachine()
    {
        float gaugeCost = Mathf.Max(0f, requiredGauge);
        if (!CanStartSkill)
        {
            return false;
        }

        isUsingSkill = true;
        lockTimer = startupLock + recoveryLock;
        damageApplied = false;
        reservedGauge = gaugeCost;

        PlayClip(castClip, 1f);
        return true;
    }

    private void UpdateSkillLock()
    {
        lockTimer -= Time.deltaTime;

        if (!damageApplied && lockTimer <= recoveryLock)
        {
            if (!specialGauge.Consume(reservedGauge))
            {
                CancelSkillFromStateMachine();
                stateMachine?.CompleteAction(PlayerActionState.Special, "SpecialGaugeUnavailable");
                return;
            }

            reservedGauge = 0f;
            ApplySkillDamage();
            damageApplied = true;
        }

        if (lockTimer <= 0f)
        {
            isUsingSkill = false;
            lockTimer = 0f;
            stateMachine?.CompleteAction(PlayerActionState.Special, "SpecialComplete");
        }
    }

    public void CancelSkillFromStateMachine()
    {
        isUsingSkill = false;
        lockTimer = 0f;
        damageApplied = false;
        reservedGauge = 0f;
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

    private void OnDisable()
    {
        bool wasUsingSkill = isUsingSkill;
        CancelSkillFromStateMachine();
        if (wasUsingSkill)
        {
            stateMachine?.CompleteAction(PlayerActionState.Special, "SpecialDisabled");
        }
    }
}
