using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SickOfIt.Json
{
    /// <summary>
    /// Converts Unity-serializable objects to and from JSON, using Unity's own rules for which fields exist.
    ///
    /// Writing emits every serialized field, so a file always shows the full state and never depends on
    /// C# field defaults staying the same.
    ///
    /// Reading is tolerant of missing fields (they keep the target's current value, which for new objects
    /// is the C# default) and strict about everything else: unknown fields, wrong value types, unknown enum
    /// names, out-of-range numbers, and unresolved asset ids are all errors with file, line, and path.
    /// Lists and arrays are always replaced, never merged.
    ///
    /// Encodings:
    ///   numbers, bools, strings  as JSON values; NaN and infinities as "NaN", "Infinity", "-Infinity"
    ///   enums                    by name ("Chaser"); [Flags] as "A, B"; values without a name as integers
    ///   asset references         by id (see <see cref="IAssetIdResolver"/>), or null
    ///   Vector2/3/4, Quaternion  [x, y, ...]; reading also accepts {"x": .., "y": ..}
    ///   Color                    "#RRGGBB" or "#RRGGBBAA" when exact, else [r, g, b, a]
    ///   Rect / RectInt           {"x", "y", "width", "height"}
    ///   LayerMask                its integer bit mask
    /// </summary>
    public sealed class UnityJsonSerializer
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string[] XYZW = { "x", "y", "z", "w" };

        readonly IAssetIdResolver assets;

        // An asset id the resolver defers (IDeferredAssetResolver): set by TryReadAsset, taken by whoever stores the value.
        string deferredId;
        Type deferredType;

        /// <summary>
        /// Called for each [RawJson] field read from JSON, with the field's JSON value (line numbers intact),
        /// so a caller can check the embedded content at import time.
        /// </summary>
        public Action<JToken, FieldInfo, JsonIssueList, string, string> RawJsonValidator;

        public UnityJsonSerializer(IAssetIdResolver assets)
        {
            this.assets = assets;
        }

        // =====================================================================
        // WRITING
        // =====================================================================

        /// <summary>Writes the serialized fields of <paramref name="target"/>, skipping names in <paramref name="skip"/>.</summary>
        public JObject WriteFields(object target, ISet<string> skip, JsonIssueList issues, string file, string path = "")
        {
            return WriteObject(target, target.GetType(), skip, issues, file, path);
        }

        public JToken WriteValue(object value, Type type, JsonIssueList issues, string file, string path)
        {
            if (type == typeof(string))
                return new JValue((string)value ?? string.Empty);
            if (type == typeof(bool))
                return new JValue((bool)value);
            if (type == typeof(float))
                return FloatToken((float)value);
            if (type == typeof(double))
                return DoubleToken((double)value);
            if (type == typeof(char))
                return new JValue(((char)value).ToString());
            if (type == typeof(ulong))
                return new JValue((ulong)value);
            if (type.IsPrimitive)
                return new JValue(Convert.ToInt64(value, Inv));
            if (type.IsEnum)
                return EnumToken(value);
            if (UnitySerializationRules.IsUnityObject(type))
                return AssetToken(value, type, issues, file, path);
            if (UnitySerializationRules.IsSupportedBuiltIn(type))
                return BuiltInToken(value, type);
            if (UnitySerializationRules.IsUnsupportedBuiltIn(type))
                throw new NotSupportedException($"{file} @{path}: {type.Name} fields are not supported in config JSON yet.");

            if (UnitySerializationRules.IsCollection(type, out var element))
            {
                var array = new JArray();
                if (value is IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                        array.Add(WriteValue(list[i], element, issues, file, $"{path}[{i}]"));
                }
                return array;
            }

            if (JsonTypeRegistry.IsPolymorphic(type))
            {
                if (value == null)
                    return JValue.CreateNull();
                var typed = new JObject { ["type"] = JsonTypeRegistry.NameOf(value.GetType()) };
                foreach (var property in WriteObject(value, value.GetType(), null, issues, file, path).Properties())
                    typed.Add(property.Name, property.Value);
                return typed;
            }

            // Unity never stores a null for a serializable class, so neither do we.
            return WriteObject(value ?? CreateInstance(type), type, null, issues, file, path);
        }

        JObject WriteObject(object target, Type type, ISet<string> skip, JsonIssueList issues, string file, string path)
        {
            var obj = new JObject();
            foreach (var field in UnitySerializationRules.GetSerializedFields(type))
            {
                if (skip != null && skip.Contains(field.Name))
                    continue;
                string fieldPath = Join(path, field.Name);
                obj[field.Name] = field.IsDefined(typeof(RawJsonAttribute), false)
                    ? RawToken((string)field.GetValue(target), issues, file, fieldPath)
                    : WriteValue(field.GetValue(target), field.FieldType, issues, file, fieldPath);
            }
            return obj;
        }

        static JToken RawToken(string json, JsonIssueList issues, string file, string path)
        {
            if (string.IsNullOrEmpty(json))
                return JValue.CreateNull();
            var token = JsonText.Parse(json, file, issues);
            if (token == null)
                issues.Error(file, path, "Stored JSON is invalid.");
            return token ?? JValue.CreateNull();
        }

        JToken AssetToken(object value, Type type, JsonIssueList issues, string file, string path)
        {
            var asset = value as Object;
            if (asset == null)
            {
                // In the editor an empty field holds a placeholder object with instance id 0. A placeholder with
                // a real instance id is a broken link (deleted asset, or a sub-asset that is never created).
                // It already behaves as null in the game; JSON can't keep the dangling link, so it is dropped.
                if (!ReferenceEquals(value, null) && asset.GetInstanceID() != 0)
                    issues.Warning(file, path, $"Broken reference to {assets.DescribeMissing(asset)}; it is null in the game and is written as null.");
                return JValue.CreateNull();
            }

            string id = assets.GetId(asset, type, out string error);
            if (id == null)
            {
                issues.Error(file, path, error);
                return JValue.CreateNull();
            }
            return new JValue(id);
        }

        static JToken BuiltInToken(object value, Type type)
        {
            if (type == typeof(Vector2)) { var v = (Vector2)value; return Floats(v.x, v.y); }
            if (type == typeof(Vector3)) { var v = (Vector3)value; return Floats(v.x, v.y, v.z); }
            if (type == typeof(Vector4)) { var v = (Vector4)value; return Floats(v.x, v.y, v.z, v.w); }
            if (type == typeof(Quaternion)) { var q = (Quaternion)value; return Floats(q.x, q.y, q.z, q.w); }
            if (type == typeof(Vector2Int)) { var v = (Vector2Int)value; return new JArray(v.x, v.y); }
            if (type == typeof(Vector3Int)) { var v = (Vector3Int)value; return new JArray(v.x, v.y, v.z); }
            if (type == typeof(Color)) return ColorToken((Color)value);
            if (type == typeof(Color32)) { var c = (Color32)value; return new JValue(Hex(c.r, c.g, c.b, c.a)); }
            if (type == typeof(Rect))
            {
                var r = (Rect)value;
                return new JObject { ["x"] = FloatToken(r.x), ["y"] = FloatToken(r.y), ["width"] = FloatToken(r.width), ["height"] = FloatToken(r.height) };
            }
            if (type == typeof(RectInt))
            {
                var r = (RectInt)value;
                return new JObject { ["x"] = r.x, ["y"] = r.y, ["width"] = r.width, ["height"] = r.height };
            }
            if (type == typeof(LayerMask)) return new JValue(((LayerMask)value).value);
            throw new NotSupportedException(type.Name);
        }

        public static JToken FloatToken(float value)
        {
            if (float.IsNaN(value)) return new JValue("NaN");
            if (float.IsPositiveInfinity(value)) return new JValue("Infinity");
            if (float.IsNegativeInfinity(value)) return new JValue("-Infinity");

            // Shortest decimal that reads back as the same float, so 0.15f is written as 0.15, not 0.15000000596.
            string text = value.ToString("G7", Inv);
            if (float.Parse(text, Inv) != value)
                text = value.ToString("G8", Inv);
            if (float.Parse(text, Inv) != value)
                text = value.ToString("G9", Inv);
            return new JValue(double.Parse(text, Inv));
        }

        public static JToken DoubleToken(double value)
        {
            if (double.IsNaN(value)) return new JValue("NaN");
            if (double.IsPositiveInfinity(value)) return new JValue("Infinity");
            if (double.IsNegativeInfinity(value)) return new JValue("-Infinity");
            return new JValue(value);
        }

        public static JToken ColorToken(Color c)
        {
            if (TryByte(c.r, out byte r) && TryByte(c.g, out byte g) && TryByte(c.b, out byte b) && TryByte(c.a, out byte a))
                return new JValue(Hex(r, g, b, a));
            return Floats(c.r, c.g, c.b, c.a);
        }

        static JToken EnumToken(object value)
        {
            string text = value.ToString();
            bool unnamed = text.Length > 0 && (char.IsDigit(text[0]) || text[0] == '-');
            return unnamed ? new JValue(Convert.ToInt64(value, Inv)) : new JValue(text);
        }

        static JArray Floats(params float[] values)
        {
            var array = new JArray();
            foreach (float v in values)
                array.Add(FloatToken(v));
            return array;
        }

        /// <summary>True when <paramref name="value"/> is exactly some byte / 255, the value a hex color reads back as.</summary>
        static bool TryByte(float value, out byte result)
        {
            result = 0;
            if (!(value >= 0f && value <= 1f))
                return false;
            int i = Mathf.RoundToInt(value * 255f);
            if (ByteToFloat(i) != value)
                return false;
            result = (byte)i;
            return true;
        }

        static float ByteToFloat(int b) => b / 255f;

        static string Hex(byte r, byte g, byte b, byte a)
        {
            return a == 255 ? $"#{r:X2}{g:X2}{b:X2}" : $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }

        // =====================================================================
        // READING
        // =====================================================================

        /// <summary>
        /// Sets fields of <paramref name="target"/> (a class instance) from <paramref name="obj"/>.
        /// Keys in <paramref name="skip"/> are left for the caller. Returns false if any error was added.
        /// </summary>
        public bool PopulateFields(JObject obj, object target, ISet<string> skip, JsonIssueList issues, string file, string path = "")
        {
            return PopulateObject(obj, target, target.GetType(), skip, issues, file, path);
        }

        /// <summary>Reads a value of <paramref name="type"/>. <paramref name="current"/> is the starting point for objects (null for a new one).</summary>
        public bool TryRead(JToken token, Type type, object current, JsonIssueList issues, string file, string path, out object result)
        {
            result = null;

            if (type == typeof(string))
            {
                if (token.Type == JTokenType.String) { result = (string)token; return true; }
                if (token.Type == JTokenType.Null) { result = string.Empty; return true; }
                return Fail(issues, file, path, token, "Expected a string.");
            }
            if (type == typeof(bool))
            {
                if (token.Type == JTokenType.Boolean) { result = (bool)token; return true; }
                return Fail(issues, file, path, token, "Expected true or false.");
            }
            if (type == typeof(float) || type == typeof(double))
            {
                if (!TryReadNumber(token, out double number))
                    return Fail(issues, file, path, token, "Expected a number.");
                result = type == typeof(float) ? (object)(float)number : number;
                return true;
            }
            if (type == typeof(char))
            {
                if (token.Type == JTokenType.String && ((string)token).Length == 1) { result = ((string)token)[0]; return true; }
                return Fail(issues, file, path, token, "Expected a one-character string.");
            }
            if (type.IsPrimitive)
                return TryReadInteger(token, type, issues, file, path, out result);
            if (type.IsEnum)
                return TryReadEnum(token, type, issues, file, path, out result);
            if (UnitySerializationRules.IsUnityObject(type))
                return TryReadAsset(token, type, issues, file, path, out result);
            if (UnitySerializationRules.IsSupportedBuiltIn(type))
                return TryReadBuiltIn(token, type, issues, file, path, out result);
            if (UnitySerializationRules.IsUnsupportedBuiltIn(type))
                return Fail(issues, file, path, token, $"{type.Name} fields are not supported in config JSON yet.");

            if (UnitySerializationRules.IsCollection(type, out var element))
                return TryReadCollection(token, type, element, issues, file, path, out result);

            if (JsonTypeRegistry.IsPolymorphic(type))
                return TryReadPolymorphic(token, type, issues, file, path, out result);

            if (token.Type != JTokenType.Object)
                return Fail(issues, file, path, token, $"Expected an object for {type.Name}. Use {{}} for all defaults.");
            object target = current ?? CreateInstance(type);
            bool ok = PopulateObject((JObject)token, target, type, null, issues, file, path);
            result = target;
            return ok;
        }

        static readonly HashSet<string> TypeKey = new HashSet<string> { "type" };

        bool TryReadPolymorphic(JToken token, Type baseType, JsonIssueList issues, string file, string path, out object result)
        {
            result = null;
            if (token.Type == JTokenType.Null)
                return true;

            var subtypes = JsonTypeRegistry.Subtypes(baseType);
            string valid = string.Join(", ", subtypes.Keys.OrderBy(k => k, StringComparer.Ordinal));
            if (!(token is JObject obj))
                return Fail(issues, file, path, token, $"Expected an object with a \"type\" ({baseType.Name}: {valid}).");

            var typeToken = obj["type"];
            if (typeToken == null || typeToken.Type != JTokenType.String)
                return Fail(issues, file, path, token, $"Missing \"type\". {baseType.Name} types: {valid}.");

            string name = (string)typeToken;
            if (!subtypes.TryGetValue(name, out var concrete))
            {
                string suggestion = SuggestName(name, subtypes.Keys);
                return Fail(issues, file, Join(path, "type"), typeToken, $"\"{name}\" is not a {baseType.Name} type.{suggestion} Valid types: {valid}.");
            }

            object target = CreateInstance(concrete);
            bool ok = PopulateObject(obj, target, concrete, TypeKey, issues, file, path);
            result = target;
            return ok;
        }

        bool PopulateObject(JObject obj, object target, Type type, ISet<string> skip, JsonIssueList issues, string file, string path)
        {
            var fields = UnitySerializationRules.GetSerializedFields(type);
            bool ok = true;
            foreach (var property in obj.Properties())
            {
                if (skip != null && skip.Contains(property.Name))
                    continue;

                string fieldPath = Join(path, property.Name);
                var field = Find(fields, property.Name);
                if (field == null)
                {
                    ok = Fail(issues, file, fieldPath, property, $"Unknown field \"{property.Name}\" on {type.Name}.{Suggest(property.Name, fields)}");
                    continue;
                }

                if (field.IsDefined(typeof(RawJsonAttribute), false))
                {
                    if (property.Value.Type == JTokenType.Null)
                    {
                        field.SetValue(target, string.Empty);
                        continue;
                    }
                    int errorsBefore = issues.ErrorCount;
                    RawJsonValidator?.Invoke(property.Value, field, issues, file, fieldPath);
                    if (issues.ErrorCount > errorsBefore)
                        ok = false;
                    field.SetValue(target, JsonText.Compact(property.Value));
                    continue;
                }

                object current = field.GetValue(target);
                deferredId = null;
                if (TryRead(property.Value, field.FieldType, current, issues, file, fieldPath, out object value))
                {
                    field.SetValue(target, value);
                    if (deferredId != null)
                    {
                        var into = field;
                        var owner = target;
                        ((IDeferredAssetResolver)assets).Bind(deferredId, deferredType, asset => into.SetValue(owner, asset));
                    }
                }
                else
                    ok = false;
                deferredId = null;
            }
            return ok;
        }

        bool TryReadCollection(JToken token, Type type, Type element, JsonIssueList issues, string file, string path, out object result)
        {
            result = null;
            if (token.Type != JTokenType.Array)
                return Fail(issues, file, path, token, "Expected an array.");

            var array = (JArray)token;
            var items = new List<object>(array.Count);
            List<(int index, string id, Type type)> deferred = null;
            bool ok = true;
            for (int i = 0; i < array.Count; i++)
            {
                deferredId = null;
                if (TryRead(array[i], element, null, issues, file, $"{path}[{i}]", out object item))
                {
                    items.Add(item);
                    if (deferredId != null)
                        (deferred ??= new List<(int, string, Type)>()).Add((items.Count - 1, deferredId, deferredType));
                }
                else
                    ok = false;
                deferredId = null;
            }
            if (!ok)
                return false;

            IList created;
            if (type.IsArray)
            {
                created = Array.CreateInstance(element, items.Count);
                for (int i = 0; i < items.Count; i++)
                    created[i] = items[i];
            }
            else
            {
                created = (IList)Activator.CreateInstance(type);
                foreach (var item in items)
                    created.Add(item);
            }
            if (deferred != null)
            {
                foreach (var (index, id, deferredAs) in deferred)
                    ((IDeferredAssetResolver)assets).Bind(id, deferredAs, asset => created[index] = asset);
            }
            result = created;
            return true;
        }

        static bool TryReadInteger(JToken token, Type type, JsonIssueList issues, string file, string path, out object result)
        {
            result = null;
            if (token.Type != JTokenType.Integer)
                return Fail(issues, file, path, token, "Expected a whole number.");
            try
            {
                result = Convert.ChangeType(((JValue)token).Value, type, Inv);
                return true;
            }
            catch (Exception)
            {
                return Fail(issues, file, path, token, $"{token} is out of range for {type.Name}.");
            }
        }

        static bool TryReadEnum(JToken token, Type type, JsonIssueList issues, string file, string path, out object result)
        {
            result = null;
            if (token.Type == JTokenType.Integer)
            {
                result = Enum.ToObject(type, (long)token);
                return true;
            }
            if (token.Type != JTokenType.String)
                return Fail(issues, file, path, token, $"Expected a {type.Name} name. Valid values: {string.Join(", ", Enum.GetNames(type))}.");

            string text = (string)token;
            string[] parts = text.Split(',');
            bool isFlags = type.IsDefined(typeof(FlagsAttribute), false);
            if (parts.Length > 1 && !isFlags)
                return Fail(issues, file, path, token, $"{type.Name} takes a single value. Valid values: {string.Join(", ", Enum.GetNames(type))}.");

            foreach (string part in parts)
            {
                string name = part.Trim();
                if (!Enum.IsDefined(type, name))
                    return Fail(issues, file, path, token, $"\"{name}\" is not a {type.Name}. Valid values: {string.Join(", ", Enum.GetNames(type))}.");
            }
            result = Enum.Parse(type, text);
            return true;
        }

        bool TryReadAsset(JToken token, Type type, JsonIssueList issues, string file, string path, out object result)
        {
            result = null;
            if (token.Type == JTokenType.Null)
                return true;
            if (token.Type != JTokenType.String)
                return Fail(issues, file, path, token, $"Expected an asset id string (a {type.Name}) or null.");

            string id = (string)token;
            if (id.Length == 0)
                return true;

            if (assets is IDeferredAssetResolver deferring && deferring.Defers(id, type))
            {
                // Filled in when its asset is loaded (the caller that stores the value binds it).
                deferredId = id;
                deferredType = type;
                return true;
            }
            var asset = assets.Resolve(id, type, out string error);
            if (asset == null)
                return Fail(issues, file, path, token, error);
            result = asset;
            return true;
        }

        static bool TryReadBuiltIn(JToken token, Type type, JsonIssueList issues, string file, string path, out object result)
        {
            result = null;
            float[] f;
            if (type == typeof(Vector2)) { if (!TryReadFloats(token, 2, issues, file, path, out f)) return false; result = new Vector2(f[0], f[1]); return true; }
            if (type == typeof(Vector3)) { if (!TryReadFloats(token, 3, issues, file, path, out f)) return false; result = new Vector3(f[0], f[1], f[2]); return true; }
            if (type == typeof(Vector4)) { if (!TryReadFloats(token, 4, issues, file, path, out f)) return false; result = new Vector4(f[0], f[1], f[2], f[3]); return true; }
            if (type == typeof(Quaternion)) { if (!TryReadFloats(token, 4, issues, file, path, out f)) return false; result = new Quaternion(f[0], f[1], f[2], f[3]); return true; }
            if (type == typeof(Vector2Int)) { if (!TryReadFloats(token, 2, issues, file, path, out f) || !Whole(f, token, issues, file, path)) return false; result = new Vector2Int((int)f[0], (int)f[1]); return true; }
            if (type == typeof(Vector3Int)) { if (!TryReadFloats(token, 3, issues, file, path, out f) || !Whole(f, token, issues, file, path)) return false; result = new Vector3Int((int)f[0], (int)f[1], (int)f[2]); return true; }
            if (type == typeof(Color)) return TryReadColor(token, issues, file, path, out result);
            if (type == typeof(Color32))
            {
                if (!TryReadColor(token, issues, file, path, out object color)) return false;
                result = (Color32)(Color)color;
                return true;
            }
            if (type == typeof(Rect) || type == typeof(RectInt))
            {
                if (!TryReadRect(token, issues, file, path, out f)) return false;
                if (type == typeof(Rect)) { result = new Rect(f[0], f[1], f[2], f[3]); return true; }
                if (!Whole(f, token, issues, file, path)) return false;
                result = new RectInt((int)f[0], (int)f[1], (int)f[2], (int)f[3]);
                return true;
            }
            if (type == typeof(LayerMask))
            {
                if (token.Type != JTokenType.Integer)
                    return Fail(issues, file, path, token, "Expected the layer mask as an integer.");
                result = (LayerMask)(int)(long)token;
                return true;
            }
            return Fail(issues, file, path, token, $"{type.Name} is not supported.");
        }

        static bool TryReadFloats(JToken token, int count, JsonIssueList issues, string file, string path, out float[] values)
        {
            values = new float[count];
            string shape = "[" + string.Join(", ", XYZW, 0, count) + "]";

            if (token is JArray array)
            {
                if (array.Count != count)
                    return Fail(issues, file, path, token, $"Expected {count} numbers {shape}, got {array.Count}.");
                for (int i = 0; i < count; i++)
                {
                    if (!TryReadNumber(array[i], out double d))
                        return Fail(issues, file, path, array[i], $"Expected a number at index {i}.");
                    values[i] = (float)d;
                }
                return true;
            }
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    int index = Array.IndexOf(XYZW, property.Name);
                    if (index < 0 || index >= count)
                        return Fail(issues, file, path, property, $"Unexpected key \"{property.Name}\". Expected {shape}.");
                }
                for (int i = 0; i < count; i++)
                {
                    var component = obj[XYZW[i]];
                    if (component == null || !TryReadNumber(component, out double d))
                        return Fail(issues, file, path, token, $"Missing or non-numeric \"{XYZW[i]}\". Expected {shape}.");
                    values[i] = (float)d;
                }
                return true;
            }
            return Fail(issues, file, path, token, $"Expected {shape}.");
        }

        static bool TryReadRect(JToken token, JsonIssueList issues, string file, string path, out float[] values)
        {
            values = new float[4];
            string[] keys = { "x", "y", "width", "height" };
            if (!(token is JObject obj))
                return Fail(issues, file, path, token, "Expected {\"x\", \"y\", \"width\", \"height\"}.");
            foreach (var property in obj.Properties())
            {
                if (Array.IndexOf(keys, property.Name) < 0)
                    return Fail(issues, file, path, property, $"Unexpected key \"{property.Name}\". Expected x, y, width, height.");
            }
            for (int i = 0; i < 4; i++)
            {
                var component = obj[keys[i]];
                if (component == null || !TryReadNumber(component, out double d))
                    return Fail(issues, file, path, token, $"Missing or non-numeric \"{keys[i]}\".");
                values[i] = (float)d;
            }
            return true;
        }

        static bool TryReadColor(JToken token, JsonIssueList issues, string file, string path, out object result)
        {
            result = null;
            if (token.Type == JTokenType.String)
            {
                string text = (string)token;
                if (text.Length == 7 || text.Length == 9)
                {
                    if (text[0] == '#' && TryHexByte(text, 1, out int r) && TryHexByte(text, 3, out int g) && TryHexByte(text, 5, out int b))
                    {
                        int a = 255;
                        if (text.Length == 9 && !TryHexByte(text, 7, out a))
                            return Fail(issues, file, path, token, "Invalid hex color. Use \"#RRGGBB\" or \"#RRGGBBAA\".");
                        result = new Color(ByteToFloat(r), ByteToFloat(g), ByteToFloat(b), ByteToFloat(a));
                        return true;
                    }
                }
                return Fail(issues, file, path, token, "Invalid hex color. Use \"#RRGGBB\" or \"#RRGGBBAA\".");
            }
            if (token is JArray array && (array.Count == 3 || array.Count == 4))
            {
                var c = new float[] { 0f, 0f, 0f, 1f };
                for (int i = 0; i < array.Count; i++)
                {
                    if (!TryReadNumber(array[i], out double d))
                        return Fail(issues, file, path, array[i], $"Expected a number at index {i}.");
                    c[i] = (float)d;
                }
                result = new Color(c[0], c[1], c[2], c[3]);
                return true;
            }
            return Fail(issues, file, path, token, "Expected a color: \"#RRGGBB\", \"#RRGGBBAA\", or [r, g, b, a] with values from 0 to 1.");
        }

        static bool TryHexByte(string text, int start, out int value)
        {
            return int.TryParse(text.Substring(start, 2), NumberStyles.AllowHexSpecifier, Inv, out value);
        }

        static bool Whole(float[] values, JToken token, JsonIssueList issues, string file, string path)
        {
            foreach (float v in values)
            {
                if (v != Mathf.Round(v))
                    return Fail(issues, file, path, token, "Expected whole numbers.");
            }
            return true;
        }

        static bool TryReadNumber(JToken token, out double value)
        {
            switch (token.Type)
            {
                case JTokenType.Integer:
                    value = Convert.ToDouble(((JValue)token).Value, Inv);
                    return true;
                case JTokenType.Float:
                    value = (double)token;
                    return true;
                case JTokenType.String:
                    switch ((string)token)
                    {
                        case "NaN": value = double.NaN; return true;
                        case "Infinity": value = double.PositiveInfinity; return true;
                        case "-Infinity": value = double.NegativeInfinity; return true;
                    }
                    break;
            }
            value = 0;
            return false;
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        public static object CreateInstance(Type type)
        {
            if (type.IsValueType)
                return Activator.CreateInstance(type);
            var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            return constructor != null ? constructor.Invoke(null) : FormatterServices.GetUninitializedObject(type);
        }

        static FieldInfo Find(FieldInfo[] fields, string name)
        {
            foreach (var field in fields)
            {
                if (field.Name == name)
                    return field;
            }
            return null;
        }

        static string Suggest(string name, FieldInfo[] fields)
        {
            return SuggestName(name, fields.Select(f => f.Name));
        }

        /// <summary>" Did you mean "x"?" for the closest of <paramref name="candidates"/>, or "" if none is close.</summary>
        public static string SuggestName(string name, IEnumerable<string> candidates)
        {
            string best = null;
            int bestDistance = int.MaxValue;
            foreach (string candidate in candidates)
            {
                int distance = Distance(name.ToLowerInvariant(), candidate.ToLowerInvariant());
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best != null && bestDistance <= Math.Max(2, name.Length / 3) ? $" Did you mean \"{best}\"?" : "";
        }

        static int Distance(string a, string b)
        {
            var row = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++)
                row[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                int diagonal = row[0];
                row[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int above = row[j];
                    row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                    diagonal = above;
                }
            }
            return row[b.Length];
        }

        static bool Fail(JsonIssueList issues, string file, string path, JToken token, string message)
        {
            issues.Error(file, path, message, JsonText.LineOf(token));
            return false;
        }

        static string Join(string path, string name) => string.IsNullOrEmpty(path) ? name : path + "." + name;
    }
}
