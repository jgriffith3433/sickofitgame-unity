using NUnit.Framework;
using UnityEngine;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;

namespace SickOfIt.Tests.Editor
{
    /// <summary>The incoming warnings' maths: where something crosses the view's edge, and how a crowd folds into a few markers.</summary>
    public class IncomingWarningsTests
    {
        static readonly Rect View = new Rect(-7f, -4f, 14f, 8f);
        const float Lookahead = 1.5f;

        static bool Cross(Vector2 at, Vector2 velocity, out EdgeSide side, out float along, out float eta) =>
            IncomingSet.Crossing(View, at, velocity, Lookahead, out side, out along, out eta);

        [Test]
        public void EntersFromEachEdgeAtTheRightPoint()
        {
            Assert.IsTrue(Cross(new Vector2(0f, 8f), new Vector2(0f, -4f), out var side, out float along, out float eta));
            Assert.AreEqual(EdgeSide.Top, side);
            Assert.AreEqual(0.5f, along, 0.001f);
            Assert.AreEqual(1f, eta, 0.001f);

            Assert.IsTrue(Cross(new Vector2(3.5f, -7f), new Vector2(0f, 4f), out side, out along, out eta));
            Assert.AreEqual(EdgeSide.Bottom, side);
            Assert.AreEqual(0.75f, along, 0.001f);
            Assert.AreEqual(0.75f, eta, 0.001f);

            Assert.IsTrue(Cross(new Vector2(-9f, 0f), new Vector2(4f, 0f), out side, out along, out eta));
            Assert.AreEqual(EdgeSide.Left, side);
            Assert.AreEqual(0.5f, along, 0.001f);
            Assert.AreEqual(0.5f, eta, 0.001f);

            Assert.IsTrue(Cross(new Vector2(9f, 2f), new Vector2(-4f, 0f), out side, out along, out eta));
            Assert.AreEqual(EdgeSide.Right, side);
            Assert.AreEqual(0.75f, along, 0.001f);
            Assert.AreEqual(0.5f, eta, 0.001f);
        }

        [Test]
        public void ADiagonalCrossesTheEdgeItReachesFirst()
        {
            // From above and to the left, heading down-right: it reaches y = 4 (the top) at x = -6, inside the sides.
            Assert.IsTrue(Cross(new Vector2(-7.5f, 5f), new Vector2(1f, -1f), out var side, out float along, out float eta));
            Assert.AreEqual(EdgeSide.Top, side);
            Assert.AreEqual(1f, eta, 0.001f);
            Assert.AreEqual((-6.5f + 7f) / 14f, along, 0.001f);
        }

        [Test]
        public void MovingAwayPassingByStandingOrTooFarGetsNothing()
        {
            Assert.IsFalse(Cross(new Vector2(0f, 6f), new Vector2(0f, 4f), out _, out _, out _), "moving away");
            Assert.IsFalse(Cross(new Vector2(-9f, 6f), new Vector2(4f, 0f), out _, out _, out _), "passes above the view");
            Assert.IsFalse(Cross(new Vector2(0f, 6f), Vector2.zero, out _, out _, out _), "standing still");
            Assert.IsFalse(Cross(new Vector2(0f, 20f), new Vector2(0f, -4f), out _, out _, out _), "arrives in 4 s, past the lookahead");
            Assert.IsFalse(Cross(new Vector2(0f, 0f), new Vector2(0f, -4f), out _, out _, out _), "already in view");
            Assert.IsFalse(Cross(new Vector2(-9f, 0f), new Vector2(-4f, 0f), out _, out _, out _), "leaving to the left");
        }

        [Test]
        public void TheLookaheadIsTheLimit()
        {
            Assert.IsTrue(Cross(new Vector2(0f, 4f + 5.9f), new Vector2(0f, -4f), out _, out _, out float eta));
            Assert.Less(eta, Lookahead);
            Assert.IsFalse(Cross(new Vector2(0f, 4f + 6.1f), new Vector2(0f, -4f), out _, out _, out _));
        }

        static IncomingSet Fill(int count, float spread, float height = 7f, float speed = 4f, int cap = 4, float merge = 0.3f)
        {
            var set = new IncomingSet();
            set.Begin(View, Lookahead, merge, cap);
            for (int i = 0; i < count; i++)
            {
                float f = count == 1 ? 0.5f : i / (float)(count - 1);
                set.Add(new Vector2(Mathf.Lerp(-spread, spread, f), 4f + height), new Vector2(0f, -speed), false);
            }
            set.End();
            return set;
        }

        [Test]
        public void ARainOfTenIsOneBand()
        {
            var set = Fill(10, 6f, height: 4f);
            Assert.AreEqual(10, set.Considered);
            Assert.AreEqual(1, set.Count);
            Assert.AreEqual(10, set.Markers[0].Count);
            Assert.AreEqual(EdgeSide.Top, set.Markers[0].Side);
            Assert.Greater(set.Markers[0].SpanMax - set.Markers[0].SpanMin, 0.5f, "the band spans the rain");
        }

        [Test]
        public void MarkersFarApartStaySeparateAndAreCappedSoonestFirst()
        {
            var set = new IncomingSet();
            set.Begin(View, Lookahead, 0.1f, 4);
            // Six tight groups along the top, each a different distance away.
            for (int i = 0; i < 6; i++)
            {
                float x = -6.5f + i * 2.6f;
                set.Add(new Vector2(x, 4f + 2f + i * 0.5f), new Vector2(0f, -4f), false);
            }
            set.End();
            Assert.AreEqual(4, set.Count, "capped");
            for (int i = 1; i < set.Count; i++)
                Assert.LessOrEqual(set.Markers[i - 1].Eta, set.Markers[i].Eta, "soonest arrival first");
            Assert.AreEqual(0.5f, set.Markers[0].Eta, 0.001f);
            Assert.AreEqual(0.875f, set.Markers[3].Eta, 0.001f, "the two farthest were dropped");
        }

        [Test]
        public void EdgesNeverMergeAndEachKeepsItsKind()
        {
            var set = new IncomingSet();
            set.Begin(View, Lookahead, 0.3f, 4);
            set.Add(new Vector2(0f, 8f), new Vector2(0f, -4f), false);
            set.Add(new Vector2(-9f, 0f), new Vector2(4f, 0f), true);
            set.Add(new Vector2(9f, 0f), new Vector2(-4f, 0f), false);
            set.Add(new Vector2(0f, -8f), new Vector2(0f, 4f), false);
            set.End();
            Assert.AreEqual(4, set.Count);
            foreach (var m in set.Markers)
                Assert.AreEqual(m.Side == EdgeSide.Left, m.Creature, $"{m.Side}");
        }

        [Test]
        public void OneThingAfterAnotherKeepsTheSoonestEta()
        {
            var set = new IncomingSet();
            set.Begin(View, Lookahead, 0.3f, 4);
            set.Add(new Vector2(0f, 4f + 5f), new Vector2(0f, -4f), false); // 1.25 s
            set.Add(new Vector2(0.5f, 4f + 2f), new Vector2(0f, -4f), true); // 0.5 s
            set.End();
            Assert.AreEqual(1, set.Count);
            Assert.AreEqual(0.5f, set.Markers[0].Eta, 0.001f);
            Assert.IsTrue(set.Markers[0].Creature, "the soonest decides the tint");
            Assert.AreEqual(2, set.Markers[0].Count);
        }

        [Test]
        public void TheSetIsRebuiltFromScratchEveryFrame()
        {
            var set = Fill(10, 6f, height: 4f);
            Assert.Greater(set.Count, 0);
            set.Begin(View, Lookahead, 0.3f, 4);
            set.End();
            Assert.AreEqual(0, set.Count, "nothing kept between frames: a rewind needs no saved state");
        }

        [Test]
        public void OpacityRisesFromFarToNear()
        {
            var cfg = new IncomingWarningsConfig();
            Assert.AreEqual(cfg.farAlpha, IncomingSet.AlphaFor(cfg.lookaheadSeconds, cfg), 0.001f);
            Assert.AreEqual(cfg.nearAlpha, IncomingSet.AlphaFor(cfg.nearSeconds, cfg), 0.001f);
            Assert.AreEqual(cfg.nearAlpha, IncomingSet.AlphaFor(0f, cfg), 0.001f);
            float middle = IncomingSet.AlphaFor((cfg.lookaheadSeconds + cfg.nearSeconds) * 0.5f, cfg);
            Assert.Greater(middle, cfg.farAlpha);
            Assert.Less(middle, cfg.nearAlpha);
        }

        [Test]
        public void TheDefaultConfigIsValidAndBadOnesAreCaught()
        {
            Assert.IsEmpty(new IncomingWarningsConfig().Validate());
            Assert.IsNotEmpty(new IncomingWarningsConfig { maxMarkers = 0 }.Validate());
            Assert.IsNotEmpty(new IncomingWarningsConfig { lookaheadSeconds = 0.1f }.Validate());
            Assert.IsNotEmpty(new IncomingWarningsConfig { farAlpha = 0.9f, nearAlpha = 0.3f }.Validate());
            Assert.IsNotEmpty(new IncomingWarningsConfig { nearSeconds = 2f }.Validate());
        }
    }
}
