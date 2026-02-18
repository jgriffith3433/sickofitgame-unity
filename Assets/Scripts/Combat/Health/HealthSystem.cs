using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SickOfIt.Combat.Health
{
    /// <summary>
    /// Generic health system that can be used by player, enemies, or spawners
    /// </summary>
    public class HealthSystem : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private int currentHealth;

        [Header("Death Settings")]
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private float deathDelay = 0f;

        [Header("Invincibility")]
        [SerializeField] private bool hasInvincibilityFrames = false;
        [SerializeField] private float invincibilityDuration = 0.5f;
        private float invincibilityTimer = 0f;

        [Header("Events")]
        public UnityEvent<int> onHealthChanged; // Passes current health
        public UnityEvent onDamaged;
        public UnityEvent onHealed;
        public UnityEvent onDeath;

        [Header("Health Bar (Optional)")]
        [Tooltip("Drag in a UI transform (e.g., Canvas child) to auto-generate a health bar")]
        [SerializeField] private Transform healthBarContainer;
        [SerializeField] private Color healthBarColor = Color.red;
        [SerializeField] private Vector2 healthBarSize = new Vector2(35f, 3f);
        [SerializeField] private bool hideWhenFull = false;

        private GameObject healthBarBackground;
        private GameObject healthBarFill;
        private Image healthBarFillImage;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0;
        public bool IsInvincible => invincibilityTimer > 0;

        private void Awake()
        {
            currentHealth = maxHealth;
            CreateHealthBar();
        }

        private void Update()
        {
            if (invincibilityTimer > 0)
            {
                invincibilityTimer -= Time.deltaTime;
            }
        }

        /// <summary>
        /// Creates a simple health bar UI if a container is assigned
        /// </summary>
        private void CreateHealthBar()
        {
            if (healthBarContainer == null)
                return;

            // Create background
            healthBarBackground = new GameObject("HealthBar_Background");
            healthBarBackground.transform.SetParent(healthBarContainer, false);
            
            Image bgImage = healthBarBackground.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark gray background
            bgImage.sprite = CreateWhiteSprite(); // Need a sprite for rendering
            
            RectTransform bgRect = healthBarBackground.GetComponent<RectTransform>();
            bgRect.sizeDelta = healthBarSize;
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;

            // Create fill (red bar)
            healthBarFill = new GameObject("HealthBar_Fill");
            healthBarFill.transform.SetParent(healthBarBackground.transform, false);
            
            healthBarFillImage = healthBarFill.AddComponent<Image>();
            healthBarFillImage.color = healthBarColor;
            healthBarFillImage.sprite = CreateWhiteSprite(); // Need a sprite for rendering
            healthBarFillImage.type = Image.Type.Filled;
            healthBarFillImage.fillMethod = Image.FillMethod.Horizontal;
            healthBarFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            
            RectTransform fillRect = healthBarFill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero; // Fill the parent

            UpdateHealthBar();
        }

        /// <summary>
        /// Creates a simple white 1x1 sprite for UI rendering
        /// </summary>
        private Sprite CreateWhiteSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>
        /// Updates the health bar fill amount
        /// </summary>
        private void UpdateHealthBar()
        {
            if (healthBarFillImage == null)
                return;

            float fillAmount = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
            healthBarFillImage.fillAmount = fillAmount;

            // Hide when full if option is enabled
            if (hideWhenFull && healthBarBackground != null)
            {
                healthBarBackground.SetActive(fillAmount < 1f);
            }
        }

        /// <summary>
        /// Take damage
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (!IsAlive || IsInvincible)
                return;

            currentHealth = Mathf.Max(0, currentHealth - damage);

            onDamaged?.Invoke();
            onHealthChanged?.Invoke(currentHealth);
            UpdateHealthBar();

            if (hasInvincibilityFrames)
            {
                invincibilityTimer = invincibilityDuration;
            }

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Heal
        /// </summary>
        public void Heal(int amount)
        {
            if (!IsAlive)
                return;

            int oldHealth = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

            if (currentHealth > oldHealth)
            {
                onHealed?.Invoke();
                onHealthChanged?.Invoke(currentHealth);
                UpdateHealthBar();
            }
        }

        /// <summary>
        /// Reset health to max
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            invincibilityTimer = 0f;
            onHealthChanged?.Invoke(currentHealth);
            UpdateHealthBar();
        }

        /// <summary>
        /// Handle death
        /// </summary>
        private void Die()
        {
            onDeath?.Invoke();

            if (destroyOnDeath)
            {
                if (deathDelay > 0)
                {
                    Destroy(gameObject, deathDelay);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
