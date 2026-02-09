using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

/// <summary>
/// Override Addressables remote load path at runtime
/// Use this to point to your CDN or custom server
/// </summary>
public class AddressablesRemotePathOverride : MonoBehaviour
{
    [Header("Remote Server Settings")]
    [Tooltip("Base URL where Addressables are hosted (e.g., https://sickofitgame.com/webgl-assets/)")]
    [SerializeField] private string remoteLoadURL = "https://sickofitgame.com/webgl-assets/";
    
    [Tooltip("Apply override on Awake")]
    [SerializeField] private bool overrideOnAwake = true;

    private void Awake()
    {
        if (overrideOnAwake)
        {
            // Detect localhost and use relative path
            string url = Application.absoluteURL;
            if (url.Contains("localhost") || url.Contains("127.0.0.1"))
            {
                // Use relative path for localhost
                SetRemoteLoadPath(Application.streamingAssetsPath + "/../Addressables/WebGL/");
            }
            else
            {
                // Use production URL
                SetRemoteLoadPath(remoteLoadURL);
            }
        }
    }

    /// <summary>
    /// Override the remote load path for Addressables
    /// Call this before loading any Addressables
    /// </summary>
    public void SetRemoteLoadPath(string url)
    {
        // Ensure URL ends with slash
        if (!url.EndsWith("/"))
        {
            url += "/";
        }

        // Transform all remote paths
        Addressables.InternalIdTransformFunc = TransformInternalId;
        
        Debug.Log($"Addressables remote URL set to: {url}");
        
        string TransformInternalId(IResourceLocation location)
        {
            // Only transform remote paths (not local ones)
            if (location.InternalId.StartsWith("http"))
            {
                // Extract just the filename from the original path
                string fileName = System.IO.Path.GetFileName(location.InternalId);
                string newUrl = url + fileName;
                
                Debug.Log($"Transformed: {location.InternalId} → {newUrl}");
                return newUrl;
            }
            
            return location.InternalId;
        }
    }

    /// <summary>
    /// For WebGL, you might want different URLs for different platforms
    /// </summary>
    public void SetPlatformSpecificURL()
    {
#if UNITY_WEBGL
        SetRemoteLoadPath("https://sickofitgame.com/webgl-assets/WebGL/");
#elif UNITY_STANDALONE_WIN
        SetRemoteLoadPath("https://sickofitgame.com/webgl-assets/StandaloneWindows64/");
#elif UNITY_ANDROID
        SetRemoteLoadPath("https://sickofitgame.com/webgl-assets/Android/");
#else
        SetRemoteLoadPath(remoteLoadURL);
#endif
    }
}
