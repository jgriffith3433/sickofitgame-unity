using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Json;

namespace SickOfIt.Gameplay.Content
{
    // =====================================================================
    // ART FOR THE FLOOR TOOLKIT AND THE WORLD BELOW (Docs/LevelDesign.md, "The art of the climb")
    //
    // Painted strips laid along a line: the lava sea's surface, a lava fall's curtain, a chain, the far sea and the
    // smoke bands of the backdrop. A strip is one sliced sheet (one frame, or a loop) repeated along its length, and
    // can scroll (lava pouring, the sea drifting). Presentation only: it reads the game clock it is given and never
    // touches the simulation, so a rewind plays it backward with everything else.
    // =====================================================================

    [Serializable]
    [Doc("Painted art laid as a strip: a sliced sheet (one frame or a loop, frames in name order) repeated along a line (a lava surface, " +
         "a chain, a lava fall, the far sea). Without a sheet, the piece keeps its drawn shape.")]
    public class ArtLoopDef
    {
        [Doc("The sliced sheet (Assets/Art/Climb/...). Empty = no art.")] public Texture2D sheet;
        [Doc("Only sprites whose names start with this.")] public string spriteNameFilter = "";
        [Doc("Frames per second of the loop (one frame = a still).")] public float fps = 12f;
        [Doc("Scroll along the strip, in tiles per second (lava pouring down, the sea drifting). 0 = still.")] public float scroll;
        [Doc("Tint multiplied into the art.")] public Color color = Color.white;

        public bool IsSet => sheet != null;
    }

    /// <summary>Draws <see cref="ArtLoopDef"/> strips as child sprites of a transform (presentation only).</summary>
    public static class FloorArt
    {
        static readonly Dictionary<Texture2D, Sprite[]> Cache = new Dictionary<Texture2D, Sprite[]>();

        /// <summary>The frames of an art loop (empty without a sheet or before the content is loaded).</summary>
        public static Sprite[] Frames(ArtLoopDef art, ISpriteSheetSource sheets)
        {
            if (art == null || art.sheet == null || sheets == null)
                return Array.Empty<Sprite>();
            if (!Cache.TryGetValue(art.sheet, out var all) || all == null || all.Length == 0)
            {
                all = sheets.GetSprites(art.sheet) ?? Array.Empty<Sprite>();
                Cache[art.sheet] = all;
            }
            if (string.IsNullOrEmpty(art.spriteNameFilter))
                return all;
            var filtered = new List<Sprite>();
            foreach (var s in all)
            {
                if (s != null && s.name.StartsWith(art.spriteNameFilter, StringComparison.Ordinal))
                    filtered.Add(s);
            }
            return filtered.ToArray();
        }

        public static Sprite[] Frames(ArtLoopDef art, GameplayWorld world) =>
            Frames(art, world != null && world.Content != null ? world.Content.Sheets : null);

        /// <summary>The frame of a loop at a time.</summary>
        public static Sprite Frame(Sprite[] frames, float fps, float time)
        {
            if (frames == null || frames.Length == 0)
                return null;
            int i = Mathf.FloorToInt(Mathf.Max(0f, time) * Mathf.Max(0.01f, fps));
            return frames[((i % frames.Length) + frames.Length) % frames.Length];
        }

        /// <summary>
        /// Lays a strip of tiles from 'from' to 'to', each 'thickness' wide across the line and as long as its art's aspect
        /// says. Vertical art (taller than wide: a chain, a fall) is laid from 'from' at its top to 'to' at its bottom;
        /// horizontal art (a surface, a sea) from left to right. fit: the tiles are stretched a little so a whole number of
        /// them fills the line exactly (chains, falls); otherwise they keep their length and may overhang the far end (seas
        /// and surfaces wider than the screen). art.scroll moves them along the line. Returns false (and hides the tiles)
        /// without art.
        /// </summary>
        public static bool Strip(Transform parent, string name, ArtLoopDef art, Sprite[] frames, Vector2 from, Vector2 to, float thickness,
            float time, SpriteLayer layer, int order, bool fit, float alpha = 1f)
        {
            var holder = Holder(parent, name);
            var frame = Frame(frames, art != null ? art.fps : 0f, time);
            Vector2 line = to - from;
            float length = line.magnitude;
            if (frame == null || length < 0.001f || thickness <= 0f)
            {
                HideAll(holder);
                return false;
            }
            Vector2 axis = line / length;
            Vector2 native = frame.bounds.size;
            bool vertical = native.y > native.x;
            float along = vertical ? native.y : native.x, across = vertical ? native.x : native.y;
            float tile = thickness * along / Mathf.Max(0.0001f, across);
            int count = fit
                ? Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(0.01f, tile)))
                : Mathf.CeilToInt(length / Mathf.Max(0.01f, tile));
            if (fit)
                tile = length / count;
            float shift = art.scroll != 0f ? Mathf.Repeat(time * art.scroll, 1f) * tile : 0f;
            // A scrolling strip needs one more tile coming in at the start.
            int shown = Mathf.Min(64, count + (shift > 0f ? 1 : 0));
            // The sprite's long axis onto the line: +x along it, or for vertical art its top at 'from'.
            float angle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg + (vertical ? 90f : 0f);
            var color = art.color;
            color.a *= alpha;
            Vector3 parentScale = parent.lossyScale;
            float sx = (vertical ? thickness : tile) / Mathf.Max(0.0001f, native.x) / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
            float sy = (vertical ? tile : thickness) / Mathf.Max(0.0001f, native.y) / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
            while (holder.childCount < shown)
            {
                var go = new GameObject(name + holder.childCount);
                go.transform.SetParent(holder, false);
                go.AddComponent<SpriteRenderer>();
            }
            for (int i = 0; i < holder.childCount; i++)
            {
                if (!holder.GetChild(i).TryGetComponent<SpriteRenderer>(out var sr))
                    continue;
                if (i >= shown)
                {
                    sr.enabled = false;
                    continue;
                }
                sr.enabled = true;
                sr.sprite = frame;
                sr.color = color;
                SpriteLayers.Apply(sr, layer, order);
                float d = (i + 0.5f) * tile + shift - (shift > 0f ? tile : 0f);
                Vector2 c = from + axis * d;
                var t = sr.transform;
                t.position = new Vector3(c.x, c.y, t.position.z);
                t.rotation = Quaternion.Euler(0f, 0f, angle);
                t.localScale = new Vector3(sx, sy, 1f);
            }
            return true;
        }

        /// <summary>Hides a strip (the piece went back to its drawn shape).</summary>
        public static void Hide(Transform parent, string name)
        {
            var holder = parent.Find(name);
            if (holder != null)
                HideAll(holder);
        }

        static void HideAll(Transform holder)
        {
            for (int i = 0; i < holder.childCount; i++)
            {
                if (holder.GetChild(i).TryGetComponent<SpriteRenderer>(out var sr))
                    sr.enabled = false;
            }
        }

        static Transform Holder(Transform parent, string name)
        {
            var holder = parent.Find(name);
            if (holder != null)
                return holder;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
