using System;
using NUnit.Framework;

namespace TechnicalSwordAction.CombatTime.Tests
{
    public sealed class CombatSlowTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void SlowAdvancesTheSameCombatTicksAtEveryRenderRate(int fps)
        {
            var c = new CombatClock(); object owner = new object();
            c.RequestSlow(owner, .25d, 2d);
            for (int i = 0; i < fps * 2; i++) c.Advance(1d/fps, () => { });
            Assert.That(c.TickCount, Is.EqualTo(30));
            Assert.That(c.SlowOwnerCount, Is.Zero);
            for (int i = 0; i < fps; i++) c.Advance(1d/fps, () => { });
            Assert.That(c.TickCount, Is.EqualTo(90));
        }
        [Test]
        public void PauseAndHitstopFreezeSlowLifetimeAndNestedOwnersReleaseSeparately()
        {
            var c = new CombatClock(); object a = new object(), b = new object(), stop = new object();
            c.RequestSlow(a,.25d,1d); c.RequestSlow(b,.5d,2d);
            c.SetPaused(true); c.Advance(5d, () => Assert.Fail());
            c.SetPaused(false); c.RequestHitstop(stop,60);
            c.Advance(1d, () => Assert.Fail());
            Assert.That(c.SlowScale,Is.EqualTo(.25d));
            c.ReleaseSlow(a); Assert.That(c.SlowScale,Is.EqualTo(.5d));
            c.Advance(1d, () => { }); Assert.That(c.TickCount,Is.EqualTo(30));
            c.ReleaseOwner(b); Assert.That(c.SlowScale,Is.EqualTo(1d));
            c.ResetStops(); Assert.That(c.SlowOwnerCount,Is.Zero);
        }
        [Test]
        public void SlowRaisedAndReleasedInsideTicksAffectsOnlyWholeTicks()
        {
            var c = new CombatClock(); object owner = new object();
            c.Advance(CombatClock.StepSeconds * 9, () =>
            {
                if (c.TickCount == 1) c.RequestSlow(owner,.25d,1d);
                if (c.TickCount == 3) c.ReleaseSlow(owner);
            });
            Assert.That(c.TickCount,Is.EqualTo(3));
            c.Advance(CombatClock.StepSeconds, () => { });
            Assert.That(c.TickCount,Is.EqualTo(4));
        }
        [TestCase(0d)] [TestCase(-.1d)] [TestCase(2d)] [TestCase(double.NaN)]
        public void InvalidScaleIsRejected(double scale)
        { Assert.Throws<ArgumentOutOfRangeException>(() => new CombatClock().RequestSlow(new object(),scale,1)); }
    }
}
