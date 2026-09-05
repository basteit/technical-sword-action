using System;

namespace TechnicalSwordAction.PlayerState
{
    /// <summary>
    /// Immutable result of resolving all requests for one combat tick.
    /// </summary>
    public readonly struct PlayerActionDecision : IEquatable<PlayerActionDecision>
    {
        public PlayerActionDecision(
            PlayerActionState currentAction,
            PlayerActionState nextAction,
            PlayerActionRequest selectedRequest,
            PlayerActionRequest requestedActions,
            PlayerActionRequest unavailableRequests,
            PlayerActionRequest stateRejectedRequests,
            PlayerActionRequest lowerPriorityRequests,
            bool pauseRequested)
        {
            CurrentAction = currentAction;
            NextAction = nextAction;
            SelectedRequest = selectedRequest;
            RequestedActions = requestedActions;
            UnavailableRequests = unavailableRequests;
            StateRejectedRequests = stateRejectedRequests;
            LowerPriorityRequests = lowerPriorityRequests;
            PauseRequested = pauseRequested;
        }

        public PlayerActionState CurrentAction { get; }

        public PlayerActionState NextAction { get; }

        public PlayerActionRequest SelectedRequest { get; }

        public PlayerActionRequest RequestedActions { get; }

        public PlayerActionRequest UnavailableRequests { get; }

        public PlayerActionRequest StateRejectedRequests { get; }

        public PlayerActionRequest LowerPriorityRequests { get; }

        public bool PauseRequested { get; }

        public bool HasSelection => SelectedRequest != PlayerActionRequest.None;

        public bool HasActionTransition => NextAction != CurrentAction;

        public PlayerActionRequest RejectedRequests =>
            UnavailableRequests | StateRejectedRequests | LowerPriorityRequests;

        public bool Equals(PlayerActionDecision other)
        {
            return CurrentAction == other.CurrentAction &&
                NextAction == other.NextAction &&
                SelectedRequest == other.SelectedRequest &&
                RequestedActions == other.RequestedActions &&
                UnavailableRequests == other.UnavailableRequests &&
                StateRejectedRequests == other.StateRejectedRequests &&
                LowerPriorityRequests == other.LowerPriorityRequests &&
                PauseRequested == other.PauseRequested;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerActionDecision other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)CurrentAction;
                hashCode = (hashCode * 397) ^ (int)NextAction;
                hashCode = (hashCode * 397) ^ (int)SelectedRequest;
                hashCode = (hashCode * 397) ^ (int)RequestedActions;
                hashCode = (hashCode * 397) ^ (int)UnavailableRequests;
                hashCode = (hashCode * 397) ^ (int)StateRejectedRequests;
                hashCode = (hashCode * 397) ^ (int)LowerPriorityRequests;
                hashCode = (hashCode * 397) ^ PauseRequested.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(PlayerActionDecision left, PlayerActionDecision right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerActionDecision left, PlayerActionDecision right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{CurrentAction} -> {NextAction}, Selected={SelectedRequest}, " +
                $"Unavailable={UnavailableRequests}, StateRejected={StateRejectedRequests}, " +
                $"LowerPriority={LowerPriorityRequests}";
        }
    }
}
