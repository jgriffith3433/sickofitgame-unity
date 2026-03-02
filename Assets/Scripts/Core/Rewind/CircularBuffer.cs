using System;

namespace SickOfIt.Core.Rewind
{
    /// <summary>
    /// Generic ring buffer with fixed capacity. Oldest entries are overwritten when full.
    /// Pure C#, no Unity dependencies.
    /// </summary>
    public class CircularBuffer<T>
    {
        private readonly T[] buffer;
        private int head;   // Next write position
        private int count;

        public int Capacity { get; }
        public int Count => count;

        public CircularBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentException("Capacity must be > 0", nameof(capacity));
            Capacity = capacity;
            buffer = new T[capacity];
        }

        /// <summary>
        /// Add an item. Overwrites the oldest entry if full.
        /// </summary>
        public void Add(T item)
        {
            buffer[head] = item;
            head = (head + 1) % Capacity;
            if (count < Capacity)
                count++;
        }

        /// <summary>
        /// Index 0 = oldest entry, Count-1 = newest.
        /// </summary>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= count)
                    throw new IndexOutOfRangeException($"Index {index} out of range [0, {count})");
                int actualIndex = (head - count + index + Capacity) % Capacity;
                return buffer[actualIndex];
            }
        }

        /// <summary>
        /// Returns the most recently added item.
        /// </summary>
        public T Newest
        {
            get
            {
                if (count == 0) throw new InvalidOperationException("Buffer is empty");
                return buffer[(head - 1 + Capacity) % Capacity];
            }
        }

        /// <summary>
        /// Returns the oldest item in the buffer.
        /// </summary>
        public T Oldest
        {
            get
            {
                if (count == 0) throw new InvalidOperationException("Buffer is empty");
                return this[0];
            }
        }

        public void Clear()
        {
            head = 0;
            count = 0;
        }

        /// <summary>
        /// Keep only the first `keepCount` entries (oldest), discard the rest.
        /// Used to trim "future" entries after rewind stops.
        /// </summary>
        public void TrimToCount(int keepCount)
        {
            if (keepCount >= count) return;
            if (keepCount <= 0)
            {
                Clear();
                return;
            }
            // Move head back to just after the kept entries
            head = (head - count + keepCount + Capacity) % Capacity;
            count = keepCount;
        }

        /// <summary>
        /// Binary search for the entry nearest to `targetTime`.
        /// `timeSelector` extracts the time float from T.
        /// Returns the index (0-based from oldest) of the nearest entry.
        /// Returns -1 if buffer is empty.
        /// </summary>
        public int FindNearestIndex(float targetTime, Func<T, float> timeSelector)
        {
            if (count == 0) return -1;
            if (count == 1) return 0;

            int lo = 0;
            int hi = count - 1;

            // Binary search for insertion point
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (timeSelector(this[mid]) < targetTime)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            // lo is the first entry >= targetTime. Check lo and lo-1 for closest.
            if (lo == 0) return 0;
            if (lo >= count) return count - 1;

            float deltaLo = Math.Abs(timeSelector(this[lo]) - targetTime);
            float deltaPrev = Math.Abs(timeSelector(this[lo - 1]) - targetTime);
            return deltaPrev <= deltaLo ? lo - 1 : lo;
        }

        /// <summary>
        /// Find the two entries bracketing `targetTime` for interpolation.
        /// Returns false if buffer is empty or has only one entry.
        /// </summary>
        public bool FindBracket(float targetTime, Func<T, float> timeSelector,
            out int lowerIndex, out int upperIndex, out float t)
        {
            lowerIndex = 0;
            upperIndex = 0;
            t = 0f;

            if (count < 2) return false;

            int nearest = FindNearestIndex(targetTime, timeSelector);
            float nearestTime = timeSelector(this[nearest]);

            if (nearestTime <= targetTime)
            {
                lowerIndex = nearest;
                upperIndex = nearest + 1 < count ? nearest + 1 : nearest;
            }
            else
            {
                lowerIndex = nearest > 0 ? nearest - 1 : 0;
                upperIndex = nearest;
            }

            float tLower = timeSelector(this[lowerIndex]);
            float tUpper = timeSelector(this[upperIndex]);
            float range = tUpper - tLower;
            t = range > 0.0001f ? (targetTime - tLower) / range : 0f;
            t = Math.Max(0f, Math.Min(1f, t));

            return true;
        }
    }
}
