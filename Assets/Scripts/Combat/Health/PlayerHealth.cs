using UnityEngine;
using UnityEngine.Events;
using SickOfIt.Core;
using SickOfIt.Core.Flow;
using SickOfIt.Data;

namespace SickOfIt.Combat.Health
{
    /// <summary>
    /// Self-contained player health component.
    /// All health values come from RogueliteManager — no serialized max health.
    /// Implements IDamageable so DamageDealer/InstantDeathTrigger work generically.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Death Scene Settings")]
        [Tooltip("Name of the death scene to load (e.g., 'Death1', 'DeathAnimation', etc.)")]
        [SerializeField] private string deathSceneName = "Death1";

        [Header("Roguelite Settings")]
        [Tooltip("The level number this player is in. Set by LevelRuntime.")]
        [SerializeField] private int currentLevelNumber = 1;

        [Header("Invincibility")]
        [SerializeField] private bool hasInvincibilityFrames = true;
        [SerializeField] private float invincibilityDuration = 0.5f;
        private float invincibilityTimer;

        [Header("Events")]
        public UnityEvent<int> onHealthChanged;
        public UnityEvent onDamaged;
        public UnityEvent onDeath;

        // Cached ref — set by generator or discovered in Awake
        private int maxHealth;
        private int currentHealth;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0;
        public bool IsInvincible => invincibilityTimer > 0;
        public float InvincibilityTimer => invincibilityTimer;

        /// <summary>
        /// Set current health directly (used by rewind system).
        /// </summary>
        public void SetHealth(int health)
        {
            currentHealth = Mathf.Clamp(health, 0, maxHealth);
            onHealthChanged?.Invoke(currentHealth);
        }

        /// <summary>
        /// Set invincibility timer directly (used by rewind system to restore i-frame state).
        /// </summary>
        /// <summary>Raises (or lowers) maximum health mid-level, healing by <paramref name="heal"/>.</summary>
        public void SetMaxHealth(int max, int heal)
        {
            maxHealth = Mathf.Max(1, max);
            currentHealth = Mathf.Clamp(currentHealth + heal, 0, maxHealth);
            onHealthChanged?.Invoke(currentHealth);
        }

        public void SetInvincibilityTimer(float time)
        {
            invincibilityTimer = Mathf.Max(0f, time);
        }

        [SerializeField] private SickOfIt.Core.Input.PlayerController controller;

        private void Awake()
        {
            if (controller == null)
                controller = GetComponentInParent<SickOfIt.Core.Input.PlayerController>();
        }

        private void Update()
        {
            if (invincibilityTimer > 0)
                invincibilityTimer -= (GameClock.Instance != null ? GameClock.Instance.DeltaTime : Time.deltaTime);
        }

        /// <summary>
        /// Set the death scene for this level. Called by LevelRuntime.
        /// </summary>
        public void SetDeathSceneName(string name)
        {
            if (!string.IsNullOrEmpty(name))
                deathSceneName = name;
        }

        /// <summary>
        /// Set the level number and apply roguelite health values.
        /// Called by LevelRuntime during level initialisation.
        /// </summary>
        public void InitializeForLevel(int levelNumber)
        {
            currentLevelNumber = levelNumber;
            var definition = GameDefinition.Instance;
            if (definition != null && definition.player != null)
                invincibilityDuration = definition.player.invincibilityDuration;
            maxHealth = RogueliteManager.Instance.GetMaxHealth();
            currentHealth = Mathf.Clamp(
                RogueliteManager.Instance.GetStartingHealthForLevel(currentLevelNumber),
                0, maxHealth);
            invincibilityTimer = 0f;
            onHealthChanged?.Invoke(currentHealth);
        }

        public void TakeDamage(int damage)
        {
            TakeDamage(damage, false);
        }

        /// <summary>
        /// Damage that can skip the post-hit invincibility window, for damage over time (sludge, poison).
        /// It still doesn't start a new invincibility window, so regular hits aren't blocked by ticks.
        /// </summary>
        public void TakeDamage(int damage, bool ignoreInvincibility)
        {
            if (!IsAlive || (IsInvincible && !ignoreInvincibility))
                return;
            if (ignoreInvincibility)
            {
                currentHealth = Mathf.Max(0, currentHealth - damage);
                onDamaged?.Invoke();
                onHealthChanged?.Invoke(currentHealth);
                if (controller != null)
                    controller.PlayHurtSound();
                if (currentHealth <= 0)
                    Die();
                return;
            }

            currentHealth = Mathf.Max(0, currentHealth - damage);
            onDamaged?.Invoke();
            onHealthChanged?.Invoke(currentHealth);

            if (hasInvincibilityFrames)
                invincibilityTimer = invincibilityDuration;

            // Play SFX via PlayerController
            if (controller != null)
                controller.PlayHurtSound();

            if (currentHealth <= 0)
                Die();
        }

        private bool deathPending;

        /// <summary>Seconds left to undo a death with a rewind before the death scene; -1 when not dying.</summary>
        public float DeathTimeLeft { get; private set; } = -1f;
        /// <summary>The death window's full length (for drawing a countdown).</summary>
        public float DeathWindow { get; private set; }

        private void Die()
        {
            if (deathPending)
                return;

            deathPending = true;
            onDeath?.Invoke();

            // Play death SFX via PlayerController
            if (controller != null)
            {
                controller.PlayDeathSound();
                controller.enabled = false;
            }

            StartCoroutine(WaitThenLoadDeath());
        }

        private System.Collections.IEnumerator WaitThenLoadDeath()
        {
            // Leave a beat so rewind can restore health and cancel the death.
            // The timer holds while time is running backward.
            // With rewind juice left the window is longer, and the HUD offers "Hold R to undo it!".
            float delay = 0.6f;
            var definition = GameDefinition.Instance;
            if (definition != null && definition.player != null)
            {
                delay = definition.player.deathRewindGrace;
                var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
                if (budget == null || budget.CanRewind)
                    delay = Mathf.Max(delay, definition.player.deathRewindWindow);
            }
            DeathWindow = delay;
            float elapsed = 0f;
            while (elapsed < delay)
            {
                DeathTimeLeft = delay - elapsed;
                if (currentHealth > 0)
                {
                    CancelDeath();
                    yield break;
                }

                if (GameClock.Instance != null && GameClock.Instance.IsRewinding)
                {
                    yield return null;
                    continue;
                }

                // Out of juice mid-window (or never had any): no point waiting the long window out.
                var juice = SickOfIt.Core.Rewind.RewindBudget.Instance;
                if (juice != null && !juice.CanRewind && elapsed >= (definition != null ? definition.player.deathRewindGrace : 0.6f))
                    break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            DeathTimeLeft = 0f;

            if (currentHealth > 0)
            {
                CancelDeath();
                yield break;
            }

            SickOfIt.Core.AnalyticsManager.Instance?.TrackPlayerDeath("combat");
            PlayDeathScene();
        }

        private void CancelDeath()
        {
            deathPending = false;
            DeathTimeLeft = -1f;
            if (controller != null)
                controller.enabled = true;
        }

        private void PlayDeathScene()
        {
            SceneTransitionManager.Instance.LoadDeathScene(deathSceneName);
        }
    }
}
