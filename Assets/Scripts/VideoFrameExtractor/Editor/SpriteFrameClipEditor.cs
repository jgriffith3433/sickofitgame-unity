#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using SickOfIt.VideoFrameExtractor.Runtime;

namespace SickOfIt.VideoFrameExtractor.Editor
{
    [CustomEditor(typeof(SpriteFrameClip))]
    public class SpriteFrameClipEditor : UnityEditor.Editor
    {
        private SerializedProperty spriteFramesDataProp;
        private SerializedProperty endBehaviourProp;

        private void OnEnable()
        {
            spriteFramesDataProp = serializedObject.FindProperty("spriteFramesData");
            endBehaviourProp = serializedObject.FindProperty("endBehaviour");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(spriteFramesDataProp, new GUIContent("Sprite Frames Data"));
            EditorGUILayout.PropertyField(endBehaviourProp, new GUIContent("Playback Mode"));

            var clip = target as SpriteFrameClip;
            if (clip != null && clip.SpriteFramesData != null)
            {
                var data = clip.SpriteFramesData;
                int totalFrames = data.GetStoredTotalFrames();
                float defaultDuration = totalFrames > 0 ? totalFrames / 30f : 0f;

                GUILayout.Space(5);
                EditorGUILayout.LabelField("Clip Info", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total Frames: {totalFrames}");
                EditorGUILayout.LabelField($"Default Duration: {defaultDuration:F2}s (at 30 FPS)");
                EditorGUILayout.LabelField($"Chunks: {data.chunks.Count}");

                // Find the TimelineClip that owns this PlayableAsset and show its current duration
                var timelineClip = FindTimelineClip(clip);
                if (timelineClip != null)
                {
                    float currentSpeed = defaultDuration > 0 ? (float)(defaultDuration / timelineClip.duration) : 1f;
                    EditorGUILayout.LabelField($"Timeline Clip Duration: {timelineClip.duration:F2}s");
                    EditorGUILayout.LabelField($"Effective Speed: {currentSpeed:F2}x");

                    GUILayout.Space(5);
                    if (GUILayout.Button("Reset Duration to 30 FPS"))
                    {
                        SetTimelineClipDuration(timelineClip, defaultDuration);
                    }
                }

                switch (clip.EndBehaviour)
                {
                    case SpriteFrameEndBehaviour.Loop:
                        EditorGUILayout.HelpBox(
                            "Loop: Extend this clip on the Timeline to repeat the animation. " +
                            "Each cycle plays all frames once at native FPS.",
                            MessageType.Info);
                        break;

                    case SpriteFrameEndBehaviour.Hold:
                        EditorGUILayout.HelpBox(
                            "Hold: Plays the animation once at native FPS (30), then freezes " +
                            "on the last frame. Extend the clip to hold the final pose longer.",
                            MessageType.Info);
                        break;

                    default: // Speed
                        EditorGUILayout.HelpBox(
                            "Speed: Resize this clip on the Timeline to change playback speed. " +
                            $"Default = 30 FPS ({defaultDuration:F2}s). Stretch = slower, shrink = faster.",
                            MessageType.Info);
                        break;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Assign a SpriteFramesData asset to configure this clip.", MessageType.Warning);
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                // Changing the playback mode affects clipCaps; force the Timeline
                // to re-evaluate so the clip immediately reflects the new mode.
                TimelineEditor.Refresh(RefreshReason.ContentsModified);
            }
        }

        /// <summary>
        /// Searches all tracks in the inspected Timeline for the TimelineClip
        /// that wraps this PlayableAsset.
        /// </summary>
        private static TimelineClip FindTimelineClip(PlayableAsset asset)
        {
            var timelineAsset = TimelineEditor.inspectedAsset;
            if (timelineAsset == null) return null;

            foreach (var track in timelineAsset.GetOutputTracks())
            {
                foreach (var timelineClip in track.GetClips())
                {
                    if (timelineClip.asset == asset)
                        return timelineClip;
                }
            }
            return null;
        }

        /// <summary>
        /// Sets a TimelineClip's duration with proper Undo support.
        /// </summary>
        internal static void SetTimelineClipDuration(TimelineClip timelineClip, double duration)
        {
            if (duration <= 0) return;

            var track = timelineClip.GetParentTrack();
            if (track != null)
                Undo.RecordObject(track, "Reset Clip Duration to 30 FPS");

            timelineClip.duration = duration;

            if (track != null)
                EditorUtility.SetDirty(track);

            TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }
    }

    /// <summary>
    /// Right-click context menu action on SpriteFrameClip clips in the Timeline.
    /// </summary>
    [MenuEntry("Reset Duration to 30 FPS", MenuPriority.CustomClipActionSection.customClipAction)]
    public class ResetClipDurationAction : ClipAction
    {
        public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
        {
            foreach (var clip in clips)
            {
                if (clip.asset is SpriteFrameClip sfClip &&
                    sfClip.SpriteFramesData != null &&
                    sfClip.SpriteFramesData.GetStoredTotalFrames() > 0)
                    return ActionValidity.Valid;
            }
            return ActionValidity.NotApplicable;
        }

        public override bool Execute(IEnumerable<TimelineClip> clips)
        {
            bool any = false;
            foreach (var clip in clips)
            {
                if (clip.asset is SpriteFrameClip sfClip &&
                    sfClip.SpriteFramesData != null)
                {
                    int totalFrames = sfClip.SpriteFramesData.GetStoredTotalFrames();
                    if (totalFrames > 0)
                    {
                        double duration = totalFrames / 30.0;
                        SpriteFrameClipEditor.SetTimelineClipDuration(clip, duration);
                        any = true;
                    }
                }
            }
            return any;
        }
    }
}
#endif
