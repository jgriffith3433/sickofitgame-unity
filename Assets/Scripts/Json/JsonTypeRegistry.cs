using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SickOfIt.Json
{
    /// <summary>Finds the concrete subclasses of <see cref="JsonPolymorphicAttribute"/> bases by their "type" names.</summary>
    public static class JsonTypeRegistry
    {
        static readonly Dictionary<Type, Dictionary<string, Type>> ByBase = new Dictionary<Type, Dictionary<string, Type>>();
        static List<Type> allNamedTypes;

        public static bool IsPolymorphic(Type type)
        {
            return type.IsClass && type.IsAbstract && type.IsDefined(typeof(JsonPolymorphicAttribute), false);
        }

        /// <summary>"type" name to concrete class, for every subclass of <paramref name="baseType"/>.</summary>
        public static IReadOnlyDictionary<string, Type> Subtypes(Type baseType)
        {
            if (ByBase.TryGetValue(baseType, out var map))
                return map;

            map = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var type in NamedTypes())
            {
                if (!baseType.IsAssignableFrom(type))
                    continue;
                string name = type.GetCustomAttribute<JsonTypeAttribute>(false).Name;
                if (map.TryGetValue(name, out var existing))
                    throw new InvalidOperationException($"\"{name}\" is used by both {existing.FullName} and {type.FullName} under {baseType.Name}.");
                map[name] = type;
            }
            ByBase[baseType] = map;
            return map;
        }

        public static string NameOf(Type type)
        {
            return type.GetCustomAttribute<JsonTypeAttribute>(false)?.Name;
        }

        static IEnumerable<Type> NamedTypes()
        {
            if (allNamedTypes != null)
                return allNamedTypes;

            allNamedTypes = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }
                foreach (var type in types)
                {
                    if (!type.IsAbstract && type.IsDefined(typeof(JsonTypeAttribute), false))
                        allNamedTypes.Add(type);
                }
            }
            return allNamedTypes;
        }
    }
}
