using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Moves backgrounds, depth layers and weather at their own share of the camera's travel (BackgroundLayerConfig.parallaxFactor,
    /// AmbienceDef). An entry's factor is how much it follows the camera: 1 = glued to the screen, 0 = fixed in the world, in between =
    /// far away, drifting slowly the other way; negative = close, rushing past. An entry with a period wraps around the camera so
    /// a short row of shapes (or a tiled sprite) covers the screen however far it travels. Presentation only: it reads the camera,
    /// never touches the simulation, and runs after the camera has been placed (shake included).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class ParallaxController : MonoBehaviour
    {
        /// <summary>One moving piece: a root transform that stands where it was built when the camera is where it was.</summary>
        public sealed class Entry
        {
            public string Name;
            public Transform Root;
            /// <summary>World position of the root when added.</summary>
            public Vector3 Home;
            /// <summary>The camera's position when added.</summary>
            public Vector2 CameraHome;
            /// <summary>Follow factor on x and y.</summary>
            public Vector2 Factor;
            /// <summary>Wrap distance on each axis (0 = does not wrap).</summary>
            public Vector2 Period;
            /// <summary>How far the root may stray from its home offset to the camera before its edge would show (infinity = no limit).</summary>
            public Vector2 Slack = new Vector2(float.PositiveInfinity, float.PositiveInfinity);

            public Vector2 Position => Root != null ? (Vector2)Root.position : Vector2.zero;
        }

        static ParallaxController instance;

        readonly List<Entry> entries = new List<Entry>();
        Camera cam;

        public IReadOnlyList<Entry> Entries => entries;
        public int Count => entries.Count;
        public Camera Camera => cam;

        /// <summary>Where the camera is now (world), as the entries see it.</summary>
        public Vector2 CameraPosition => cam != null ? (Vector2)cam.transform.position : Vector2.zero;

        /// <summary>The controller of this level, made on first use.</summary>
        public static ParallaxController Obtain(Camera camera)
        {
            if (instance != null && instance.cam == camera)
                return instance;
            if (!Application.isPlaying || camera == null)
                return null;
            var go = new GameObject("[Parallax]");
            instance = go.AddComponent<ParallaxController>();
            instance.cam = camera;
            return instance;
        }

        /// <summary>The live controller, if a level has made one (the playtest reads it).</summary>
        public static ParallaxController Current => instance;

        public Entry Add(string name, Transform root, Vector2 factor, Vector2 period)
        {
            var entry = new Entry
            {
                Name = name,
                Root = root,
                Home = root.position,
                CameraHome = CameraPosition,
                Factor = factor,
                Period = period,
            };
            entries.Add(entry);
            return entry;
        }

        public Entry Find(string name)
        {
            foreach (var e in entries)
                if (e.Name == name)
                    return e;
            return null;
        }

        bool placed;
        Vector2 lastCamPos;
        int lastEntries;

        void LateUpdate()
        {
            if (cam == null)
                return;
            Vector2 camPos = cam.transform.position;
            // Rewinding, the layers are only placed when the camera has moved (it follows the rewound hero, and a layer
            // must keep its factor to it, so it isn't frozen outright); still, they stay where they were.
            bool rewinding = GameClock.Instance != null && GameClock.Instance.IsRewinding;
            if (rewinding && placed && camPos == lastCamPos && entries.Count == lastEntries)
                return;
            placed = true;
            lastCamPos = camPos;
            lastEntries = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Root == null)
                    continue;
                Vector2 p = Place(e, camPos);
                e.Root.position = new Vector3(p.x, p.y, e.Home.z);
            }
        }

        /// <summary>Where an entry stands when the camera is at camPos (pure, so a test can check it).</summary>
        public static Vector2 Place(Entry e, Vector2 camPos)
        {
            Vector2 delta = camPos - e.CameraHome;
            Vector2 p = new Vector2(e.Home.x + delta.x * e.Factor.x, e.Home.y + delta.y * e.Factor.y);
            Vector2 offset = new Vector2(e.Home.x - e.CameraHome.x, e.Home.y - e.CameraHome.y);
            p.x = Axis(p.x, camPos.x, offset.x, e.Period.x, e.Slack.x);
            p.y = Axis(p.y, camPos.y, offset.y, e.Period.y, e.Slack.y);
            return p;
        }

        static float Axis(float p, float cam, float offset, float period, float slack)
        {
            if (!float.IsInfinity(slack))
                p = Mathf.Clamp(p, cam + offset - slack, cam + offset + slack);
            if (period > 0f)
                p = cam + Mathf.Repeat(p - cam + period * 0.5f, period) - period * 0.5f;
            return p;
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
