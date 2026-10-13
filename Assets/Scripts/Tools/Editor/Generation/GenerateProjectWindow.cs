#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SickOfIt.Data;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generate Project, plus the authoring clips that are not on disk yet.
    /// </summary>
    public class GenerateProjectWindow : EditorWindow
    {
        Vector2 scroll;
        List<SourceMediaEntry> entries = new List<SourceMediaEntry>();

        [MenuItem("SickOfIt/Generate Project")]
        public static void Open()
        {
            var window = GetWindow<GenerateProjectWindow>("Generate Project");
            window.minSize = new Vector2(560, 420);
            window.Refresh();
            window.Show();
        }

        void OnEnable()
        {
            Refresh();
        }

        void Refresh()
        {
            entries = SourceMediaCatalog.Collect(LoadGameDefinition());
        }

        void OnGUI()
        {
            var gd = LoadGameDefinition();
            EditorGUILayout.LabelField("Generate Project", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Builds the Unity project from GameDefinition. Source clips stay outside the project.", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate", GUILayout.Height(28)))
                    ProjectGenerator.GenerateProject();
                if (GUILayout.Button("Refresh", GUILayout.Height(28), GUILayout.Width(90)))
                    Refresh();
                if (GUILayout.Button("Create Folders", GUILayout.Height(28), GUILayout.Width(120)))
                {
                    SourceMediaCatalog.CreateFolders(entries);
                    Refresh();
                }
            }

            if (gd == null)
            {
                EditorGUILayout.HelpBox("No GameDefinition found.", MessageType.Error);
                return;
            }

            int missing = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].Exists)
                    missing++;
            }

            EditorGUILayout.Space(4);
            if (missing == 0)
                EditorGUILayout.HelpBox("Every source file is in place.", MessageType.Info);
            else
                EditorGUILayout.HelpBox(missing + " source file" + (missing == 1 ? "" : "s") + " missing.", MessageType.Warning);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            string group = null;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Group != group)
                {
                    group = entry.Group;
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
                }

                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = entry.Exists
                    ? new Color(0.35f, 0.7f, 0.35f)
                    : new Color(0.85f, 0.3f, 0.25f);
                EditorGUILayout.LabelField((entry.Exists ? "Present  " : "Missing  ") + entry.Label, style);
                EditorGUILayout.LabelField(entry.Path, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        static GameDefinition LoadGameDefinition()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDefinition");
            if (guids.Length == 0)
                return null;
            return AssetDatabase.LoadAssetAtPath<GameDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
#endif
