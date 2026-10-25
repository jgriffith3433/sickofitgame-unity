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
    /// <summary>The parallax rule and the Circles' air (Docs/GameplayCookbook.md "Weather and life").</summary>
    public class AmbienceTests
    {
        static ParallaxController.Entry Entry(Vector2 factor, Vector2 period, Vector2? slack = null)
        {
            var e = new ParallaxController.Entry { Name = "t", Home = new Vector3(2f, 1f, 0f), CameraHome = new Vector2(2f, 1f), Factor = factor, Period = period };
            if (slack.HasValue)
                e.Slack = slack.Value;
            return e;
        }

        [Test]
        public void AFarLayerMovesAtItsShareOfTheCamera()
        {
            var far = Entry(new Vector2(0.8f, 0.8f), Vector2.zero);
            Vector2 p = ParallaxController.Place(far, new Vector2(12f, 1f));
            Assert.That(p.x, Is.EqualTo(2f + 10f * 0.8f).Within(1e-4f));
            Assert.That(p.y, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void FactorOneIsGluedToTheScreenAndZeroIsFixedInTheWorld()
        {
            Vector2 cam = new Vector2(7f, 5f);
            Vector2 glued = ParallaxController.Place(Entry(Vector2.one, Vector2.zero), cam);
            Vector2 fixedInWorld = ParallaxController.Place(Entry(Vector2.zero, Vector2.zero), cam);
            Assert.That(glued.x, Is.EqualTo(cam.x).Within(1e-4f));
            Assert.That(glued.y, Is.EqualTo(cam.y).Within(1e-4f));
            Assert.That(fixedInWorld.x, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(fixedInWorld.y, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void AWrappingLayerStaysWithinHalfAPeriodOfTheCamera()
        {
            var row = Entry(new Vector2(0.5f, 1f), new Vector2(10f, 0f));
            for (float x = 2f; x < 80f; x += 1.7f)
            {
                Vector2 p = ParallaxController.Place(row, new Vector2(x, 1f));
                Assert.That(Mathf.Abs(p.x - x), Is.LessThanOrEqualTo(5f + 1e-3f), $"camera at {x}");
            }
        }

        [Test]
        public void ALayerHeldByItsEdgeNeverStraysPastItsSlack()
        {
            var layer = Entry(new Vector2(0f, 0f), Vector2.zero, new Vector2(1.5f, 1.5f));
            Vector2 p = ParallaxController.Place(layer, new Vector2(30f, 20f));
            Assert.That(p.x, Is.EqualTo(30f - 1.5f).Within(1e-4f));
            Assert.That(p.y, Is.EqualTo(20f - 1.5f).Within(1e-4f));
        }

        static string CircleDir => Path.Combine(Application.dataPath, "GameConfig", "content", "circles");

        [Test]
        public void EveryBuiltCircleHasItsOwnAirWithinTheBudget()
        {
            var signatures = new HashSet<string>();
            int circles = 0;
            foreach (var file in Directory.GetFiles(CircleDir, "*.json"))
            {
                var json = JObject.Parse(File.ReadAllText(file));
                var amb = json["ambience"] as JObject;
                Assert.That(amb, Is.Not.Null, Path.GetFileName(file) + " has no ambience");
                var weather = (amb["weather"] as JArray) ?? new JArray();
                int bits = weather.Sum(w => (int?)w["count"] ?? 30);
                Assert.That(bits, Is.LessThanOrEqualTo(CircleAmbience.MaxBits), Path.GetFileName(file) + " has more weather than the cap");
                Assert.That(weather.Count, Is.GreaterThanOrEqualTo(2), Path.GetFileName(file) + " needs at least two kinds of weather");
                Assert.That(((amb["life"] as JArray) ?? new JArray()).Count, Is.GreaterThanOrEqualTo(2), Path.GetFileName(file) + " needs background life");
                Assert.That(((amb["depth"] as JArray) ?? new JArray()).Count, Is.GreaterThanOrEqualTo(1), Path.GetFileName(file) + " needs a depth layer");
                string sig = string.Join(",", weather.Select(w => (string)w["kind"]).OrderBy(k => k)) + "|" + string.Join(",", ((JArray)amb["life"]).Select(l => (string)l["shape"] + (string)l["behavior"]).OrderBy(k => k));
                Assert.That(signatures.Add(sig), Is.True, Path.GetFileName(file) + " has the same air as another Circle");
                circles++;
            }
            Assert.That(circles, Is.GreaterThanOrEqualTo(3));
        }
    }
}
