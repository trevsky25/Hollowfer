using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Brings the MCP For Unity HTTP bridge up whenever the editor loads, so
    /// Claude Code can connect without the manual Window > MCP For Unity step.
    /// </summary>
    [InitializeOnLoad]
    internal static class McpBridgeBootstrap
    {
        static McpBridgeBootstrap()
        {
            // Opt in to the package's own auto-start for future editor launches.
            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
            EditorApplication.delayCall += () => _ = EnsureBridgeAsync();
        }

        private static async Task EnsureBridgeAsync()
        {
            try
            {
                if (MCPServiceLocator.Bridge.IsRunning) return;

                if (!MCPServiceLocator.Server.IsLocalHttpServerReachable()
                    && !MCPServiceLocator.Server.StartLocalHttpServer(quiet: true))
                {
                    Debug.LogWarning("[Hollowfen] MCP bootstrap: failed to start local HTTP server.");
                    return;
                }

                for (int attempt = 0; attempt < 30; attempt++)
                {
                    if (MCPServiceLocator.Server.IsLocalHttpServerReachable()
                        && await MCPServiceLocator.Bridge.StartAsync())
                    {
                        Debug.Log("[Hollowfen] MCP bridge connected.");
                        return;
                    }
                    await Task.Delay(1000);
                }

                Debug.LogWarning("[Hollowfen] MCP bootstrap: bridge did not connect within 30s.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Hollowfen] MCP bootstrap failed: {e.Message}");
            }
        }
    }
}
