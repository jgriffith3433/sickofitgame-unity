using System;
using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Where the hero is in Hell: the floor counter (script counter "floor"), the distance travelled (counter
    /// "distance"), floors ticking by as a travel stage covers ground, and the milestone lines for floors reached.
    /// The counters are saved with the world's; the rest of its state is saved here.
    /// </summary>
    public sealed class StageProgress
    {
        public const string FloorCounter = "floor";
        public const string DistanceCounter = "distance";

        readonly GameplayWorld world;
        readonly FloorsDef floors;

        // ─── Saved state ──────────────────────────────────────────────
        int floorsFromDistance;
        int lastFloor;

        public StageProgress(GameplayWorld world, LevelScript script)
        {
            this.world = world;
            floors = script != null ? script.floors : new FloorsDef();
        }

        public FloorsDef Floors => floors;
        public bool HasFloors => floors.start > 0;
        public int Floor => HasFloors ? Mathf.RoundToInt(world.GetCounter(FloorCounter)) : 0;
        public float Distance => world.GetCounter(DistanceCounter);

        /// <summary>The stage's circle, or null.</summary>
        public CircleDef Circle =>
            !string.IsNullOrEmpty(floors.circle) && world.Content.Circles.TryGetValue(floors.circle, out var circle) ? circle : null;

        /// <summary>0 at floors.start, 1 at floors.end; -1 when the stage doesn't measure floors.</summary>
        public float FloorProgress
        {
            get
            {
                if (!HasFloors || floors.end <= 0 || floors.end >= floors.start)
                    return -1f;
                return Mathf.Clamp01((float)(floors.start - Floor) / (floors.start - floors.end));
            }
        }

        public void Start()
        {
            floorsFromDistance = 0;
            if (!HasFloors)
                return;
            world.SetCounter(FloorCounter, floors.start);
            lastFloor = floors.start;
        }

        public void SetFloor(int floor)
        {
            if (!HasFloors)
            {
                Debug.LogWarning("[StageProgress] setFloor/climbFloors on a stage without floors.start does nothing.");
                return;
            }
            world.SetCounter(FloorCounter, floor);
        }

        /// <summary>After the camera moved: distance and floors from travel, then lines for any floor reached.</summary>
        public void Tick(float now)
        {
            if (world.Effects.Mode == CameraMode.Travel)
                world.SetCounter(DistanceCounter, world.Effects.TravelDistance);
            if (!HasFloors)
                return;

            if (floors.unitsPerFloor > 0f && world.Effects.Mode == CameraMode.Travel)
            {
                int climbed = Mathf.FloorToInt(Distance / floors.unitsPerFloor + 0.0001f);
                if (climbed > floorsFromDistance)
                {
                    int current = Floor;
                    int next = current - (climbed - floorsFromDistance);
                    // Travel alone never goes past the stage's last floor; actions still can.
                    if (floors.end > 0 && current >= floors.end)
                        next = Mathf.Max(next, floors.end);
                    floorsFromDistance = climbed;
                    world.SetCounter(FloorCounter, next);
                }
            }

            int floor = Floor;
            if (floor != lastFloor)
            {
                FloorChanged(lastFloor, floor, now);
                lastFloor = floor;
            }
        }

        void FloorChanged(int from, int to, float now)
        {
            world.Telemetry.Record("floor", world.LevelName, to);
            if (to >= from)
                return;
            // Every milestone passed on the way up (from > m.floor >= to), from any circle, in order.
            var reached = new List<MilestoneDef>();
            var ids = new List<string>(world.Content.Circles.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                foreach (var m in world.Content.Circles[id].milestones)
                {
                    if (m.floor < from && m.floor >= to)
                        reached.Add(m);
                }
            }
            reached.Sort((a, b) => b.floor.CompareTo(a.floor));
            foreach (var m in reached)
            {
                world.Hud.ShowText(m.text, m.style, m.color, now + Mathf.Max(0.5f, Mathf.Max(m.duration, SickOfIt.Core.Audio.Sfx.LengthOf(m.sound) + 0.4f)));
                SickOfIt.Core.Audio.Sfx.Play(m.sound);
                world.Telemetry.Record("milestone", m.text, m.floor);
            }
        }

        public void SaveState(StateWriter w)
        {
            w.Int(floorsFromDistance);
            w.Int(lastFloor);
        }

        public void LoadState(StateReader r)
        {
            floorsFromDistance = r.Int();
            lastFloor = r.Int();
        }
    }
}
