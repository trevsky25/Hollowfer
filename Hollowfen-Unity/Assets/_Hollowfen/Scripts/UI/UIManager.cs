using System.Collections;
using System.Collections.Generic;
using Hollowfen.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private Transform _screenRoot;
        [SerializeField] private CanvasGroup _fadeOverlay;
        [SerializeField] private float _fadeDuration = 0.2f;
        [SerializeField] private string _initialScreenId;
        [SerializeField] private GameObject _pauseMenuPrefab;

        private InputActions _input;
        private readonly Dictionary<string, UIScreen> _screens = new Dictionary<string, UIScreen>();
        private readonly Stack<UIScreen> _stack = new Stack<UIScreen>();
        private bool _transitioning;

        public bool HasOpenScreen => _stack.Count > 0;
        public UIScreen TopScreen => _stack.Count > 0 ? _stack.Peek() : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureEventSystem();

            SceneManager.sceneLoaded += OnSceneLoaded;

            _input = new InputActions();
            _input.UI.Cancel.performed += OnCancelInput;
            _input.Player.Pause.performed += OnPauseInput;
            _input.Player.Enable();

            EnsureFadeOverlay();
            EnsureUIInputModule();
            DiscoverAndRegisterScreens();
            UpdateInputMapState();

            if (!string.IsNullOrEmpty(_initialScreenId))
                OpenScreen(_initialScreenId);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_input != null)
            {
                _input.UI.Cancel.performed -= OnCancelInput;
                _input.Dispose();
                _input = null;
            }
            Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Tear down any duplicate EventSystem the new scene brought along —
            // the DDOL'd one from the original Scene_MainMenu load is the keeper.
            var all = UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all.Length > 1)
            {
                EventSystem keep = null;
                foreach (var e in all)
                    if (e.gameObject.scene.name == "DontDestroyOnLoad") { keep = e; break; }
                if (keep == null) keep = all[0];
                foreach (var e in all)
                    if (e != keep) Destroy(e.gameObject);
            }
        }

        // Async scene transition with the loading screen layered on top.
        // If nextScreenId is non-null and registered, the loading screen is replaced
        // by that screen after the scene finishes loading; otherwise it just closes.
        public void LoadSceneAndOpen(string sceneName, string nextScreenId = null)
        {
            StartCoroutine(LoadSceneRoutine(sceneName, nextScreenId));
        }

        private IEnumerator LoadSceneRoutine(string sceneName, string nextScreenId)
        {
            CloseAll();

            bool hasLoading = _screens.ContainsKey("loading");
            if (hasLoading)
            {
                OpenScreen("loading");
                // Give the loading screen a frame to activate before kicking off the load.
                yield return null;
                yield return new WaitForSecondsRealtime(0.05f);
            }

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone) yield return null;

            // Brief settle so the new scene has a frame to render before we hand off.
            yield return new WaitForSecondsRealtime(0.25f);

            if (!string.IsNullOrEmpty(nextScreenId) && _screens.ContainsKey(nextScreenId))
            {
                ReplaceScreen(nextScreenId);
            }
            else if (TopScreen != null && TopScreen.ScreenId == "loading")
            {
                Back();
            }
        }

        public void RegisterScreen(UIScreen screen)
        {
            if (screen == null) return;
            if (string.IsNullOrEmpty(screen.ScreenId))
            {
                Debug.LogWarning($"[UIManager] Screen on '{screen.name}' has no ScreenId; skipping.", screen);
                return;
            }
            _screens[screen.ScreenId] = screen;
            screen.EnsureInitialized();
            screen.gameObject.SetActive(false);
        }

        public void UnregisterScreen(UIScreen screen)
        {
            if (screen == null || string.IsNullOrEmpty(screen.ScreenId)) return;
            if (_screens.TryGetValue(screen.ScreenId, out var existing) && existing == screen)
                _screens.Remove(screen.ScreenId);

            if (!_stack.Contains(screen)) return;
            // Stack iterates top→bottom; pushing then popping reverses twice = original order, minus removed.
            var keep = new Stack<UIScreen>();
            foreach (var s in _stack) if (s != screen) keep.Push(s);
            _stack.Clear();
            while (keep.Count > 0) _stack.Push(keep.Pop());
            UpdateInputMapState();
        }

        public void OpenScreen(string screenId)
        {
            if (_transitioning || string.IsNullOrEmpty(screenId)) return;
            if (!_screens.TryGetValue(screenId, out var next))
            {
                Debug.LogWarning($"[UIManager] Unknown ScreenId '{screenId}'.");
                return;
            }
            if (TopScreen == next) return;
            StartCoroutine(TransitionRoutine(next, push: true, replace: false));
        }

        public void ReplaceScreen(string screenId)
        {
            if (_transitioning || string.IsNullOrEmpty(screenId)) return;
            if (!_screens.TryGetValue(screenId, out var next))
            {
                Debug.LogWarning($"[UIManager] Unknown ScreenId '{screenId}'.");
                return;
            }
            StartCoroutine(TransitionRoutine(next, push: true, replace: true));
        }

        public void Back()
        {
            if (_transitioning || _stack.Count == 0) return;
            StartCoroutine(TransitionRoutine(null, push: false, replace: false));
        }

        // Synchronously close every screen on the stack. Used when transitioning to
        // a new scene where the menu chain shouldn't carry over.
        public void CloseAll()
        {
            while (_stack.Count > 0)
            {
                var top = _stack.Pop();
                top.OnClose();
                top.gameObject.SetActive(false);
            }
            UpdateInputMapState();
        }

        private IEnumerator TransitionRoutine(UIScreen next, bool push, bool replace)
        {
            _transitioning = true;

            // Skip global fade for modal push (next is modal) or modal pop (top of stack is modal).
            bool fade = true;
            if (push && next != null && next.IsModal) fade = false;
            if (!push && _stack.Count > 0 && _stack.Peek().IsModal) fade = false;

            if (fade) yield return Fade(_fadeOverlay != null ? _fadeOverlay.alpha : 0f, 1f);

            UIScreen previous = TopScreen;

            if (replace)
            {
                if (previous != null)
                {
                    _stack.Pop();
                    previous.OnClose();
                    previous.gameObject.SetActive(false);
                }
                if (next != null)
                {
                    _stack.Push(next);
                    next.gameObject.SetActive(true);
                    SetCanvasSortOrder(next, _stack.Count);
                    next.OnOpen();
                }
            }
            else if (push)
            {
                // Modals leave the underlying screen visible behind them.
                if (previous != null && next != null && !next.IsModal)
                    previous.gameObject.SetActive(false);
                if (next != null)
                {
                    _stack.Push(next);
                    next.gameObject.SetActive(true);
                    SetCanvasSortOrder(next, _stack.Count);
                    next.OnOpen();
                }
            }
            else
            {
                if (previous != null)
                {
                    _stack.Pop();
                    previous.OnClose();
                    previous.gameObject.SetActive(false);
                }
                if (_stack.Count > 0)
                {
                    var resumed = _stack.Peek();
                    if (!resumed.gameObject.activeSelf)
                    {
                        resumed.gameObject.SetActive(true);
                        resumed.OnOpen();
                    }
                }
            }

            UpdateInputMapState();
            SetFocusToTop();

            if (fade) yield return Fade(1f, 0f);
            _transitioning = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (_fadeOverlay == null || _fadeDuration <= 0f)
            {
                if (_fadeOverlay != null)
                {
                    _fadeOverlay.alpha = to;
                    _fadeOverlay.blocksRaycasts = to > 0.01f;
                }
                yield break;
            }
            _fadeOverlay.blocksRaycasts = true;
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _fadeOverlay.alpha = Mathf.Lerp(from, to, t / _fadeDuration);
                yield return null;
            }
            _fadeOverlay.alpha = to;
            _fadeOverlay.blocksRaycasts = to > 0.01f;
        }

        private void OnCancelInput(InputAction.CallbackContext ctx)
        {
            if (_transitioning) return;
            var top = TopScreen;
            if (top != null) top.OnBack();
        }

        private void OnPauseInput(InputAction.CallbackContext ctx)
        {
            if (_transitioning) return;
            // Don't re-open if pause is already on top.
            if (TopScreen != null && TopScreen.ScreenId == "pause") return;
            // Don't open over a confirm modal — let the user resolve it first.
            if (TopScreen != null && TopScreen.IsModal) return;
            EnsurePauseInstance();
            OpenScreen("pause");
        }

        // Lazy-instantiate the pause menu from its prefab on first request.
        // Lets gameplay scenes pick up pause without needing PauseScreen in their hierarchy.
        private void EnsurePauseInstance()
        {
            if (_screens.ContainsKey("pause")) return;
            if (_pauseMenuPrefab == null) return;

            var parent = _screenRoot != null ? _screenRoot : transform;
            var instance = Instantiate(_pauseMenuPrefab, parent);
            instance.name = _pauseMenuPrefab.name;
            var screen = instance.GetComponent<UIScreen>();
            if (screen != null) RegisterScreen(screen);
        }

        private void UpdateInputMapState()
        {
            if (_input == null) return;
            if (_stack.Count > 0) _input.UI.Enable();
            else _input.UI.Disable();
        }

        private void SetFocusToTop()
        {
            var top = TopScreen;
            if (top == null) return;
            var es = EventSystem.current;
            if (es == null)
            {
                Debug.LogWarning("[UIManager] No EventSystem in scene; gamepad navigation will not work.");
                return;
            }
            es.SetSelectedGameObject(null);
            if (top.DefaultSelected != null)
                es.SetSelectedGameObject(top.DefaultSelected);
        }

        private void DiscoverAndRegisterScreens()
        {
            var root = _screenRoot != null ? _screenRoot : transform;
            var screens = root.GetComponentsInChildren<UIScreen>(includeInactive: true);
            foreach (var s in screens) RegisterScreen(s);
        }

        private static void SetCanvasSortOrder(UIScreen screen, int stackPosition)
        {
            if (screen == null) return;
            var canvas = screen.GetComponentInChildren<Canvas>(true);
            if (canvas != null) canvas.sortingOrder = stackPosition * 10;
        }

        // Make sure exactly one DDOL'd EventSystem exists. If a scene-level one is
        // present, persist it; otherwise spawn one. Keeping the EventSystem out of
        // Scene_MainMenu's saved hierarchy avoids the "only one active Event System"
        // warning when returning to that scene from gameplay.
        private void EnsureEventSystem()
        {
            var es = EventSystem.current;
            if (es != null)
            {
                if (es.transform.parent == null && es.gameObject.scene.name != "DontDestroyOnLoad")
                    DontDestroyOnLoad(es.gameObject);
                return;
            }
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        private void EnsureUIInputModule()
        {
            var es = EventSystem.current;
            if (es == null) return;
            var module = es.GetComponent<InputSystemUIInputModule>();
            if (module == null) return;
            if (module.move == null)   module.move   = InputActionReference.Create(_input.UI.Navigate);
            if (module.submit == null) module.submit = InputActionReference.Create(_input.UI.Submit);
            if (module.cancel == null) module.cancel = InputActionReference.Create(_input.UI.Cancel);
        }

        private void EnsureFadeOverlay()
        {
            if (_fadeOverlay != null) return;

            var go = new GameObject(
                "FadeOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup),
                typeof(Image));
            go.transform.SetParent(transform, worldPositionStays: false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;

            _fadeOverlay = go.GetComponent<CanvasGroup>();
            _fadeOverlay.alpha = 0f;
            _fadeOverlay.blocksRaycasts = false;
            _fadeOverlay.interactable = false;
        }
    }
}
