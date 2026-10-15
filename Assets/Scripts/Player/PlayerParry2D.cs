using UnityEngine;
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
    private PlayerMotor2D motor;
    private PlayerParryCounter2D counter;
    [SerializeField, Min(0f)] private float maxParryHeightDifference = 1.25f;
    public Damageable2D LastParriedEnemy { get; private set; }
    public event System.Action<Damageable2D, ParryResult> Succeeded;

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
        motor = GetComponent<PlayerMotor2D>();
        counter = GetComponent<PlayerParryCounter2D>();
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

    public void CombatTick()
    {
    }

    public void CombatTickTimers()
    {
        UpdateTimers();
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
        LastParriedEnemy = null;
        attemptCount++;
        return true;
    }

    public bool TryResolveParry(out ParryResult result)
    {
        return TryResolveParry(transform.position, out result);
    }

    public bool TryResolveParry(Vector2 sourcePosition, out ParryResult result)
    {
        return ResolveParry(sourcePosition, null, true, out result);
    }

    public bool TryResolveSourceParry(Vector2 contactPosition, Damageable2D source, bool parryable, out ParryResult result)
    {
        return ResolveParry(contactPosition, source, parryable, out result);
    }

    private bool ResolveParry(Vector2 sourcePosition, Damageable2D source, bool parryable, out ParryResult result)
    {
        if (!parryActive || !parryable || (source != null && !source.IsAlive))
        {
            result = ParryResult.None;
            return false;
        }

        float dist = Vector2.Distance(transform.position, sourcePosition);
        if (dist > parrySnapDistance || Mathf.Abs(transform.position.y - sourcePosition.y) > maxParryHeightDifference)
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
        LastParriedEnemy = source;
        if (source != null)
        {
            float dx = source.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > .001f) motor?.SetFacing(dx > 0 ? 1 : -1);
            source.ApplyParryStun(result);
            if (counter == null) counter = GetComponent<PlayerParryCounter2D>();
            counter?.RegisterParriedEnemy(source);
        }
        Succeeded?.Invoke(source, result);
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
        LastParriedEnemy = null;

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

