namespace TechnicalSwordAction.PlayerState
{
    /// <summary>
    /// Stateless arbitration for player action requests. Callers own input buffering,
    /// resource/ground checks, state completion, and cancel-window timing.
    /// </summary>
    public static class PlayerActionResolver
    {
        private static readonly PlayerActionRequest[] Priority =
        {
            PlayerActionRequest.Dash,
            PlayerActionRequest.Parry,
            PlayerActionRequest.Special,
            PlayerActionRequest.Attack,
            PlayerActionRequest.Heal,
            PlayerActionRequest.Jump,
            PlayerActionRequest.Interact
        };

        private const PlayerActionRequest DefenseCancelRequests =
            PlayerActionRequest.Dash | PlayerActionRequest.Parry;

        private const PlayerActionRequest LateCancelRequests =
            PlayerActionRequest.Special | PlayerActionRequest.Jump;

        public static PlayerActionDecision Resolve(
            PlayerActionState currentAction,
            PlayerActionRequest requests,
            PlayerActionRequest legalRequests,
            PlayerAttackCancelWindow openCancelWindows)
        {
            PlayerActionRequest requestedActions = requests & PlayerActionRequest.All;
            PlayerActionRequest requestedGameplay = requestedActions & PlayerActionRequest.Gameplay;
            PlayerActionRequest legalGameplay = legalRequests & PlayerActionRequest.Gameplay;
            PlayerActionRequest availableRequests = requestedGameplay & legalGameplay;
            PlayerActionRequest unavailableRequests = requestedGameplay & ~legalGameplay;
            bool pauseRequested = (requestedActions & PlayerActionRequest.Pause) != 0;

            if (pauseRequested)
            {
                return new PlayerActionDecision(
                    currentAction,
                    currentAction,
                    PlayerActionRequest.Pause,
                    requestedActions,
                    unavailableRequests,
                    PlayerActionRequest.None,
                    requestedGameplay,
                    true);
            }

            PlayerActionRequest stateAllowedRequests = GetStateAllowedRequests(
                currentAction,
                openCancelWindows);
            PlayerActionRequest stateRejectedRequests = availableRequests & ~stateAllowedRequests;
            PlayerActionRequest candidates = availableRequests & stateAllowedRequests;
            PlayerActionRequest selectedRequest = SelectHighestPriority(candidates);
            PlayerActionRequest lowerPriorityRequests =
                candidates & ~selectedRequest;
            PlayerActionState nextAction = GetNextAction(currentAction, selectedRequest);

            return new PlayerActionDecision(
                currentAction,
                nextAction,
                selectedRequest,
                requestedActions,
                unavailableRequests,
                stateRejectedRequests,
                lowerPriorityRequests,
                false);
        }

        private static PlayerActionRequest GetStateAllowedRequests(
            PlayerActionState currentAction,
            PlayerAttackCancelWindow openCancelWindows)
        {
            if (currentAction == PlayerActionState.Neutral)
            {
                return PlayerActionRequest.Gameplay;
            }

            if (currentAction != PlayerActionState.Attack)
            {
                return PlayerActionRequest.None;
            }

            PlayerActionRequest result = PlayerActionRequest.None;
            if ((openCancelWindows & PlayerAttackCancelWindow.Defense) != 0)
            {
                result |= DefenseCancelRequests;
            }

            if ((openCancelWindows & PlayerAttackCancelWindow.Late) != 0)
            {
                result |= LateCancelRequests;
            }

            return result;
        }

        private static PlayerActionRequest SelectHighestPriority(PlayerActionRequest candidates)
        {
            for (int index = 0; index < Priority.Length; index++)
            {
                PlayerActionRequest request = Priority[index];
                if ((candidates & request) != 0)
                {
                    return request;
                }
            }

            return PlayerActionRequest.None;
        }

        public static int GetPriority(PlayerActionRequest request)
        {
            for (int index = 0; index < Priority.Length; index++)
            {
                if (Priority[index] == request)
                {
                    return index;
                }
            }

            return -1;
        }

        private static PlayerActionState GetNextAction(
            PlayerActionState currentAction,
            PlayerActionRequest selectedRequest)
        {
            switch (selectedRequest)
            {
                case PlayerActionRequest.Dash:
                    return PlayerActionState.Dash;
                case PlayerActionRequest.Parry:
                    return PlayerActionState.Parry;
                case PlayerActionRequest.Special:
                    return PlayerActionState.Special;
                case PlayerActionRequest.Attack:
                    return PlayerActionState.Attack;
                case PlayerActionRequest.Heal:
                    return PlayerActionState.Heal;
                case PlayerActionRequest.Jump:
                    return PlayerActionState.Neutral;
                case PlayerActionRequest.Interact:
                    return PlayerActionState.Interact;
                default:
                    return currentAction;
            }
        }
    }
}
