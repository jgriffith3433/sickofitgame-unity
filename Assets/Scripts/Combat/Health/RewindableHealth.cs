using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;

namespace SickOfIt.Combat.Health
{
    /// <summary>
    /// Records and restores health state (current, max, invincibility timer).
    /// Works with both PlayerHealth and EnemyHealth via their public SetHealth/SetInvincibilityTimer methods.
    /// 
    /// Records on change (via onHealthChanged event) and on a safety-net tick.
    /// </summary>
    public class RewindableHealth : BaseRewindable<RewindableHealth.HealthSnapshot>
    {
        [Header("Health Reference (set ONE)")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private EnemyHealth enemyHealth;

        private int lastRecordedHealth;
        private float lastRecordTime;
        private const float SafetyTickInterval = 0.25f;

        public struct HealthSnapshot
        {
            public float time;
            public int currentHealth;
            public int maxHealth;
            public float invincibilityTimer;
        }

        protected override void Awake()
        {
            base.Awake();

            // Auto-discover if not assigned
            if (playerHealth == null && enemyHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
                if (playerHealth == null)
                    enemyHealth = GetComponent<EnemyHealth>();
            }
        }

        private void OnEnable()
        {
            if (playerHealth != null)
                playerHealth.onHealthChanged.AddListener(OnHealthChanged);
            else if (enemyHealth != null)
                enemyHealth.onHealthChanged.AddListener(OnHealthChanged);
        }

        private void OnDisable()
        {
            if (playerHealth != null)
                playerHealth.onHealthChanged.RemoveListener(OnHealthChanged);
            else if (enemyHealth != null)
                enemyHealth.onHealthChanged.RemoveListener(OnHealthChanged);
        }

        private void OnHealthChanged(int newHealth)
        {
            // Force a recording on health change
            if (GameClock.Instance != null && !GameClock.Instance.IsRewinding)
            {
                float time = GameClock.Instance.Time;
                history.Add(CaptureSnapshot(time));
                lastRecordedHealth = newHealth;
                lastRecordTime = time;
            }
        }

        /// <summary>
        /// Override to also do safety-net tick recording.
        /// </summary>
        public override void RecordState(float time)
        {
            int currentHP = GetCurrentHealth();
            bool changed = currentHP != lastRecordedHealth;
            bool safetyTick = time - lastRecordTime >= SafetyTickInterval;

            if (changed || safetyTick)
            {
                history.Add(CaptureSnapshot(time));
                lastRecordedHealth = currentHP;
                lastRecordTime = time;
            }
        }

        protected override HealthSnapshot CaptureSnapshot(float time)
        {
            if (playerHealth != null)
            {
                return new HealthSnapshot
                {
                    time = time,
                    currentHealth = playerHealth.CurrentHealth,
                    maxHealth = playerHealth.MaxHealth,
                    invincibilityTimer = playerHealth.InvincibilityTimer
                };
            }
            else if (enemyHealth != null)
            {
                return new HealthSnapshot
                {
                    time = time,
                    currentHealth = enemyHealth.CurrentHealth,
                    maxHealth = enemyHealth.MaxHealth,
                    invincibilityTimer = enemyHealth.InvincibilityTimer
                };
            }
            return default;
        }

        protected override void ApplySnapshot(HealthSnapshot snapshot)
        {
            if (playerHealth != null)
            {
                playerHealth.SetHealth(snapshot.currentHealth);
                playerHealth.SetInvincibilityTimer(snapshot.invincibilityTimer);
            }
            else if (enemyHealth != null)
            {
                enemyHealth.SetHealth(snapshot.currentHealth);
                enemyHealth.SetInvincibilityTimer(snapshot.invincibilityTimer);
            }
        }

        protected override float GetSnapshotTime(HealthSnapshot snapshot) => snapshot.time;

        private int GetCurrentHealth()
        {
            if (playerHealth != null) return playerHealth.CurrentHealth;
            if (enemyHealth != null) return enemyHealth.CurrentHealth;
            return 0;
        }
    }
}
