using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using SickOfIt.Json;
using Object = UnityEngine.Object;

namespace SickOfIt.Tests.Editor
{
    public class UnityJsonSerializerTests
    {
        public enum Kind { A, B, C }

        [Flags]
        public enum Mask { None = 0, X = 1, Y = 2, Z = 4 }

        [Serializable]
        public class Inner
        {
            public int count = 2;
            public string label = "x";
        }

        [Serializable]
        public struct Pair
        {
            public int a;
            public float b;
        }

        [Serializable]
        public class Sample
        {
            public int i = 5;
            public byte small = 3;
            public float f = 0.15f;
            public double d = 1.5;
            public bool flag = true;
            public string text = "hi";
            public Kind kind = Kind.B;
            public Mask mask = Mask.X | Mask.Z;
            public Vector2 v2 = new Vector2(1f, 2f);
            public Vector3 v3;
            public Vector2Int cell = new Vector2Int(3, 4);
            public Color white = Color.white;
            public Color odd = new Color(0.123f, 0.5f, 1f, 0.3f);
            public Rect rect = new Rect(1f, 2f, 3f, 4f);
            public LayerMask layers;
            public Inner inner = new Inner();
            public Inner nullInner;
            public Pair pair;
            public List<Inner> items = new List<Inner> { new Inner(), new Inner() };
            public int[] numbers = { 1, 2, 3 };
            public Texture2D texture;
            [SerializeField] int hidden = 7;
            [NonSerialized] public int skipped = 9;
            public Dictionary<string, int> dictionary;
            public static int shared = 1;
            public int Hidden => hidden;
        }

        /// <summary>Asset ids are the asset's name; only assets handed to the constructor resolve.</summary>
        sealed class FakeAssets : IAssetIdResolver
        {
            readonly Dictionary<string, Object> byId = new Dictionary<string, Object>();

            public FakeAssets(params Object[] assets)
            {
                foreach (var asset in assets)
                    byId[asset.name] = asset;
            }

            public string GetId(Object asset, Type fieldType, out string error)
            {
                error = byId.ContainsValue(asset) ? null : "unknown asset";
                return error == null ? asset.name : null;
            }

            public Object Resolve(string id, Type fieldType, out string error)
            {
                error = byId.TryGetValue(id, out var asset) && fieldType.IsInstanceOfType(asset) ? null : $"No asset \"{id}\".";
                return error == null ? asset : null;
            }

            public string DescribeMissing(Object missing) => "a missing asset";
        }

        static UnityJsonSerializer Serializer(params Object[] assets) => new UnityJsonSerializer(new FakeAssets(assets));

        static JObject Write(object target, UnityJsonSerializer serializer = null)
        {
            var issues = new JsonIssueList();
            var obj = (serializer ?? Serializer()).WriteFields(target, null, issues, "test.json");
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
            return obj;
        }

        /// <summary>Formats and re-parses, so reading always goes through real JSON text.</summary>
        static JObject Reparse(JObject obj)
        {
            var issues = new JsonIssueList();
            var token = JsonText.Parse(JsonText.Format(obj), "test.json", issues);
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
            return (JObject)token;
        }

        static JsonIssueList Populate(string json, object target, UnityJsonSerializer serializer = null)
        {
            var issues = new JsonIssueList();
            var token = JsonText.Parse(json, "test.json", issues);
            if (token != null)
                (serializer ?? Serializer()).PopulateFields((JObject)token, target, null, issues, "test.json");
            return issues;
        }

        [Test]
        public void WritesExactlyTheFieldsUnitySerializes()
        {
            var keys = Write(new Sample()).Properties().Select(p => p.Name).ToList();

            Assert.That(keys, Does.Contain("hidden"));
            Assert.That(keys, Does.Not.Contain("skipped"));
            Assert.That(keys, Does.Not.Contain("dictionary"));
            Assert.That(keys, Does.Not.Contain("shared"));
            Assert.That(keys, Does.Not.Contain("Hidden"));
            Assert.That(keys.First(), Is.EqualTo("i"), "fields keep declaration order");
        }

        [Test]
        public void ReadableEncodings()
        {
            var obj = Write(new Sample());

            Assert.That((string)obj["kind"], Is.EqualTo("B"));
            Assert.That((string)obj["mask"], Is.EqualTo("X, Z"));
            Assert.That((string)obj["white"], Is.EqualTo("#FFFFFF"));
            Assert.That(obj["odd"].Type, Is.EqualTo(JTokenType.Array), "colors that aren't exact bytes keep full precision");
            Assert.That(JsonText.Format(obj["v2"]).Trim(), Is.EqualTo("[1.0, 2.0]"));
            Assert.That(JsonText.Format(obj["f"]).Trim(), Is.EqualTo("0.15"));
            Assert.That(obj["nullInner"].Type, Is.EqualTo(JTokenType.Object), "Unity never stores null classes, so neither does JSON");
            Assert.That(obj["texture"].Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void EveryValueRoundTrips()
        {
            var texture = new Texture2D(1, 1) { name = "tex" };
            try
            {
                var serializer = Serializer(texture);
                var source = new Sample
                {
                    i = -42, small = 250, f = 1e-7f, d = Math.PI, flag = false, text = "quote \" and é",
                    kind = Kind.C, mask = Mask.Y, v2 = new Vector2(-0.1f, 3.3f), v3 = new Vector3(1, 2, 3),
                    cell = new Vector2Int(-1, 9), white = new Color(0f, 0.2f, 0.4f, 0.6f), odd = new Color(1e-3f, 2f, -1f, 0.5f),
                    rect = new Rect(-1, -2, 5.5f, 6), layers = 1 << 8, inner = new Inner { count = 9, label = "in" },
                    pair = new Pair { a = 4, b = 0.25f }, items = new List<Inner> { new Inner { count = 7 } }, numbers = new[] { 9 },
                    texture = texture,
                };
                var written = Write(source, serializer);

                var target = new Sample();
                var issues = new JsonIssueList();
                serializer.PopulateFields(Reparse(written), target, null, issues, "test.json");
                Assert.That(issues, Is.Empty, string.Join("\n", issues));

                Assert.That(JsonText.Format(Write(target, serializer)), Is.EqualTo(JsonText.Format(written)));
                Assert.That(target.texture, Is.SameAs(texture));
                Assert.That(target.Hidden, Is.EqualTo(7));
                Assert.That(target.pair.b, Is.EqualTo(0.25f));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void FloatsRoundTripBitForBit()
        {
            var random = new System.Random(1234);
            // -0 is left out: Mono may drop its sign when parsing, and config data never relies on it.
            var values = new List<float> { 0f, 0.15f, 1f / 3f, float.Epsilon, float.MaxValue, float.MinValue, 16777217f, 1e-30f, 123456.789f };
            for (int n = 0; n < 200000; n++)
            {
                float f = BitConverter.ToSingle(BitConverter.GetBytes(random.Next(int.MinValue, int.MaxValue)), 0);
                if (!float.IsNaN(f) && !float.IsInfinity(f))
                    values.Add(f);
            }

            foreach (float value in values)
            {
                var issues = new JsonIssueList();
                var token = JsonText.Parse(JsonText.Format(new JArray(UnityJsonSerializer.FloatToken(value))), "f", issues);
                Assert.That(issues, Is.Empty);
                var serializer = Serializer();
                Assert.That(serializer.TryRead(((JArray)token)[0], typeof(float), null, issues, "f", "", out object read), Is.True);
                Assert.That(BitConverter.GetBytes((float)read), Is.EqualTo(BitConverter.GetBytes(value)), $"float {value:R}");
            }
        }

        [Test]
        public void NonFiniteFloatsRoundTrip()
        {
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var issues = new JsonIssueList();
                Serializer().TryRead(UnityJsonSerializer.FloatToken(value), typeof(float), null, issues, "f", "", out object read);
                Assert.That(((float)read).Equals(value), Is.True);
            }
        }

        [Test]
        public void MissingFieldsKeepTheirDefaults()
        {
            var target = new Sample();
            var issues = Populate("{ \"i\": 9 }", target);

            Assert.That(issues, Is.Empty);
            Assert.That(target.i, Is.EqualTo(9));
            Assert.That(target.text, Is.EqualTo("hi"));
            Assert.That(target.items.Count, Is.EqualTo(2));
        }

        [Test]
        public void ListsAndArraysAreReplacedNotMerged()
        {
            var target = new Sample();
            var issues = Populate("{ \"items\": [ {} ], \"numbers\": [] }", target);

            Assert.That(issues, Is.Empty);
            Assert.That(target.items.Count, Is.EqualTo(1));
            Assert.That(target.items[0].count, Is.EqualTo(2), "new list items get C# defaults");
            Assert.That(target.numbers, Is.Empty);
        }

        [Test]
        public void UnknownFieldIsAnErrorWithASuggestionAndLine()
        {
            var issues = Populate("{\n  \"cuont\": 1\n}", new Inner());

            Assert.That(issues.ErrorCount, Is.EqualTo(1));
            Assert.That(issues[0].Message, Does.Contain("Did you mean \"count\""));
            Assert.That(issues[0].Line, Is.EqualTo(2));
            Assert.That(issues[0].Path, Is.EqualTo("cuont"));
        }

        [Test]
        public void WrongValuesAreErrorsThatNameTheProblem()
        {
            Assert.That(Populate("{ \"i\": \"five\" }", new Sample())[0].Message, Does.Contain("whole number"));
            Assert.That(Populate("{ \"i\": 1.5 }", new Sample())[0].Message, Does.Contain("whole number"));
            Assert.That(Populate("{ \"small\": 300 }", new Sample())[0].Message, Does.Contain("out of range"));
            Assert.That(Populate("{ \"kind\": \"D\" }", new Sample())[0].Message, Does.Contain("A, B, C"));
            Assert.That(Populate("{ \"kind\": \"b\" }", new Sample()).HasErrors, Is.True, "enum names are case-sensitive");
            Assert.That(Populate("{ \"kind\": \"A, B\" }", new Sample()).HasErrors, Is.True, "only [Flags] enums combine");
            Assert.That(Populate("{ \"v2\": [1] }", new Sample())[0].Message, Does.Contain("2 numbers"));
            Assert.That(Populate("{ \"white\": \"red\" }", new Sample())[0].Message, Does.Contain("#RRGGBB"));
            Assert.That(Populate("{ \"inner\": null }", new Sample()).HasErrors, Is.True);
            Assert.That(Populate("{ \"texture\": \"missing\" }", new Sample())[0].Message, Does.Contain("No asset"));
            Assert.That(Populate("{ \"items\": [ { \"count\": \"x\" } ] }", new Sample())[0].Path, Is.EqualTo("items[0].count"));
        }

        [Test]
        public void AlternativeInputFormsAreAccepted()
        {
            var target = new Sample();
            var issues = Populate("{ \"v2\": { \"x\": 5, \"y\": 6 }, \"white\": \"#80FF0040\", \"odd\": [1, 0, 0], \"mask\": \"Y, Z\", \"kind\": 2 }", target);

            Assert.That(issues, Is.Empty, string.Join("\n", issues));
            Assert.That(target.v2, Is.EqualTo(new Vector2(5, 6)));
            Assert.That(target.white, Is.EqualTo(new Color(128 / 255f, 1f, 0f, 64 / 255f)));
            Assert.That(target.odd, Is.EqualTo(new Color(1, 0, 0, 1)));
            Assert.That(target.mask, Is.EqualTo(Mask.Y | Mask.Z));
            Assert.That(target.kind, Is.EqualTo(Kind.C));
        }

        [Test]
        public void ParserRejectsDuplicatesAndTrailingContent()
        {
            var issues = new JsonIssueList();
            Assert.That(JsonText.Parse("{ \"a\": 1, \"a\": 2 }", "t", issues), Is.Null);
            Assert.That(issues.HasErrors, Is.True);

            issues.Clear();
            Assert.That(JsonText.Parse("{ } { }", "t", issues), Is.Null);
            Assert.That(issues.HasErrors, Is.True);

            issues.Clear();
            Assert.That((int)JsonText.Parse("{ \"a\": 1, }", "t", issues)?["a"], Is.EqualTo(1), "a trailing comma is harmless, so it is tolerated");
            Assert.That(issues, Is.Empty);

            issues.Clear();
            Assert.That(JsonText.Parse("{ \"a\": 1 \"b\": 2 }", "t", issues), Is.Null, "a missing comma is an error");
            Assert.That(issues.HasErrors, Is.True);

            issues.Clear();
            Assert.That(JsonText.Parse("{ \"date\": \"2026-10-13\" } // note", "t", issues)?["date"]?.Type, Is.EqualTo(JTokenType.String));
            Assert.That(issues, Is.Empty);
        }

        /// <summary>Ids starting "later/" are known but not loaded (a player build's on-demand art: GameAssets).</summary>
        sealed class DeferredAssets : IDeferredAssetResolver
        {
            public readonly Dictionary<string, List<Action<Object>>> Bound = new Dictionary<string, List<Action<Object>>>();

            public bool Defers(string id, Type fieldType) => id.StartsWith("later/", StringComparison.Ordinal);

            public void Bind(string id, Type fieldType, Action<Object> assign)
            {
                if (!Bound.TryGetValue(id, out var list))
                    Bound[id] = list = new List<Action<Object>>();
                list.Add(assign);
            }

            public void Load(string id, Object asset)
            {
                foreach (var assign in Bound[id])
                    assign(asset);
            }

            public string GetId(Object asset, Type fieldType, out string error) { error = null; return asset.name; }
            public Object Resolve(string id, Type fieldType, out string error) { error = $"No asset \"{id}\"."; return null; }
            public string DescribeMissing(Object missing) => "a missing asset";
        }

        [Serializable]
        public class Holder
        {
            public Texture2D one;
            public List<Texture2D> list = new List<Texture2D>();
            public Texture2D[] array = new Texture2D[0];
            public Inner inner = new Inner();
        }

        [Test]
        public void DeferredAssetsReadEmptyThenFillAndEmptyInPlace()
        {
            var assets = new DeferredAssets();
            var holder = new Holder();
            var issues = Populate("{ \"one\": \"later/a\", \"list\": [\"later/a\", \"later/b\"], \"array\": [\"later/b\"], \"inner\": { \"count\": 4 } }",
                holder, new UnityJsonSerializer(assets));

            Assert.That(issues, Is.Empty, "a deferred id is no error");
            Assert.That(holder.one, Is.Null);
            Assert.That(holder.list, Has.Count.EqualTo(2), "the list keeps its places");
            Assert.That(holder.inner.count, Is.EqualTo(4), "a nested object after a deferred id still reads");
            Assert.That(assets.Bound["later/a"], Has.Count.EqualTo(2));
            Assert.That(assets.Bound["later/b"], Has.Count.EqualTo(2));

            var a = new Texture2D(1, 1) { name = "a" };
            var b = new Texture2D(1, 1) { name = "b" };
            try
            {
                assets.Load("later/a", a);
                assets.Load("later/b", b);
                Assert.That(holder.one, Is.SameAs(a));
                Assert.That(holder.list, Is.EqualTo(new[] { a, b }));
                Assert.That(holder.array, Is.EqualTo(new[] { b }));

                assets.Load("later/a", null);
                Assert.That(holder.one, Is.Null, "released");
                Assert.That(holder.list[0], Is.Null);
                Assert.That(holder.list[1], Is.SameAs(b));
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void FormattingIsCompactAndDeterministic()
        {
            var obj = new JObject { ["name"] = "a", ["pos"] = new JArray(1.5, 0.0), ["empty"] = new JArray(), ["nested"] = new JObject { ["x"] = 1 } };
            const string expected = "{\n  \"name\": \"a\",\n  \"pos\": [1.5, 0.0],\n  \"empty\": [],\n  \"nested\": {\n    \"x\": 1\n  }\n}\n";
            Assert.That(JsonText.Format(obj), Is.EqualTo(expected));
        }
    }
}
