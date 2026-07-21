using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hollowfen.Settings
{
    /// <summary>
    /// Applies the player's readable-interface preference to every production screen, including
    /// code-built overlays created after a scene load. Motion and caption preferences are read
    /// directly by their presentation owners so they can preserve gameplay timing and input.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class AccessibilityPresentationPolicy : MonoBehaviour
    {
        private static AccessibilityPresentationPolicy _instance;
        private Coroutine _refreshRoutine;

        public static Vector2 ReferenceResolution =>
            new Vector2(1920f, 1080f) / GameSettings.InterfaceScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var host = new GameObject("[Accessibility Presentation]");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<AccessibilityPresentationPolicy>();
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
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start() => RequestRefresh();

        private void OnDestroy()
        {
            if (_instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
        }

        private void OnSceneLoaded(Scene _, LoadSceneMode __) => RequestRefresh();

        public static void RequestRefresh()
        {
            if (_instance == null) return;
            if (_instance._refreshRoutine != null)
                _instance.StopCoroutine(_instance._refreshRoutine);
            _instance._refreshRoutine = _instance.StartCoroutine(_instance.RefreshAcrossFrames());
        }

        private IEnumerator RefreshAcrossFrames()
        {
            // Apply immediately, then again after code-built screens have had their Awake/Start pass.
            ApplyAll();
            yield return null;
            ApplyAll();
            _refreshRoutine = null;
        }

        private static void ApplyAll()
        {
            Vector2 reference = ReferenceResolution;
            foreach (CanvasScaler scaler in Resources.FindObjectsOfTypeAll<CanvasScaler>())
            {
                if (scaler == null || !scaler.gameObject.scene.IsValid() &&
                    scaler.gameObject.scene.name != "DontDestroyOnLoad") continue;
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
                scaler.referenceResolution = reference;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
            }
        }
    }
}
