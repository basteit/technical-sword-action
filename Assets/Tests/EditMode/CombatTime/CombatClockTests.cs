using System;
using NUnit.Framework;

namespace TechnicalSwordAction.CombatTime.Tests
{
    public sealed class CombatClockTests
    {
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void EqualElapsedTimeProducesExactlySixtyTicksPerSecond(int renderFps)
        {
            var clock = new CombatClock();
            int callbacks = 0;
            for (int frame = 0; frame < renderFps * 10; frame++)
                clock.Advance(1d / renderFps, () => callbacks++);
            Assert.That(clock.TickCount, Is.EqualTo(600));
            Assert.That(callbacks, Is.EqualTo(600));
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(10d));
        }

        [Test]
        public void SubTickRemaindersAccumulateWithoutEarlyExecution()
        {
            var clock = new CombatClock();
            clock.Advance(CombatClock.StepSeconds / 2d, () => { });
            Assert.That(clock.TickCount, Is.Zero);
            clock.Advance(CombatClock.StepSeconds / 2d, () => { });
            Assert.That(clock.TickCount, Is.EqualTo(1));
        }

        [Test]
        public void PauseDiscardsPartialFrameAndNeverAccruesCatchUpDebt()
        {
            var clock = new CombatClock();
            clock.Advance(CombatClock.StepSeconds / 2d, () => { });
            clock.SetPaused(true);
            clock.Advance(1000d, () => Assert.Fail("Paused combat tick"));
            clock.SetPaused(false);
            clock.Advance(CombatClock.StepSeconds / 2d, () => { });
            Assert.That(clock.TickCount, Is.Zero);
            clock.Advance(CombatClock.StepSeconds / 2d, () => { });
            Assert.That(clock.TickCount, Is.EqualTo(1));
        }

        [Test]
        public void PauseRaisedDuringCatchUpStopsRemainingCallbacks()
        {
            var clock = new CombatClock();
            clock.Advance(1d, () => clock.SetPaused(true));
            Assert.That(clock.TickCount, Is.EqualTo(1));
            clock.SetPaused(false);
            clock.Advance(CombatClock.StepSeconds, () => { });
            Assert.That(clock.TickCount, Is.EqualTo(2));
        }

        [Test]
        public void NestedHitstopUsesMaximumRemainingDurationNotSum()
        {
            var clock = new CombatClock();
            object shortOwner = new object(), longOwner = new object();
            clock.RequestHitstop(shortOwner, 2);
            clock.RequestHitstop(longOwner, 5);
            clock.RequestHitstop(longOwner, 1);
            clock.Advance(CombatClock.StepSeconds * 2, () => Assert.Fail("Hitstopped tick"));
            Assert.That(clock.HitstopOwnerCount, Is.EqualTo(1));
            Assert.That(clock.HitstopRemainingTicks, Is.EqualTo(3));
            clock.Advance(CombatClock.StepSeconds * 3, () => Assert.Fail("Hitstopped tick"));
            Assert.That(clock.IsHitStopped, Is.False);
            clock.Advance(CombatClock.StepSeconds, () => { });
            Assert.That(clock.TickCount, Is.EqualTo(1));
        }

        [Test]
        public void OwnerReleaseCannotCancelOtherOwnerOrPause()
        {
            var clock = new CombatClock();
            object a = new object(), b = new object();
            clock.RequestHitstop(a, 8);
            clock.RequestHitstop(b, 3);
            clock.SetPaused(true);
            clock.ReleaseOwner(a);
            clock.Advance(60d, () => Assert.Fail("Paused tick"));
            Assert.That(clock.IsPaused, Is.True);
            Assert.That(clock.HitstopRemainingTicks, Is.EqualTo(3));
            clock.ReleaseOwner(b);
            Assert.That(clock.IsPaused, Is.True);
            Assert.That(clock.IsHitStopped, Is.False);
        }

        [Test]
        public void HitstopRaisedByATickStartsWithTheNextTick()
        {
            var clock = new CombatClock();
            object owner = new object();
            int callbacks = 0;
            clock.Advance(CombatClock.StepSeconds * 5, () =>
            {
                callbacks++;
                if (callbacks == 1) clock.RequestHitstop(owner, 3);
            });
            Assert.That(callbacks, Is.EqualTo(2));
            Assert.That(clock.TickCount, Is.EqualTo(2));
        }

        [Test]
        public void OneHundredResetsReleaseStopsAndKeepTimestampsMonotonic()
        {
            var clock = new CombatClock();
            for (int iteration = 1; iteration <= 100; iteration++)
            {
                clock.RequestHitstop(new object(), 100);
                clock.SetPaused(true);
                clock.ResetStops();
                Assert.That(clock.IsPaused, Is.False, $"reset {iteration}");
                Assert.That(clock.HitstopOwnerCount, Is.Zero, $"reset {iteration}");
                clock.Advance(CombatClock.StepSeconds, () => { });
                Assert.That(clock.TickCount, Is.EqualTo(iteration));
            }
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidElapsedTimeIsRejected(double elapsed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatClock().Advance(elapsed, () => { }));
        }

        [Test]
        public void NullCallbackAndOwnerAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new CombatClock().Advance(0d, null));
            Assert.Throws<ArgumentNullException>(() => new CombatClock().RequestHitstop(null, 1));
        }
    }
}
