using UnityEngine;
using SickOfIt.Core.Rewind;
using SickOfIt.Data;

namespace SickOfIt.Core
{
    /// <summary>
    /// Records and restores the player's points total during rewind.
    /// Without this, collecting a diamond and rewinding keeps the points.
    /// 
    /// Attach to the LevelRuntime GameObject (same as RewindTimeline).
    /// Auto-reads from RogueliteManager and restores on rewind.
    /// </summary>
    public class RewindableScore : BaseRewindable<RewindableScore.ScoreSnapshot>
    {
        public struct ScoreSnapshot
        {
            public float time;
            public int points;
        }

        protected override ScoreSnapshot CaptureSnapshot(float time)
        {
            return new ScoreSnapshot
            {
                time = time,
                points = RogueliteManager.Instance != null ? RogueliteManager.Instance.GetPoints() : 0
            };
        }

        protected override void ApplySnapshot(ScoreSnapshot snapshot)
        {
            if (RogueliteManager.Instance != null)
                RogueliteManager.Instance.SetPoints(snapshot.points);
        }

        protected override float GetSnapshotTime(ScoreSnapshot snapshot) => snapshot.time;
    }
}
