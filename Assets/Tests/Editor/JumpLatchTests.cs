using NUnit.Framework;
using SickOfIt.Core.Input;

namespace SickOfIt.Tests.Editor
{
    public class JumpLatchTests
    {
        const int Buffer = 6; // 0.12 s at 50 Hz

        [Test]
        public void APressIsConsumedOnce()
        {
            var latch = new JumpLatch();
            latch.Press(1);
            Assert.IsTrue(latch.Pending(1, Buffer));
            latch.Consume();
            Assert.IsFalse(latch.Pending(1, Buffer), "one tap, one jump");
        }

        [Test]
        public void TwoPressesInOneFrameStayTwo()
        {
            var latch = new JumpLatch();
            latch.Press(5);
            latch.Press(5);
            Assert.AreEqual(2, latch.Count);
            latch.Consume();
            Assert.IsTrue(latch.Pending(5, Buffer));
        }

        [Test]
        public void ABufferedPressSurvivesSixTicksButNotSeven()
        {
            var latch = new JumpLatch();
            latch.Press(10);
            Assert.IsTrue(latch.Pending(16, Buffer));
            Assert.IsFalse(latch.Pending(17, Buffer));
            Assert.AreEqual(0, latch.Count);
        }

        [Test]
        public void AHitchCannotDropAPress()
        {
            // The press is stamped for the next tick; a long frame runs ticks afterwards, and the first one sees it.
            var latch = new JumpLatch();
            latch.Press(100);
            Assert.IsTrue(latch.Pending(100, Buffer));
            Assert.IsTrue(latch.Fresh(100));
        }

        [Test]
        public void ClearDropsEverything()
        {
            var latch = new JumpLatch();
            latch.Press(1);
            latch.Press(1);
            latch.Clear();
            Assert.IsFalse(latch.Pending(1, Buffer));
            Assert.IsFalse(latch.Fresh(1));
        }

        [Test]
        public void FreshIsOnlyTheTickThePressLandedOn()
        {
            var latch = new JumpLatch();
            latch.Press(3);
            Assert.IsFalse(latch.Fresh(4));
        }
    }
}
