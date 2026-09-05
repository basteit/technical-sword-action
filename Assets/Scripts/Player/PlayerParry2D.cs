using UnityEngine;
using UnityEngine.InputSystem;
using TechnicalSwordAction.PlayerState;

public enum ParryResult
{
    None,
    Normal,
    Just
}

public class PlayerParry2D : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    [Header("Parry Window")]
    [SerializeField] private float parryWindowDuration = 0.2f;
    [SerializeField] private float justParryDuration = 0.07f;
    [SerializeField] private float parryCooldown = 0.22f;

    [Header("Parry Fail")]
    [SerializeField, Min(0f)] private float failLockDuration = 0.3f;
    [SerializeField, Min(0f)] private float successLockDuration = 0.16f;

    [Header("Parry Snap")]
    [SerializeField] private float parrySnapDistance = 1.9f;

    [Header("References")]
    [SerializeField] private PlayerSpecialSkill2D specialSkill;
    [SerializeField] private PlayerStateMachine stateMachine;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip normalParryClip;
    [SerializeField] private AudioClip justParryClip;
    [SerializeField] private AudioClip parryMissClip;

    private bool parryActive;
    private bool parryResolved;
    private float parryTimer;
    private float parryElapsed;
    private float cooldownTimer;
    private float failLockTimer;
    private float successLockTimer;
    private int attemptCount;
    private int successCount;
    private int justSuccessCount;
    private int missCount;

    public bool IsParryActive => parryActive;
    public bool IsFailLocked => failLockTimer > 0f;
    public bool IsSuccessLocked => successLockTimer > 0f;
    public bool CanStartParry => isActiveAndEnabled &&
                                 cooldownTimer <= 0f &&
                                 !parryActive &&
                                 !IsFailLocked &&
                                 !IsSuccessLocked;
    public float ParryRemaining => Mathf.Max(0f, parryTimer);
    public float ParryCooldownRemaining => Mathf.Max(0f, cooldownTimer);
    public float FailLockRemaining => Mathf.Max(0f, failLockTimer);
    public float SuccessLockRemaining => Mathf.Max(0f, successLockTimer);
    public ParryResult LastParryResult { get; private set; } = ParryResult.None;
    public int AttemptCount => attemptCount;
    public int SuccessCount => successCount;
    public int JustSuccessCount => justSuccessCount;
    public int MissCount => missCount;
    public float SuccessRate => attemptCount > 0 ? (float)successCount / attemptCount : 0f;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (specialSkill == null)
        {
            specialSkill = GetComponent<PlayerSpecialSkill2D>();
        }

        if (stateMachine == null)
        {
            stateMachine = GetComponent<PlayerStateMachine>();
        }
    }

    public int CombatTickOrder => -100;

    private void OnEnable()
    {
        CombatTimeController.Register(this);
    }

    private void Update()
    {
        if (CombatTimeController.AcceptsGameplayInput)
        {
            ReadInput();
        }
    }

    public void CombatTick()
    {
    }

    public void CombatTickTimers()
    {
        UpdateTimers();
    }

    private void ReadInput()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            stateMachine?.RequestAction(PlayerActionRequest.Parry);
        }
    }

    private void UpdateTimers()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer = CombatTimeController.AdvanceTimer(cooldownTimer);
        }

        if (failLockTimer > 0f)
        {
            failLockTimer = CombatTimeController.AdvanceTimer(failLockTimer);
            if (failLockTimer <= 0f)
            {
                stateMachine?.CompleteAction(PlayerActionState.ParryFail, "ParryFailComplete");
            }
        }

        if (successLockTimer > 0f)
        {
            successLockTimer = CombatTimeController.AdvanceTimer(successLockTimer);
            if (successLockTimer <= 0f)
            {
                stateMachine?.CompleteAction(PlayerActionState.ParrySuccess, "ParrySuccessComplete");
            }
        }

        if (!parryActive)
        {
            return;
        }

        parryTimer = CombatTimeController.AdvanceTimer(parryTimer);
        parryElapsed += CombatTimeController.StepSeconds;

        if (parryTimer <= 0f)
        {
            parryActive = false;
            LastParryResult = ParryResult.None;

            if (!parryResolved)
            {
                ApplyFailLock();
                PlayClip(parryMissClip, 0.8f);
                stateMachine?.ChangeActionPhase(
                    PlayerActionState.Parry,
                    PlayerActionState.ParryFail,
                    "ParryMiss");
                if (failLockTimer <= 0f)
                {
                    stateMachine?.CompleteAction(PlayerActionState.ParryFail, "ParryFailComplete");
                }
            }
        }
    }

    public bool TryStartParryFromStateMachine()
    {
        if (!CanStartParry)
        {
            return false;
        }

        parryActive = true;
        parryResolved = false;
        parryTimer = parryWindowDuration;
        parryElapsed = 0f;
        cooldownTimer = parryCooldown;
        LastParryResult = ParryResult.None;
        attemptCount++;
        return true;
    }

    public bool TryResolveParry(out ParryResult result)
    {
        return TryResolveParry(transform.position, out result);
    }

    public bool TryResolveParry(Vector2 sourcePosition, out ParryResult result)
    {
        if (!parryActive)
        {
            result = ParryResult.None;
            return false;
        }

        float dist = Vector2.Distance(transform.position, sourcePosition);
        if (dist > parrySnapDistance)
        {
            result = ParryResult.None;
            return false;
        }

        result = parryElapsed <= justParryDuration ? ParryResult.Just : ParryResult.Normal;
        LastParryResult = result;
        parryActive = false;
        parryResolved = true;
        parryTimer = 0f;
        successLockTimer = successLockDuration;

        if (result == ParryResult.Just)
        {
            justSuccessCount++;
            PlayClip(justParryClip, 1f);
        }
        else
        {
            PlayClip(normalParryClip, 0.95f);
        }

        successCount++;
        stateMachine?.ChangeActionPhase(
            PlayerActionState.Parry,
            PlayerActionState.ParrySuccess,
            "ParrySuccess");

        if (successLockTimer <= 0f)
        {
            stateMachine?.CompleteAction(PlayerActionState.ParrySuccess, "ParrySuccessComplete");
        }

        return true;
    }

    public void CancelParryFromStateMachine(bool clearCooldown = false)
    {
        parryActive = false;
        parryResolved = false;
        parryTimer = 0f;
        parryElapsed = 0f;
        failLockTimer = 0f;
        successLockTimer = 0f;
        LastParryResult = ParryResult.None;

        if (clearCooldown)
        {
            cooldownTimer = 0f;
        }
    }

    private void ApplyFailLock()
    {
        failLockTimer = Mathf.Max(0f, failLockDuration);
        missCount++;
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip, volume);
    }

    private void OnDisable()
    {
        CombatTimeController.Unregister(this);
        bool hadAction = parryActive || IsFailLocked || IsSuccessLocked;
        CancelParryFromStateMachine(true);
        if (hadAction)
        {
            stateMachine?.CompleteParryAction("ParryDisabled");
        }
    }
}

