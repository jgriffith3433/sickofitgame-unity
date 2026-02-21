using UnityEngine;
using MobileControllerFramework;
using TMPro;

namespace SickOfIt.Core.UI
{
    /// <summary>
    /// Component for individual level selection buttons using MobileControllerFramework.
    /// Displays level number, completion state, lock state, and roguelite health info.
    /// </summary>
    public class LevelButton : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private GameObject completedIndicator; // Star, checkmark, etc.
        [SerializeField] private GameObject lockedIndicator; // Lock icon, etc.

        [Header("Roguelite Health Display")]
        [Tooltip("Shows the health Yooba ended this level with (e.g. '2/3').")]
        [SerializeField] private TextMeshProUGUI healthInfoText;

        [Header("Visual Feedback (Optional)")]
        [SerializeField] private CanvasGroup canvasGroup; // For opacity control on locked buttons
        [SerializeField] private float lockedAlpha = 0.5f;
        [SerializeField] private float unlockedAlpha = 1f;

        private int levelNumber;
        private bool isUnlocked;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>
        /// Configure the button state and display.
        /// </summary>
        /// <param name="level">Level number</param>
        /// <param name="completed">Whether the level has been completed</param>
        /// <param name="unlocked">Whether the level is accessible</param>
        /// <param name="onClickAction">Callback when button is tapped</param>
        /// <param name="endHealth">Health Yooba ended with (-1 if not completed)</param>
        /// <param name="maxHealth">Max health for display</param>
        public void Setup(int level, bool completed, bool unlocked, System.Action onClickAction, int endHealth = -1, int maxHealth = 3)
        {
            levelNumber = level;
            isUnlocked = unlocked;

            // Set level text
            if (levelText != null)
            {
                levelText.text = levelNumber.ToString();
            }

            // Show/hide completed indicator
            if (completedIndicator != null)
            {
                completedIndicator.SetActive(completed);
            }

            // Show/hide locked indicator
            if (lockedIndicator != null)
            {
                lockedIndicator.SetActive(!unlocked);
            }

            // Set health info text
            if (healthInfoText != null)
            {
                if (completed && endHealth >= 0)
                {
                    healthInfoText.gameObject.SetActive(true);
                    healthInfoText.text = $"{endHealth}/{maxHealth}";
                }
                else
                {
                    healthInfoText.gameObject.SetActive(false);
                }
            }

            // Set button interactability and event
            if (button != null)
            {
                // Use Enable()/Disable() methods from MobileControllerFramework
                if (unlocked)
                {
                    button.Enable();
                }
                else
                {
                    button.Disable();
                }

                button.TapEndEvent.RemoveAllListeners();

                if (unlocked)
                {
                    button.TapEndEvent.AddListener(() => onClickAction?.Invoke());
                }
            }

            // Set visual feedback for locked state
            if (canvasGroup != null)
            {
                canvasGroup.alpha = unlocked ? unlockedAlpha : lockedAlpha;
            }
        }
    }
}
