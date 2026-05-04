using System.Collections;
using System.Collections.Generic;
using Hollowfen.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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

            _input = new InputActions();
            _input.UI.Cancel.performed += OnCancelInput;

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
            if (_input != null)
            {
                _input.UI.Cancel.performed -= OnCancelInput;
                _input.Dispose();
                _input = null;
            }
            Instance = null;
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
