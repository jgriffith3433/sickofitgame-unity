#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SickOfIt.Data;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Generate Project, plus per-clip sprite sheets for videos, deaths, and backgrounds.
    /// </summary>
    public class GenerateProjectWindow : EditorWindow
    {
        Vector2 scroll;
        List<SourceMediaEntry> entries = new List<SourceMediaEntry>();
        bool busy;

        [MenuItem("SickOfIt/Generate Project")]
        public static void Open()
        {
            var window = GetWindow<GenerateProjectWindow>("Generate Project");
            window.minSize = new Vector2(640, 420);
            window.Refresh();
            window.Show();
        }

        void OnEnable()
        {
            Refresh();
        }

        void Refresh()
        {
            var gd = LoadGameDefinition();
            entries = SourceMediaCatalog.Collect(gd);
            for (int i = 0; i < entries.Count; i++)
                VideoSpriteSheetBaker.Annotate(gd, entries[i]);
        }

        void OnGUI()
        {
            var gd = LoadGameDefinition();
            EditorGUILayout.LabelField("Generate Project", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Builds the Unity project from GameDefinition. Source clips stay outside the project. " +
                "Build Sheets packs one video, death, or background mp4 into addressable sprite sheets.",
                EditorStyles.wordWrappedMiniLabel);

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

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField((entry.Exists ? "Present  " : "Missing  ") + entry.Label, style);
                    if (VideoSpriteSheetBaker.IsSequence(entry.Group))
                    {
                        GUI.enabled = entry.Exists && !busy;
                        string caption = entry.SheetsBuilt ? "Regenerate" : "Build Sheets";
                        if (GUILayout.Button(caption, GUILayout.Width(110)))
                            QueueSheetBuild(entry);
                        GUI.enabled = true;
                    }
                }

                EditorGUILayout.LabelField(entry.Path, EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(entry.SheetDetail))
                    EditorGUILayout.LabelField(entry.SheetDetail, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        void QueueSheetBuild(SourceMediaEntry entry)
        {
            if (busy || entry == null)
                return;

            busy = true;
            string clipId = entry.Label;
            string mp4Path = entry.Path;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    if (this == null)
                        return;
                    VideoSpriteSheetBaker.Regenerate(LoadGameDefinition(), clipId, mp4Path);
                    if (this == null)
                        return;
                    Refresh();
                    Repaint();
                }
                finally
                {
                    busy = false;
                }
            };
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
