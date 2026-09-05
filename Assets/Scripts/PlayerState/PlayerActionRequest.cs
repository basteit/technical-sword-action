using System;

namespace TechnicalSwordAction.PlayerState
{
    /// <summary>
    /// Action requests captured for one combat tick.
    /// </summary>
    [Flags]
    public enum PlayerActionRequest
    {
        None = 0,
        Dash = 1 << 0,
        Parry = 1 << 1,
        Special = 1 << 2,
        Attack = 1 << 3,
        Heal = 1 << 4,
        Jump = 1 << 5,
        Interact = 1 << 6,
        Pause = 1 << 7,

        Gameplay = Dash | Parry | Special | Attack | Heal | Jump | Interact,
        All = Gameplay | Pause
    }
}
