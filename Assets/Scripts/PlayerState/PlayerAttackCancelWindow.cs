using System;

namespace TechnicalSwordAction.PlayerState
{
    /// <summary>
    /// Attack cancel categories currently open. Timing and per-step data belong to Issue #48.
    /// </summary>
    [Flags]
    public enum PlayerAttackCancelWindow
    {
        None = 0,
        Defense = 1 << 0,
        Late = 1 << 1,
        All = Defense | Late
    }
}
