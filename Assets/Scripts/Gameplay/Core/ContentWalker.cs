using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SickOfIt.Gameplay.Content;
using SickOfIt.Json;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Walks a content object graph field by field, in declaration order, with JSON-style paths.
    /// Used to index actions for the director, to check references, and to generate documentation.
    /// </summary>
    public static class ContentWalker
    {
        /// <summary>Called for every field of every content object reached: (owner, field, value, path).</summary>
        public delegate void FieldVisitor(object owner, FieldInfo field, object value, string path);

        public static void Walk(object root, FieldVisitor visitor, string path = "")
        {
            Walk(root, visitor, path, 0);
        }

        static void Walk(object node, FieldVisitor visitor, string path, int depth)
        {
            if (node == null || depth > 64)
                return;
            var type = node.GetType();
            if (!IsContentObject(type))
                return;

            foreach (var field in UnitySerializationRules.GetSerializedFields(type))
            {
                object value = field.GetValue(node);
                string fieldPath = string.IsNullOrEmpty(path) ? field.Name : path + "." + field.Name;
                visitor(node, field, value, fieldPath);

                if (value is IList list && !(value is string))
                {
                    for (int i = 0; i < list.Count; i++)
                        Walk(list[i], visitor, $"{fieldPath}[{i}]", depth + 1);
                }
                else if (value != null && IsContentObject(value.GetType()))
                {
                    Walk(value, visitor, fieldPath, depth + 1);
                }
            }
        }

        static bool IsContentObject(Type type)
        {
            return type.IsClass && type != typeof(string) && !typeof(UnityEngine.Object).IsAssignableFrom(type)
                && type.Namespace != null && type.Namespace.StartsWith("SickOfIt.Gameplay", StringComparison.Ordinal);
        }

        /// <summary>Every GameAction reachable from <paramref name="root"/>, in a stable order.</summary>
        public static void CollectActions(object root, List<GameAction> into)
        {
            if (root is GameAction rootAction)
                into.Add(rootAction);
            Walk(root, (owner, field, value, path) =>
            {
                if (value is GameAction action)
                    into.Add(action);
                else if (value is IList list)
                {
                    foreach (var item in list)
                    {
                        if (item is GameAction a)
                            into.Add(a);
                    }
                }
            });
        }
    }
}
