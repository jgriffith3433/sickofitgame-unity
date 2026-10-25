using NUnit.Framework;
using UnityEngine;
using SickOfIt.Core.Rewind;
using SickOfIt.Data.Config;

namespace SickOfIt.Tests.Editor
{
    public class RewindBudgetTests
    {
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
