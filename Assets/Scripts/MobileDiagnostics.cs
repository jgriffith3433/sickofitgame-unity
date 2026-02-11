using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Mobile diagnostic tool - checks for common WebGL mobile issues
/// Automatically runs on game start and logs findings
/// </summary>
public class MobileDiagnostics : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        GameObject diagnosticsObj = new GameObject("MobileDiagnostics");
        diagnosticsObj.AddComponent<MobileDiagnostics>();
        DontDestroyOnLoad(diagnosticsObj);
    }

    private void Start()
    {
        StartCoroutine(RunDiagnostics());
    }

    private IEnumerator RunDiagnostics()
    {
        yield return new WaitForSeconds(1f); // Wait for systems to initialize

        Debug.Log("========================================");
        Debug.Log("[DIAGNOSTICS] MOBILE DIAGNOSTICS REPORT");
        Debug.Log("========================================");

        // Platform Info
        Debug.Log($"Platform: {Application.platform}");
        Debug.Log($"Is Mobile: {Application.isMobilePlatform}");
        Debug.Log($"Device: {SystemInfo.deviceModel}");
        Debug.Log($"OS: {SystemInfo.operatingSystem}");
        Debug.Log($"Screen: {Screen.width}x{Screen.height} @ {Screen.dpi}dpi");
        
        // Memory Info
        Debug.Log($"System Memory: {SystemInfo.systemMemorySize}MB");
        Debug.Log($"Graphics Memory: {SystemInfo.graphicsMemorySize}MB");
        
        // Network Info
        Debug.Log($"Internet: {Application.internetReachability}");
        
        // Check if we're on WebGL
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            Debug.Log("[OK] Running on WebGL");
            
            // Check Addressables settings
            yield return CheckAddressablesSetup();
        }
        else
        {
            Debug.Log("[INFO] Not running on WebGL - diagnostics limited");
        }
        
        // Check for key managers
        CheckManagers();
        
        // Check PlayerPrefs
        CheckPlayerPrefs();
        
        Debug.Log("========================================");
        Debug.Log("[DIAGNOSTICS] DIAGNOSTICS COMPLETE");
        Debug.Log("========================================");
    }

    private IEnumerator CheckAddressablesSetup()
    {
        Debug.Log("--- Addressables Check ---");
        
        bool checkComplete = false;
        long downloadSize = -1;
        
        AddressablesOperationManager.Instance.GetLabelDownloadSize("Video1_Assets", (size) =>
        {
            downloadSize = size;
            checkComplete = true;
        });
        
        float timeout = 10f;
        float elapsed = 0f;
        while (!checkComplete && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (checkComplete)
        {
            if (downloadSize == 0)
            {
                Debug.Log("[OK] Video1_Assets is cached locally");
            }
            else if (downloadSize > 0)
            {
                Debug.Log($"[DOWNLOAD] Video1_Assets needs download: {FormatBytes(downloadSize)}");
                
                // Check if size is unreasonably large for mobile
                if (downloadSize > 50 * 1024 * 1024) // 50MB
                {
                    Debug.LogWarning($"[WARN] WARNING: Bundle is large ({FormatBytes(downloadSize)}) - may timeout on mobile!");
                }
            }
            else
            {
                Debug.LogWarning("[WARN] Unable to determine Video1_Assets download size");
            }
        }
        else
        {
            Debug.LogError("[ERROR] Addressables check timed out - this may indicate a problem!");
        }
    }

    private void CheckManagers()
    {
        Debug.Log("--- Manager Check ---");
        
        // Check for critical managers
        var managers = new string[]
        {
            "SceneTransitionManager",
            "AddressablesSceneLoader",
            "AddressablesOperationManager",
            "GameProgressManager",
            "PreloadManager"
        };
        
        foreach (string managerName in managers)
        {
            GameObject managerObj = GameObject.Find(managerName);
            if (managerObj != null)
            {
                Debug.Log($"[OK] {managerName} found");
            }
            else
            {
                // Try to find by component
                object instance = FindManagerInstance(managerName);
                if (instance != null)
                {
                    Debug.Log($"[OK] {managerName} exists (singleton)");
                }
                else
                {
                    Debug.LogWarning($"[WARN] {managerName} not found");
                }
            }
        }
    }

    private object FindManagerInstance(string managerName)
    {
        switch (managerName)
        {
            case "SceneTransitionManager":
                return SceneTransitionManager.Instance != null ? SceneTransitionManager.Instance : null;
            case "AddressablesSceneLoader":
                return AddressablesSceneLoader.Instance != null ? AddressablesSceneLoader.Instance : null;
            case "AddressablesOperationManager":
                return AddressablesOperationManager.Instance != null ? AddressablesOperationManager.Instance : null;
            case "GameProgressManager":
                return GameProgressManager.Instance != null ? GameProgressManager.Instance : null;
            case "PreloadManager":
                return PreloadManager.Instance != null ? PreloadManager.Instance : null;
            default:
                return null;
        }
    }

    private void CheckPlayerPrefs()
    {
        Debug.Log("--- PlayerPrefs Check ---");
        
        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        int maxLevel = PlayerPrefs.GetInt("MaxLevelReached", 1);
        int returnLevel = PlayerPrefs.GetInt("ReturnToLevel", 0);
        string returnScene = PlayerPrefs.GetString("ReturnScene", "");
        
        Debug.Log($"Current Level: {currentLevel}");
        Debug.Log($"Max Level Reached: {maxLevel}");
        
        if (returnLevel > 0)
        {
            Debug.Log($"Return To Level: {returnLevel}");
            Debug.Log($"Return Scene: {returnScene}");
        }
        
        // Check for Level 1 visited flag
        int level1Visited = PlayerPrefs.GetInt("Level_1_Visited", 0);
        Debug.Log($"Level 1 Visited: {level1Visited == 1}");
    }

    private string FormatBytes(long bytes)
    {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024f).ToString("F2") + " KB";
        return (bytes / (1024f * 1024f)).ToString("F2") + " MB";
    }

    /// <summary>
    /// Call this to run diagnostics again (useful after loading scenes)
    /// </summary>
    public static void RunDiagnosticsNow()
    {
        MobileDiagnostics instance = FindObjectOfType<MobileDiagnostics>();
        if (instance != null)
        {
            instance.StartCoroutine(instance.RunDiagnostics());
        }
        else
        {
            Debug.LogWarning("MobileDiagnostics not found. Creating new instance...");
            GameObject diagnosticsObj = new GameObject("MobileDiagnostics");
            diagnosticsObj.AddComponent<MobileDiagnostics>();
        }
    }

    /// <summary>
    /// Quick network connectivity test
    /// </summary>
    public static void TestNetworkConnectivity()
    {
        GameObject testObj = new GameObject("NetworkTest");
        testObj.AddComponent<MobileDiagnostics>().StartCoroutine(TestNetworkCoroutine());
    }

    private static IEnumerator TestNetworkCoroutine()
    {
        Debug.Log("[NETWORK] Testing network connectivity...");
        
        // Test 1: Simple ping to a reliable server
        using (UnityWebRequest request = UnityWebRequest.Get("https://www.google.com"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[OK] Network connectivity OK");
            }
            else
            {
                Debug.LogError($"[ERROR] Network test failed: {request.error}");
                Debug.LogError("This may prevent Addressables from downloading!");
            }
        }
        
        // Test 2: Check if we can reach Addressables remote path
        // Note: Replace this URL with your actual Addressables hosting URL
        string addressablesUrl = "ServerData/WebGL"; // Default local path
        Debug.Log($"Addressables path: {addressablesUrl}");
        
        yield return null;
    }
}
