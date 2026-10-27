namespace SickOfIt.Core.Input
{
    /// <summary>
    /// Jump presses, latched from any source (event stream, key, button) and consumed once per fixed tick.
    /// Time is counted in fixed ticks, not seconds, so a frame hitch can't expire a press before a tick sees it.
    /// A press is stamped with the tick that will consume it (<c>tick</c> = the fixed tick that has just run, plus one).
    /// </summary>
    public struct JumpLatch
    {
        int count;
        int lastTick;

        /// <summary>Presses waiting, whatever their age.</summary>
        public int Count => count;

        /// <summary>Latch one press for the fixed tick <paramref name="nextTick"/>.</summary>
        public void Press(int nextTick)
        {
            count++;
            lastTick = nextTick;
        }

        /// <summary>Is a press waiting that is no older than <paramref name="bufferTicks"/> at <paramref name="tick"/>? Older ones are dropped.</summary>
        public bool Pending(int tick, int bufferTicks)
        {
            if (count > 0 && tick - lastTick > bufferTicks)
                count = 0;
            return count > 0;
        }

        /// <summary>The newest press landed on this very tick (a fresh press, not a buffered one).</summary>
        public bool Fresh(int tick) => count > 0 && lastTick == tick;

        /// <summary>Use one press: exactly one jump per tap.</summary>
        public void Consume()
        {
            if (count > 0)
                count--;
        }

        public void Clear() => count = 0;
    }
}
