using UnityEngine;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Simple debug display that shows preload information on screen
/// Just add this component to any GameObject to see preload status
/// </summary>
public class PreloadDebugUI : MonoBehaviour
{
    [Header("Display Settings")]
    [SerializeField] private int fontSize = 14;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Vector2 position = new Vector2(10, 10);

    [Header("Common Labels to Check")]
    [SerializeField] private List<string> commonLabels = new List<string>
    {
        "Level1",
        "Level2",
        "Video1",
        "Video2"
    };

    private StringBuilder displayText = new StringBuilder();
    private GUIStyle guiStyle;
    private float updateInterval = 0.5f;
    private float lastUpdateTime;

    private void Start()
    {
        SetupGUIStyle();
    }

    private void SetupGUIStyle()
    {
        guiStyle = new GUIStyle();
        guiStyle.fontSize = fontSize;
        guiStyle.normal.textColor = textColor;
        guiStyle.alignment = TextAnchor.UpperLeft;
    }

    private void Update()
    {
        // Update display periodically
        if (Time.time - lastUpdateTime > updateInterval)
        {
            UpdateDisplayInfo();
            lastUpdateTime = Time.time;
        }
    }

    private void UpdateDisplayInfo()
    {
        displayText.Clear();
        displayText.AppendLine("=== PRELOAD DEBUG INFO ===");
        displayText.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        displayText.AppendLine($"Time: {Time.time:F1}s");
        displayText.AppendLine();
        displayText.AppendLine("Common Labels:");
        
        foreach (string label in commonLabels)
        {
            displayText.AppendLine($"  • {label}");
        }
        
        displayText.AppendLine();
        displayText.AppendLine("Note: Check Console for detailed preload logs");
        displayText.AppendLine("Enable 'Show Debug Logs' on AddressablesOperationManager");
    }

    private void OnGUI()
    {
        if (guiStyle == null)
        {
            SetupGUIStyle();
        }

        // Set GUI to lowest depth so it doesn't block any UI interactions
        GUI.depth = int.MaxValue;
        
        GUI.Label(new Rect(position.x, position.y, 400, 600), displayText.ToString(), guiStyle);
    }
}
