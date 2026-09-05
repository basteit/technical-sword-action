using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TechnicalSwordAction.PlayerState;

public class PlayerAttack2D : MonoBehaviour
{
    private const int ComboStepCount = 4;
    private const float SixFramesAtSixtyFps = 6f / 60f;

    [System.Serializable]
    private struct AttackStepData
    {
        public int damage;
        public float radius;
        public float gaugeGain;
        public float fallbackDuration;
    }

    [Header("Combo")]
    [SerializeField, Min(0f)] private float inputBufferDuration = SixFramesAtSixtyFps;
    [SerializeField, Min(0f)] private float fallbackGraceDuration = 0.12f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string attackTriggerName = "AttackTrigger";
    [SerializeField] private string comboStepParamName = "ComboStep";

    [Header("Hit Base")]
    [SerializeField] private Transform hitPoint;
    [SerializeField] private float attackRadius = 0.65f;
    [SerializeField] private LayerMask targetLayers;

    [Header("Per Step (1-4)")]
    [SerializeField] private AttackStepData[] attackSteps =
    {
        new AttackStepData { damage = 1, radius = 0.65f, gaugeGain = 2f, fallbackDuration = 0.62f },
        new AttackStepData { damage = 1, radius = 0.70f, gaugeGain = 2f, fallbackDuration = 0.52f },
        new AttackStepData { damage = 2, radius = 0.75f, gaugeGain = 2f, fallbackDuration = 0.62f },
        new AttackStepData { damage = 3, radius = 0.80f, gaugeGain = 5f, fallbackDuration = 1.02f }
    };

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
    [SerializeField] private PlayerMotor2D motor;
    [SerializeField] private PlayerDamageReceiver2D damageReceiver;
    [SerializeField] private PlayerParry2D parry;
    [SerializeField] private PlayerSpecialGauge specialGauge;
    [SerializeField] private PlayerSpecialSkill2D specialSkill;
    [SerializeField] private PlayerStateMachine stateMachine;

    private readonly HashSet<Damageable2D> damagedTargets = new();

    private int comboStep;
    private bool isAttacking;
    private bool comboWindowOpen;
    private bool queuedNextAttack;
    private bool hitAppliedForCurrentStep;
    private float bufferedAttackRemaining;
    private float stepTimeoutRemaining;
    private int attackTriggerHash;
    private int comboStepHash;
    private PlayerAttackCancelWindow openCancelWindows;

    public bool IsAttacking => isAttacking;
    public int ComboStep => comboStep;
    public int MaxComboStep => ComboStepCount;
    public bool IsComboWindowOpen => comboWindowOpen;
    public bool HasQueuedAttack => queuedNextAttack;
    public bool HitAppliedForCurrentStep => hitAppliedForCurrentStep;
    public float InputBufferRemaining => Mathf.Max(0f, bufferedAttackRemaining);
    public float StepTimeoutRemaining => Mathf.Max(0f, stepTimeoutRemaining);
    public PlayerAttackCancelWindow OpenCancelWindows => openCancelWindows;
    public bool CanStartAttackFromStateMachine => isActiveAndEnabled && !isAttacking;
    public int ComboAttemptCount { get; private set; }
    public int FullComboCount { get; private set; }
    public float FullComboRate => ComboAttemptCount <= 0 ? 0f : FullComboCount / (float)ComboAttemptCount;
    public int TimeoutFallbackCount { get; private set; }
    public string LastEndReason { get; private set; } = "None";

    private void Awake()
    {
        EnsureReferences();
        EnsureStepData();
        attackTriggerHash = Animator.StringToHash(attackTriggerName);
        comboStepHash = Animator.StringToHash(comboStepParamName);
        ResetAttackState(true);
    }

    private void OnValidate()
    {
        inputBufferDuration = Mathf.Max(0f, inputBufferDuration);
        fallbackGraceDuration = Mathf.Max(0f, fallbackGraceDuration);
        EnsureStepData();
    }

    private void Update()
    {
        UpdateBufferedInput();

        if (isAttacking)
        {
            stepTimeoutRemaining -= Time.deltaTime;
            if (stepTimeoutRemaining <= 0f)
            {
                TimeoutFallbackCount++;
                LastEndReason = "TimeoutFallback";
                ResetAttackState(true);
                stateMachine?.CompleteAction(PlayerActionState.Attack, "AttackTimeoutFallback");
            }
        }

        if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
        {
            RequestAttack();
        }
    }

    public bool RequestAttack()
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        if (!isAttacking)
        {
            return stateMachine != null && stateMachine.RequestAction(PlayerActionRequest.Attack);
        }

        if (comboStep >= ComboStepCount)
        {
            return false;
        }

        bufferedAttackRemaining = Mathf.Max(inputBufferDuration, SixFramesAtSixtyFps);
        TryConsumeBufferedInput();
        return true;
    }

    public bool TryStartAttackFromStateMachine()
    {
        if (!CanStartAttackFromStateMachine)
        {
            return false;
        }

        StartAttackStep(1);
        return true;
    }

    public void CancelAttack(string reason = "Cancelled")
    {
        if (!isAttacking && comboStep == 0)
        {
            return;
        }

        LastEndReason = reason;
        ResetAttackState(true);
    }

    private void StartAttackStep(int step)
    {
        comboStep = Mathf.Clamp(step, 1, ComboStepCount);
        if (comboStep == 1)
        {
            ComboAttemptCount++;
        }

        isAttacking = true;
        comboWindowOpen = false;
        queuedNextAttack = false;
        hitAppliedForCurrentStep = false;
        bufferedAttackRemaining = 0f;
        openCancelWindows = PlayerAttackCancelWindow.None;

        AttackStepData data = GetStepData(comboStep);
        stepTimeoutRemaining = data.fallbackDuration + fallbackGraceDuration;
        damagedTargets.Clear();

        if (animator != null)
        {
            animator.SetInteger(comboStepHash, comboStep);
            animator.ResetTrigger(attackTriggerHash);
            animator.SetTrigger(attackTriggerHash);
        }

        PlayClip(attackSwingClip, 0.9f);
    }

    public void OnAttackHit(int step)
    {
        if (!CanProcessAnimationEvent(step) || hitAppliedForCurrentStep || hitPoint == null)
        {
            return;
        }

        hitAppliedForCurrentStep = true;
        damagedTargets.Clear();

        AttackStepData data = GetStepData(step);
        bool hitSomething = false;
        float radius = data.radius > 0f ? data.radius : attackRadius;
        Collider2D[] hits = Physics2D.OverlapCircleAll(hitPoint.position, radius, targetLayers);

        for (int i = 0; i < hits.Length; i++)
        {
            Damageable2D damageable = hits[i].GetComponentInParent<Damageable2D>();
            if (damageable == null || !damagedTargets.Add(damageable))
            {
                continue;
            }

            Vector2 knockbackDir = (hits[i].transform.position - transform.position).normalized;
            if (knockbackDir.sqrMagnitude < 0.01f)
            {
                knockbackDir = transform.right;
            }

            damageable.TakeHit(data.damage, knockbackDir);
            hitSomething = true;
        }

        if (!hitSomething)
        {
            return;
        }

        if (specialGauge != null)
        {
            specialGauge.AddOnAttackHit(data.gaugeGain);
        }

        PlayClip(hitConfirmClip, 1f);
    }

    public void OnComboWindowOpen(int step)
    {
        if (!CanProcessAnimationEvent(step) || comboStep >= ComboStepCount)
        {
            return;
        }

        comboWindowOpen = true;
        TryConsumeBufferedInput();
    }

    public void OnComboWindowClose(int step)
    {
        if (!CanProcessAnimationEvent(step))
        {
            return;
        }

        comboWindowOpen = false;
    }

    // #48 owns the configured frame ranges. These methods are the runtime seam
    // that its timeline/data driver will call when each inclusive window changes.
    public void OnDefenseCancelWindowOpen()
    {
        if (isAttacking)
        {
            openCancelWindows |= PlayerAttackCancelWindow.Defense;
        }
    }

    public void OnDefenseCancelWindowClose()
    {
        openCancelWindows &= ~PlayerAttackCancelWindow.Defense;
    }

    public void OnLateCancelWindowOpen()
    {
        if (isAttacking)
        {
            openCancelWindows |= PlayerAttackCancelWindow.Late;
        }
    }

    public void OnLateCancelWindowClose()
    {
        openCancelWindows &= ~PlayerAttackCancelWindow.Late;
    }

    public void OnAttackEnd(int step)
    {
        if (!CanProcessAnimationEvent(step))
        {
            return;
        }

        CompleteAttackStep(step, "AnimationEvent");
    }

    private void CompleteAttackStep(int expectedStep, string reason)
    {
        if (!IsCurrentStep(expectedStep))
        {
            return;
        }

        comboWindowOpen = false;

        if (queuedNextAttack && comboStep < ComboStepCount)
        {
            LastEndReason = $"{reason}->Next";
            StartAttackStep(comboStep + 1);
            return;
        }

        if (comboStep == ComboStepCount)
        {
            FullComboCount++;
        }

        LastEndReason = reason;
        ResetAttackState(true);
        stateMachine?.CompleteAction(PlayerActionState.Attack, $"Attack{reason}");
    }

    private void UpdateBufferedInput()
    {
        if (bufferedAttackRemaining <= 0f)
        {
            return;
        }

        bufferedAttackRemaining = Mathf.Max(0f, bufferedAttackRemaining - Time.deltaTime);
        TryConsumeBufferedInput();
    }

    private void TryConsumeBufferedInput()
    {
        if (!isAttacking || !comboWindowOpen || comboStep >= ComboStepCount || bufferedAttackRemaining <= 0f)
        {
            return;
        }

        queuedNextAttack = true;
        bufferedAttackRemaining = 0f;
    }

    private bool IsCurrentStep(int step)
    {
        return isAttacking && step == comboStep && step >= 1 && step <= ComboStepCount;
    }

    private bool CanProcessAnimationEvent(int step)
    {
        if (!IsCurrentStep(step))
        {
            return false;
        }

        return true;
    }

    private void ResetAttackState(bool updateAnimator)
    {
        isAttacking = false;
        comboStep = 0;
        comboWindowOpen = false;
        queuedNextAttack = false;
        hitAppliedForCurrentStep = false;
        bufferedAttackRemaining = 0f;
        stepTimeoutRemaining = 0f;
        openCancelWindows = PlayerAttackCancelWindow.None;
        damagedTargets.Clear();

        if (updateAnimator && animator != null && attackTriggerHash != 0 && comboStepHash != 0)
        {
            animator.ResetTrigger(attackTriggerHash);
            animator.SetInteger(comboStepHash, 0);
        }
    }

    private AttackStepData GetStepData(int step)
    {
        int index = Mathf.Clamp(step - 1, 0, ComboStepCount - 1);
        if (attackSteps == null || index >= attackSteps.Length)
        {
            return GetDefaultStepData(index);
        }

        return attackSteps[index];
    }

    private void EnsureReferences()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (motor == null) motor = GetComponent<PlayerMotor2D>();
        if (damageReceiver == null) damageReceiver = GetComponent<PlayerDamageReceiver2D>();
        if (parry == null) parry = GetComponent<PlayerParry2D>();
        if (specialGauge == null) specialGauge = GetComponent<PlayerSpecialGauge>();
        if (specialSkill == null) specialSkill = GetComponent<PlayerSpecialSkill2D>();
        if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void EnsureStepData()
    {
        AttackStepData[] normalized = new AttackStepData[ComboStepCount];

        for (int i = 0; i < normalized.Length; i++)
        {
            AttackStepData defaults = GetDefaultStepData(i);
            AttackStepData source = attackSteps != null && i < attackSteps.Length ? attackSteps[i] : defaults;

            if (source.damage <= 0) source.damage = defaults.damage;
            if (source.radius <= 0f) source.radius = defaults.radius;
            if (source.gaugeGain <= 0f) source.gaugeGain = defaults.gaugeGain;
            if (source.fallbackDuration <= 0f) source.fallbackDuration = defaults.fallbackDuration;

            normalized[i] = source;
        }

        int strongestEarlierStep = Mathf.Max(normalized[0].damage, normalized[1].damage, normalized[2].damage);
        if (normalized[3].damage <= strongestEarlierStep)
        {
            normalized[3].damage = strongestEarlierStep + 1;
        }

        attackSteps = normalized;
    }

    private AttackStepData GetDefaultStepData(int index)
    {
        return index switch
        {
            0 => new AttackStepData { damage = 1, radius = 0.65f, gaugeGain = 2f, fallbackDuration = 0.62f },
            1 => new AttackStepData { damage = 1, radius = 0.70f, gaugeGain = 2f, fallbackDuration = 0.52f },
            2 => new AttackStepData { damage = 2, radius = 0.75f, gaugeGain = 2f, fallbackDuration = 0.62f },
            _ => new AttackStepData { damage = 3, radius = 0.80f, gaugeGain = 5f, fallbackDuration = 1.02f }
        };
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

        AttackStepData data = GetStepData(Mathf.Max(1, comboStep));
        float radius = data.radius > 0f ? data.radius : attackRadius;

        if (isAttacking)
        {
            Gizmos.color = activeFillColor;
            Gizmos.DrawSphere(hitPoint.position, radius);
            Gizmos.color = activeHitboxColor;
        }
        else
        {
            Gizmos.color = idleHitboxColor;
        }

        Gizmos.DrawWireSphere(hitPoint.position, radius);
    }

    private void OnDisable()
    {
        bool hadAttack = isAttacking || comboStep != 0;
        if (hadAttack)
        {
            LastEndReason = "Disabled";
        }

        ResetAttackState(true);
        if (hadAttack)
        {
            stateMachine?.CompleteAction(PlayerActionState.Attack, "AttackDisabled");
        }
    }

    private void OnDrawGizmos()
    {
        if (showHitboxAlways)
        {
            DrawHitGizmo();
        }
    }

    private void OnDrawGizmosSelected()
    {
        DrawHitGizmo();
    }
}
