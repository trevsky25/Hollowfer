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
        private TMP_Text _titleRule;
        private TMP_Text _latin;
        private Image _edibilityDot;
        private TMP_Text _edibilityLabel;
        private RectTransform _edibilityChip;
        private TMP_Text _body;
        private RectTransform _statStrip;
        private TMP_Text _statHabitat;
        private TMP_Text _statSeason;
        private TMP_Text _statLookalikes;
        private TMP_Text _foragerNote;
        private Button _forageBtn;
        private Button _leaveBtn;
        private TMP_Text _forageGlyph;
        private TMP_Text _leaveGlyph;

        [Header("Inspect art")]
        [SerializeField, Tooltip("Optional parchment texture used as the panel background. Falls back to a procedural cream when null.")]
        private Sprite _parchmentSprite;

        [Header("Inspect controls")]
        [SerializeField, Tooltip("Mouse-drag rotation: degrees per pixel.")]
        private float _mouseRotateSpeed = 0.35f;
        [SerializeField, Tooltip("Mouse wheel zoom: ortho-size delta per scroll tick.")]
        private float _mouseZoomSpeed = 0.0008f;
        [SerializeField, Tooltip("Gamepad right-stick rotation: degrees per second at full deflection.")]
        private float _gamepadRotateSpeed = 140f;
        [SerializeField, Tooltip("Gamepad trigger zoom: ortho-size delta per second at full trigger.")]
        private float _gamepadZoomSpeed = 0.18f;
        [SerializeField, Tooltip("Mouse middle-drag pan: world meters per pixel.")]
        private float _mousePanSpeed = 0.0006f;
        [SerializeField, Tooltip("Gamepad left-stick pan: world meters per second at full deflection.")]
        private float _gamepadPanSpeed = 0.20f;

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

            if (MushroomPreviewer.Instance != null)
                MushroomPreviewer.Instance.Show(data, !discovered);

            _canvas.enabled = true;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;

            Time.timeScale = 0f;
            PlayerInteractor.Suspended = true;
            PlayerInteractor.SetPlayerInputEnabled(false); // block Player/Jump etc from firing on Submit

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
            PlayerInteractor.SetPlayerInputEnabled(true);

            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;

            _currentNode = null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        // Like Hide() but does NOT restore time / suspended / player-input — the harvest cinematic
        // takes ownership of those across the screen-close → animation transition.
        private void HideForCinematic()
        {
            _canvas.enabled = false;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            if (MushroomPreviewer.Instance != null) MushroomPreviewer.Instance.Clear();

            // Cursor is fine to restore now — gameplay world is visible during the cinematic.
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;

            _currentNode = null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void OnForageClicked()
        {
            var node = _currentNode;
            HideForCinematic();
            if (node != null) node.BeginHarvest();
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

            // Hot-swap button glyphs to whatever pad the player is using right now.
            RefreshButtonGlyphs();

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

            // Gamepad: right stick rotate, left stick pan, triggers zoom
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
                Vector2 ls = pad.leftStick.ReadValue();
                if (ls.sqrMagnitude > 0.0025f)
                {
                    prev.ApplyPanDelta(new Vector2(
                        ls.x * _gamepadPanSpeed * dt,
                        ls.y * _gamepadPanSpeed * dt));
                }
                float zoomGp = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                if (Mathf.Abs(zoomGp) > 0.05f)
                {
                    prev.ApplyZoomDelta(-zoomGp * _gamepadZoomSpeed * dt);
                }
            }

            // Mouse: left-drag rotate (in preview rect), middle-drag pan (in preview rect),
            // scroll wheel to zoom (anywhere over the screen).
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                bool inRect = _previewBgRT != null
                    && RectTransformUtility.RectangleContainsScreenPoint(_previewBgRT, mouse.position.ReadValue(), null);
                if (mouse.leftButton.isPressed && inRect)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    if (d.sqrMagnitude > 0.01f)
                    {
                        prev.ApplyRotationDelta(
                            d.x * _mouseRotateSpeed,
                           -d.y * _mouseRotateSpeed);
                    }
                }
                if (mouse.middleButton.isPressed && inRect)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    if (d.sqrMagnitude > 0.01f)
                    {
                        prev.ApplyPanDelta(new Vector2(
                            d.x * _mousePanSpeed,
                            d.y * _mousePanSpeed));
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

                var ediColor = HollowfenPalette.Edibility(data.Edibility);
                _edibilityDot.color = ediColor;
                _edibilityLabel.color = HollowfenPalette.InkDeep;
                _edibilityLabel.text = data.Edibility.ToString().ToUpperInvariant();
                if (_edibilityChip != null) _edibilityChip.gameObject.SetActive(true);

                _body.text = string.IsNullOrEmpty(data.Description) ? "" : data.Description;

                if (_statStrip != null)
                {
                    bool any = !string.IsNullOrEmpty(data.Habitat)
                            || !string.IsNullOrEmpty(data.Season)
                            || !string.IsNullOrEmpty(data.Lookalikes);
                    _statStrip.gameObject.SetActive(any);
                    SetStat(_statHabitat, "HABITAT", data.Habitat);
                    SetStat(_statSeason, "SEASON", data.Season);
                    SetStat(_statLookalikes, "LOOK-ALIKES", data.Lookalikes);
                }

                if (_foragerNote != null)
                {
                    bool hasNote = !string.IsNullOrEmpty(data.Notes);
                    _foragerNote.gameObject.SetActive(hasNote);
                    if (hasNote) _foragerNote.text = "“" + data.Notes + "”";
                }
            }
            else
            {
                _eyebrow.text = Hollowfen.Localization.Get("inspect.unknown.eyebrow");
                _title.text = Hollowfen.Localization.Get("inspect.unknown.title");
                _latin.gameObject.SetActive(false);
                if (_edibilityChip != null) _edibilityChip.gameObject.SetActive(false);
                _body.text = Hollowfen.Localization.Get("inspect.unknown.body");
                if (_statStrip != null) _statStrip.gameObject.SetActive(false);
                if (_foragerNote != null) _foragerNote.gameObject.SetActive(false);
            }
        }

        private void RefreshButtonGlyphs()
        {
            if (_forageGlyph == null || _leaveGlyph == null) return;
            string forage, leave;
            ResolveGlyphs(out forage, out leave);
            if (_forageGlyph.text != forage) _forageGlyph.text = forage;
            if (_leaveGlyph.text  != leave)  _leaveGlyph.text  = leave;
        }

        // Detects the most recently used gamepad and picks the right brand glyphs.
        // Falls back to keyboard prompts when no pad is connected.
        private static void ResolveGlyphs(out string forage, out string leave)
        {
            // Forage shortcut = Player/Interact (buttonNorth) → Triangle (PS) / Y (Xbox) / X (Switch).
            // Leave shortcut  = UI/Cancel (buttonEast)       → Circle   (PS) / B (Xbox) / A (Switch).
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad == null)
            {
                forage = "E";
                leave = "Esc";
                return;
            }
            string n = pad.GetType().Name;
            string product = pad.description.product != null ? pad.description.product.ToLowerInvariant() : "";

            bool isPS = n.Contains("DualSense") || n.Contains("DualShock") || n.Contains("PS4") || n.Contains("PS5")
                     || product.Contains("dualsense") || product.Contains("dualshock") || product.Contains("playstation")
                     || product.Contains("wireless controller"); // Sony's marketing name on macOS HID
            bool isXbox = n.Contains("XInput") || n.Contains("Xbox")
                       || product.Contains("xbox");
            bool isSwitch = n.Contains("Switch") || n.Contains("Joy")
                         || product.Contains("nintendo") || product.Contains("pro controller");

            if (isPS)
            {
                forage = "△";
                leave = "○";
            }
            else if (isXbox)
            {
                forage = "Y";
                leave = "B";
            }
            else if (isSwitch)
            {
                // Nintendo: top button is X, east button is A (note: SOUTH is B on Switch — face buttons swapped vs Xbox)
                forage = "X";
                leave = "A";
            }
            else
            {
                // Generic gamepad — default to PS since DualSense is what's commonly used here.
                forage = "△";
                leave = "○";
            }
        }

        private static void SetStat(TMP_Text t, string label, string value)
        {
            if (t == null) return;
            if (string.IsNullOrEmpty(value)) { t.gameObject.SetActive(false); return; }
            t.gameObject.SetActive(true);
            string goldHex = ColorUtility.ToHtmlStringRGB(HollowfenPalette.Gold);
            string inkHex = ColorUtility.ToHtmlStringRGB(InkSoftDark);
            t.richText = true;
            t.text = $"<size=11><color=#{goldHex}><b>{label}</b></color></size>\n<color=#{inkHex}>{value}</color>";
        }

        private static readonly Color InkSoftDark = new Color(0.20f, 0.16f, 0.12f, 1f);
        private static readonly Color BodyInk     = new Color(0.18f, 0.14f, 0.10f, 1f);

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            // Full-screen dark scrim — gameplay reads but the panel pops
            var scrim = UICanvasUtil.NewImage("Scrim", transform, new Color(0f, 0f, 0f, 0.78f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            // Panel: 1500×940 parchment journal page (taller so buttons sit cleanly below preview)
            const float panelW = 1500f;
            const float panelH = 940f;
            const float pad = 56f;

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.raycastTarget = true;
            // Rounded journal page (batch-47 square sweep) — the paper family established by
            // pause/settings/ConfirmModal. The old square parchment texture read as a hard box.
            panelImg.color = HollowfenPalette.Parchment;
            UICanvasUtil.Roundify(panelImg, 24);
            var panelRT = (RectTransform)panel.transform;
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(panelW, panelH);
            panelRT.anchoredPosition = Vector2.zero;

            // Vignette overlay — darkens the corners slightly so the parchment reads aged
            var vignette = UICanvasUtil.NewImage("Vignette", panel.transform, new Color(0f, 0f, 0f, 0.18f), false);
            var vignetteImg = vignette.GetComponent<Image>();
            UICanvasUtil.Stretch((RectTransform)vignette.transform);
            // Inset past the rounded corner so the gradient's dark edge doesn't overhang the curve.
            ((RectTransform)vignette.transform).offsetMin = new Vector2(10f, 10f);
            ((RectTransform)vignette.transform).offsetMax = new Vector2(-10f, -10f);
            // Replace flat with a vertical gradient (top + bottom darker, mid clear)
            vignetteImg.sprite = UICanvasUtil.MakeVerticalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0.00f, new Color(0f, 0f, 0f, 0.32f)),
                new UICanvasUtil.GradientStop(0.18f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(0.82f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(1.00f, new Color(0f, 0f, 0f, 0.28f)),
            }, 256);
            vignetteImg.color = Color.white;

            // Double-rule frame inside the panel (outer + inner gold lines, both faint)
            BuildPanelFrame(panelRT, panelW, panelH);

            // Eyebrow strip at the very top: "FIELD JOURNAL — SPECIMEN"
            var topEyebrow = UICanvasUtil.NewEyebrow("TopEyebrow", panel.transform,
                "FIELD JOURNAL  ·  SPECIMEN", 13f, HollowfenPalette.Gold, TMPro.TextAlignmentOptions.Center);
            var teRT = topEyebrow.rectTransform;
            teRT.anchorMin = new Vector2(0f, 1f);
            teRT.anchorMax = new Vector2(1f, 1f);
            teRT.pivot = new Vector2(0.5f, 1f);
            teRT.sizeDelta = new Vector2(0f, 18f);
            teRT.anchoredPosition = new Vector2(0f, -22f);

            // === LEFT: 3D PREVIEW ===
            // Frame around the preview area — thin gold inset. Sits in the upper portion of the panel
            // so the centered button row below has clear vertical room.
            const float previewSize = 700f;
            const float previewYOffset = 60f;
            var previewFrame = UICanvasUtil.NewImage("PreviewFrame", panel.transform, HollowfenPalette.GoldFaint, false);
            UICanvasUtil.Roundify(previewFrame.GetComponent<Image>(), 16);
            var pfRT = (RectTransform)previewFrame.transform;
            pfRT.anchorMin = new Vector2(0f, 0.5f);
            pfRT.anchorMax = new Vector2(0f, 0.5f);
            pfRT.pivot = new Vector2(0f, 0.5f);
            pfRT.sizeDelta = new Vector2(previewSize + 4f, previewSize + 4f);
            pfRT.anchoredPosition = new Vector2(pad - 2f, previewYOffset - 2f);

            var previewBg = UICanvasUtil.NewImage("PreviewBg", panel.transform, HollowfenPalette.Parchment, false);
            UICanvasUtil.Roundify(previewBg.GetComponent<Image>(), 14);
            _previewBgRT = (RectTransform)previewBg.transform;
            _previewBgRT.anchorMin = new Vector2(0f, 0.5f);
            _previewBgRT.anchorMax = new Vector2(0f, 0.5f);
            _previewBgRT.pivot = new Vector2(0f, 0.5f);
            _previewBgRT.sizeDelta = new Vector2(previewSize, previewSize);
            _previewBgRT.anchoredPosition = new Vector2(pad, previewYOffset);

            var previewGO = new GameObject("Preview", typeof(RectTransform));
            previewGO.transform.SetParent(previewBg.transform, false);
            _previewImage = previewGO.AddComponent<RawImage>();
            _previewImage.raycastTarget = false;
            UICanvasUtil.Stretch((RectTransform)previewGO.transform);
            if (MushroomPreviewer.Instance != null)
                _previewImage.texture = MushroomPreviewer.Instance.RenderTexture;

            // Hint moved to TOP of preview frame so it never collides with the centered button row below.
            var hintScrim = UICanvasUtil.NewImage("HintScrim", previewBg.transform, new Color(0f, 0f, 0f, 0.28f), false);
            var hsRT = (RectTransform)hintScrim.transform;
            hsRT.anchorMin = new Vector2(0f, 1f);
            hsRT.anchorMax = new Vector2(1f, 1f);
            hsRT.pivot = new Vector2(0.5f, 1f);
            hsRT.sizeDelta = new Vector2(0f, 26f);
            hsRT.anchoredPosition = Vector2.zero;
            var hint = UICanvasUtil.NewBody("Hint", hintScrim.transform,
                "L/M/R-Drag · Scroll   ·   L/R-Stick · LT/RT",
                12f, HollowfenPalette.Cream,
                TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(hint.rectTransform);

            // === RIGHT: TEXT COLUMN ===
            var col = UICanvasUtil.NewRect("TextCol", panel.transform);
            const float colLeft = pad + previewSize + 56f;
            col.anchorMin = new Vector2(0f, 0f);
            col.anchorMax = new Vector2(1f, 1f);
            col.pivot = new Vector2(0f, 1f);
            col.offsetMin = new Vector2(colLeft, 130f);
            col.offsetMax = new Vector2(-pad, -78f);

            float y = 0f;

            // Eyebrow (edibility category, gold uppercase, letter-spaced)
            _eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", col, "", 14f, HollowfenPalette.Gold);
            var eRT = _eyebrow.rectTransform;
            eRT.anchorMin = new Vector2(0f, 1f); eRT.anchorMax = new Vector2(1f, 1f);
            eRT.pivot = new Vector2(0f, 1f);
            eRT.sizeDelta = new Vector2(0f, 18f);
            eRT.anchoredPosition = new Vector2(0f, y);
            y -= 30f;

            // Title (Georgia serif, dark on parchment)
            _title = UICanvasUtil.NewHeading("Title", col, "", 68f, HollowfenPalette.InkDeep,
                TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var tRT = _title.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0f, 1f);
            tRT.sizeDelta = new Vector2(0f, 80f);
            tRT.anchoredPosition = new Vector2(0f, y);
            y -= 88f;

            // Gold underline rule
            var underline = UICanvasUtil.NewImage("TitleRule", col, HollowfenPalette.Gold, false);
            var urRT = (RectTransform)underline.transform;
            urRT.anchorMin = new Vector2(0f, 1f); urRT.anchorMax = new Vector2(0f, 1f);
            urRT.pivot = new Vector2(0f, 1f);
            urRT.sizeDelta = new Vector2(120f, 2f);
            urRT.anchoredPosition = new Vector2(0f, y);
            y -= 18f;

            // Latin (italic moss)
            _latin = UICanvasUtil.NewBody("Latin", col, "", 22f, HollowfenPalette.Moss,
                TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.TopLeft);
            var lRT = _latin.rectTransform;
            lRT.anchorMin = new Vector2(0f, 1f); lRT.anchorMax = new Vector2(1f, 1f);
            lRT.pivot = new Vector2(0f, 1f);
            lRT.sizeDelta = new Vector2(0f, 30f);
            lRT.anchoredPosition = new Vector2(0f, y);
            y -= 36f;

            // Edibility chip — pill: dot inside, label inside, parchment with gold border feel
            _edibilityChip = UICanvasUtil.NewRect("EdibilityChip", col);
            _edibilityChip.anchorMin = new Vector2(0f, 1f); _edibilityChip.anchorMax = new Vector2(0f, 1f);
            _edibilityChip.pivot = new Vector2(0f, 1f);
            _edibilityChip.sizeDelta = new Vector2(220f, 30f);
            _edibilityChip.anchoredPosition = new Vector2(0f, y);

            var chipBg = UICanvasUtil.NewImage("ChipBg", _edibilityChip, new Color(0f, 0f, 0f, 0.06f), false);
            UICanvasUtil.Roundify(chipBg.GetComponent<Image>(), 15);   // full pill at 30px height
            UICanvasUtil.Stretch((RectTransform)chipBg.transform);

            var dotGO = UICanvasUtil.NewImage("Dot", _edibilityChip, Color.white, false);
            dotGO.GetComponent<Image>().sprite = UICanvasUtil.Circle(); // a dot, not a tiny square
            var dotRT = (RectTransform)dotGO.transform;
            dotRT.anchorMin = new Vector2(0f, 0.5f); dotRT.anchorMax = new Vector2(0f, 0.5f);
            dotRT.pivot = new Vector2(0f, 0.5f);
            dotRT.sizeDelta = new Vector2(12f, 12f);
            dotRT.anchoredPosition = new Vector2(12f, 0f);
            _edibilityDot = dotGO.GetComponent<Image>();

            _edibilityLabel = UICanvasUtil.NewEyebrow("EdibilityLabel", _edibilityChip, "", 14f, HollowfenPalette.InkDeep);
            var elRT = _edibilityLabel.rectTransform;
            elRT.anchorMin = new Vector2(0f, 0.5f); elRT.anchorMax = new Vector2(1f, 0.5f);
            elRT.pivot = new Vector2(0f, 0.5f);
            elRT.sizeDelta = new Vector2(-32f, 24f);
            elRT.anchoredPosition = new Vector2(32f, 0f);
            y -= 44f;

            // Body description (dark on parchment)
            _body = UICanvasUtil.NewBody("Body", col, "", 21f, BodyInk,
                TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var bRT = _body.rectTransform;
            bRT.anchorMin = new Vector2(0f, 1f); bRT.anchorMax = new Vector2(1f, 1f);
            bRT.pivot = new Vector2(0f, 1f);
            bRT.sizeDelta = new Vector2(0f, 200f);
            bRT.anchoredPosition = new Vector2(0f, y);
            _body.lineSpacing = 8f;
            y -= 220f;

            // Stat strip — three columns: HABITAT / SEASON / LOOK-ALIKES.
            // Height sized for the worst-case stat value (LOOK-ALIKES tends to run long with safety copy).
            const float statStripH = 170f;
            _statStrip = UICanvasUtil.NewRect("StatStrip", col);
            _statStrip.anchorMin = new Vector2(0f, 1f); _statStrip.anchorMax = new Vector2(1f, 1f);
            _statStrip.pivot = new Vector2(0f, 1f);
            _statStrip.sizeDelta = new Vector2(0f, statStripH);
            _statStrip.anchoredPosition = new Vector2(0f, y);

            // Top rule on stat strip
            var statRule = UICanvasUtil.NewImage("StatRule", _statStrip, HollowfenPalette.GoldFaint, false);
            var srRT = (RectTransform)statRule.transform;
            srRT.anchorMin = new Vector2(0f, 1f); srRT.anchorMax = new Vector2(1f, 1f);
            srRT.pivot = new Vector2(0.5f, 1f);
            srRT.sizeDelta = new Vector2(0f, 1f);
            srRT.anchoredPosition = Vector2.zero;

            _statHabitat   = MakeStatColumn(_statStrip, 0f / 3f);
            _statSeason    = MakeStatColumn(_statStrip, 1f / 3f);
            _statLookalikes= MakeStatColumn(_statStrip, 2f / 3f);

            y -= statStripH + 12f;

            // Forager's note — italic gold pull-quote
            _foragerNote = UICanvasUtil.NewBody("ForagerNote", col, "", 18f, HollowfenPalette.Gold,
                TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.TopLeft);
            var fnRT = _foragerNote.rectTransform;
            fnRT.anchorMin = new Vector2(0f, 1f); fnRT.anchorMax = new Vector2(1f, 1f);
            fnRT.pivot = new Vector2(0f, 1f);
            fnRT.sizeDelta = new Vector2(0f, 80f);
            fnRT.anchoredPosition = new Vector2(0f, y);
            _foragerNote.lineSpacing = 6f;

            // === BUTTONS ===
            var btnRow = UICanvasUtil.NewRect("Buttons", panel.transform);
            btnRow.anchorMin = new Vector2(0.5f, 0f); btnRow.anchorMax = new Vector2(0.5f, 0f);
            btnRow.pivot = new Vector2(0.5f, 0f);
            btnRow.sizeDelta = new Vector2(720f, 78f);
            btnRow.anchoredPosition = new Vector2(0f, 36f);

            _forageBtn = MakeJournalButton("ForageBtn", btnRow,
                Hollowfen.Localization.Get("inspect.btn.forage"),
                HollowfenPalette.Gold, HollowfenPalette.InkDeep, true,
                new Vector2(-176f, 0f), out _forageGlyph);
            _forageBtn.onClick.AddListener(OnForageClicked);

            _leaveBtn = MakeJournalButton("LeaveBtn", btnRow,
                Hollowfen.Localization.Get("inspect.btn.leave"),
                Color.white, HollowfenPalette.InkDeep, false,
                new Vector2(176f, 0f), out _leaveGlyph);
            _leaveBtn.onClick.AddListener(OnLeaveClicked);

            RefreshButtonGlyphs();

            var fNav = _forageBtn.navigation; fNav.mode = Navigation.Mode.Explicit;
            fNav.selectOnRight = _leaveBtn; _forageBtn.navigation = fNav;
            var lNav = _leaveBtn.navigation; lNav.mode = Navigation.Mode.Explicit;
            lNav.selectOnLeft = _forageBtn; _leaveBtn.navigation = lNav;
        }

        // Inner-frame double-rule: outer thin gold + inner thinner gold, both at panel-edge inset.
        // Rounded outlines (batch-47) — the old 4-strip rectangles read as hard square boxes.
        private static void BuildPanelFrame(RectTransform panelRT, float w, float h)
        {
            MakeInsetOutline(panelRT, "FrameOuter", 14f, HollowfenPalette.GoldFaint, 1.5f, 16);
            MakeInsetOutline(panelRT, "FrameInner", 22f, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.22f), 1f, 12);
        }

        private static void MakeInsetOutline(RectTransform parent, string name, float inset, Color color, float thickness, int radius)
        {
            var go = UICanvasUtil.NewImage(name, parent, color, false);
            UICanvasUtil.RoundifyOutline(go.GetComponent<Image>(), radius, thickness);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static TMP_Text MakeStatColumn(RectTransform parent, float anchorX)
        {
            var t = UICanvasUtil.NewBody("Stat", parent, "", 14f, BodyInk,
                TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(anchorX, 0f); rt.anchorMax = new Vector2(anchorX + 1f / 3f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(8f, 4f);
            rt.offsetMax = new Vector2(-8f, -10f);
            t.lineSpacing = 4f;
            return t;
        }

        // Solid CTA button (Forage) vs outlined parchment button (Leave). Includes a small glyph pill on the left
        // so users see exactly which controller button maps to it without leaving the screen.
        // Returns the inner glyph TMP_Text via `out` so the caller can hot-swap PS/Xbox/keyboard glyphs.
        private static Button MakeJournalButton(string name, RectTransform parent, string label,
                                                Color bg, Color fg, bool isSolid, Vector2 anchored,
                                                out TMP_Text glyphTextOut)
        {
            var bgGO = new GameObject(name, typeof(RectTransform));
            bgGO.transform.SetParent(parent, false);
            var bgRT = (RectTransform)bgGO.transform;
            bgRT.anchorMin = new Vector2(0.5f, 0.5f); bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.pivot = new Vector2(0.5f, 0.5f);
            bgRT.sizeDelta = new Vector2(316f, 64f);
            bgRT.anchoredPosition = anchored;

            var img = bgGO.AddComponent<Image>();
            img.color = isSolid ? bg : new Color(bg.r, bg.g, bg.b, 0.15f);
            img.raycastTarget = true;
            UICanvasUtil.Roundify(img, 14);

            // Outline ring for the non-solid (Leave) button — rounded hairline (batch-47).
            if (!isSolid)
            {
                var outline = UICanvasUtil.NewImage("Outline", bgRT, HollowfenPalette.GoldFaint, false);
                UICanvasUtil.RoundifyOutline(outline.GetComponent<Image>(), 14, 1.5f);
                outline.transform.SetSiblingIndex(0);
                UICanvasUtil.Stretch((RectTransform)outline.transform);
            }

            var btn = bgGO.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            Color baseCol = img.color;
            cb.normalColor = baseCol;
            cb.highlightedColor = isSolid ? Color.Lerp(bg, Color.white, 0.18f) : new Color(bg.r, bg.g, bg.b, 0.30f);
            cb.selectedColor   = isSolid ? Color.Lerp(bg, Color.white, 0.22f) : new Color(bg.r, bg.g, bg.b, 0.35f);
            cb.pressedColor    = isSolid ? Color.Lerp(bg, Color.black, 0.18f) : new Color(bg.r, bg.g, bg.b, 0.10f);
            cb.disabledColor   = Color.Lerp(baseCol, Color.black, 0.5f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            // Glyph pill on the left
            var glyphGO = UICanvasUtil.NewImage("Glyph", bgRT,
                new Color(0f, 0f, 0f, isSolid ? 0.18f : 0.10f), false);
            UICanvasUtil.Roundify(glyphGO.GetComponent<Image>(), 10);
            var gRT = (RectTransform)glyphGO.transform;
            gRT.anchorMin = new Vector2(0f, 0.5f); gRT.anchorMax = new Vector2(0f, 0.5f);
            gRT.pivot = new Vector2(0f, 0.5f);
            gRT.sizeDelta = new Vector2(36f, 36f);
            gRT.anchoredPosition = new Vector2(14f, 0f);

            var glyphTxt = UICanvasUtil.NewBody("GlyphTxt", glyphGO.transform, "?", 22f, fg,
                TMPro.FontStyles.Bold, TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(glyphTxt.rectTransform);
            glyphTxt.raycastTarget = false;
            glyphTextOut = glyphTxt;

            // Label
            var lblTxt = UICanvasUtil.NewEyebrow("Label", bgRT, label, 18f, fg, TMPro.TextAlignmentOptions.Center);
            var lblRT = lblTxt.rectTransform;
            lblRT.anchorMin = new Vector2(0f, 0f); lblRT.anchorMax = new Vector2(1f, 1f);
            lblRT.pivot = new Vector2(0.5f, 0.5f);
            lblRT.offsetMin = new Vector2(60f, 0f);
            lblRT.offsetMax = new Vector2(-16f, 0f);
            lblTxt.raycastTarget = false;

            return btn;
        }
    }
}
