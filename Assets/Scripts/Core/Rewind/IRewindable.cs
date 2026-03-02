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

        /// <summary>Called when rewind starts (freeze physics, etc.).</summary>
        void OnRewindStart();

        /// <summary>Called when rewind stops (unfreeze physics, trim future, etc.).</summary>
        void OnRewindStop();

        /// <summary>Whether this component is a RewindableGameObject (processes first during restore).</summary>
        bool IsGameObjectRewindable { get; }
    }
}
