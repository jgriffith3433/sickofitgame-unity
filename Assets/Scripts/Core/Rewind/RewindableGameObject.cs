using UnityEngine;

namespace SickOfIt.Core.Rewind
{
    /// <summary>
    /// Tracks GameObject.activeSelf for rewind. Critical for enemy death/reactivation.
    /// 
    /// Uses Awake/OnDestroy for registration so dead (inactive) enemies stay registered.
    /// Only records when activeSelf actually changes (plus safety-net tick).
    /// 
    /// During restore: if target time is before earliest snapshot, assumes "didn't exist" → SetActive(false).
    /// </summary>
    public class RewindableGameObject : BaseRewindable<RewindableGameObject.GOSnapshot>
    {
        private bool lastRecordedState;
        private float lastRecordTime;
        private const float SafetyTickInterval = 0.5f;

        public struct GOSnapshot
        {
            public float time;
            public bool activeSelf;
        }

        public override bool IsGameObjectRewindable => true;

        protected override void Awake()
        {
            base.Awake();
            lastRecordedState = gameObject.activeSelf;
        }

        /// <summary>
        /// Override RecordState to only record on change or safety-net.
        /// Called by RewindTimeline even when inactive (IsGameObjectRewindable = true).
        /// </summary>
        public override void RecordState(float time)
        {
            bool currentState = gameObject.activeSelf;
            bool stateChanged = currentState != lastRecordedState;
            bool safetyTick = time - lastRecordTime >= SafetyTickInterval;

            if (stateChanged || safetyTick)
            {
                history.Add(new GOSnapshot { time = time, activeSelf = currentState });
                lastRecordedState = currentState;
                lastRecordTime = time;
            }
        }

        protected override GOSnapshot CaptureSnapshot(float time)
        {
            return new GOSnapshot
            {
                time = time,
                activeSelf = gameObject.activeSelf
            };
        }

        protected override void ApplySnapshot(GOSnapshot snapshot)
        {
            if (gameObject.activeSelf != snapshot.activeSelf)
                gameObject.SetActive(snapshot.activeSelf);
        }

        protected override float GetSnapshotTime(GOSnapshot snapshot) => snapshot.time;

        /// <summary>
        /// If rewinding past our earliest recording, this object didn't exist yet → deactivate.
        /// </summary>
        protected override void ApplyPreExistenceDefault()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
