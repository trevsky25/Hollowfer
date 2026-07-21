using System.Collections.Generic;
using UnityEngine;

namespace Hollowfen.Settings
{
    /// <summary>
    /// Applies persisted display choices before the first scene renders. Resolution width and
    /// height are stored alongside the legacy list index so a refresh-rate or display change
    /// cannot silently make an old index select a different resolution.
    /// </summary>
    public static class DisplaySettings
    {
        private const string PrefFullscreen = "graphics.fullscreen";
        private const string PrefResolutionIndex = "graphics.resolutionIndex";
        private const string PrefResolutionWidth = "graphics.resolutionWidth";
        private const string PrefResolutionHeight = "graphics.resolutionHeight";
        private const string PrefQuality = "graphics.qualityIndex";

        private static bool _appliedThisRun;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => _appliedThisRun = false;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyBeforeFirstScene() => ApplySavedGraphics();

        public static void RecordResolution(int index, int width, int height)
        {
            PlayerPrefs.SetInt(PrefResolutionIndex, index);
            PlayerPrefs.SetInt(PrefResolutionWidth, width);
            PlayerPrefs.SetInt(PrefResolutionHeight, height);
        }

        public static void ApplySavedGraphics()
        {
            if (_appliedThisRun) return;
            _appliedThisRun = true;

            ApplySavedQuality();

            bool hasFullscreen = PlayerPrefs.HasKey(PrefFullscreen);
            bool hasResolution = PlayerPrefs.HasKey(PrefResolutionWidth) &&
                                 PlayerPrefs.HasKey(PrefResolutionHeight);
            bool hasLegacyResolution = PlayerPrefs.HasKey(PrefResolutionIndex);
            if (!hasFullscreen && !hasResolution && !hasLegacyResolution) return;

            bool fullscreen = PlayerPrefs.GetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0) == 1;
            Vector2Int selected = new Vector2Int(Screen.width, Screen.height);
            List<Vector2Int> available = GetAvailableResolutions();

            if (hasResolution)
            {
                var requested = new Vector2Int(
                    PlayerPrefs.GetInt(PrefResolutionWidth, selected.x),
                    PlayerPrefs.GetInt(PrefResolutionHeight, selected.y));
                if (available.Contains(requested)) selected = requested;
            }
            else if (hasLegacyResolution)
            {
                int index = PlayerPrefs.GetInt(PrefResolutionIndex, -1);
                if (index >= 0 && index < available.Count)
                {
                    selected = available[index];
                    RecordResolution(index, selected.x, selected.y);
                }
            }

            FullScreenMode mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(selected.x, selected.y, mode);
        }

        private static void ApplySavedQuality()
        {
            if (!PlayerPrefs.HasKey(PrefQuality)) return;
            string[] names = QualitySettings.names;
            if (names == null || names.Length == 0) return;

            int requested = PlayerPrefs.GetInt(PrefQuality, QualitySettings.GetQualityLevel());
            int valid = Mathf.Clamp(requested, 0, names.Length - 1);
            if (requested != valid) PlayerPrefs.SetInt(PrefQuality, valid);
            if (QualitySettings.GetQualityLevel() != valid)
                QualitySettings.SetQualityLevel(valid, true);
        }

        private static List<Vector2Int> GetAvailableResolutions()
        {
            var result = new List<Vector2Int>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (!result.Contains(size)) result.Add(size);
            }

            if (result.Count == 0)
                result.Add(new Vector2Int(Screen.width, Screen.height));
            return result;
        }
    }
}
