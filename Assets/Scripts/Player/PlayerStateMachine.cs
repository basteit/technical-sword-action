using System;
using System.Collections.Generic;
using TechnicalSwordAction.PlayerState;
using UnityEngine;

public enum PlayerState
{
    Idle,
    Move,
    Jump,
    Fall,
    Dash,
    Parry,
    ParryFail,
    Attack,
    Special,
    Hit
}

public enum PlayerActionRejectionReason
{
    None,
    PausePriority,
    Dead,
    CombatSuspended,
    GameplayBlocked,
    Unavailable,
    CurrentActionLocked,
    LowerPriority,
    StartFailed
}

public interface IPlayerActionStateHandler
{
    PlayerActionState ActionState { get; }
    bool CanStartAction { get; }
    float LockRemaining { get; }
    bool TryStartAction();
    void CancelAction();
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class PlayerStateMachine : MonoBehaviour
{
    private sealed class CollisionIgnoreRecord
    {
        public Collider2D OwnCollider;
        public readonly HashSet<PlayerActionState> Owners = new();
    }

    [SerializeField] private PlayerMotor2D motor;
    [SerializeField] private PlayerAttack2D attack;
    [SerializeField] private PlayerDamageReceiver2D damageReceiver;
    [SerializeField] private PlayerParry2D parry;
    [SerializeField] private PlayerSpecialSkill2D specialSkill;
    [SerializeField] private PlayerInteractor2D interactor;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private bool debugLogStateChange;

    private readonly Dictionary<PlayerActionState, IPlayerActionStateHandler> actionHandlers = new();
    private readonly Dictionary<Collider2D, CollisionIgnoreRecord> ignoredCollisions = new();
    private readonly List<Collider2D> collisionReleaseBuffer = new();

    private PlayerActionRequest pendingRequests;
    private bool interactionObservedGameplayBlock;
    private bool isResetting;

    public PlayerLifeState LifeState { get; private set; } = PlayerLifeState.Alive;
    public PlayerActionState ActionState { get; private set; } = PlayerActionState.Neutral;
    public PlayerLocomotionState LocomotionState { get; private set; } = PlayerLocomotionState.Airborne;
    public PlayerActionState PreviousActionState { get; private set; } = PlayerActionState.Neutral;
    public PlayerActionRequest LastRequestedActions { get; private set; } = PlayerActionRequest.None;
    public PlayerActionRequest LastAcceptedRequest { get; private set; } = PlayerActionRequest.None;
    public PlayerActionRejectionReason LastRejectionReason { get; private set; }
    public string LastTransitionReason { get; private set; } = "Initialized";
    public int LastAcceptedPriority { get; private set; } = -1;
    public int TransitionCount { get; private set; }
    public int PauseRequestCount { get; private set; }
    public bool CombatSuspended { get; private set; }
    public int ActiveActionOwnerCount => ActionState == PlayerActionState.Neutral ? 0 : 1;
    public int ActiveCollisionIgnoreCount => ignoredCollisions.Count;
    public PlayerActionRequest PendingRequests => pendingRequests;

    // Compatibility view for the existing prototype Animator/debug overlay.
    public PlayerState CurrentState => EvaluateLegacyState();

    public bool BlocksStandardMovement =>
        LifeState == PlayerLifeState.Dead ||
        CombatSuspended ||
        DialogueController.GameplayInputBlocked ||
        ActionState == PlayerActionState.ParrySuccess ||
        ActionState == PlayerActionState.ParryFail ||
        ActionState == PlayerActionState.ParryCounter ||
        ActionState == PlayerActionState.OpportunityStrike ||
        ActionState == PlayerActionState.Special ||
        ActionState == PlayerActionState.Hit ||
        ActionState == PlayerActionState.Heal ||
        ActionState == PlayerActionState.Interact;

    public float RemainingLock
    {
        get
        {
            switch (ActionState)
            {
                case PlayerActionState.Attack:
                    return attack != null ? attack.StepTimeoutRemaining : 0f;
                case PlayerActionState.Dash:
                    return motor != null ? motor.DashRemaining : 0f;
                case PlayerActionState.Parry:
                    return parry != null ? parry.ParryRemaining : 0f;
                case PlayerActionState.ParrySuccess:
                    return parry != null ? parry.SuccessLockRemaining : 0f;
                case PlayerActionState.ParryFail:
                    return parry != null ? parry.FailLockRemaining : 0f;
                case PlayerActionState.Special:
                    return specialSkill != null ? specialSkill.LockRemaining : 0f;
                case PlayerActionState.Hit:
                    return damageReceiver != null ? damageReceiver.HitLockRemaining : 0f;
                default:
                    return actionHandlers.TryGetValue(ActionState, out IPlayerActionStateHandler handler)
                        ? handler.LockRemaining
                        : 0f;
            }
        }
    }

    public event Action PauseRequested;

    private void Awake()
    {
        ResolveReferences();
        DiscoverActionHandlers();
        LifeState = PlayerLifeState.Alive;
        ActionState = PlayerActionState.Neutral;
        PreviousActionState = PlayerActionState.Neutral;
        UpdateLocomotionState();
    }

    private void Update()
    {
        UpdateLocomotionState();
        UpdateInteractionState();
    }

    private void LateUpdate()
    {
        UpdateLocomotionState();
        ResolvePendingRequests();
    }

    public bool RequestAction(PlayerActionRequest requests)
    {
        PlayerActionRequest normalized = requests & PlayerActionRequest.All;
        if (normalized == PlayerActionRequest.None || !isActiveAndEnabled)
        {
            return false;
        }

        pendingRequests |= normalized;
        return true;
    }

    public bool RequestPause()
    {
        return RequestAction(PlayerActionRequest.Pause);
    }

    public void SetCombatSuspended(bool suspended)
    {
        CombatSuspended = suspended;
        if (suspended)
        {
            pendingRequests &= PlayerActionRequest.Pause;
        }
    }

    public void CompleteAction(PlayerActionState expectedAction, string reason)
    {
        if (isResetting || ActionState != expectedAction)
        {
            return;
        }

        TransitionTo(PlayerActionState.Neutral, reason, false);
    }

    public void CompleteParryAction(string reason)
    {
        if (ActionState == PlayerActionState.Parry ||
            ActionState == PlayerActionState.ParrySuccess ||
            ActionState == PlayerActionState.ParryFail)
        {
            TransitionTo(PlayerActionState.Neutral, reason, false);
        }
    }

    public void ChangeActionPhase(
        PlayerActionState expectedAction,
        PlayerActionState nextAction,
        string reason)
    {
        if (LifeState == PlayerLifeState.Dead ||
            ActionState != expectedAction ||
            !IsAllowedPhaseTransition(expectedAction, nextAction))
        {
            return;
        }

        TransitionTo(nextAction, reason, false);
    }

    public bool ForceHit(string reason)
    {
        if (LifeState == PlayerLifeState.Dead)
        {
            return false;
        }

        // Event-driven dialogue can be active while ActionState is still
        // Neutral, so it must be interrupted independently of Interact.
        DialogueController.InterruptActive();

        if (ActionState != PlayerActionState.Hit)
        {
            TransitionTo(PlayerActionState.Hit, reason, true);
        }

        pendingRequests = PlayerActionRequest.None;
        return true;
    }

    public void SetDead(string reason)
    {
        if (LifeState == PlayerLifeState.Dead)
        {
            return;
        }

        ResetToSafeState(reason, false);
        LastAcceptedRequest = PlayerActionRequest.None;
        LastRejectionReason = PlayerActionRejectionReason.Dead;
    }

    public void Revive(string reason = "Revived")
    {
        ResetToSafeState(reason, true);
    }

    public void RegisterActionHandler(IPlayerActionStateHandler handler)
    {
        if (handler == null || handler.ActionState == PlayerActionState.Neutral)
        {
            return;
        }

        actionHandlers[handler.ActionState] = handler;
    }

    public void UnregisterActionHandler(IPlayerActionStateHandler handler)
    {
        if (handler != null &&
            actionHandlers.TryGetValue(handler.ActionState, out IPlayerActionStateHandler current) &&
            ReferenceEquals(current, handler))
        {
            actionHandlers.Remove(handler.ActionState);
        }
    }

    public void AcquireCollisionIgnore(
        PlayerActionState owner,
        Collider2D ownCollider,
        Collider2D otherCollider)
    {
        if (ownCollider == null || otherCollider == null || ownCollider == otherCollider)
        {
            return;
        }

        if (!ignoredCollisions.TryGetValue(otherCollider, out CollisionIgnoreRecord record))
        {
            record = new CollisionIgnoreRecord { OwnCollider = ownCollider };
            ignoredCollisions.Add(otherCollider, record);
            Physics2D.IgnoreCollision(ownCollider, otherCollider, true);
        }

        record.Owners.Add(owner);
    }

    public void ReleaseCollisionIgnores(PlayerActionState owner)
    {
        collisionReleaseBuffer.Clear();
        foreach (KeyValuePair<Collider2D, CollisionIgnoreRecord> pair in ignoredCollisions)
        {
            pair.Value.Owners.Remove(owner);
            if (pair.Value.Owners.Count == 0)
            {
                collisionReleaseBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < collisionReleaseBuffer.Count; i++)
        {
            Collider2D other = collisionReleaseBuffer[i];
            if (!ignoredCollisions.TryGetValue(other, out CollisionIgnoreRecord record))
            {
                continue;
            }

            if (record.OwnCollider != null && other != null)
            {
                Physics2D.IgnoreCollision(record.OwnCollider, other, false);
            }

            ignoredCollisions.Remove(other);
        }

        collisionReleaseBuffer.Clear();
    }

    public void ResetToSafeState(string reason = "SafeReset", bool alive = true)
    {
        if (isResetting)
        {
            return;
        }

        isResetting = true;
        PlayerActionState interruptedAction = ActionState;

        attack?.CancelAttack(reason);
        motor?.CancelDashFromStateMachine(true);
        parry?.CancelParryFromStateMachine(true);
        specialSkill?.CancelSkillFromStateMachine();
        damageReceiver?.CancelHitFromStateMachine();
        DialogueController.InterruptActive();
        interactor?.CancelInteractionFromStateMachine();

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        foreach (IPlayerActionStateHandler handler in actionHandlers.Values)
        {
            handler?.CancelAction();
        }

        ReleaseAllCollisionIgnores();
        pendingRequests = PlayerActionRequest.None;
        CombatSuspended = false;
        interactionObservedGameplayBlock = false;
        PreviousActionState = interruptedAction;
        ActionState = PlayerActionState.Neutral;
        LifeState = alive ? PlayerLifeState.Alive : PlayerLifeState.Dead;
        LastAcceptedRequest = PlayerActionRequest.None;
        LastAcceptedPriority = -1;
        LastRejectionReason = PlayerActionRejectionReason.None;
        LastTransitionReason = reason;
        TransitionCount++;
        isResetting = false;
        UpdateLocomotionState();
        LogTransition(interruptedAction, PlayerActionState.Neutral, reason);
    }

    private void ResolvePendingRequests()
    {
        PlayerActionRequest requests = pendingRequests;
        pendingRequests = PlayerActionRequest.None;

        if (requests == PlayerActionRequest.None)
        {
            return;
        }

        LastRequestedActions = requests;
        LastAcceptedRequest = PlayerActionRequest.None;
        LastAcceptedPriority = -1;

        if (LifeState == PlayerLifeState.Dead)
        {
            ResolveBlockedRequests(requests, PlayerActionRejectionReason.Dead);
            return;
        }

        if (CombatSuspended)
        {
            ResolveBlockedRequests(requests, PlayerActionRejectionReason.CombatSuspended);
            return;
        }

        if (DialogueController.GameplayInputBlocked && ActionState != PlayerActionState.Interact)
        {
            ResolveBlockedRequests(requests, PlayerActionRejectionReason.GameplayBlocked);
            return;
        }

        PlayerActionRequest legalRequests = BuildLegalRequests();
        PlayerAttackCancelWindow cancelWindows = attack != null
            ? attack.OpenCancelWindows
            : PlayerAttackCancelWindow.None;
        PlayerActionDecision decision = PlayerActionResolver.Resolve(
            ActionState,
            requests,
            legalRequests,
            cancelWindows);

        RecordDecision(decision);
        if (decision.PauseRequested)
        {
            PauseRequestCount++;
            PauseRequested?.Invoke();
            return;
        }

        if (!decision.HasSelection)
        {
            return;
        }

        if (!TryExecute(decision))
        {
            LastAcceptedRequest = PlayerActionRequest.None;
            LastAcceptedPriority = -1;
            LastRejectionReason = PlayerActionRejectionReason.StartFailed;
        }
    }

    private void ResolveBlockedRequests(
        PlayerActionRequest requests,
        PlayerActionRejectionReason gameplayReason)
    {
        if ((requests & PlayerActionRequest.Pause) != 0)
        {
            LastAcceptedRequest = PlayerActionRequest.Pause;
            LastRejectionReason = (requests & PlayerActionRequest.Gameplay) != 0
                ? PlayerActionRejectionReason.PausePriority
                : PlayerActionRejectionReason.None;
            PauseRequestCount++;
            PauseRequested?.Invoke();
            return;
        }

        LastRejectionReason = gameplayReason;
    }

    private PlayerActionRequest BuildLegalRequests()
    {
        PlayerActionRequest legal = PlayerActionRequest.None;

        if (motor != null && motor.CanStartDash) legal |= PlayerActionRequest.Dash;
        if (parry != null && parry.CanStartParry) legal |= PlayerActionRequest.Parry;
        if (specialSkill != null && specialSkill.CanStartSkill) legal |= PlayerActionRequest.Special;
        if (attack != null && attack.CanStartAttackFromStateMachine) legal |= PlayerActionRequest.Attack;
        if (motor != null && motor.CanStartJump) legal |= PlayerActionRequest.Jump;
        if (interactor != null && interactor.CanStartInteraction()) legal |= PlayerActionRequest.Interact;

        if (LocomotionState == PlayerLocomotionState.Grounded &&
            actionHandlers.TryGetValue(PlayerActionState.Heal, out IPlayerActionStateHandler heal) &&
            heal.CanStartAction)
        {
            legal |= PlayerActionRequest.Heal;
        }

        return legal;
    }

    private bool TryExecute(PlayerActionDecision decision)
    {
        PlayerActionRequest request = decision.SelectedRequest;
        PlayerActionState nextAction = decision.NextAction;

        switch (request)
        {
            case PlayerActionRequest.Dash:
                if (motor == null || !motor.TryStartDashFromStateMachine()) return false;
                TransitionTo(nextAction, "AcceptedDash", true);
                return true;

            case PlayerActionRequest.Parry:
                if (parry == null || !parry.TryStartParryFromStateMachine()) return false;
                TransitionTo(nextAction, "AcceptedParry", true);
                return true;

            case PlayerActionRequest.Special:
                if (specialSkill == null || !specialSkill.TryStartSkillFromStateMachine()) return false;
                TransitionTo(nextAction, "AcceptedSpecial", true);
                return true;

            case PlayerActionRequest.Attack:
                if (attack == null || !attack.TryStartAttackFromStateMachine()) return false;
                TransitionTo(nextAction, "AcceptedAttack", true);
                return true;

            case PlayerActionRequest.Heal:
                if (!actionHandlers.TryGetValue(PlayerActionState.Heal, out IPlayerActionStateHandler heal) ||
                    !heal.TryStartAction()) return false;
                TransitionTo(nextAction, "AcceptedHeal", true);
                return true;

            case PlayerActionRequest.Jump:
                if (motor == null || !motor.TryStartJumpFromStateMachine()) return false;
                TransitionTo(nextAction, "AcceptedJump", true);
                LocomotionState = PlayerLocomotionState.Airborne;
                return true;

            case PlayerActionRequest.Interact:
                if (interactor == null || !interactor.TryStartInteractionFromStateMachine()) return false;
                TransitionTo(nextAction, "AcceptedInteract", true);
                interactionObservedGameplayBlock = DialogueController.GameplayInputBlocked;
                return true;

            default:
                return false;
        }
    }

    private void RecordDecision(PlayerActionDecision decision)
    {
        LastAcceptedRequest = decision.SelectedRequest;
        LastAcceptedPriority = PlayerActionResolver.GetPriority(decision.SelectedRequest);

        if (decision.PauseRequested &&
            (decision.RequestedActions & PlayerActionRequest.Gameplay) != PlayerActionRequest.None)
        {
            LastRejectionReason = PlayerActionRejectionReason.PausePriority;
        }
        else if (decision.UnavailableRequests != PlayerActionRequest.None)
        {
            LastRejectionReason = PlayerActionRejectionReason.Unavailable;
        }
        else if (decision.StateRejectedRequests != PlayerActionRequest.None)
        {
            LastRejectionReason = PlayerActionRejectionReason.CurrentActionLocked;
        }
        else if (decision.LowerPriorityRequests != PlayerActionRequest.None)
        {
            LastRejectionReason = PlayerActionRejectionReason.LowerPriority;
        }
        else
        {
            LastRejectionReason = PlayerActionRejectionReason.None;
        }
    }

    private void TransitionTo(
        PlayerActionState nextAction,
        string reason,
        bool cancelPrevious)
    {
        PlayerActionState previous = ActionState;
        if (previous == nextAction)
        {
            return;
        }

        if (cancelPrevious)
        {
            CancelActionExecutor(previous, false);
        }

        PreviousActionState = previous;
        ActionState = nextAction;
        LastTransitionReason = reason;
        TransitionCount++;

        if (nextAction != PlayerActionState.Interact)
        {
            interactionObservedGameplayBlock = false;
        }

        LogTransition(previous, nextAction, reason);
    }

    private void CancelActionExecutor(PlayerActionState action, bool clearPersistentState)
    {
        switch (action)
        {
            case PlayerActionState.Attack:
                attack?.CancelAttack("StateTransition");
                break;
            case PlayerActionState.Dash:
                motor?.CancelDashFromStateMachine(clearPersistentState);
                break;
            case PlayerActionState.Parry:
            case PlayerActionState.ParrySuccess:
            case PlayerActionState.ParryFail:
                parry?.CancelParryFromStateMachine(clearPersistentState);
                break;
            case PlayerActionState.Special:
                specialSkill?.CancelSkillFromStateMachine();
                break;
            case PlayerActionState.Hit:
                damageReceiver?.CancelHitFromStateMachine(clearPersistentState);
                break;
            case PlayerActionState.Interact:
                interactor?.CancelInteractionFromStateMachine();
                break;
            default:
                if (actionHandlers.TryGetValue(action, out IPlayerActionStateHandler handler))
                {
                    handler.CancelAction();
                }
                break;
        }
    }

    private void UpdateLocomotionState()
    {
        LocomotionState = motor != null && motor.IsGrounded
            ? PlayerLocomotionState.Grounded
            : PlayerLocomotionState.Airborne;
    }

    private void UpdateInteractionState()
    {
        if (ActionState != PlayerActionState.Interact)
        {
            return;
        }

        if (DialogueController.GameplayInputBlocked)
        {
            interactionObservedGameplayBlock = true;
            return;
        }

        if (interactionObservedGameplayBlock || !DialogueController.GameplayInputBlocked)
        {
            CompleteAction(PlayerActionState.Interact, "InteractionComplete");
        }
    }

    private PlayerState EvaluateLegacyState()
    {
        if (LifeState == PlayerLifeState.Dead)
        {
            return PlayerState.Hit;
        }

        switch (ActionState)
        {
            case PlayerActionState.Attack:
            case PlayerActionState.ParryCounter:
            case PlayerActionState.OpportunityStrike:
                return PlayerState.Attack;
            case PlayerActionState.Dash:
                return PlayerState.Dash;
            case PlayerActionState.Parry:
            case PlayerActionState.ParrySuccess:
                return PlayerState.Parry;
            case PlayerActionState.ParryFail:
                return PlayerState.ParryFail;
            case PlayerActionState.Special:
                return PlayerState.Special;
            case PlayerActionState.Hit:
                return PlayerState.Hit;
        }

        if (LocomotionState == PlayerLocomotionState.Airborne)
        {
            return motor != null && motor.Velocity.y > 0.05f ? PlayerState.Jump : PlayerState.Fall;
        }

        return motor != null && Mathf.Abs(motor.MoveInput) > 0.01f
            ? PlayerState.Move
            : PlayerState.Idle;
    }

    private void ResolveReferences()
    {
        if (motor == null) motor = GetComponent<PlayerMotor2D>();
        if (attack == null) attack = GetComponent<PlayerAttack2D>();
        if (damageReceiver == null) damageReceiver = GetComponent<PlayerDamageReceiver2D>();
        if (parry == null) parry = GetComponent<PlayerParry2D>();
        if (specialSkill == null) specialSkill = GetComponent<PlayerSpecialSkill2D>();
        if (interactor == null) interactor = GetComponent<PlayerInteractor2D>();
        if (body == null) body = GetComponent<Rigidbody2D>();
    }

    private void DiscoverActionHandlers()
    {
        actionHandlers.Clear();
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPlayerActionStateHandler handler)
            {
                RegisterActionHandler(handler);
            }
        }
    }

    private static bool IsAllowedPhaseTransition(
        PlayerActionState current,
        PlayerActionState next)
    {
        return (current == PlayerActionState.Parry &&
                (next == PlayerActionState.ParrySuccess || next == PlayerActionState.ParryFail)) ||
               (current == PlayerActionState.ParrySuccess &&
                next == PlayerActionState.ParryCounter);
    }

    private void ReleaseAllCollisionIgnores()
    {
        foreach (KeyValuePair<Collider2D, CollisionIgnoreRecord> pair in ignoredCollisions)
        {
            if (pair.Value.OwnCollider != null && pair.Key != null)
            {
                Physics2D.IgnoreCollision(pair.Value.OwnCollider, pair.Key, false);
            }
        }

        ignoredCollisions.Clear();
        collisionReleaseBuffer.Clear();
    }

    private void LogTransition(
        PlayerActionState previous,
        PlayerActionState next,
        string reason)
    {
        if (debugLogStateChange)
        {
            Debug.Log($"[PlayerAction] {previous} -> {next} ({reason})", this);
        }
    }

    private void OnDisable()
    {
        ResetToSafeState("ControllerDisabled", LifeState != PlayerLifeState.Dead);
    }

    private void OnDestroy()
    {
        ReleaseAllCollisionIgnores();
    }
}
