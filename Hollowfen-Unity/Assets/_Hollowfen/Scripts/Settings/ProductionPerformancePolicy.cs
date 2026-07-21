using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hollowfen.Settings
{
    /// <summary>
    /// One production-wide contract for frame pacing and the main presentation camera.
    /// Hollowfen targets a stable 60 fps at native render scale. Hardware VSync is used
    /// whenever the current display is a clean multiple of 60 Hz; other refresh rates use
    /// Unity's 60 fps software cap rather than silently running at 72/82/144 fps.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class ProductionPerformancePolicy : MonoBehaviour
    {
        public const int TargetFrameRate = 60;
        public const float PcLodBias = 1.25f;

        private const double RefreshMatchToleranceHz = 1.0;
        private const float DisplayRecheckSeconds = 2f;
        private const int CameraSearchFrames = 120;

        private static ProductionPerformancePolicy _instance;

        private float _nextDisplayCheck;
        private double _lastRefreshRate = -1d;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private Camera _configuredCamera;
        private Coroutine _cameraSearch;
        private Coroutine _displayRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            var host = new GameObject("[Production Performance]");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<ProductionPerformancePolicy>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // The Standalone PC quality level already owns native render scale, full texture
            // mipmaps, four-cascade 2K soft shadows, SSAO, HDR, and Forward+. Keep the highest
            // fidelity texture sampling active without mutating the player's selected level.
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            ApplyLodBudget();
            OnDemandRendering.renderFrameInterval = 1;
            ApplyFramePacing(true);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            BeginCameraSearch();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextDisplayCheck) return;
            _nextDisplayCheck = Time.unscaledTime + DisplayRecheckSeconds;

            double refreshRate = CurrentRefreshRate();
            int expectedVSync = VSyncCountForRefreshRate(refreshRate);
            bool displayChanged = Screen.width != _lastScreenWidth ||
                                  Screen.height != _lastScreenHeight ||
                                  Math.Abs(refreshRate - _lastRefreshRate) > 0.1d;

            // Quality changes can also restore their serialized VSync value, so validate the
            // live setting even when the monitor itself has not changed.
            if (displayChanged || QualitySettings.vSyncCount != expectedVSync ||
                Application.targetFrameRate != TargetFrameRate)
                ApplyFramePacing(displayChanged);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) RequestDisplayRefresh();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _configuredCamera = null;
            BeginCameraSearch();
            RequestDisplayRefresh();
        }

        /// <summary>
        /// Resolution, fullscreen, and quality controls call this after changing display state.
        /// The delayed pass lets Unity finish switching modes before the refresh rate is sampled.
        /// </summary>
        public static void RequestDisplayRefresh()
        {
            if (_instance == null) return;
            if (_instance._displayRefresh != null)
                _instance.StopCoroutine(_instance._displayRefresh);
            _instance._displayRefresh = _instance.StartCoroutine(_instance.RefreshDisplayNextFrame());
        }

        private IEnumerator RefreshDisplayNextFrame()
        {
            yield return null;
            ApplyFramePacing(true);
            _displayRefresh = null;
        }

        private void ApplyFramePacing(bool logChange)
        {
            double refreshRate = CurrentRefreshRate();
            int vSyncCount = VSyncCountForRefreshRate(refreshRate);

            // Unity ignores targetFrameRate while VSync is active. Keeping both authored makes
            // the fallback deterministic if a monitor switch requires VSync to be disabled.
            Application.targetFrameRate = TargetFrameRate;
            QualitySettings.vSyncCount = vSyncCount;
            ApplyLodBudget();
            OnDemandRendering.renderFrameInterval = 1;

            _lastRefreshRate = refreshRate;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _nextDisplayCheck = Time.unscaledTime + DisplayRecheckSeconds;

            if (!logChange) return;
            string pacing = vSyncCount > 0
                ? $"hardware VSync every {vSyncCount} refresh(es)"
                : "60 fps software cap (display is not a clean 60 Hz multiple)";
            Debug.Log($"[Performance] {TargetFrameRate} FPS production target · " +
                      $"{refreshRate:0.##} Hz display · {pacing} · quality {QualitySettings.names[QualitySettings.GetQualityLevel()]} · LOD bias {QualitySettings.lodBias:0.##}");
        }

        private static void ApplyLodBudget()
        {
            int quality = QualitySettings.GetQualityLevel();
            string qualityName = quality >= 0 && quality < QualitySettings.names.Length
                ? QualitySettings.names[quality]
                : string.Empty;
            QualitySettings.lodBias = string.Equals(qualityName, "PC",
                StringComparison.OrdinalIgnoreCase) ? PcLodBias : 1f;
        }

        private static double CurrentRefreshRate()
        {
            double refreshRate = Screen.currentResolution.refreshRateRatio.value;
            return double.IsNaN(refreshRate) || double.IsInfinity(refreshRate) || refreshRate <= 0d
                ? TargetFrameRate
                : refreshRate;
        }

        internal static int VSyncCountForRefreshRate(double refreshRate)
        {
            if (double.IsNaN(refreshRate) || double.IsInfinity(refreshRate) || refreshRate <= 0d)
                return 0;

            int refreshMultiple = Mathf.Clamp(
                Mathf.RoundToInt((float)(refreshRate / TargetFrameRate)), 1, 4);
            double synchronizedRate = TargetFrameRate * refreshMultiple;
            return Math.Abs(refreshRate - synchronizedRate) <= RefreshMatchToleranceHz
                ? refreshMultiple
                : 0;
        }

        private void BeginCameraSearch()
        {
            if (_cameraSearch != null) StopCoroutine(_cameraSearch);
            _cameraSearch = StartCoroutine(ConfigureMainCameraWhenReady());
        }

        private IEnumerator ConfigureMainCameraWhenReady()
        {
            for (int frame = 0; frame < CameraSearchFrames; frame++)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    ConfigureMainCamera(mainCamera);
                    _cameraSearch = null;
                    yield break;
                }
                yield return null;
            }

            _cameraSearch = null;
        }

        private void ConfigureMainCamera(Camera mainCamera)
        {
            if (_configuredCamera == mainCamera) return;

            mainCamera.allowHDR = true;
            mainCamera.allowDynamicResolution = false; // Native render scale is the quality baseline.
            mainCamera.useOcclusionCulling = true;

            if (mainCamera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
            {
                // SMAA High is stable on foliage and fine geometry without TAA's history ghosting.
                // Keep the post pipeline active for SMAA, stop-NaN, and dithering, but do not let it
                // consume scene volumes. Scene_Hollowfen still contains the asset pack's global
                // "Medieval Fantasy" demo profile (warm gain/white balance plus DOF, motion blur,
                // and grain); enabling that profile changed Hollowfen's established world palette.
                // A zero volume mask restores the authored sun/sky/ambient/fog color while retaining
                // the camera-quality passes that motivated this production policy.
                cameraData.renderPostProcessing = true;
                cameraData.volumeLayerMask = 0;
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
                cameraData.stopNaN = true;
                cameraData.dithering = true;
                cameraData.renderShadows = true;
            }

            _configuredCamera = mainCamera;
            Debug.Log("[Performance] Main camera · native scale · HDR · SMAA High · neutral volume mask · dithering · occlusion culling");
        }
    }
}
