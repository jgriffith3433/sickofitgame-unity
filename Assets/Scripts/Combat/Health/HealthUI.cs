using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace SickOfIt.Combat.Health
{
    /// <summary>
    /// Heart-based HUD for player health in levels.
    /// References PlayerHealth directly — not EnemyHealth.
    /// </summary>
    public class HealthUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Transform heartsContainer;
        [SerializeField] private GameObject heartPrefab;

        [Header("Settings")]
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private Color fullHealthColor = Color.white;
        [SerializeField] private Color emptyHealthColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        [Header("Auto-Find Retry")]
        [Tooltip("How long to keep retrying to find the player (seconds). On WebGL, asset loading can take 30+ seconds.")]
        [SerializeField] private float findPlayerTimeout = 60f;

        private List<Image> heartImages = new List<Image>();

        private void Start()
        {
            if (autoFindPlayer && playerHealth == null)
            {
                StartCoroutine(FindPlayerAndInitialize());
                return;
            }

            Initialize();
        }

        private IEnumerator FindPlayerAndInitialize()
        {
            float elapsed = 0f;

            while (playerHealth == null && elapsed < findPlayerTimeout)
            {
                playerHealth = FindObjectOfType<PlayerHealth>(true);

                if (playerHealth != null)
                    break;

                elapsed += 0.25f;
                yield return new WaitForSeconds(0.25f);
            }

            if (playerHealth == null)
            {
                Debug.LogError("HealthUI: No PlayerHealth found after waiting!");
                yield break;
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
