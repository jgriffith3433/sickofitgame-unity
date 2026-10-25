using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SickOfIt.Gameplay;

namespace SickOfIt.Tests.Editor
{
    /// <summary>The achievements list (ui/codex.json, Docs/Story.md "Achievements"): enough of them, each one reachable and in the game's tone.</summary>
    public class AchievementTests
    {
        static JArray Load() => (JArray)JObject.Parse(File.ReadAllText("Assets/GameConfig/ui/codex.json"))["achievements"];

        [Test]
        public void AtLeastFortyWithUniqueIds()
        {
            var list = Load();
            Assert.GreaterOrEqual(list.Count, 40);
            var ids = list.Select(a => (string)a["id"]).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "duplicate achievement ids");
            var names = list.Select(a => (string)a["name"]).ToList();
            Assert.AreEqual(names.Count, names.Distinct().Count(), "duplicate achievement names");
        }

        [Test]
        public void EveryAchievementWatchesACountedStat()
        {
            foreach (var a in Load())
            {
                Assert.IsTrue(AchievementTracker.IsKnownStat((string)a["stat"]), (string)a["id"] + " watches an unknown stat: " + (string)a["stat"]);
                Assert.GreaterOrEqual((int)(a["target"] ?? 1), 1, (string)a["id"]);
                Assert.IsFalse(string.IsNullOrEmpty((string)a["title"]), (string)a["id"] + " has no title");
                Assert.IsFalse(string.IsNullOrEmpty((string)a["description"]), (string)a["id"] + " has no description");
                if ((bool?)a["secret"] == true)
                    Assert.IsFalse(string.IsNullOrEmpty((string)a["hint"]), (string)a["id"] + " is secret without a hint");
            }
        }

        [Test]
        public void NoOfficeOrWorkJokes()
        {
            string[] banned = { "meeting", "inbox", "monday", "coffee", "office", "deadline", "email", "paycheck", "overtime", "manager" };
            foreach (var a in Load())
            {
                string text = string.Join(" ", new[] { "name", "description", "title", "hint" }.Select(k => (string)a[k] ?? "")).ToLowerInvariant();
                foreach (string word in banned)
                    Assert.IsFalse(text.Contains(word), (string)a["id"] + " mentions " + word);
            }
        }

        [Test]
        public void EveryPerEnemyStatNamesARealCreature()
        {
            var names = new HashSet<string>();
            foreach (string file in Directory.GetFiles("Assets/GameConfig/content/entities", "*.json"))
            {
                string name = (string)JObject.Parse(File.ReadAllText(file))["displayName"];
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }
            foreach (var a in Load())
            {
                string stat = (string)a["stat"];
                if (stat.StartsWith("kill:"))
                    Assert.IsTrue(names.Contains(stat.Substring(5)), (string)a["id"] + " counts kills of '" + stat.Substring(5) + "', which no entity is called");
            }
        }
    }
}
