using UnityEngine;
using TMPro;
using SickOfIt.Data;

namespace SickOfIt.Combat.Health
{
    /// <summary>
    /// Simple UI component that displays the player's current points.
    /// Event-driven — updates only when points actually change.
    /// Drop this on any GameObject with a TextMeshProUGUI child in a level scene.
    /// </summary>
    public class PointsUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI pointsText;

        [Header("Display")]
        [Tooltip("Format string for points display. {0} is replaced with the point count.")]
        [SerializeField] private string displayFormat = "Points: {0}";

        private void Start()
        {
            if (pointsText == null)
                pointsText = GetComponentInChildren<TextMeshProUGUI>();

            // Show current value immediately
            UpdateDisplay(RogueliteManager.Instance.GetPoints());
        }

        private void OnEnable()
        {
            RogueliteManager.Instance.OnPointsChanged += UpdateDisplay;
        }

        private void OnDisable()
        {
            // Guard against Instance being destroyed during shutdown
            if (RogueliteManager.Instance != null)
                RogueliteManager.Instance.OnPointsChanged -= UpdateDisplay;
        }

        private void UpdateDisplay(int points)
        {
            if (pointsText != null)
                pointsText.text = string.Format(displayFormat, points);
        }
    }
}
