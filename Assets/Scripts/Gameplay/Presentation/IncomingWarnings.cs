using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    /// <summary>Which edge of the view something is about to cross.</summary>
    public enum EdgeSide { Top, Bottom, Left, Right }

    /// <summary>One incoming-warning marker: a place on an edge, how long until the first thing there is in view, how many it stands for.</summary>
    public struct IncomingMarker
    {
        public EdgeSide Side;
        /// <summary>Where on the edge (0..1: left to right on the top and bottom, bottom to top on the sides) the soonest thing crosses.</summary>
        public float Along;
        /// <summary>The span of the edge the merged things cross (0..1); equal to Along for one thing.</summary>
        public float SpanMin, SpanMax;
        /// <summary>Seconds until the soonest of them is in view.</summary>
        public float Eta;
        public int Count;
        /// <summary>The soonest one is a creature (otherwise a shot).</summary>
        public bool Creature;
    }

    /// <summary>
    /// The maths of the incoming warnings, with no Unity objects: where a thing outside the view crosses its edge, and how a crowd
    /// of them folds into a few markers (merged by closeness along the edge, soonest first, capped). Everything is worked out
    /// again from the live positions and velocities each frame and nothing is kept, so a rewind needs no saving.
    /// Fill it with <see cref="Begin"/>, <see cref="Add"/> and <see cref="End"/>; the result is <see cref="Markers"/>. It reuses its list: no allocation per frame.
    /// </summary>
    public sealed class IncomingSet
    {
        const int MaxClusters = 32;

        readonly List<IncomingMarker> clusters = new List<IncomingMarker>(MaxClusters);
        Rect view;
        float lookahead, mergeGap;
        int cap;

        /// <summary>The markers after <see cref="End"/>: soonest arrival first, at most the cap.</summary>
        public IReadOnlyList<IncomingMarker> Markers => clusters;
        public int Count => clusters.Count;
        /// <summary>Things that got a marker (merged ones counted each), before the cap.</summary>
        public int Considered { get; private set; }

        public void Begin(Rect viewRect, float lookaheadSeconds, float mergeFraction, int maxMarkers)
        {
            clusters.Clear();
            view = viewRect;
            lookahead = lookaheadSeconds;
            mergeGap = Mathf.Max(0.001f, mergeFraction);
            cap = Mathf.Max(1, maxMarkers);
            Considered = 0;
        }

        /// <summary>
        /// Where a thing at 'position' moving at 'velocity' (world units per second) first enters 'view', if it is outside it now and does so
        /// within 'lookahead' seconds. A thing inside the view, moving away or sideways past it, standing still or too far off gets none.
        /// </summary>
        public static bool Crossing(Rect view, Vector2 position, Vector2 velocity, float lookahead, out EdgeSide side, out float along, out float eta)
        {
            side = EdgeSide.Top;
            along = 0f;
            eta = 0f;
            if (view.width <= 0f || view.height <= 0f || view.Contains(position))
                return false;
            float tEnter = 0f, tExit = float.MaxValue;
            bool xDecides = false;
            if (!Slab(position.x, velocity.x, view.xMin, view.xMax, ref tEnter, ref tExit, ref xDecides, true))
                return false;
            if (!Slab(position.y, velocity.y, view.yMin, view.yMax, ref tEnter, ref tExit, ref xDecides, false))
                return false;
            if (tEnter > tExit || tEnter > lookahead)
                return false;
            eta = tEnter;
            Vector2 at = position + velocity * tEnter;
            if (xDecides)
            {
                side = position.x < view.xMin ? EdgeSide.Left : EdgeSide.Right;
                along = Mathf.Clamp01((at.y - view.yMin) / view.height);
            }
            else
            {
                side = position.y > view.yMax ? EdgeSide.Top : EdgeSide.Bottom;
                along = Mathf.Clamp01((at.x - view.xMin) / view.width);
            }
            return true;
        }

        // The time window in which one axis is inside [lo, hi]; narrows [tEnter, tExit]; notes which axis is the later entry.
        static bool Slab(float p, float v, float lo, float hi, ref float tEnter, ref float tExit, ref bool xDecides, bool isX)
        {
            if (Mathf.Abs(v) < 1e-4f)
                return p >= lo && p <= hi;
            float a = (lo - p) / v, b = (hi - p) / v;
            if (a > b)
            {
                float t = a;
                a = b;
                b = t;
            }
            if (a > tEnter)
            {
                tEnter = a;
                xDecides = isX;
            }
            tExit = Mathf.Min(tExit, b);
            return tExit >= 0f;
        }

        /// <summary>Offers one hostile thing. Returns true if it will enter the view within the lookahead (and so is part of a marker).</summary>
        public bool Add(Vector2 position, Vector2 velocity, bool creature)
        {
            if (!Crossing(view, position, velocity, lookahead, out var side, out float along, out float eta))
                return false;
            Considered++;
            for (int i = 0; i < clusters.Count; i++)
            {
                var c = clusters[i];
                if (c.Side != side || along < c.SpanMin - mergeGap || along > c.SpanMax + mergeGap)
                    continue;
                Join(ref c, along, along, eta, 1, creature);
                clusters[i] = c;
                return true;
            }
            if (clusters.Count < MaxClusters)
                clusters.Add(new IncomingMarker { Side = side, Along = along, SpanMin = along, SpanMax = along, Eta = eta, Count = 1, Creature = creature });
            return true;
        }

        static void Join(ref IncomingMarker into, float spanMin, float spanMax, float eta, int count, bool creature)
        {
            if (eta < into.Eta)
            {
                into.Eta = eta;
                into.Creature = creature;
            }
            into.SpanMin = Mathf.Min(into.SpanMin, spanMin);
            into.SpanMax = Mathf.Max(into.SpanMax, spanMax);
            into.Count += count;
            // Rests on the middle of what it stands for, so a band is drawn across its span rather than at one fireball.
            into.Along = (into.SpanMin + into.SpanMax) * 0.5f;
        }

        /// <summary>Folds markers that grew towards each other, sorts the soonest first and drops what is past the cap.</summary>
        public void End()
        {
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < clusters.Count && !merged; i++)
                {
                    for (int j = i + 1; j < clusters.Count; j++)
                    {
                        var a = clusters[i];
                        var b = clusters[j];
                        if (a.Side != b.Side || b.SpanMin > a.SpanMax + mergeGap || a.SpanMin > b.SpanMax + mergeGap)
                            continue;
                        bool bSooner = b.Eta < a.Eta;
                        Join(ref a, b.SpanMin, b.SpanMax, b.Eta, b.Count, b.Creature);
                        if (!bSooner)
                            a.Creature = clusters[i].Creature;
                        clusters[i] = a;
                        clusters.RemoveAt(j);
                        merged = true;
                        break;
                    }
                }
            }
            // Insertion sort by arrival: a handful of items, no allocation.
            for (int i = 1; i < clusters.Count; i++)
            {
                var item = clusters[i];
                int j = i - 1;
                while (j >= 0 && clusters[j].Eta > item.Eta)
                {
                    clusters[j + 1] = clusters[j];
                    j--;
                }
                clusters[j + 1] = item;
            }
            if (clusters.Count > cap)
                clusters.RemoveRange(cap, clusters.Count - cap);
        }

        /// <summary>The marker's opacity: farAlpha at the lookahead, nearAlpha from nearSeconds on.</summary>
        public static float AlphaFor(float eta, IncomingWarningsConfig cfg)
        {
            float span = Mathf.Max(0.01f, cfg.lookaheadSeconds - cfg.nearSeconds);
            float t = Mathf.Clamp01((cfg.lookaheadSeconds - eta) / span);
            return Mathf.Lerp(cfg.farAlpha, cfg.nearAlpha, t);
        }
    }
}
