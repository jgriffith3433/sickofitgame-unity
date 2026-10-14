using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SickOfIt.Json
{
    /// <summary>
    /// The subset of Unity's serialization rules that decides which fields an asset stores.
    /// Config JSON follows these exactly, so a JSON file holds the same data the asset does.
    /// </summary>
    public static class UnitySerializationRules
    {
        static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();

        /// <summary>Unity value types and classes that Unity serializes and config JSON has an encoding for.</summary>
        static readonly HashSet<Type> SupportedBuiltIns = new HashSet<Type>
        {
            typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Quaternion),
            typeof(Vector2Int), typeof(Vector3Int),
            typeof(Color), typeof(Color32),
            typeof(Rect), typeof(RectInt),
            typeof(LayerMask),
        };

        /// <summary>Types Unity serializes that config JSON does not encode yet. Hitting one is a loud error, never silent data loss.</summary>
        static readonly HashSet<Type> UnsupportedBuiltIns = new HashSet<Type>
        {
            typeof(AnimationCurve), typeof(Gradient), typeof(Matrix4x4), typeof(RectOffset),
            typeof(Bounds), typeof(BoundsInt), typeof(Hash128), typeof(GUIStyle),
        };

        public static bool IsSupportedBuiltIn(Type type) => SupportedBuiltIns.Contains(type);
        public static bool IsUnsupportedBuiltIn(Type type) => UnsupportedBuiltIns.Contains(type);

        /// <summary>Serialized fields of <paramref name="type"/>, base class fields first, each in declaration order.</summary>
        public static FieldInfo[] GetSerializedFields(Type type)
        {
            if (FieldCache.TryGetValue(type, out var cached))
                return cached;

            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(object) && !IsUnityObjectBase(t); t = t.BaseType)
                chain.Add(t);
            chain.Reverse();

            var fields = new List<FieldInfo>();
            foreach (var t in chain)
            {
                var declared = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .OrderBy(f => f.MetadataToken);
                foreach (var field in declared)
                {
                    if (IsSerializedField(field))
                        fields.Add(field);
                }
            }

            var result = fields.ToArray();
            FieldCache[type] = result;
            return result;
        }

        public static bool IsSerializedField(FieldInfo field)
        {
            if (field.IsStatic || field.IsInitOnly || field.IsLiteral)
                return false;
            if (field.IsDefined(typeof(NonSerializedAttribute), false))
                return false;

            bool serializeReference = field.IsDefined(typeof(SerializeReference), false);
            if (!field.IsPublic && !serializeReference && !field.IsDefined(typeof(SerializeField), false))
                return false;
            if (serializeReference)
                throw new NotSupportedException(
                    $"{field.DeclaringType?.Name}.{field.Name} uses [SerializeReference], which config JSON does not support.");

            return IsSerializableType(field.FieldType);
        }

        /// <summary>Whether Unity would serialize a field of this type. Unity silently skips the rest (dictionaries, interfaces, ...).</summary>
        public static bool IsSerializableType(Type type)
        {
            if (IsCollection(type, out var element))
                return !IsCollection(element, out _) && IsSerializableElement(element);
            return IsSerializableElement(type);
        }

        /// <summary>True for T[] and List&lt;T&gt;, the two collection shapes Unity serializes.</summary>
        public static bool IsCollection(Type type, out Type element)
        {
            if (type.IsArray && type.GetArrayRank() == 1)
            {
                element = type.GetElementType();
                return true;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                element = type.GetGenericArguments()[0];
                return true;
            }
            element = null;
            return false;
        }

        public static bool IsUnityObject(Type type) => typeof(UnityEngine.Object).IsAssignableFrom(type);

        static bool IsSerializableElement(Type type)
        {
            if (type.IsPrimitive)
                return type != typeof(IntPtr) && type != typeof(UIntPtr);
            if (type == typeof(string) || type.IsEnum)
                return true;
            if (IsUnityObject(type))
                return true;
            if (SupportedBuiltIns.Contains(type) || UnsupportedBuiltIns.Contains(type))
                return true;
            // Content types only: Unity never stores these, config JSON picks the subclass by "type".
            if (JsonTypeRegistry.IsPolymorphic(type))
                return true;
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                return false;
            if (typeof(Delegate).IsAssignableFrom(type))
                return false;
            if (type.Namespace != null && (type.Namespace == "System" || type.Namespace.StartsWith("System.", StringComparison.Ordinal)))
                return false;
            return type.IsDefined(typeof(SerializableAttribute), false);
        }

        static bool IsUnityObjectBase(Type type)
        {
            return type == typeof(UnityEngine.Object) || type == typeof(ScriptableObject) || type == typeof(MonoBehaviour)
                || type == typeof(Behaviour) || type == typeof(Component);
        }
    }
}
