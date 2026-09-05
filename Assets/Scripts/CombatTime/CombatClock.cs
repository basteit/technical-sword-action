using System;
using System.Collections.Generic;

namespace TechnicalSwordAction.CombatTime
{
    /// <summary>A render-independent 60 Hz scheduler. Pausing never accrues catch-up debt.</summary>
    public sealed class CombatClock
    {
        public const int TickRate = 60;
        public const double StepSeconds = 1d / TickRate;
        private readonly Dictionary<object, int> hitstops = new();
        private readonly List<object> owners = new();
        private double accumulator;
        public long TickCount { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsHitStopped => hitstops.Count != 0;
        public int HitstopOwnerCount => hitstops.Count;
        public double ElapsedSeconds => TickCount * StepSeconds;
        public int HitstopRemainingTicks
        {
            get
            {
                int remaining = 0;
                foreach (int value in hitstops.Values) remaining = Math.Max(remaining, value);
                return remaining;
            }
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            accumulator = 0d;
        }

        public void RequestHitstop(object owner, int ticks)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (ticks <= 0) return;
            hitstops.TryGetValue(owner, out int existing);
            hitstops[owner] = Math.Max(existing, ticks);
        }

        public void ReleaseOwner(object owner)
        {
            if (owner != null) hitstops.Remove(owner);
        }

        /// <summary>Release time requests without rewinding cooldown timestamps.</summary>
        public void ResetStops(bool clearRemainder = true)
        {
            hitstops.Clear();
            IsPaused = false;
            if (clearRemainder) accumulator = 0d;
        }

        public void Advance(double unscaledSeconds, Action tick)
        {
            if (double.IsNaN(unscaledSeconds) || double.IsInfinity(unscaledSeconds) || unscaledSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(unscaledSeconds));
            if (tick == null) throw new ArgumentNullException(nameof(tick));
            if (IsPaused) return;

            accumulator += unscaledSeconds;
            while (accumulator + 1e-9d >= StepSeconds && !IsPaused)
            {
                accumulator = Math.Max(0d, accumulator - StepSeconds);
                if (IsHitStopped)
                {
                    owners.Clear();
                    owners.AddRange(hitstops.Keys);
                    foreach (object owner in owners)
                    {
                        int remaining = hitstops[owner] - 1;
                        if (remaining == 0) hitstops.Remove(owner);
                        else hitstops[owner] = remaining;
                    }
                    continue;
                }

                TickCount++;
                tick(); // Requests raised here affect the next tick, not half of this one.
            }
        }
    }
}
