using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SickOfIt.Combat.Health
{
    /// <summary>
    /// Health component for enemies and destructible objects.
    /// Max health is configured in the Inspector. For player health, use PlayerHealth instead.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
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
        private float invincibilityTimer;

        [Header("Events")]
        public UnityEvent<int> onHealthChanged;
        public UnityEvent onDamaged;
        public UnityEvent onDeath;

        [Header("Health Bar (Optional)")]
        [Tooltip("Drag in a UI transform to auto-generate a health bar")]
        [SerializeField] private Transform healthBarContainer;
        [SerializeField] private Color healthBarColor = Color.red;
        [SerializeField] private Vector2 healthBarSize = new Vector2(35f, 3f);
        [SerializeField] private bool hideWhenFull = false;

        private GameObject healthBarBackground;
        private Image healthBarFillImage;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0;
        public bool IsInvincible => invincibilityTimer > 0;

        /// <summary>
        /// Set max health (and reset current health). Call before Awake or immediately after Instantiate.
        /// </summary>
        public void SetMaxHealth(int health)
        {
            maxHealth = health;
            currentHealth = health;
        }

        /// <summary>
        /// Set current health directly (used by rewind system).
        /// </summary>
        public void SetHealth(int health)
        {
            currentHealth = Mathf.Clamp(health, 0, maxHealth);
        }

        private void Awake()
        {
            currentHealth = maxHealth;
            CreateHealthBar();
        }

        private void Update()
        {
            if (invincibilityTimer > 0)
                invincibilityTimer -= Time.deltaTime;
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive || IsInvincible)
                return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            onDamaged?.Invoke();
            onHealthChanged?.Invoke(currentHealth);
            UpdateHealthBar();

            if (hasInvincibilityFrames)
                invincibilityTimer = invincibilityDuration;

            if (currentHealth <= 0)
                Die();
        }

        private void Die()
        {
            onDeath?.Invoke();

            if (destroyOnDeath)
            {
                if (deathDelay > 0)
                    Destroy(gameObject, deathDelay);
                else
                    Destroy(gameObject);
            }
        }

        #region Health Bar

        private void CreateHealthBar()
        {
            if (healthBarContainer == null)
                return;

            healthBarBackground = new GameObject("HealthBar_Background");
            healthBarBackground.transform.SetParent(healthBarContainer, false);

            var bgImage = healthBarBackground.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            bgImage.sprite = CreateWhiteSprite();

            var bgRect = healthBarBackground.GetComponent<RectTransform>();
            bgRect.sizeDelta = healthBarSize;
            bgRect.anchorMin = bgRect.anchorMax = bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;

            var healthBarFill = new GameObject("HealthBar_Fill");
            healthBarFill.transform.SetParent(healthBarBackground.transform, false);

            healthBarFillImage = healthBarFill.AddComponent<Image>();
            healthBarFillImage.color = healthBarColor;
            healthBarFillImage.sprite = CreateWhiteSprite();
            healthBarFillImage.type = Image.Type.Filled;
            healthBarFillImage.fillMethod = Image.FillMethod.Horizontal;
            healthBarFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

            var fillRect = healthBarFill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;

            UpdateHealthBar();
        }

        private static Sprite CreateWhiteSprite()
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private void UpdateHealthBar()
        {
            if (healthBarFillImage == null)
                return;

            float fillAmount = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
            healthBarFillImage.fillAmount = fillAmount;

            if (hideWhenFull && healthBarBackground != null)
                healthBarBackground.SetActive(fillAmount < 1f);
        }

        #endregion
    }
}
