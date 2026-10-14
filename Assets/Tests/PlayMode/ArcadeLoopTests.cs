using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.Gameplay;

namespace SickOfIt.Tests.PlayMode
{
    /// <summary>Rewind juice, rewind-orb drops, and upgrade offers.</summary>
    public class ArcadeLoopTests
    {
        const string UpgradesKey = "GameplayUpgrades";
        TestLevel level;
        string savedUpgrades;

        [SetUp]
        public void SetUp()
        {
            savedUpgrades = PlayerPrefs.GetString(UpgradesKey, null);
            UpgradeProgress.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            level?.Destroy();
            level = null;
            Time.timeScale = 1f;
            if (savedUpgrades != null)
                PlayerPrefs.SetString(UpgradesKey, savedUpgrades);
            else
                PlayerPrefs.DeleteKey(UpgradesKey);
        }

        static readonly Dictionary<string, string> Content = new Dictionary<string, string>
        {
            ["entities/orb.json"] = "{ 'schemaVersion': 1, 'team': 'Neutral', 'health': 0, 'body': { 'radius': 0.2, 'solid': false }, " +
                                    "'abilities': [ { 'type': 'collect', 'magnetRadius': 0, 'do': [ { 'type': 'addRewind', 'seconds': 1, 'text': '' } ] } ] }",
            ["entities/pinata.json"] = "{ 'schemaVersion': 1, 'health': 1, 'drops': [ { 'entity': 'orb', 'chance': 1, 'count': 2 } ] }",
            ["upgrades/fast.json"] = "{ 'schemaVersion': 1, 'title': 'Fast', 'maxLevel': 3, 'modifiersPerLevel': [ { 'stat': 'AttackSpeed', 'multiply': 2 } ] }",
        };

        static RewindBudgetConfig Budget(float start, float max) => new RewindBudgetConfig { enabled = true, startSeconds = start, maxSeconds = max, costPerSecond = 1f };

        [UnityTest]
        public IEnumerator RewindSpendsJuiceAndStopsWhenEmpty()
        {
            level = TestLevel.Create(TestLevel.BuildContent(Content), budget: Budget(1f, 3f));
            yield return level.Until(3f);
            Assert.That(RewindBudget.Instance.Juice, Is.EqualTo(1f));

            level.Clock.StartRewind(4f);
            yield return level.UntilCondition(() => !level.Clock.IsRewinding, 10f);

            Assert.That(RewindBudget.Instance.Juice, Is.EqualTo(0f));
            Assert.That(RewindBudget.Instance.CanRewind, Is.False);
            Assert.That(level.Clock.Time, Is.InRange(1.7f, 2.3f), "one second of juice rewinds about one second");
        }

        [UnityTest]
        public IEnumerator KilledEnemiesDropPickupsThatRefillJuice()
        {
            level = TestLevel.Create(TestLevel.BuildContent(Content), budget: Budget(0f, 5f));
            var pinata = level.World.Spawn("pinata", new Vector2(0.05f, 0f), new SpawnOptions());
            pinata.Damage(5, null);
            Assert.That(level.World.GetCounter("spawned:orb"), Is.EqualTo(2f), "chance 1, count 2");
            yield return level.Until(0.4f);
            Assert.That(RewindBudget.Instance.Juice, Is.EqualTo(2f), "both orbs land on the player and pay 1 each");
        }

        [UnityTest]
        public IEnumerator PickupsPayOnceEvenWhenARewindReplaysThem()
        {
            const string script = @"{ 'rules': [ { 'when': { 'type': 'atTime', 'seconds': 0.5 },
                                                   'do': [ { 'type': 'spawn', 'entity': 'orb', 'at': [0, 0] } ] } ] }";
            level = TestLevel.Create(TestLevel.BuildContent(Content, script), budget: Budget(0f, 5f));
            yield return level.Until(1.3f);
            Assert.That(RewindBudget.Instance.Juice, Is.EqualTo(1f).Within(0.01f), "collected once");

            yield return level.RewindTo(0.3f);
            float afterRewind = RewindBudget.Instance.Juice;
            Assert.That(afterRewind, Is.LessThan(0.2f), "rewinding about a second spent about a second");

            yield return level.Until(1.3f);
            Assert.That(level.World.GetCounter("spawned:orb"), Is.EqualTo(1f), "the replay spawned the orb again");
            Assert.That(RewindBudget.Instance.Juice, Is.EqualTo(afterRewind).Within(0.01f), "but picking it up again paid nothing");
        }

        [UnityTest]
        public IEnumerator UpgradeOfferPausesGrantsAndIsNotRepeatedByRewind()
        {
            const string script = @"{ 'rules': [ { 'when': { 'type': 'atTime', 'seconds': 0.5 },
                                                   'do': [ { 'type': 'offerUpgrades', 'choices': ['fast'] } ] } ] }";
            level = TestLevel.Create(TestLevel.BuildContent(Content, script));
            yield return level.UntilCondition(() => level.World.Hud.PickerOpen, 5f);
            Assert.That(level.World.Hud.PickerOpen, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "the game waits for the pick");

            level.World.Hud.Choose(0);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(UpgradeProgress.LevelOf("fast"), Is.EqualTo(1));
            Assert.That(level.World.Player.Stat(SickOfIt.Gameplay.Content.Stat.AttackSpeed, 1f), Is.EqualTo(2f));

            yield return level.Until(1.2f);
            yield return level.RewindTo(0.2f);
            yield return level.Until(1.0f);
            Assert.That(level.World.Hud.PickerOpen, Is.False, "replaying the offer moment doesn't offer again");
            Assert.That(UpgradeProgress.LevelOf("fast"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OwnedUpgradesApplyAtLevelStart()
        {
            UpgradeProgress.Add("fast");
            UpgradeProgress.Add("fast");
            level = TestLevel.Create(TestLevel.BuildContent(Content));
            yield return null;
            Assert.That(level.World.Player.Stat(SickOfIt.Gameplay.Content.Stat.AttackSpeed, 1f), Is.EqualTo(4f), "two levels of x2");
        }
    }
}
