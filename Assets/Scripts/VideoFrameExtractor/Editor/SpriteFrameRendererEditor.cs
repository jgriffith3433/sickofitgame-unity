#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using SickOfIt.VideoFrameExtractor.Runtime;

namespace SickOfIt.VideoFrameExtractor.Editor
{
    [CustomEditor(typeof(SpriteFrameRenderer))]
    public class SpriteFrameRendererEditor : UnityEditor.Editor
    {
        private SpriteFrameRenderer renderer;

        private void OnEnable()
        {
            renderer = (SpriteFrameRenderer)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Status
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            bool isLoaded = renderer.IsLoaded;
            int totalFrames = renderer.TotalFrames;

            if (Application.isPlaying)
            {
                if (isLoaded)
                {
                    EditorGUILayout.HelpBox(
                        "Ready\n" +
                        "Total frames: " + totalFrames + "\n" +
                        "Current frame: " + renderer.GetCurrentFrame() + "\n" +
                        "Progress: " + (renderer.GetProgress() * 100f).ToString("F1") + "%",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Loading chunks...\nWaiting for initial preload to complete.",
                        MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Not playing. Chunks load automatically when the Timeline plays.\n" +
                    "Scrub the Timeline to preview frames in edit mode.",
                    MessageType.Info);
            }

            // Debug fields (read-only)
            GUI.enabled = false;
            EditorGUILayout.IntField("Current Frame", renderer.GetCurrentFrame());
            EditorGUILayout.IntField("Total Frames", renderer.TotalFrames);
            GUI.enabled = true;

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "SPRITE FRAME PLAYBACK:\n\n" +
                "SpriteFrameRenderer is legacy. Use SpriteFramePlayer instead.\n" +
                "SpriteFramePlayer is driven by GameClock and supports rewind.\n\n" +
                "ExperienceRunner creates SpriteFramePlayers automatically\n" +
                "from PlaySpriteSequence events in the timeline config.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
