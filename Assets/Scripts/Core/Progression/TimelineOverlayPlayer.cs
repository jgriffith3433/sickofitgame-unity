using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Draws a timeline's <see cref="TimelineOverlay"/>s (keyframed pictures over a video, death or level).
    /// Every frame each overlay's pose and sheet frame are sampled from the game clock's time, never tweened,
    /// so rewinding the clock plays them backward, a seek lands on the right pose, and nothing has to be saved
    /// for rewind. Pure presentation: it reads the clock and changes nothing else.
    /// Created by <see cref="ExperienceRunner"/> when a timeline has overlays.
    /// </summary>
    public class TimelineOverlayPlayer : MonoBehaviour
    {
        /// <summary>An overlay appeared (true) or disappeared (false), with the clock time. For logs and the playtest.</summary>
        public static event Action<string, bool, float> VisibilityChanged;

        static readonly List<TimelineOverlayPlayer> players = new List<TimelineOverlayPlayer>();

        sealed class Item
        {
            public TimelineOverlay Config;
            public SpriteRenderer Renderer;
            public Sprite[] Frames;
            public bool Visible;
            public OverlayKeyframe Pose;
            public int Frame;
        }

        readonly List<Item> items = new List<Item>();
        float ownTime;

        /// <summary>The clock time the overlays were last drawn at.</summary>
        public float SampledTime { get; private set; }

        /// <summary>
        /// The last drawn pose of the overlay with this name in any running timeline: whether it is on screen,
        /// its pose (position, rotation in degrees, scale, alpha) and sheet frame.
        /// </summary>
        public static bool TryGetPose(string name, out bool visible, out OverlayKeyframe pose, out int frame)
        {
            for (int p = 0; p < players.Count; p++)
            {
                var items = players[p].items;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Config.name != name)
                        continue;
                    visible = items[i].Visible;
                    pose = items[i].Pose;
                    frame = items[i].Frame;
                    return true;
                }
            }
            visible = false;
            pose = null;
            frame = 0;
            return false;
        }

        public void Play(List<TimelineOverlay> overlays)
        {
            if (overlays == null)
                return;
            var definition = GameDefinition.Instance;
            foreach (var overlay in overlays)
            {
                if (overlay == null)
                    continue;
                string problem = null;
                foreach (string p in overlay.Problems())
                {
                    problem = p;
                    break;
                }
                if (problem != null)
                {
                    Debug.LogWarning($"[TimelineOverlay] Skipping it: {problem}.");
                    continue;
                }

                var go = new GameObject("Overlay " + overlay.name);
                // Under the owner, so it lives (and unloads) in the owner's scene.
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                SpriteLayers.Apply(renderer, overlay.sortLayer, overlay.sortOrder);
                renderer.enabled = false;

                Sprite[] frames = overlay.spriteSheet != null && definition != null
                    ? Filter(definition.SpritesOf(overlay.spriteSheet), overlay.spriteNameFilter)
                    : Array.Empty<Sprite>();
                if (frames.Length == 0 && overlay.sprite != null)
                    frames = new[] { overlay.sprite };
                if (frames.Length == 0)
                    Debug.LogWarning($"[TimelineOverlay] \"{overlay.name}\": the sheet {(overlay.spriteSheet != null ? overlay.spriteSheet.name : "(none)")} has no sprites in the asset registry; nothing to draw.");

                items.Add(new Item { Config = overlay, Renderer = renderer, Frames = frames });
            }
            if (items.Count > 0 && !players.Contains(this))
                players.Add(this);
            Sample(ClockTime());
        }

        static Sprite[] Filter(Sprite[] sprites, string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return sprites;
            var kept = new List<Sprite>();
            foreach (var sprite in sprites)
            {
                if (sprite != null && sprite.name.Contains(filter))
                    kept.Add(sprite);
            }
            return kept.ToArray();
        }

        float ClockTime()
        {
            var clock = GameClock.Instance;
            return clock != null ? clock.Time : ownTime;
        }

        void LateUpdate()
        {
            if (GameClock.Instance == null)
                ownTime += Time.deltaTime;
            Sample(ClockTime());
        }

        void Sample(float t)
        {
            SampledTime = t;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                bool visible = item.Config.Sample(t, out var pose) && item.Frames.Length > 0;
                if (visible)
                {
                    item.Pose = pose;
                    item.Frame = item.Config.FrameAt(t, item.Frames.Length);
                    var renderer = item.Renderer;
                    renderer.sprite = item.Frames[item.Frame];
                    var color = item.Config.tint;
                    color.a *= pose.alpha;
                    renderer.color = color;
                    var tr = renderer.transform;
                    tr.position = new Vector3(pose.position.x, pose.position.y, tr.position.z);
                    tr.rotation = Quaternion.Euler(0f, 0f, pose.rotation);
                    tr.localScale = new Vector3(pose.scale, pose.scale, 1f);
                }

                item.Renderer.enabled = visible;
                if (visible != item.Visible)
                {
                    item.Visible = visible;
                    Debug.Log($"[TimelineOverlay] \"{item.Config.name}\" {(visible ? "shown" : "hidden")} at {t:F2}s");
                    VisibilityChanged?.Invoke(item.Config.name, visible, t);
                }
            }
        }

        void OnDestroy()
        {
            players.Remove(this);
        }
    }
}
