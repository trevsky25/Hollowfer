using Hollowfen.Data;
using Hollowfen.Input;
using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.Foraging
{
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(Canvas))]
    public class InspectScreen : MonoBehaviour
    {
        public static InspectScreen Instance { get; private set; }

        private Canvas _canvas;
        private CanvasGroup _group;
        private InputActions _input;

        private RawImage _previewImage;
        private RectTransform _previewBgRT;
        private TMP_Text _eyebrow;
        private TMP_Text _title;
        private TMP_Text _latin;
        private Image _edibilityDot;
        private TMP_Text _edibilityLabel;
        private TMP_Text _body;
        private Button _forageBtn;
        private Button _leaveBtn;

        [Header("Inspect controls")]
        [SerializeField, Tooltip("Mouse-drag rotation: degrees per pixel.")]
        private float _mouseRotateSpeed = 0.35f;
        [SerializeField, Tooltip("Mouse wheel zoom: ortho-size delta per scroll tick.")]
        private float _mouseZoomSpeed = 0.0008f;
        [SerializeField, Tooltip("Gamepad right-stick rotation: degrees per second at full deflection.")]
        private float _gamepadRotateSpeed = 140f;
        [SerializeField, Tooltip("Gamepad trigger zoom: ortho-size delta per second at full trigger.")]
        private float _gamepadZoomSpeed = 0.18f;

        private MushroomNode _currentNode;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;
        private bool _built;
        private GameObject _lastSelectedButton;

        public bool IsOpen => _canvas != null && _canvas.enabled;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _canvas = GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 60;
            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.Init1080();
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            BuildIfNeeded();
            _input = new InputActions();
            _canvas.enabled = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _input?.Dispose();
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _input.UI.Enable();
                _input.UI.Cancel.performed += OnCancel;
                _input.UI.Submit.performed += OnSubmit;
                // Player/Interact (Triangle / E) doubles as Forage shortcut while the screen is open.
                _input.Player.Enable();
                _input.Player.Interact.performed += OnPlayerInteractShortcut;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.UI.Cancel.performed -= OnCancel;
                _input.UI.Submit.performed -= OnSubmit;
                _input.Player.Interact.performed -= OnPlayerInteractShortcut;
                _input.UI.Disable();
                _input.Player.Disable();
            }
        }

        // Static entry point used by MushroomNode.Interact.
        public static void Open(MushroomNode node)
        {
            if (Instance == null || node == null || node.Data == null) return;
            Instance.ShowInternal(node);
        }

        private void ShowInternal(MushroomNode node)
        {
            BuildIfNeeded();
            EnsureEventSystem();
            _currentNode = node;
            var data = node.Data;
            bool discovered = MushroomDiscovery.IsDiscovered(data.Id);
            ApplyContent(data, discovered);

            if (MushroomPreviewer.Instance != null) MushroomPreviewer.Instance.Show(data);

            _canvas.enabled = true;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;

            Time.timeScale = 0f;
            PlayerInteractor.Suspended = true;

            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (EventSystem.current != null && _forageBtn != null)
            {
                EventSystem.current.SetSelectedGameObject(_forageBtn.gameObject);
                _lastSelectedButton = _forageBtn.gameObject;
            }
        }

        // If we entered Scene_Hollowfen directly (no MainMenu boot flow), UIManager's DDOL'd EventSystem
        // is missing. Create one on the fly so buttons + gamepad nav work.
        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                new GameObject("EventSystem", typeof(EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }
            // Don't deselect the focused button when the mouse moves off it.
            var module = EventSystem.current != null
                ? EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>()
                : null;
            if (module != null) module.deselectOnBackgroundClick = false;
        }

        private void Hide()
        {
            _canvas.enabled = false;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            if (MushroomPreviewer.Instance != null) MushroomPreviewer.Instance.Clear();

            Time.timeScale = 1f;
            PlayerInteractor.Suspended = false;

            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;

            _currentNode = null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void OnForageClicked()
        {
            var node = _currentNode;
            Hide();
            if (node != null) node.Harvest();
        }

        private void OnLeaveClicked()
        {
            Hide();
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (IsOpen) Hide();
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Keep gamepad navigation alive: if the EventSystem deselected (e.g., mouse moved off a button),
            // restore the last button we focused so left/right still toggles Forage <-> Leave.
            var es = EventSystem.current;
            if (es != null && _forageBtn != null && _leaveBtn != null)
            {
                var sel = es.currentSelectedGameObject;
                if (sel == _forageBtn.gameObject || sel == _leaveBtn.gameObject)
                    _lastSelectedButton = sel;
                else if (sel == null)
                    es.SetSelectedGameObject(_lastSelectedButton != null ? _lastSelectedButton : _forageBtn.gameObject);
            }

            var prev = MushroomPreviewer.Instance;
            if (prev == null) return;
            float dt = Time.unscaledDeltaTime;

            // Gamepad: right stick rotate, triggers zoom
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null)
            {
                Vector2 rs = pad.rightStick.ReadValue();
                if (rs.sqrMagnitude > 0.0025f)
                {
                    prev.ApplyRotationDelta(
                        rs.x * _gamepadRotateSpeed * dt,
                       -rs.y * _gamepadRotateSpeed * dt);
                }
                float zoomGp = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                if (Mathf.Abs(zoomGp) > 0.05f)
                {
                    prev.ApplyZoomDelta(-zoomGp * _gamepadZoomSpeed * dt);
                }
            }

            // Mouse: drag to rotate (only if pointer is over the preview rect),
            // scroll wheel to zoom (anywhere over the screen).
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.isPressed && _previewBgRT != null)
                {
                    Vector2 sp = mouse.position.ReadValue();
                    if (RectTransformUtility.RectangleContainsScreenPoint(_previewBgRT, sp, null))
                    {
                        Vector2 d = mouse.delta.ReadValue();
                        if (d.sqrMagnitude > 0.01f)
                        {
                            prev.ApplyRotationDelta(
                                d.x * _mouseRotateSpeed,
                               -d.y * _mouseRotateSpeed);
                        }
                    }
                }
                Vector2 scroll = mouse.scroll.ReadValue();
                if (Mathf.Abs(scroll.y) > 0.01f)
                {
                    prev.ApplyZoomDelta(-scroll.y * _mouseZoomSpeed);
                }
            }
        }

        private void OnSubmit(InputAction.CallbackContext _)
        {
            if (!IsOpen) return;
            // If a button is focused, route through it (so users see the press feedback);
            // otherwise default to Forage.
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel != null && sel == (_leaveBtn != null ? _leaveBtn.gameObject : null)) { OnLeaveClicked(); return; }
            OnForageClicked();
        }

        private void OnPlayerInteractShortcut(InputAction.CallbackContext _)
        {
            if (!IsOpen) return;
            OnForageClicked();
        }

        private void ApplyContent(MushroomFieldGuideData data, bool discovered)
        {
            if (discovered)
            {
                _eyebrow.text = (string.IsNullOrEmpty(data.EdibilityLabel) ? data.Edibility.ToString() : data.EdibilityLabel).ToUpperInvariant();
                _title.text = data.CommonName;
                _latin.text = data.LatinName;
                _latin.gameObject.SetActive(!string.IsNullOrEmpty(data.LatinName));
                _edibilityDot.color = HollowfenPalette.Edibility(data.Edibility);
                _edibilityLabel.color = HollowfenPalette.Edibility(data.Edibility);
                _edibilityLabel.text = data.Edibility.ToString().ToUpperInvariant();
                _edibilityDot.transform.parent.gameObject.SetActive(true);
                _body.text = string.IsNullOrEmpty(data.Description) ? "" : data.Description;
            }
            else
            {
                _eyebrow.text = Hollowfen.Localization.Get("inspect.unknown.eyebrow");
                _title.text = Hollowfen.Localization.Get("inspect.unknown.title");
                _latin.gameObject.SetActive(false);
                _edibilityDot.transform.parent.gameObject.SetActive(false);
                _body.text = Hollowfen.Localization.Get("inspect.unknown.body");
            }
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            // Scrim
            var scrim = UICanvasUtil.NewImage("Scrim", transform, new Color(0f, 0f, 0f, 0.72f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            // Center panel
            var panel = UICanvasUtil.NewImage("Panel", transform, HollowfenPalette.InkSoft, true);
            var panelRT = (RectTransform)panel.transform;
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(1280f, 720f);
            panelRT.anchoredPosition = Vector2.zero;

            // Subtle gold border at top of panel
            var rule = UICanvasUtil.NewImage("TopRule", panel.transform, HollowfenPalette.GoldFaint, false);
            var ruleRT = (RectTransform)rule.transform;
            ruleRT.anchorMin = new Vector2(0f, 1f);
            ruleRT.anchorMax = new Vector2(1f, 1f);
            ruleRT.pivot = new Vector2(0.5f, 1f);
            ruleRT.sizeDelta = new Vector2(-64f, 1.5f);
            ruleRT.anchoredPosition = new Vector2(0f, -28f);

            // Left: 3D preview
            var previewBg = UICanvasUtil.NewImage("PreviewBg", panel.transform, new Color(0.040f, 0.035f, 0.028f, 1f), false);
            _previewBgRT = (RectTransform)previewBg.transform;
            _previewBgRT.anchorMin = new Vector2(0f, 0.5f);
            _previewBgRT.anchorMax = new Vector2(0f, 0.5f);
            _previewBgRT.pivot = new Vector2(0f, 0.5f);
            _previewBgRT.sizeDelta = new Vector2(560f, 560f);
            _previewBgRT.anchoredPosition = new Vector2(48f, 28f);

            var previewGO = new GameObject("Preview", typeof(RectTransform));
            previewGO.transform.SetParent(previewBg.transform, false);
            _previewImage = previewGO.AddComponent<RawImage>();
            _previewImage.raycastTarget = false;
            UICanvasUtil.Stretch((RectTransform)previewGO.transform);
            ((RectTransform)previewGO.transform).offsetMin = new Vector2(8f, 8f);
            ((RectTransform)previewGO.transform).offsetMax = new Vector2(-8f, -8f);
            if (MushroomPreviewer.Instance != null)
                _previewImage.texture = MushroomPreviewer.Instance.RenderTexture;

            // Hint inside the preview frame, bottom-center, with a subtle dark scrim for legibility
            var hintScrim = UICanvasUtil.NewImage("HintScrim", previewBg.transform, new Color(0f, 0f, 0f, 0.40f), false);
            var hintScrimRT = (RectTransform)hintScrim.transform;
            hintScrimRT.anchorMin = new Vector2(0f, 0f);
            hintScrimRT.anchorMax = new Vector2(1f, 0f);
            hintScrimRT.pivot = new Vector2(0.5f, 0f);
            hintScrimRT.sizeDelta = new Vector2(-16f, 26f);
            hintScrimRT.anchoredPosition = new Vector2(0f, 8f);

            var hint = UICanvasUtil.NewBody("Hint", hintScrim.transform,
                "Drag · Scroll  |  R-Stick · LT/RT",
                13f, HollowfenPalette.Parchment,
                TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(hint.rectTransform);

            // Right: text column
            var col = UICanvasUtil.NewRect("TextCol", panel.transform);
            col.anchorMin = new Vector2(0f, 0f);
            col.anchorMax = new Vector2(1f, 1f);
            col.pivot = new Vector2(0f, 0.5f);
            col.offsetMin = new Vector2(656f, 120f);
            col.offsetMax = new Vector2(-48f, -76f);

            _eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", col, "", 18f, HollowfenPalette.Gold);
            var eyebrowRT = _eyebrow.rectTransform;
            eyebrowRT.anchorMin = new Vector2(0f, 1f);
            eyebrowRT.anchorMax = new Vector2(1f, 1f);
            eyebrowRT.pivot = new Vector2(0f, 1f);
            eyebrowRT.sizeDelta = new Vector2(0f, 24f);
            eyebrowRT.anchoredPosition = Vector2.zero;

            _title = UICanvasUtil.NewHeading("Title", col, "", 64f, HollowfenPalette.Cream,
                TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var titleRT = _title.rectTransform;
            titleRT.anchorMin = new Vector2(0f, 1f);
            titleRT.anchorMax = new Vector2(1f, 1f);
            titleRT.pivot = new Vector2(0f, 1f);
            titleRT.sizeDelta = new Vector2(0f, 80f);
            titleRT.anchoredPosition = new Vector2(0f, -28f);

            _latin = UICanvasUtil.NewBody("Latin", col, "", 22f, HollowfenPalette.Moss,
                TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.TopLeft);
            var latinRT = _latin.rectTransform;
            latinRT.anchorMin = new Vector2(0f, 1f);
            latinRT.anchorMax = new Vector2(1f, 1f);
            latinRT.pivot = new Vector2(0f, 1f);
            latinRT.sizeDelta = new Vector2(0f, 28f);
            latinRT.anchoredPosition = new Vector2(0f, -116f);

            // Edibility chip
            var chip = UICanvasUtil.NewRect("Edibility", col);
            chip.anchorMin = new Vector2(0f, 1f);
            chip.anchorMax = new Vector2(0f, 1f);
            chip.pivot = new Vector2(0f, 1f);
            chip.sizeDelta = new Vector2(280f, 24f);
            chip.anchoredPosition = new Vector2(0f, -156f);

            var dotGO = UICanvasUtil.NewImage("Dot", chip, Color.white, false);
            var dotRT = (RectTransform)dotGO.transform;
            dotRT.anchorMin = new Vector2(0f, 0.5f);
            dotRT.anchorMax = new Vector2(0f, 0.5f);
            dotRT.pivot = new Vector2(0f, 0.5f);
            dotRT.sizeDelta = new Vector2(12f, 12f);
            dotRT.anchoredPosition = new Vector2(0f, 0f);
            _edibilityDot = dotGO.GetComponent<Image>();

            _edibilityLabel = UICanvasUtil.NewEyebrow("EdibilityLabel", chip, "", 16f, Color.white);
            var elRT = _edibilityLabel.rectTransform;
            elRT.anchorMin = new Vector2(0f, 0.5f);
            elRT.anchorMax = new Vector2(1f, 0.5f);
            elRT.pivot = new Vector2(0f, 0.5f);
            elRT.sizeDelta = new Vector2(-22f, 24f);
            elRT.anchoredPosition = new Vector2(22f, 0f);

            _body = UICanvasUtil.NewBody("Body", col, "", 22f, HollowfenPalette.Parchment,
                TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var bodyRT = _body.rectTransform;
            bodyRT.anchorMin = new Vector2(0f, 0f);
            bodyRT.anchorMax = new Vector2(1f, 1f);
            bodyRT.pivot = new Vector2(0f, 1f);
            bodyRT.offsetMin = new Vector2(0f, 100f);
            bodyRT.offsetMax = new Vector2(0f, -200f);

            // Buttons row
            var btnRow = UICanvasUtil.NewRect("Buttons", panel.transform);
            btnRow.anchorMin = new Vector2(0.5f, 0f);
            btnRow.anchorMax = new Vector2(0.5f, 0f);
            btnRow.pivot = new Vector2(0.5f, 0f);
            btnRow.sizeDelta = new Vector2(680f, 64f);
            btnRow.anchoredPosition = new Vector2(0f, 32f);

            _forageBtn = MakeButton("ForageBtn", btnRow, Hollowfen.Localization.Get("inspect.btn.forage"),
                HollowfenPalette.Gold, HollowfenPalette.InkDeep, new Vector2(-160f, 0f));
            _forageBtn.onClick.AddListener(OnForageClicked);

            _leaveBtn = MakeButton("LeaveBtn", btnRow, Hollowfen.Localization.Get("inspect.btn.leave"),
                new Color(0.30f, 0.27f, 0.22f, 1f), HollowfenPalette.Cream, new Vector2(160f, 0f));
            _leaveBtn.onClick.AddListener(OnLeaveClicked);

            // Hook gamepad navigation Forage <-> Leave
            var fNav = _forageBtn.navigation;
            fNav.mode = Navigation.Mode.Explicit;
            fNav.selectOnRight = _leaveBtn;
            _forageBtn.navigation = fNav;

            var lNav = _leaveBtn.navigation;
            lNav.mode = Navigation.Mode.Explicit;
            lNav.selectOnLeft = _forageBtn;
            _leaveBtn.navigation = lNav;
        }

        private static Button MakeButton(string name, RectTransform parent, string label, Color bg, Color fg, Vector2 anchored)
        {
            var bgGO = UICanvasUtil.NewImage(name, parent, bg, true);
            var bgRT = (RectTransform)bgGO.transform;
            bgRT.anchorMin = new Vector2(0.5f, 0.5f);
            bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.pivot = new Vector2(0.5f, 0.5f);
            bgRT.sizeDelta = new Vector2(280f, 56f);
            bgRT.anchoredPosition = anchored;

            var btn = bgGO.AddComponent<Button>();
            var img = bgGO.GetComponent<Image>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = bg;
            cb.highlightedColor = Color.Lerp(bg, Color.white, 0.18f);
            cb.selectedColor = Color.Lerp(bg, Color.white, 0.22f);
            cb.pressedColor = Color.Lerp(bg, Color.black, 0.18f);
            cb.disabledColor = Color.Lerp(bg, Color.black, 0.5f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var text = UICanvasUtil.NewEyebrow("Label", bgGO.transform, label, 18f, fg, TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch((RectTransform)text.transform);
            return btn;
        }
    }
}
