namespace TechnicalSwordAction.PlayerState
{
    /// <summary>
    /// Player lifetime state. Pause deliberately does not belong to combat state.
    /// </summary>
    public enum PlayerLifeState
    {
        Alive,
        Dead
    }

    /// <summary>
    /// The player's single, mutually exclusive combat action.
    /// </summary>
    public enum PlayerActionState
    {
        Neutral,
        Attack,
        Dash,
        Parry,
        ParrySuccess,
        ParryFail,
        ParryCounter,
        OpportunityStrike,
        Special,
        Hit,
        Heal,
        Interact
    }

    /// <summary>
    /// Ground/air state stored independently from <see cref="PlayerActionState"/>.
    /// </summary>
    public enum PlayerLocomotionState
    {
        Grounded,
        Airborne
    }
}
