using System;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Anything whose state must survive a rewind. Save writes every value that drives future decisions
    /// (timers, counters, random state, current state machine state); Load reads them back in the same order.
    /// Positions and velocities are covered separately by RewindableTransform / RewindableRigidbody2D.
    /// </summary>
    public interface ISimState
    {
        void SaveState(StateWriter writer);
        void LoadState(StateReader reader);
    }

    /// <summary>Appends values to a snapshot. Reused between snapshots, so it never allocates in steady state.</summary>
    public sealed class StateWriter
    {
        internal float[] Floats = new float[32];
        internal int[] Ints = new int[32];
        internal string[] Strings = new string[8];
        internal int FloatCount;
        internal int IntCount;
        internal int StringCount;

        public void Reset()
        {
            FloatCount = 0;
            IntCount = 0;
            StringCount = 0;
        }

        public void Float(float value)
        {
            if (FloatCount == Floats.Length)
                Array.Resize(ref Floats, Floats.Length * 2);
            Floats[FloatCount++] = value;
        }

        public void Int(int value)
        {
            if (IntCount == Ints.Length)
                Array.Resize(ref Ints, Ints.Length * 2);
            Ints[IntCount++] = value;
        }

        public void String(string value)
        {
            if (StringCount == Strings.Length)
                Array.Resize(ref Strings, Strings.Length * 2);
            Strings[StringCount++] = value;
        }

        public void Bool(bool value) => Int(value ? 1 : 0);
        public void UInt(uint value) => Int(unchecked((int)value));
    }

    /// <summary>Reads values back in the order they were written.</summary>
    public sealed class StateReader
    {
        float[] floats;
        int[] ints;
        string[] strings;
        int floatIndex;
        int intIndex;
        int stringIndex;
        int floatCount;
        int intCount;
        int stringCount;

        internal void Begin(float[] floatData, int floatLength, int[] intData, int intLength, string[] stringData, int stringLength)
        {
            floats = floatData;
            ints = intData;
            strings = stringData;
            floatCount = floatLength;
            intCount = intLength;
            stringCount = stringLength;
            floatIndex = 0;
            intIndex = 0;
            stringIndex = 0;
        }

        public float Float()
        {
            if (floatIndex >= floatCount)
                throw new InvalidOperationException("Snapshot has fewer floats than LoadState reads. SaveState and LoadState must match.");
            return floats[floatIndex++];
        }

        public int Int()
        {
            if (intIndex >= intCount)
                throw new InvalidOperationException("Snapshot has fewer ints than LoadState reads. SaveState and LoadState must match.");
            return ints[intIndex++];
        }

        public string String()
        {
            if (stringIndex >= stringCount)
                throw new InvalidOperationException("Snapshot has fewer strings than LoadState reads. SaveState and LoadState must match.");
            return strings[stringIndex++];
        }

        public bool Bool() => Int() != 0;
        public uint UInt() => unchecked((uint)Int());

        public bool AtEnd => floatIndex == floatCount && intIndex == intCount && stringIndex == stringCount;
    }

    /// <summary>
    /// A deterministic random number generator (xorshift32). Its whole state is one uint, so it is saved
    /// with the rest of the simulation and a rewind replays the same rolls.
    /// </summary>
    [Serializable]
    public struct DeterministicRandom
    {
        public uint State;

        public DeterministicRandom(uint seed)
        {
            State = seed == 0 ? 0x9E3779B9u : seed;
        }

        /// <summary>An independent stream, e.g. one per spawned entity, that doesn't depend on update order.</summary>
        public static DeterministicRandom Derive(uint seed, uint stream)
        {
            uint mixed = seed ^ (stream * 0x85EBCA6Bu + 0xC2B2AE35u);
            mixed ^= mixed >> 16;
            mixed *= 0x7FEB352Du;
            mixed ^= mixed >> 15;
            mixed *= 0x846CA68Bu;
            mixed ^= mixed >> 16;
            return new DeterministicRandom(mixed);
        }

        public uint NextUInt()
        {
            uint x = State;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State = x;
            return x;
        }

        /// <summary>A float in [0, 1).</summary>
        public float Value() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * Value();

        /// <summary>An int in [min, max).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextUInt() % (uint)(max - min));
    }
}
