using UnityEngine;
using TMPro;
using MobileControllerFramework;
using SickOfIt.Core.Flow;
using SickOfIt.Data;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Manages the Upgrade Shop scene where players can spend points to purchase upgrades.
    /// Access this scene from the Main Menu.
    /// </summary>
    public class UpgradeShopManager : MonoBehaviour
    {
        [Header("UI - Points Display")]
        [SerializeField] private TextMeshProUGUI pointsText;

        [Header("UI - Extra Jump Upgrade")]
        [SerializeField] private TextMeshProUGUI extraJumpTitleText;
        [SerializeField] private TextMeshProUGUI extraJumpDescriptionText;
        [SerializeField] private TextMeshProUGUI extraJumpCostText;
        [SerializeField] private TextMeshProUGUI extraJumpStatusText;
        [SerializeField] private MobileControllerFramework.Button buyExtraJumpButton;

        [Header("UI - Health Display")]
        [SerializeField] private TextMeshProUGUI cumulativeHealthText;

        [Header("Navigation")]
        [SerializeField] private MobileControllerFramework.Button backButton;

        [Header("Debug")]
        [SerializeField] private MobileControllerFramework.Button addPointsDebugButton;
        [SerializeField] private int debugPointsAmount = 500;

        private void Start()
        {
            SetupButtons();
            RefreshUI();
        }

        private void SetupButtons()
        {
            if (buyExtraJumpButton != null)
                buyExtraJumpButton.TapEndEvent.AddListener(OnBuyExtraJumpClicked);

            if (backButton != null)
                backButton.TapEndEvent.AddListener(OnBackClicked);

            if (addPointsDebugButton != null)
                addPointsDebugButton.TapEndEvent.AddListener(OnAddPointsDebugClicked);
        }

        /// <summary>
        /// Refresh all UI elements to reflect current state.
        /// </summary>
        public void RefreshUI()
        {
            RefreshPointsDisplay();
            RefreshExtraJumpDisplay();
            RefreshHealthDisplay();
        }

        private void RefreshPointsDisplay()
        {
            if (pointsText != null)
            {
                pointsText.text = $"Diamonds: {RogueliteManager.Instance.GetPoints()}";
            }
        }

        private void RefreshExtraJumpDisplay()
        {
            int purchased = RogueliteManager.Instance.GetExtraJumpsPurchased();
            bool maxed = RogueliteManager.Instance.HasMaxExtraJumps();

            if (extraJumpTitleText != null)
            {
                extraJumpTitleText.text = "Extra Air Jump";
            }

            if (extraJumpDescriptionText != null)
            {
                extraJumpDescriptionText.text = $"Air Jumps: {purchased} / {RogueliteManager.Instance.MaxExtraJumps}";
            }

            if (maxed)
            {
                if (extraJumpCostText != null)
                    extraJumpCostText.text = "";

                if (extraJumpStatusText != null)
                    extraJumpStatusText.text = "MAX";

                if (buyExtraJumpButton != null)
                    buyExtraJumpButton.Disable();
            }
            else
            {
                int nextCost = RogueliteManager.Instance.GetExtraJumpCost(purchased + 1);

                if (extraJumpCostText != null)
                    extraJumpCostText.text = $"Cost: {nextCost}";

                if (extraJumpStatusText != null)
                    extraJumpStatusText.text = "";

                if (buyExtraJumpButton != null)
                {
                    if (RogueliteManager.Instance.CanAffordNextExtraJump())
                        buyExtraJumpButton.Enable();
                    else
                        buyExtraJumpButton.Disable();
                }
            }
        }

        private void RefreshHealthDisplay()
        {
            if (cumulativeHealthText != null)
            {
                int totalLevels = GameProgressManager.Instance.GetTotalLevels();
                int maxHealth = RogueliteManager.Instance.GetMaxHealth();
                int cumulativeHealth = RogueliteManager.Instance.GetCumulativeHealth(totalLevels);
                cumulativeHealthText.text = $"Yooba's Health: {cumulativeHealth} / {maxHealth}";
            }
        }

        private void OnBuyExtraJumpClicked()
        {
            bool success = RogueliteManager.Instance.PurchaseExtraJump();
            if (success)
            {
                Debug.Log("[UpgradeShop] Extra jump purchased!");
            }
            else
            {
                Debug.Log("[UpgradeShop] Could not purchase extra jump.");
            }

            RefreshUI();
        }

        private void OnBackClicked()
        {
            SceneTransitionManager.Instance.LoadMainMenu();
        }

        private void OnAddPointsDebugClicked()
        {
            RogueliteManager.Instance.AddPoints(debugPointsAmount);
            Debug.Log($"[UpgradeShop] Debug: Added {debugPointsAmount} points.");
            RefreshUI();
        }
    }
}
