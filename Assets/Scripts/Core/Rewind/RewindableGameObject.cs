using UnityEngine;
using SickOfIt.Core;

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
        private bool hiddenBeforeBirth;
        private bool replayBirth = true;
        private ObjectPool pool;
        private const float SafetyTickInterval = 0.5f;

        public override bool ReplaysBirth => replayBirth;

        /// <summary>
        /// Pooled shots and diamonds are caused by an action. Rewinding past
        /// that action must not play the same shot again on the way forward.
        /// </summary>
        public void BindPool(ObjectPool owner)
        {
            pool = owner;
            replayBirth = false;
        }

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
            // Samples from before birth would look like a death and block respawn.
            if (HasBirth && time < BornAt - 0.0001f)
                return;

            bool currentState = gameObject.activeSelf;

            // Birth is the first moment the object is actually on.
            if (!HasBirth)
            {
                if (!currentState)
                    return;

                Remember(new GOSnapshot { time = time, activeSelf = true });
                lastRecordedState = true;
                lastRecordTime = time;
                return;
            }

            bool stateChanged = currentState != lastRecordedState;
            bool safetyTick = time - lastRecordTime >= SafetyTickInterval;

            if (stateChanged || safetyTick)
            {
                Remember(new GOSnapshot { time = time, activeSelf = currentState });
                lastRecordedState = currentState;
                lastRecordTime = time;
            }
        }

        public override void OnRewindStart()
        {
            base.OnRewindStart();
            hiddenBeforeBirth = false;
        }

        public override void OnRewindStop()
        {
            base.OnRewindStop();
            // The cursor was left at the time we rewound away from.
            lastRecordTime = GameClock.Instance != null ? GameClock.Instance.Time : 0f;
            lastRecordTime -= SafetyTickInterval;
        }

        protected override void OnLifeReset()
        {
            hiddenBeforeBirth = false;
            lastRecordedState = false;
            lastRecordTime = -999f;
        }

        private void OnDisable()
        {
            // Death and despawn must hit the timeline immediately. The recorder
            // only samples a few times a second, so without this the next catch-up
            // still thinks the object is alive and turns it back on.
            if (!HasBirth) return;
            if (GameClock.Instance == null || GameClock.Instance.IsRewinding) return;

            float time = GameClock.Instance.Time;
            if (time < BornAt - 0.0001f) return;
            if (!lastRecordedState) return;

            Remember(new GOSnapshot { time = time, activeSelf = false });
            lastRecordedState = false;
            lastRecordTime = time;
        }

        public override void CatchUp(float time)
        {
            if (!HasBirth)
                return;

            if (time < BornAt)
            {
                hiddenBeforeBirth = true;
                if (gameObject.activeSelf)
                    gameObject.SetActive(false);
                pool?.SyncRewindPresence(gameObject, false);
                return;
            }

            // Only objects rewound to before they existed come back.
            // A death during normal play must stay dead.
            if (!hiddenBeforeBirth)
                return;

            hiddenBeforeBirth = false;
            if (!replayBirth || InactiveInHistory(time))
                return;

            gameObject.SetActive(true);
            lastRecordedState = true;
            pool?.SyncRewindPresence(gameObject, true);
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
            if (snapshot.activeSelf)
                DetachFromInactiveParent();

            if (gameObject.activeSelf != snapshot.activeSelf)
                gameObject.SetActive(snapshot.activeSelf);

            pool?.SyncRewindPresence(gameObject, snapshot.activeSelf);
        }

        private void DetachFromInactiveParent()
        {
            Transform parent = transform.parent;
            if (parent != null && !parent.gameObject.activeInHierarchy)
                transform.SetParent(null);
        }

        protected override float GetSnapshotTime(GOSnapshot snapshot) => snapshot.time;

        /// <summary>
        /// If rewinding past our earliest recording, this object didn't exist yet → deactivate.
        /// Playing forward past that time turns it back on via CatchUp.
        /// </summary>
        protected override void ApplyPreExistenceDefault()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
            pool?.SyncRewindPresence(gameObject, false);
        }

        private bool InactiveInHistory(float time)
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                GOSnapshot snapshot = history[i];
                if (snapshot.time <= time + 0.001f)
                    return !snapshot.activeSelf;
            }

            return false;
        }
    }
}
