#if UNITY_EDITOR
using System;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// Monitors the MCP Unity WebSocket server and force-restarts it if it dies.
    /// Uses reflection to avoid hard asmdef dependency on mcp-unity package.
    /// Checks every 5 seconds, restarts after 2 consecutive failures (10s down).
    /// </summary>
    [InitializeOnLoad]
    public static class McpServerMonitor
    {
        private static double _nextCheckTime;
        private const double CHECK_INTERVAL = 5.0;
        private static int _port = 8090;
        private static bool _initialized;
        private static bool _disabled;
        private static int _consecutiveFailures;
        
        // Cached reflection handles
        private static object _serverInstance;
        private static MethodInfo _startMethod;
        private static MethodInfo _stopMethod;
        private static PropertyInfo _isListeningProp;

        static McpServerMonitor()
        {
            _nextCheckTime = EditorApplication.timeSinceStartup + 10.0; // Wait 10s for initial load
            _initialized = false;
            _disabled = false;
            _consecutiveFailures = 0;
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (_disabled) return;
            if (EditorApplication.timeSinceStartup < _nextCheckTime) return;
            _nextCheckTime = EditorApplication.timeSinceStartup + CHECK_INTERVAL;

            if (!_initialized)
            {
                TryInitialize();
                return; // Skip first check cycle to let things settle
            }

            CheckAndRestart();
        }

        private static void TryInitialize()
        {
            try
            {
                // Find the McpUnityServer type across all loaded assemblies
                Type serverType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        serverType = asm.GetType("McpUnity.Unity.McpUnityServer", false);
                        if (serverType != null) break;
                    }
                    catch { /* skip problematic assemblies */ }
                }

                if (serverType == null)
                {
                    Debug.Log("[McpMonitor] MCP Unity package not found. Disabling monitor.");
                    _disabled = true;
                    EditorApplication.update -= OnUpdate;
                    return;
                }

                // Get Instance property
                var instanceProp = serverType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp == null)
                {
                    Debug.LogWarning("[McpMonitor] Cannot find McpUnityServer.Instance. Disabling.");
                    _disabled = true;
                    return;
                }

                _serverInstance = instanceProp.GetValue(null);
                if (_serverInstance == null)
                {
                    Debug.LogWarning("[McpMonitor] McpUnityServer.Instance returned null. Will retry.");
                    return; // Don't disable — retry on next cycle
                }

                _isListeningProp = serverType.GetProperty("IsListening", BindingFlags.Public | BindingFlags.Instance);
                _startMethod = serverType.GetMethod("StartServer", BindingFlags.Public | BindingFlags.Instance);
                _stopMethod = serverType.GetMethod("StopServer", BindingFlags.Public | BindingFlags.Instance);

                if (_isListeningProp == null || _startMethod == null)
                {
                    Debug.LogWarning("[McpMonitor] Cannot find required MCP server methods. Disabling.");
                    _disabled = true;
                    return;
                }

                // Try to read port from settings
                TryReadPort();

                _initialized = true;
                Debug.Log($"[McpMonitor] Initialized. Monitoring MCP server on port {_port}.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[McpMonitor] Init error: {ex.Message}");
                // Don't disable — retry next cycle
            }
        }

        private static void TryReadPort()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var settingsType = asm.GetType("McpUnity.Unity.McpUnitySettings", false);
                    if (settingsType == null) continue;

                    var settingsInstanceProp = settingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (settingsInstanceProp == null) continue;

                    var settings = settingsInstanceProp.GetValue(null);
                    if (settings == null) continue;

                    var portField = settingsType.GetField("Port");
                    if (portField != null)
                    {
                        _port = (int)portField.GetValue(settings);
                    }
                    break;
                }
            }
            catch { /* use default port */ }
        }

        private static void CheckAndRestart()
        {
            try
            {
                bool isListening = false;

                // Check via IsListening property
                try
                {
                    isListening = (bool)_isListeningProp.GetValue(_serverInstance);
                }
                catch
                {
                    // Instance may have been invalidated by domain reload
                    _initialized = false;
                    return;
                }

                if (!isListening)
                {
                    _consecutiveFailures++;
                    if (_consecutiveFailures >= 2)
                    {
                        Debug.Log($"[McpMonitor] MCP server not listening for {_consecutiveFailures * CHECK_INTERVAL}s. Restarting...");
                        try
                        {
                            _stopMethod?.Invoke(_serverInstance, null);
                        }
                        catch { /* ignore stop errors */ }

                        try
                        {
                            _startMethod.Invoke(_serverInstance, null);
                            Debug.Log("[McpMonitor] MCP server restarted.");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[McpMonitor] Restart failed: {ex.InnerException?.Message ?? ex.Message}");
                        }
                        _consecutiveFailures = 0;
                    }
                }
                else
                {
                    if (_consecutiveFailures > 0)
                    {
                        Debug.Log("[McpMonitor] MCP server recovered.");
                    }
                    _consecutiveFailures = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[McpMonitor] Check error: {ex.Message}");
                _initialized = false; // Force re-init
            }
        }

        private static bool TcpPortOpen(int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect("127.0.0.1", port, null, null);
                    bool connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                    if (connected)
                    {
                        client.EndConnect(result);
                        return true;
                    }
                }
            }
            catch { /* not listening */ }
            return false;
        }
    }
}
#endif
