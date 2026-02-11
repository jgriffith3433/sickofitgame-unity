using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// On-screen debug console for mobile WebGL testing
/// Shows Unity console logs directly in the game UI
/// Triple-tap the screen to toggle visibility (or press P key in Editor)
/// </summary>
public class MobileDebugConsole : MonoBehaviour
{
    private static MobileDebugConsole _instance;
    public static MobileDebugConsole Instance
    {
        get
        {
            if (_instance == null)
            {
                // Create console UI automatically
                GameObject consoleObj = new GameObject("MobileDebugConsole");
                _instance = consoleObj.AddComponent<MobileDebugConsole>();
                DontDestroyOnLoad(consoleObj);
                _instance.Initialize();
            }
            return _instance;
        }
    }

    [Header("Settings")]
    [SerializeField] private bool enableOnStart = true;
    [SerializeField] private int maxLogCount = 50;
    [SerializeField] private float tapTimeout = 0.5f; // Time window for triple-tap
    
    [Header("Editor Testing")]
    [Tooltip("Enable testing in Unity Editor (press P to toggle console)")]
    [SerializeField] private bool inEditor = true;

    private Canvas canvas;
    private GameObject consolePanel;
    private TextMeshProUGUI logText;
    private ScrollRect scrollRect;
    private List<string> logs = new List<string>();
    private bool isVisible = true;
    private System.Text.StringBuilder logBuilder = new System.Text.StringBuilder(2000);
    private bool needsDisplayUpdate = false;
    private float lastDisplayUpdate = 0f;
    private float displayUpdateInterval = 0.1f; // Only update display 10 times per second

    // Triple-tap detection
    private int tapCount = 0;
    private float lastTapTime = 0f;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Initialize()
    {
        // Create Canvas
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // Always on top
        
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        gameObject.AddComponent<GraphicRaycaster>();

        // Create semi-transparent panel
        consolePanel = new GameObject("ConsolePanel");
        consolePanel.transform.SetParent(transform);
        
        RectTransform panelRect = consolePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(1, 0.5f); // Bottom half of screen
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        Image panelImage = consolePanel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.85f);
        
        Mask mask = consolePanel.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        // Create ScrollRect
        scrollRect = consolePanel.AddComponent<ScrollRect>();
        scrollRect.viewport = panelRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // Create content holder
        GameObject content = new GameObject("Content");
        content.transform.SetParent(consolePanel.transform);
        
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 2000); // Start with large height
        
        ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        scrollRect.content = contentRect;

        // Create text
        GameObject textObj = new GameObject("LogText");
        textObj.transform.SetParent(content.transform);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        
        logText = textObj.AddComponent<TextMeshProUGUI>();
        logText.fontSize = 24;
        logText.color = Color.white;
        logText.alignment = TextAlignmentOptions.TopLeft;
        logText.enableWordWrapping = true;
        
        LayoutElement layoutElement = textObj.AddComponent<LayoutElement>();
        layoutElement.minHeight = 100;
        layoutElement.preferredHeight = -1;
        layoutElement.flexibleHeight = 1;
        
        string toggleHint = Application.isEditor && inEditor ? "Press P to toggle" : "Triple-tap to toggle visibility";
        logText.text = $"[Mobile Debug Console Ready]\n{toggleHint}\n\n";
        
        // Create Copy Button
        CreateCopyButton();

        // Register for Unity log callbacks
        Application.logMessageReceived += HandleLog;

        if (enableOnStart)
        {
            Log("MobileDebugConsole initialized successfully");
            Log($"Platform: {Application.platform}");
            Log($"Device: {SystemInfo.deviceModel}");
            Log($"OS: {SystemInfo.operatingSystem}");
            Log($"Memory: {SystemInfo.systemMemorySize}MB");
        }
        else
        {
            SetVisible(false);
        }
    }
    
    private void CreateCopyButton()
    {
        // Create button container
        GameObject buttonObj = new GameObject("CopyButton");
        buttonObj.transform.SetParent(consolePanel.transform, false);
        
        RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1, 1);
        buttonRect.anchorMax = new Vector2(1, 1);
        buttonRect.pivot = new Vector2(1, 1);
        buttonRect.anchoredPosition = new Vector2(-10, -10);
        buttonRect.sizeDelta = new Vector2(150, 50);
        
        // Button background
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.5f, 0.8f, 0.9f);
        
        // Add button component
        UnityEngine.UI.Button button = buttonObj.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = buttonImage;
        
        // Button colors
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.5f, 0.8f, 0.9f);
        colors.highlightedColor = new Color(0.3f, 0.6f, 0.9f, 1f);
        colors.pressedColor = new Color(0.15f, 0.4f, 0.7f, 1f);
        button.colors = colors;
        
        // Button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        TextMeshProUGUI buttonText = textObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = "Copy Logs";
        buttonText.fontSize = 20;
        buttonText.color = Color.white;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.fontStyle = TMPro.FontStyles.Bold;
        
        // Add click handler
        button.onClick.AddListener(CopyLogsToClipboard);
    }
    
    private void CopyLogsToClipboard()
    {
        string allLogs = string.Join("\n", logs);
        
        // Try standard clipboard first
        try
        {
            GUIUtility.systemCopyBuffer = allLogs;
            Debug.Log("[Console] Logs copied to clipboard!");
            
            // Show visual feedback
            StartCoroutine(ShowCopyFeedback());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Console] Failed to copy to clipboard: {e.Message}");
            
            // Fallback for WebGL
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                CopyToClipboardWebGL(allLogs);
            }
        }
    }
    
    private void CopyToClipboardWebGL(string text)
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            // Use JavaScript to copy to clipboard in WebGL
            Application.ExternalEval($@"
                navigator.clipboard.writeText('{text.Replace("'", "\\'")}').then(function() {{
                    console.log('Logs copied to clipboard!');
                }}).catch(function(err) {{
                    console.error('Failed to copy: ', err);
                    // Fallback: create textarea and copy
                    var textarea = document.createElement('textarea');
                    textarea.value = '{text.Replace("'", "\\'")}';
                    document.body.appendChild(textarea);
                    textarea.select();
                    document.execCommand('copy');
                    document.body.removeChild(textarea);
                }});
            ");
            Debug.Log("[Console] Logs copied via WebGL!");
            StartCoroutine(ShowCopyFeedback());
        }
        catch
        {
            Debug.LogError("[Console] WebGL clipboard copy failed");
        }
        #endif
    }
    
    private System.Collections.IEnumerator ShowCopyFeedback()
    {
        // Temporarily change button text to show feedback
        GameObject buttonObj = GameObject.Find("CopyButton");
        if (buttonObj != null)
        {
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                string originalText = buttonText.text;
                buttonText.text = "Copied!";
                buttonText.color = Color.green;
                
                yield return new WaitForSeconds(1.5f);
                
                buttonText.text = originalText;
                buttonText.color = Color.white;
            }
        }
    }

    private void Update()
    {
        // Update display at a limited rate to prevent constant allocations
        if (needsDisplayUpdate && Time.unscaledTime - lastDisplayUpdate > displayUpdateInterval)
        {
            UpdateLogDisplay();
            lastDisplayUpdate = Time.unscaledTime;
        }
        
        // Editor testing mode - press P key
        if (Application.isEditor && inEditor)
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                ToggleVisibility();
                Debug.Log($"[MobileDebugConsole] Toggled visibility: {isVisible}");
            }
        }
        
        // Triple-tap detection for mobile
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                float currentTime = Time.unscaledTime;
                
                if (currentTime - lastTapTime < tapTimeout)
                {
                    tapCount++;
                    if (tapCount >= 3)
                    {
                        ToggleVisibility();
                        tapCount = 0;
                    }
                }
                else
                {
                    tapCount = 1;
                }
                
                lastTapTime = currentTime;
            }
        }
        
        // Also support backtick/F1 for quick access
        if (Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.F1))
        {
            ToggleVisibility();
        }
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string log = "";
        
        switch (type)
        {
            case LogType.Error:
            case LogType.Exception:
                log = $"[ERROR] {logString}";
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    log += $"\n{stackTrace}";
                }
                break;
            case LogType.Warning:
                log = $"[WARN] {logString}";
                break;
            case LogType.Log:
                log = logString;
                break;
            case LogType.Assert:
                log = $"[ASSERT] {logString}";
                break;
        }
        
        AddLog(log);
    }

    private void AddLog(string log)
    {
        logs.Add(log);
        
        // Keep only recent logs
        if (logs.Count > maxLogCount)
        {
            logs.RemoveAt(0);
        }
        
        UpdateLogDisplay();
    }

    private void UpdateLogDisplay()
    {
        if (logText != null)
        {
            logText.text = string.Join("\n", logs);
        }
    }

    public void ToggleVisibility()
    {
        SetVisible(!isVisible);
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (consolePanel != null)
        {
            consolePanel.SetActive(visible);
        }
    }

    public static void Log(string message)
    {
        Debug.Log(message);
    }

    public static void LogWarning(string message)
    {
        Debug.LogWarning(message);
    }

    public static void LogError(string message)
    {
        Debug.LogError(message);
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    // Manual initialization if you want to control when it appears
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        // Automatically create console on game start
        var instance = Instance;
    }
}
