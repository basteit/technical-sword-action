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
    [SerializeField] private ParryTimingDefinition timing;
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
    private long resolvedTick = -1;
    public bool LastResolvedAsPrimary { get; private set; }
    public int ElapsedFrames => Mathf.RoundToInt(parryElapsed * 60);
    public int AcceptanceStartFrame => timing != null ? timing.startFrame : 0;
    public int AcceptanceEndFrame => timing != null ? timing.endFrame : Mathf.RoundToInt(parryWindowDuration * 60);
    [SerializeField, Min(0f)] private float maxParryHeightDifference = 1.25f;
    public Damageable2D LastParriedEnemy { get; private set; }
    public event System.Action<Damageable2D, ParryResult> Succeeded;
    public event System.Action Failed;

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
                if (timing != null) cooldownTimer = timing.cooldownFrames / 60f;
                stateMachine?.CompleteAction(PlayerActionState.ParryFail, "ParryFailComplete");
            }
        }

        if (successLockTimer > 0f)
        {
            successLockTimer = CombatTimeController.AdvanceTimer(successLockTimer);
            if (successLockTimer <= 0f)
            {
                if (timing != null) cooldownTimer = timing.cooldownFrames / 60f;
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
                if (!CombatFeedback2D.IsManaging(gameObject)) PlayClip(parryMissClip, 0.8f);
                Failed?.Invoke();
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
        parryTimer = timing != null ? (timing.endFrame + 1) / 60f : parryWindowDuration;
        parryElapsed = 0f;
        cooldownTimer = timing != null ? 0f : parryCooldown;
        resolvedTick = -1;
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
        LastResolvedAsPrimary = false;
        bool secondary = !parryActive && parryResolved && CombatTimeController.IsExecutingTick && resolvedTick == CombatTimeController.TickCount;
        if ((!parryActive && !secondary) || !parryable || (source != null && !source.IsAlive) ||
            (timing != null && !secondary && (ElapsedFrames < timing.startFrame || ElapsedFrames > timing.endFrame)))
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

        result = secondary ? LastParryResult :
            (timing != null ? ElapsedFrames <= timing.justEndFrame : parryElapsed <= justParryDuration) ? ParryResult.Just : ParryResult.Normal;
        if (secondary)
        {
            if (counter == null) counter = GetComponent<PlayerParryCounter2D>();
            counter?.RegisterParriedEnemy(source);
            return true;
        }
        LastResolvedAsPrimary = true;
        resolvedTick = CombatTimeController.TickCount;
        LastParryResult = result;
        parryActive = false;
        parryResolved = true;
        parryTimer = 0f;
        successLockTimer = timing != null ? (result == ParryResult.Just ? timing.justSuccessFrames : timing.successFrames) / 60f : successLockDuration;

        if (result == ParryResult.Just)
        {
            justSuccessCount++;
            if (!CombatFeedback2D.IsManaging(gameObject)) PlayClip(justParryClip, 1f);
        }
        else
        {
            if (!CombatFeedback2D.IsManaging(gameObject)) PlayClip(normalParryClip, 0.95f);
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
        if (timing != null && (parryActive || successLockTimer > 0 || failLockTimer > 0) && !clearCooldown)
            cooldownTimer = Mathf.Max(cooldownTimer, timing.cooldownFrames / 60f);
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
        failLockTimer = timing != null ? Mathf.Max(0, timing.failEndFrame + 1 - ElapsedFrames) / 60f : Mathf.Max(0f, failLockDuration);
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

