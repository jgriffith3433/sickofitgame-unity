using NUnit.Framework;
using SickOfIt.Core.Progression;

namespace SickOfIt.Tests.Editor
{
    /// <summary>The Hawkes Net reconnect policy (GhostLobby): capped backoff with jitter, 410 / repeated 4xx stop.</summary>
    public class NetRetryTests
    {
        [Test]
        public void BackoffDoublesFromTwoSecondsAndCapsAtSixty()
        {
            var r = new NetRetry(new System.Random(1));
            float[] basis = { 2, 4, 8, 16, 32, 60, 60 };
            for (int i = 0; i < basis.Length; i++)
            {
                float d = r.Failed("502");
                Assert.GreaterOrEqual(d, basis[i] * 0.75f - 0.001f, "step " + i);
                Assert.LessOrEqual(d, basis[i] * 1.25f + 0.001f, "step " + i);
            }
        }

        [Test]
        public void GoneStopsAtOnce()
        {
            Assert.AreEqual(-1f, new NetRetry().Failed("WebSocket 410 Gone"));
        }

        [Test]
        public void ThirdClientErrorInARowStopsButServerErrorsAndSlowDownDoNot()
        {
            var r = new NetRetry();
            Assert.Greater(r.Failed("ticket: 403"), 0f);
            Assert.Greater(r.Failed("ticket: 403"), 0f);
            Assert.AreEqual(-1f, r.Failed("ticket: 403"));
            var s = new NetRetry();
            for (int i = 0; i < 6; i++)
                Assert.Greater(s.Failed(i % 2 == 0 ? "429" : "502"), 0f);
        }

        [Test]
        public void SuccessStartsTheBackoffOver()
        {
            var r = new NetRetry(new System.Random(2));
            for (int i = 0; i < 5; i++) r.Failed("502");
            r.Succeeded();
            Assert.LessOrEqual(r.Failed("502"), 2.5f + 0.001f);
        }
    }
}
