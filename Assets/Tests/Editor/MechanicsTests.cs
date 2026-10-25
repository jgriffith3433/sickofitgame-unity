using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Tests.Editor
{
    /// <summary>The five JSON mechanics: bouncing shots, teleport, front shield, sweeping laser, predictive aim.</summary>
    public class MechanicsTests
    {
        // ─── Predictive aim ───────────────────────────────────────────

        [Test]
        public void LeadDirectionMeetsAMovingTarget()
        {
            var origin = Vector2.zero;
            var target = new Vector2(6f, 0f);
            var velocity = new Vector2(0f, 2f);
            const float speed = 5f;
            Vector2 dir = Emission.LeadDirection(target, velocity, origin, speed);
            // Fly the shot and the target until the shot's distance matches the time: they must meet.
            float t = 0f;
            for (; t < 5f; t += 0.001f)
            {
                if ((target + velocity * t - dir * speed * t).magnitude < 0.01f)
                    break;
            }
            Assert.That(t, Is.LessThan(5f), "the led shot never meets the target");
            Assert.That(dir.y, Is.GreaterThan(0.1f), "it must aim ahead of a target climbing away");
        }

        [Test]
        public void LeadDirectionOfAStandingTargetIsStraightAtIt()
        {
            Vector2 dir = Emission.LeadDirection(new Vector2(3f, 4f), Vector2.zero, Vector2.zero, 6f);
            Assert.That(Vector2.Distance(dir, new Vector2(0.6f, 0.8f)), Is.LessThan(0.001f));
        }

        [Test]
        public void LeadDirectionFallsBackToStraightWhenTheTargetOutrunsTheShot()
        {
            // Running away at twice the shot's speed: no intercept.
            Vector2 dir = Emission.LeadDirection(new Vector2(4f, 0f), new Vector2(10f, 0f), Vector2.zero, 5f);
            Assert.That(Vector2.Distance(dir, Vector2.right), Is.LessThan(0.001f));
        }

        // ─── Front shield ─────────────────────────────────────────────

        [Test]
        public void ShieldCoversItsFrontAndNotItsBack()
        {
            var at = new Vector2(2f, 0f);
            Assert.That(FrontShieldAbility.Covers(at, 1f, new Vector2(5f, 0f), 90f), Is.True, "in front, facing right");
            Assert.That(FrontShieldAbility.Covers(at, 1f, new Vector2(-1f, 0f), 90f), Is.False, "behind, facing right");
            Assert.That(FrontShieldAbility.Covers(at, -1f, new Vector2(-1f, 0f), 90f), Is.True, "in front, facing left");
            Assert.That(FrontShieldAbility.Covers(at, -1f, new Vector2(5f, 1f), 90f), Is.False, "behind, facing left");
            // A narrow shield lets a shot from well above the front through.
            Assert.That(FrontShieldAbility.Covers(at, 1f, new Vector2(2.5f, 3f), 45f), Is.False);
            Assert.That(FrontShieldAbility.Covers(at, 1f, new Vector2(5f, 1f), 45f), Is.True);
        }

        // ─── Laser beam ───────────────────────────────────────────────

        [Test]
        public void BeamSegmentHitsABoxOnlyWhereItPasses()
        {
            var box = new Bounds(new Vector3(3f, 0f, 0f), new Vector3(1f, 2f, 1f));
            Assert.That(MechanicGeometry.SegmentHitsBounds(new Vector2(0f, 0f), new Vector2(6f, 0f), box), Is.True, "straight through");
            Assert.That(MechanicGeometry.SegmentHitsBounds(new Vector2(0f, 3f), new Vector2(6f, 3f), box), Is.False, "passes above");
            Assert.That(MechanicGeometry.SegmentHitsBounds(new Vector2(0f, 0f), new Vector2(2f, 0f), box), Is.False, "stops short");
            Assert.That(MechanicGeometry.SegmentHitsBounds(new Vector2(3f, -5f), new Vector2(3f, 5f), box), Is.True, "vertical through");
            Assert.That(MechanicGeometry.SegmentHitsBounds(new Vector2(0f, -2f), new Vector2(6f, 2f), box), Is.True, "diagonal through");
        }

        // ─── The test enemies ─────────────────────────────────────────

        static GameContent LoadFromDisk(params string[] ids)
        {
            var sources = new List<GameContent.FileSource>();
            var seen = new HashSet<string>();
            var queue = new Queue<string>(ids);
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                if (!seen.Add(id))
                    continue;
                string path = Path.Combine("Assets", "GameConfig", "content", "entities", id + ".json");
                Assert.That(File.Exists(path), Is.True, path);
                var json = JToken.Parse(File.ReadAllText(path));
                sources.Add(new GameContent.FileSource { Path = "entities/" + id + ".json", DisplayPath = path, Json = json });
                if (json["extends"] != null)
                    queue.Enqueue((string)json["extends"]);
                // Entities the abilities and death actions name (a marker, a shot) come along.
                foreach (var token in json.SelectTokens("$..marker").Concat(json.SelectTokens("$..projectile")).Concat(json.SelectTokens("$..entity")))
                {
                    if (token.Type == JTokenType.String && !string.IsNullOrEmpty((string)token))
                        queue.Enqueue((string)token);
                }
            }
            var noAssets = new RegistryAssetResolver(null);
            var content = GameContent.Build(sources, new List<GameContent.ScriptSource>(), noAssets, noAssets);
            ContentValidator.Validate(content, content.Issues);
            return content;
        }

        [TestCase("ricochetImp", "ricochetEmber")]
        [TestCase("blinkImp", "blinkMark")]
        [TestCase("bulwarkImp", null)]
        [TestCase("leadImp", null)]
        [TestCase("beamLens", null)]
        public void EachTestEnemyLoadsWithoutErrors(string id, string also)
        {
            var content = also == null ? LoadFromDisk(id) : LoadFromDisk(id, also);
            // Sprite sheets are not in this test's (empty) asset registry; every other error counts.
            var errors = content.Issues.Where(i => i.Severity == SickOfIt.Json.JsonIssueSeverity.Error && !i.Message.Contains("isn't in this build")).ToList();
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            Assert.That(content.Entities.ContainsKey(id), Is.True);
        }

        [Test]
        public void EachMechanicHasATestEnemyThatUsesIt()
        {
            var content = LoadFromDisk("ricochetImp", "blinkImp", "bulwarkImp", "leadImp", "beamLens");
            bool Has<T>(string id) where T : Ability => content.Entities[id].abilities.OfType<T>().Any();

            var bounceShot = content.Entities["ricochetEmber"].abilities.OfType<ProjectileAbility>().Single();
            Assert.That(bounceShot.bounces, Is.GreaterThan(0), "bouncing projectile");
            Assert.That(Has<TeleportAbility>("blinkImp"), Is.True, "teleport");
            Assert.That(Has<FrontShieldAbility>("bulwarkImp"), Is.True, "front shield");
            Assert.That(Has<LaserSweepAbility>("beamLens"), Is.True, "rotating laser");
            Assert.That(content.Entities["leadImp"].abilities.OfType<ShootAbility>().Single().aim, Is.EqualTo(Aim.Predictive), "predictive aim");
        }
    }
}
