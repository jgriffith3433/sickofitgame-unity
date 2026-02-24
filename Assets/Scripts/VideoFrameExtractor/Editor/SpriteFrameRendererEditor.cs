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
                "TIMELINE-DRIVEN SPRITE FRAMES:\n\n" +
                "1. Add a SpriteFrameTrack to your Timeline\n" +
                "2. Bind this SpriteFrameRenderer as the track target\n" +
                "3. Create clips referencing SpriteFramesData assets\n" +
                "4. Configure chunk loading on the SpriteFrameTrack\n\n" +
                "Everything is automatic - the track discovers data\n" +
                "from clips and manages chunk loading itself.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
