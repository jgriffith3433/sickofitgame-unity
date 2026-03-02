using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SickOfIt.Combat.Health
{
    /// <summary>
    /// Heart-based HUD for player health in levels.
    /// References PlayerHealth directly — wired by LevelRuntime via SetPlayerHealth().
    /// </summary>
    public class HealthUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Transform heartsContainer;
        [SerializeField] private GameObject heartPrefab;

        [Header("Settings")]
        [SerializeField] private Color fullHealthColor = Color.white;
        [SerializeField] private Color emptyHealthColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        private List<Image> heartImages = new List<Image>();

        /// <summary>
        /// Inject the PlayerHealth reference so HealthUI doesn't need FindObjectOfType.
        /// Called by LevelSceneGenerator or LevelRuntime.
        /// </summary>
        public void SetPlayerHealth(PlayerHealth health)
        {
            playerHealth = health;
        }

        private void Start()
        {
            if (playerHealth == null)
            {
                Debug.LogError("HealthUI: PlayerHealth not assigned! Wire via SetPlayerHealth() from LevelRuntime.");
                return;
            }

            Initialize();
        }

        private void Initialize()
        {
            if (playerHealth == null)
            {
                Debug.LogError("HealthUI: No PlayerHealth found!");
                return;
            }

            playerHealth.onHealthChanged.AddListener(OnHealthChanged);
            CreateHearts();
            UpdateHealthDisplay(playerHealth.CurrentHealth);
        }

        private void OnHealthChanged(int currentHealth)
        {
            if (heartImages.Count != playerHealth.MaxHealth)
                CreateHearts();

            UpdateHealthDisplay(currentHealth);
        }

        private void CreateHearts()
        {
            foreach (Transform child in heartsContainer)
                Destroy(child.gameObject);

            heartImages.Clear();

            for (int i = 0; i < playerHealth.MaxHealth; i++)
            {
                GameObject heart = Instantiate(heartPrefab, heartsContainer);
                Image heartImage = heart.GetComponent<Image>();
                if (heartImage != null)
                {
                    heartImage.color = fullHealthColor;
                    heartImages.Add(heartImage);
                }
            }
        }

        private void UpdateHealthDisplay(int currentHealth)
        {
            for (int i = 0; i < heartImages.Count; i++)
                heartImages[i].color = i < currentHealth ? fullHealthColor : emptyHealthColor;
        }
    }
}
