using NUnit.Framework;
using UnityEngine;
using SickOfIt.Core.Rewind;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data.Config;

namespace SickOfIt.Tests.Editor
{
    public class RewindBudgetTests
    {
        static float Luminance(Color c)
        {
            float Lin(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Lin(Mathf.Clamp01(c.r)) + 0.7152f * Lin(Mathf.Clamp01(c.g)) + 0.0722f * Lin(Mathf.Clamp01(c.b));
        }

        static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        [Test]
        public void FloaterTimingAndContrastMeetReadabilityBar()
        {
            Assert.GreaterOrEqual(HudFloaters.HoldSeconds, 0.6f);
            Assert.AreEqual(0.3f, HudFloaters.PopSeconds, 0.05f);
            Assert.Greater(HudFloaters.Life, HudFloaters.HoldSeconds);

            var hudStyle = new HudStyleConfig();
            var pillOnSky = Color.Lerp(Color.white, HudFloaters.PillInk, HudFloaters.LastPillAlpha);
            var pillOnSlag = Color.Lerp(Color.black, HudFloaters.PillInk, HudFloaters.LastPillAlpha);
            foreach (var raw in new[] { hudStyle.rewind, hudStyle.rewindEmpty, hudStyle.healthFill, new Color(0.15f, 0.1f, 0.2f, 1f) })
            {
                var c = HudFloaters.EnsureReadableColor(raw);
                Assert.GreaterOrEqual(Contrast(c, pillOnSky), 5.0f);
                Assert.GreaterOrEqual(Contrast(c, pillOnSlag), 5.0f);
            }
        }

        [Test]
        public void DefaultsGiveAboutTenSecondsAndRewards()
        {
            var config = new RewindBudgetConfig();
            Assert.GreaterOrEqual(config.startSeconds, 10f);
            Assert.Greater(config.cleanKillRefund, 0f);
            Assert.Greater(config.survivalBonus, 0f);
        }

        [Test]
        public void RewardAddsUpToTheCapAndTellsTheHud()
        {
            var go = new GameObject("budget");
            try
            {
                var budget = go.AddComponent<RewindBudget>();
                budget.Configure(8f, 10f, 1f);
                float told = 0f;
                string label = null;
                budget.Rewarded += (s, l) => { told = s; label = l; };
                Assert.AreEqual(1f, budget.Reward(1f, "CLEAN KILL"), 0.001f);
                Assert.AreEqual(9f, budget.Juice, 0.001f);
                Assert.AreEqual(1f, told, 0.001f);
                Assert.AreEqual("CLEAN KILL", label);
                Assert.AreEqual(1f, budget.Reward(5f, "CLEAN KILL"), 0.001f);
                Assert.AreEqual(10f, budget.Juice, 0.001f);
                Assert.AreEqual(0f, budget.Reward(1f, "CLEAN KILL"), 0.001f);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
