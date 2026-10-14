using System;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Records an <see cref="ISimState"/> into the rewind timeline and restores it, so a rewind followed by
    /// forward play re-simulates from exactly the state at the branch point.
    ///
    /// Restore is discrete (the newest snapshot at or before the target time). Owners call
    /// <see cref="RecordNow"/> right after a discrete event (a shot, a state change, a rule firing) so a
    /// rewind to just after the event can't roll the decision back and repeat it.
    /// Storage is recycled, so recording does not allocate once the buffer is full.
    /// </summary>
    public sealed class SimHistory : MonoBehaviour, IRewindable
    {
        const int Capacity = 1024;

        sealed class Slot
        {
            public float Time;
            public float[] Floats = new float[16];
            public int[] Ints = new int[16];
            public string[] Strings = new string[4];
            public int FloatCount;
            public int IntCount;
            public int StringCount;
        }

        static readonly StateWriter Writer = new StateWriter();
        static readonly StateReader Reader = new StateReader();

        readonly Slot[] slots = new Slot[Capacity];
        int head;
        int count;
        bool hasBirth;
        float bornAt;
        ISimState target;
        Func<bool> isReady;

        public int Count => count;

        /// <summary>Records <paramref name="state"/>. <paramref name="ready"/> (optional) says whether it has been set up yet; nothing is recorded before.</summary>
        public void Bind(ISimState state, Func<bool> ready = null)
        {
            target = state;
            isReady = ready;
        }

        void Awake()
        {
            RewindTimeline.Instance?.Register(this);
        }

        void Start()
        {
            RewindTimeline.Instance?.Register(this);
        }

        void OnDestroy()
        {
            RewindTimeline.Instance?.Unregister(this);
        }

        /// <summary>Records the current state at the current game time (unless time is running backward).</summary>
        public void RecordNow()
        {
            var clock = GameClock.Instance;
            if (clock == null || clock.IsRewinding || !isActiveAndEnabled)
                return;
            RecordState(clock.Time);
        }

        // ─── IRewindable ──────────────────────────────────────────────

        public bool IsGameObjectRewindable => false;

        public void RecordState(float time)
        {
            if (target == null || (isReady != null && !isReady()))
                return;
            if (hasBirth && time < bornAt - 0.0001f)
                return;
            if (!hasBirth)
            {
                hasBirth = true;
                bornAt = time;
            }

            // A second record at the same time replaces the first, so the newest decision wins.
            Slot slot;
            if (count > 0 && Newest().Time >= time - 0.00001f && Newest().Time <= time + 0.00001f)
            {
                slot = Newest();
            }
            else
            {
                slot = slots[head] ?? (slots[head] = new Slot());
                head = (head + 1) % Capacity;
                if (count < Capacity)
                    count++;
            }

            Writer.Reset();
            target.SaveState(Writer);
            slot.Time = time;
            Copy(Writer.Floats, Writer.FloatCount, ref slot.Floats);
            Copy(Writer.Ints, Writer.IntCount, ref slot.Ints);
            slot.FloatCount = Writer.FloatCount;
            slot.IntCount = Writer.IntCount;
            Copy(Writer.Strings, Writer.StringCount, ref slot.Strings);
            slot.StringCount = Writer.StringCount;
        }

        public void RestoreState(float time)
        {
            if (target == null || count == 0)
                return;
            int index = NewestAtOrBefore(time);
            if (index < 0)
                return;

            var slot = At(index);
            Reader.Begin(slot.Floats, slot.FloatCount, slot.Ints, slot.IntCount, slot.Strings, slot.StringCount);
            target.LoadState(Reader);
            if (!Reader.AtEnd)
                throw new InvalidOperationException($"{name}: LoadState read less than SaveState wrote. Save and Load must match.");
        }

        public void CatchUp(float time)
        {
        }

        public void OnRewindStart()
        {
        }

        public void OnRewindStop()
        {
            var clock = GameClock.Instance;
            if (clock == null)
                return;
            // Drop the discarded future.
            float now = clock.Time;
            while (count > 0 && Newest().Time > now + 0.001f)
            {
                head = (head - 1 + Capacity) % Capacity;
                count--;
            }
        }

        public void BeginNewLife()
        {
            head = 0;
            count = 0;
            hasBirth = false;
        }

        public void CaptureBirth()
        {
            RecordNow();
        }

        // ─── Ring helpers ─────────────────────────────────────────────

        Slot At(int index) => slots[(head - count + index + Capacity) % Capacity];
        Slot Newest() => slots[(head - 1 + Capacity) % Capacity];

        int NewestAtOrBefore(float time)
        {
            int lo = 0;
            int hi = count - 1;
            int found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (At(mid).Time <= time + 0.00001f)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return found;
        }

        static void Copy<T>(T[] source, int length, ref T[] destination)
        {
            if (destination.Length < length)
                destination = new T[Math.Max(length, destination.Length * 2)];
            Array.Copy(source, destination, length);
        }
    }
}
