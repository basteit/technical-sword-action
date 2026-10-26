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
        private sealed class SlowRequest { public double Scale; public int Remaining; }
        private readonly Dictionary<object, SlowRequest> slows = new();
        private readonly List<object> slowOwners = new();
        private double combatAccumulator;
        private readonly List<object> owners = new();
        private double accumulator;
        public long TickCount { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsHitStopped => hitstops.Count != 0;
        public int HitstopOwnerCount => hitstops.Count;
        public int SlowOwnerCount => slows.Count;
        public double SlowScale
        {
            get { double scale = 1d; foreach (var request in slows.Values) scale = Math.Min(scale, request.Scale); return scale; }
        }
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
            if (owner != null) { hitstops.Remove(owner); slows.Remove(owner); }
        }

        /// <summary>Duration uses unscaled 60 Hz slots, frozen by Pause and Hitstop. Lowest scale wins.</summary>
        public void RequestSlow(object owner, double scale, double realSeconds)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d || scale > 1d)
                throw new ArgumentOutOfRangeException(nameof(scale));
            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(realSeconds));
            slows[owner] = new SlowRequest { Scale = scale, Remaining = (int)Math.Ceiling(realSeconds / StepSeconds - 1e-7d) };
        }

        public void ReleaseSlow(object owner) { if (owner != null) slows.Remove(owner); }

        /// <summary>Release time requests without rewinding cooldown timestamps.</summary>
        public void ResetStops(bool clearRemainder = true)
        {
            hitstops.Clear();
            slows.Clear();
            combatAccumulator = 0d;
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

                double scale = SlowScale;
                slowOwners.Clear();
                slowOwners.AddRange(slows.Keys);
                foreach (object owner in slowOwners)
                    if (--slows[owner].Remaining <= 0) slows.Remove(owner);
                combatAccumulator += StepSeconds * scale;
                if (combatAccumulator + 1e-9d < StepSeconds) continue;
                combatAccumulator = Math.Max(0d, combatAccumulator - StepSeconds);
                TickCount++;
                tick(); // Time requests raised here affect the next wall slot, never half a combat tick.
            }
        }
    }
}
