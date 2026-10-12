#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Ensures the five sprite sorting layers exist, back to front, after Default.
    /// Generate Project calls this before it assigns layers on prefabs.
    /// </summary>
    public static class SortingLayerSetup
    {
        public const int Background1Id = 1411202101;
        public const int Background2Id = 1411202102;
        public const int PlayerId = 1411202103;
        public const int Foreground1Id = 1411202104;
        public const int Foreground2Id = 1411202105;
        public const int UiId = 1411202106;

        private static readonly (string name, int id)[] Layers =
        {
            ("Background1", Background1Id),
            ("Background2", Background2Id),
            ("Player", PlayerId),
            ("Foreground1", Foreground1Id),
            ("Foreground2", Foreground2Id),
            ("UI", UiId),
        };

        public static void Ensure()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[SortingLayerSetup] TagManager.asset not found.");
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            var sortingLayers = tagManager.FindProperty("m_SortingLayers");
            if (sortingLayers == null || !sortingLayers.isArray)
            {
                Debug.LogError("[SortingLayerSetup] m_SortingLayers is missing on TagManager.");
                return;
            }

            var knownIds = new Dictionary<string, int>();
            var extras = new List<(string name, int id, int locked)>();
            for (int i = 0; i < sortingLayers.arraySize; i++)
            {
                var element = sortingLayers.GetArrayElementAtIndex(i);
                string name = element.FindPropertyRelative("name").stringValue;
                int id = element.FindPropertyRelative("uniqueID").intValue;
                int locked = element.FindPropertyRelative("locked").intValue;
                if (name == "Default")
                    continue;

                bool known = false;
                for (int n = 0; n < Layers.Length; n++)
                {
                    if (Layers[n].name != name)
                        continue;
                    knownIds[name] = id;
                    known = true;
                    break;
                }

                if (!known)
                    extras.Add((name, id, locked));
            }

            sortingLayers.arraySize = 1 + Layers.Length + extras.Count;
            Write(sortingLayers, 0, "Default", 0, 0);
            for (int i = 0; i < Layers.Length; i++)
            {
                int id = knownIds.TryGetValue(Layers[i].name, out int found) ? found : Layers[i].id;
                Write(sortingLayers, i + 1, Layers[i].name, id, 0);
            }

            for (int i = 0; i < extras.Count; i++)
                Write(sortingLayers, 1 + Layers.Length + i, extras[i].name, extras[i].id, extras[i].locked);

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Write(SerializedProperty array, int index, string name, int id, int locked)
        {
            var element = array.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("name").stringValue = name;
            element.FindPropertyRelative("uniqueID").intValue = id;
            element.FindPropertyRelative("locked").intValue = locked;
        }
    }
}
#endif
