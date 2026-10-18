using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;
using SickOfIt.Json;
using Object = UnityEngine.Object;

namespace SickOfIt.Tests.Editor
{
    public class GameplayContentTests
    {
        static GameContent Build(Dictionary<string, string> files, string script = null)
        {
            var sources = files.Select(f => new GameContent.FileSource { Path = f.Key, DisplayPath = "content/" + f.Key, Json = JToken.Parse(f.Value.Replace('\'', '"')) });
            var scripts = new List<GameContent.ScriptSource>();
            if (script != null)
                scripts.Add(new GameContent.ScriptSource { LevelName = "L", DisplayPath = "levels/L.json", JsonPath = "script", Json = JToken.Parse(script.Replace('\'', '"')) });
            var noAssets = new RegistryAssetResolver(null);
            var content = GameContent.Build(sources, scripts, noAssets, noAssets);
            ContentValidator.Validate(content, content.Issues);
            return content;
        }

        static string Errors(GameContent c) => string.Join("\n", c.Issues);

        // ─── Rewind safety ────────────────────────────────────────────

        static IEnumerable<Type> Concrete(Type baseType) => JsonTypeRegistry.Subtypes(baseType).Values.OrderBy(t => t.Name);

        /// <summary>
        /// Every private value-type field of an ability or trigger is runtime state, so SaveState must write it
        /// and LoadState must read it back. Fill the fields with distinct values, save, load into a fresh copy,
        /// and require them to match. A forgotten field fails here instead of as a desync after a rewind.
        /// </summary>
        [TestCaseSource(nameof(StateTypes))]
        public void SaveStateCoversEveryRuntimeField(Type type)
        {
            var source = Activator.CreateInstance(type);
            var fields = StateFields(type).ToList();
            float seed = 1.25f;
            foreach (var f in fields)
                f.SetValue(source, Distinct(f.FieldType, ref seed));

            var writer = new StateWriter();
            Save(source, writer);

            var copy = Activator.CreateInstance(type);
            var reader = new StateReader();
            ReaderBegin(reader, writer);
            Load(copy, reader);
            Assert.That(reader.AtEnd, Is.True, $"{type.Name}: LoadState read less than SaveState wrote");

            foreach (var f in fields)
                Assert.That(f.GetValue(copy), Is.EqualTo(f.GetValue(source)), $"{type.Name}.{f.Name} is not saved for rewind");
        }

        public static IEnumerable<Type> StateTypes() => Concrete(typeof(Ability)).Concat(Concrete(typeof(Trigger)));

        static IEnumerable<FieldInfo> StateFields(Type type)
        {
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.FieldType.IsValueType && !f.Name.Contains("k__BackingField"))
                        yield return f;
                }
            }
        }

        static object Distinct(Type type, ref float seed)
        {
            seed += 1.5f;
            if (type == typeof(float)) return seed;
            if (type == typeof(int)) return (int)(seed * 3);
            if (type == typeof(bool)) return true;
            if (type == typeof(Vector2)) return new Vector2(seed, -seed);
            if (type == typeof(uint)) return (uint)(seed * 7);
            return Activator.CreateInstance(type);
        }

        static void Save(object o, StateWriter w)
        {
            if (o is Ability a) a.SaveState(w);
            else ((Trigger)o).SaveState(w);
        }

        static void Load(object o, StateReader r)
        {
            if (o is Ability a) a.LoadState(r);
            else ((Trigger)o).LoadState(r);
        }

        static void ReaderBegin(StateReader reader, StateWriter writer)
        {
            var begin = typeof(StateReader).GetMethod("Begin", BindingFlags.Instance | BindingFlags.NonPublic);
            var wt = typeof(StateWriter);
            object Get(string name) => wt.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(writer);
            begin.Invoke(reader, new[] { Get("Floats"), Get("FloatCount"), Get("Ints"), Get("IntCount"), Get("Strings"), Get("StringCount") });
        }

        [Test]
        public void EveryContentTypeIsDocumented()
        {
            var undocumented = new List<string>();
            foreach (var baseType in new[] { typeof(Ability), typeof(Trigger), typeof(GameAction), typeof(Pattern) })
            {
                foreach (var t in Concrete(baseType))
                {
                    if (t.GetCustomAttribute<DocAttribute>() == null && t.GetFields(BindingFlags.Instance | BindingFlags.Public).Length > 0)
                        undocumented.Add(t.Name);
                }
            }
            Assert.That(undocumented, Is.Empty, "Add a [Doc] so the generated reference explains it");
        }

        // ─── Parsing and validation ───────────────────────────────────

        [Test]
        public void ExtendsMergesObjectsAndReplacesLists()
        {
            var c = Build(new Dictionary<string, string>
            {
                ["entities/base.json"] = "{ 'health': 3, 'moveSpeed': 2, 'body': { 'radius': 0.5, 'solid': true }, 'abilities': [ { 'type': 'chase' } ] }",
                ["entities/fast.json"] = "{ 'extends': 'base', 'moveSpeed': 5, 'body': { 'radius': 0.25 }, 'abilities': [ { 'type': 'wander' } ] }",
            });
            Assert.That(c.Issues.HasErrors, Is.False, Errors(c));
            var fast = c.Entities["fast"];
            Assert.That(fast.health, Is.EqualTo(3));
            Assert.That(fast.moveSpeed, Is.EqualTo(5f));
            Assert.That(fast.body.radius, Is.EqualTo(0.25f));
            Assert.That(fast.body.solid, Is.True, "nested objects merge");
            Assert.That(fast.abilities.Single(), Is.TypeOf<WanderAbility>(), "lists replace");
        }

        [Test]
        public void UnknownTypeSuggestsTheRightOne()
        {
            var c = Build(new Dictionary<string, string> { ["entities/x.json"] = "{ 'abilities': [ { 'type': 'chace' } ] }" });
            var error = c.Issues.First(i => i.Severity == JsonIssueSeverity.Error);
            Assert.That(error.Message, Does.Contain("Did you mean \"chase\""));
            Assert.That(error.Path, Is.EqualTo("abilities[0].type"));
            Assert.That(error.File, Is.EqualTo("content/entities/x.json"));
        }

        [Test]
        public void ScenesLoadAndAreValidatedLikeLevelScripts()
        {
            var c = Build(new Dictionary<string, string>
            {
                ["scenes/intro.json"] = "{ 'schemaVersion': 1, 'world': { 'camera': 'Travel' }, 'phases': [ { 'name': 'a', 'onEnter': [ " +
                                        "{ 'type': 'walkPlayerTo', 'x': 2, 'emit': 'there' }, { 'type': 'cameraZoom', 'size': 1.5 }, " +
                                        "{ 'type': 'goToPhase', 'phase': 'nope' } ] } ] }",
            });
            Assert.That(c.Scenes.ContainsKey("intro"), Is.True);
            string all = Errors(c);
            Assert.That(all, Does.Contain("No phase \"nope\""));
            Assert.That(all, Does.Contain("only move the Static camera"));
            Assert.That(all, Does.Not.Contain("No win condition"), "a scene ends through its own actions");
        }

        [Test]
        public void ValidatorCatchesBrokenReferences()
        {
            const string script = @"{
                'phases': [ { 'name': 'a', 'rules': [
                    { 'when': { 'type': 'after', 'seconds': 1 }, 'do': [ { 'type': 'goToPhase', 'phase': 'b' }, { 'type': 'spawn', 'entity': 'goblin' } ] },
                    { 'when': { 'type': 'after', 'seconds': 2 }, 'do': [ { 'type': 'kill' }, { 'type': 'wait', 'seconds': 1 } ] },
                    { 'do': [] } ] } ]
            }";
            var c = Build(new Dictionary<string, string> { ["entities/gobln.json"] = "{ }" }, script);
            string all = Errors(c);
            Assert.That(all, Does.Contain("No phase \"b\""));
            Assert.That(all, Does.Contain("No entity \"goblin\"").And.Contain("Did you mean \"gobln\""));
            Assert.That(all, Does.Contain("Say what to target"));
            Assert.That(all, Does.Contain("only works inside a sequence"));
            Assert.That(all, Does.Contain("no \"when\" trigger"));
        }

        [Test]
        public void CirclesLoadAndTheirReferencesAreChecked()
        {
            const string script = @"{
                'floors': { 'start': 1000, 'end': 990, 'unitsPerFloor': 2, 'circle': 'wastes' },
                'world': { 'camera': 'Travel' },
                'phases': [ { 'name': 'a', 'onEnter': [ { 'type': 'generateFloors', 'circle': 'wastse', 'untilFloor': 995 } ] } ]
            }";
            var c = Build(new Dictionary<string, string>
            {
                ["entities/imp.json"] = "{ }",
                ["circles/wastes.json"] = "{ 'firstFloor': 1000, 'lastFloor': 901, 'enemies': [ { 'entity': 'ipm' } ], " +
                                          "'hazards': [ { 'telegraph': 0.5, 'actions': [] } ], 'milestones': [ { 'floor': 800, 'text': 'x' } ] }",
            }, script);
            Assert.That(c.Circles.ContainsKey("wastes"), Is.True);
            string all = Errors(c);
            Assert.That(all, Does.Contain("No entity \"ipm\"").And.Contain("Did you mean \"imp\""));
            Assert.That(all, Does.Contain("No circle \"wastse\"").And.Contain("Did you mean \"wastes\""));
            Assert.That(all, Does.Contain("at least 1 second"));
            Assert.That(all, Does.Contain("outside this circle"));
        }

        [Test]
        public void FloorsMustCountDown()
        {
            var c = Build(new Dictionary<string, string> { ["circles/up.json"] = "{ 'firstFloor': 901, 'lastFloor': 1000 }" },
                "{ 'floors': { 'start': 976, 'end': 1000 }, 'phases': [ { 'name': 'a' } ] }");
            string all = Errors(c);
            Assert.That(all, Does.Contain("firstFloor (the bottom) must be the larger number"));
            Assert.That(all, Does.Contain("end must be a lower floor than start"));
        }

        [Test]
        public void DifficultyIsInterpolatedAcrossTheCircle()
        {
            var circle = new CircleDef
            {
                firstFloor = 1000, lastFloor = 900,
                easiest = new DifficultyDef { maxAlive = 2, breather = 4f },
                hardest = new DifficultyDef { maxAlive = 4, breather = 2f },
            };
            Assert.That(circle.DifficultyAt(1000).maxAlive, Is.EqualTo(2));
            Assert.That(circle.DifficultyAt(950).maxAlive, Is.EqualTo(3));
            Assert.That(circle.DifficultyAt(950).breather, Is.EqualTo(3f).Within(0.001f));
            Assert.That(circle.DifficultyAt(800).maxAlive, Is.EqualTo(4), "clamped past the top");
        }

        [Test]
        public void ExtendsLoopIsAnError()
        {
            var c = Build(new Dictionary<string, string>
            {
                ["entities/a.json"] = "{ 'extends': 'b' }",
                ["entities/b.json"] = "{ 'extends': 'a' }",
            });
            Assert.That(Errors(c), Does.Contain("loops"));
        }

        [Test]
        public void ProjectContentLoadsWithoutErrors()
        {
            var content = GameContent.FromDefinition(GameDefinition.Instance);
            Assert.That(content.Issues.HasErrors, Is.False, Errors(content));
            Assert.That(content.Entities.Keys, Is.SupersetOf(new[] { "chaser", "fighter", "sentinel", "shooter" }));
            Assert.That(content.LevelScripts.Count, Is.GreaterThanOrEqualTo(4));
            var issues = new JsonIssueList();
            ContentValidator.Validate(content, issues);
            Assert.That(issues.HasErrors, Is.False, string.Join("\n", issues));
        }

        [Test]
        public void RemoteBundleRoundTripsAndBadBundlesAreRejected()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sickofit-bundle-test.json");
            var report = SickOfIt.Tools.Editor.GameConfig.RemoteContentBundle.Build(path);
            Assert.That(report.Success, Is.True, report.ToText());
            string bundle = System.IO.File.ReadAllText(path);

            var content = GameContent.FromDefinition(GameDefinition.Instance, bundle);
            Assert.That(content.Issues.HasErrors, Is.False, Errors(content));
            Assert.That(content.Entities.Keys, Is.EquivalentTo(GameContent.FromDefinition(GameDefinition.Instance).Entities.Keys));

            var broken = JObject.Parse(bundle);
            broken["files"]["entities/broken.json"] = JObject.Parse("{ \"abilities\": [ { \"type\": \"nope\" } ] }");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                Assert.That(RemoteContent.Apply(broken.ToString()), Is.False, "a bundle with errors is never used");
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void ConvertedEnemiesKeepTheirAnimationFrames()
        {
            var content = GameContent.FromDefinition(GameDefinition.Instance);
            foreach (var id in new[] { "chaser", "fighter", "sentinel", "shooter" })
            {
                var animation = new EntityAnimation(content.Entities[id].visual.animator, content.Sheets, null, null);
                Assert.That(animation.FirstSprite, Is.Not.Null, $"{id} has frames from the asset registry");
            }
        }
    }
}
