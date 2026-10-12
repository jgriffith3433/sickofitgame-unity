namespace SickOfIt.Core.Rewind
{
    /// <summary>
    /// Interface for any component that participates in the rewind system.
    /// Implemented by BaseRewindable&lt;T&gt; — you rarely implement this directly.
    /// </summary>
    public interface IRewindable
    {
        /// <summary>Record the current state at this game time.</summary>
        void RecordState(float time);

        /// <summary>Restore state to the given game time.</summary>
        void RestoreState(float time);

        /// <summary>
        /// Forward playback after a rewind. Re-presents objects that were rewound
        /// to before they existed, once game time reaches their birth.
        /// </summary>
        void CatchUp(float time);

        /// <summary>Called when rewind starts (freeze physics, etc.).</summary>
        void OnRewindStart();

        /// <summary>Called when rewind stops (unfreeze physics, trim future, etc.).</summary>
        void OnRewindStop();

        /// <summary>Whether this component is a RewindableGameObject (processes first during restore).</summary>
        bool IsGameObjectRewindable { get; }

        /// <summary>Drop history so a pooled object can live a new life.</summary>
        void BeginNewLife();

        /// <summary>Record the current state as this life's birth.</summary>
        void CaptureBirth();
    }
}
