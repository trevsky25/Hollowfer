using UnityEditor;
using UnityEditorInternal;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Keeps Play mode ticking while the editor is in the background. The editor throttles
    /// the player loop when unfocused, which freezes Time/physics and breaks the automated
    /// Play-mode verification driven over the MCP bridge. Editor-only; no effect on builds.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeBackgroundTicker
    {
        static PlayModeBackgroundTicker()
        {
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPaused
                && !InternalEditorUtility.isApplicationActive)
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }
    }
}
