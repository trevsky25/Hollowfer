using System.Collections.Generic;
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
    public class InventoryScreen : MonoBehaviour
    {
        public static InventoryScreen Instance { get; private set; }

        [Header("Inventory art")]
        [SerializeField, Tooltip("Optional parchment texture (same asset as InspectScreen). Falls back to flat parchment color.")]
        private Sprite _parchmentSprite;

        [Header("Preview controls")]
        [SerializeField] private float _mouseRotateSpeed = 0.35f;
        [SerializeField] private float _mouseZoomSpeed = 0.0008f;
        [SerializeField] private float _gamepadRotateSpeed = 140f;
        [SerializeField] private float _gamepadZoomSpeed = 0.18f;
        [SerializeField] private float _mousePanSpeed = 0.0006f;
        [SerializeField] private float _gamepadPanSpeed = 0.20f;

        private Canvas _canvas;
        private CanvasGroup _group;
        private InputActions _input;

        private bool _built;
        private GridLayoutGroup _grid;
        private RectTransform _gridContent;
        private RectTransform _previewBgRT;
        private RawImage _previewImage;
        private TMP_Text _previewMissingNote;
        private TMP_Text _emptyText;
        private GameObject _emptyState;
        private GameObject _populatedState;
        private TMP_Text _selectedTitle;
        private TMP_Text _selectedLatin;
        private TMP_Text _selectedCount;
        private Button _closeBtn;
        private TMP_Text _closeGlyph;
        private TMP_Text _keepsakesLabel;
        private GameObject _lastSelected;

        private readonly List<Cell> _cells = new List<Cell>();
        private MushroomFieldGuideData _selectedData;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        // Inspect Mode: when true, gamepad camera input is unlocked and EventSystem nav is suspended.
        // Browse mode (default) lets the gamepad navigate the card grid via UI/Navigate.
        private bool _inspectMode;
        private TMP_Text _hintText;
        private Image _previewFrameImage;

        private static readonly Color BodyInk = new Color(0.18f, 0.14f, 0.10f, 1f);

        public bool IsOpen => _canvas != null && _canvas.enabled;

        private struct Cell
        {
            public RectTransform Root;
            public Button Button;
            public Image Photo;
            public Image Border;
            public TMP_Text NameText;
            public TMP_Text CountBadge;
            public MushroomFieldGuideData Data;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _canvas = GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 55; // below Inspect (60) so an open Inspect always paints on top
            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.Init1080();
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            _input = new InputActions();
            _canvas.enabled = false;

            InventoryRuntime.OnChanged += OnInventoryChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            InventoryRuntime.OnChanged -= OnInventoryChanged;
            _input?.Dispose();
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _input.UI.Enable();
                _input.UI.Cancel.performed += OnCancel;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.UI.Cancel.performed -= OnCancel;
                _input.UI.Disable();
            }
        }

        public void Open()
        {
            BuildIfNeeded();
            EnsureEventSystem();
            _inspectMode = false; // always start in browse
            ApplyModeVisuals();
            Refresh();

            _canvas.enabled = true;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;

            Time.timeScale = 0f;
            PlayerInteractor.Suspended = true;
            PlayerInteractor.SetPlayerInputEnabled(false);

            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Default-select first cell or close button
            GameObject first = null;
            if (_cells.Count > 0) first = _cells[0].Button.gameObject;
            else if (_closeBtn != null) first = _closeBtn.gameObject;
            if (first != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(first);
                _lastSelected = first;
            }
        }

        public void Close()
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

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (!IsOpen) return;
            // First Cancel exits Inspect Mode if active; second one closes the inventory.
            if (_inspectMode) { SetInspectMode(false); return; }
            Close();
        }

        private void SetInspectMode(bool enabled)
        {
            if (_inspectMode == enabled) return;
            _inspectMode = enabled;
            ApplyModeVisuals();

            var es = EventSystem.current;
            if (enabled)
            {
                // Suspend card nav by clearing the EventSystem selection. UI/Navigate fires but has
                // nothing to navigate from, so left-stick is free for camera pan.
                if (es != null) es.SetSelectedGameObject(null);
            }
            else
            {
                // Restore selection so UI/Navigate works on cards again.
                if (es != null && _lastSelected != null) es.SetSelectedGameObject(_lastSelected);
                // Reset preview view to the configured hero shot for the currently-selected mushroom.
                if (MushroomPreviewer.Instance != null) MushroomPreviewer.Instance.ResetView();
            }
        }

        private void ApplyModeVisuals()
        {
            if (_previewFrameImage != null)
            {
                _previewFrameImage.color = _inspectMode ? HollowfenPalette.Gold : HollowfenPalette.GoldFaint;
            }
            if (_hintText != null)
            {
                _hintText.text = _inspectMode
                    ? "INSPECT MODE   ·   L/R-Stick · LT/RT · M-Drag   ·   R1 / Tab / B to exit"
                    : "L-Stick / D-Pad: navigate   ·   R1 / Tab: inspect mushroom";
            }
        }

        private void OnInventoryChanged(string id, int count)
        {
            if (!IsOpen) return;
            Refresh();
        }

        // --- Per-frame: keep selection alive across mouse motion + drive preview from input ---
        private void Update()
        {
            if (!IsOpen) return;

            RefreshCloseGlyph();

            // Toggle Inspect Mode: gamepad R1 (right shoulder), keyboard Tab.
            var pad = Gamepad.current;
            if (pad != null && pad.rightShoulder.wasPressedThisFrame) SetInspectMode(!_inspectMode);
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) SetInspectMode(!_inspectMode);

            // Browse-mode-only: maintain card selection + sync preview to it.
            if (!_inspectMode)
            {
                var es = EventSystem.current;
                if (es != null)
                {
                    var sel = es.currentSelectedGameObject;
                    if (sel != null) _lastSelected = sel;
                    else if (_lastSelected != null) es.SetSelectedGameObject(_lastSelected);

                    if (sel != null)
                    {
                        for (int i = 0; i < _cells.Count; i++)
                        {
                            if (_cells[i].Button != null && _cells[i].Button.gameObject == sel)
                            {
                                SelectCell(i);
                                break;
                            }
                        }
                    }
                }
            }

            var prev = MushroomPreviewer.Instance;
            if (prev == null) return;
            float dt = Time.unscaledDeltaTime;

            // GAMEPAD camera input — only in Inspect Mode (left-stick conflicts with UI/Navigate otherwise).
            // Right stick + triggers also gated to inspect mode for consistency, even though they don't conflict.
            if (_inspectMode && pad != null)
            {
                Vector2 rs = pad.rightStick.ReadValue();
                if (rs.sqrMagnitude > 0.0025f)
                    prev.ApplyRotationDelta(rs.x * _gamepadRotateSpeed * dt, -rs.y * _gamepadRotateSpeed * dt);
                Vector2 ls = pad.leftStick.ReadValue();
                if (ls.sqrMagnitude > 0.0025f)
                    prev.ApplyPanDelta(new Vector2(ls.x * _gamepadPanSpeed * dt, ls.y * _gamepadPanSpeed * dt));
                float zoomGp = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                if (Mathf.Abs(zoomGp) > 0.05f) prev.ApplyZoomDelta(-zoomGp * _gamepadZoomSpeed * dt);
            }

            // MOUSE camera input — always available. Drags are gated to the preview rect, so they can't
            // conflict with card clicks (which happen elsewhere on the panel).
            var mouse = Mouse.current;
            if (mouse != null)
            {
                bool inRect = _previewBgRT != null
                    && RectTransformUtility.RectangleContainsScreenPoint(_previewBgRT, mouse.position.ReadValue(), null);
                if (mouse.leftButton.isPressed && inRect)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    if (d.sqrMagnitude > 0.01f)
                        prev.ApplyRotationDelta(d.x * _mouseRotateSpeed, -d.y * _mouseRotateSpeed);
                }
                if (mouse.middleButton.isPressed && inRect)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    if (d.sqrMagnitude > 0.01f)
                        prev.ApplyPanDelta(new Vector2(d.x * _mousePanSpeed, d.y * _mousePanSpeed));
                }
                Vector2 scroll = mouse.scroll.ReadValue();
                if (Mathf.Abs(scroll.y) > 0.01f) prev.ApplyZoomDelta(-scroll.y * _mouseZoomSpeed);
            }
        }

        private void RefreshCloseGlyph()
        {
            if (_closeGlyph == null) return;
            // Close = UI/Cancel (buttonEast) — brand glyph via the shared resolver (batch-48).
            string g = Gamepad.current == null
                ? "Esc"
                : Hollowfen.UI.ControllerGlyphs.For(Hollowfen.UI.ControllerGlyphs.Face.East);
            if (_closeGlyph.text != g) _closeGlyph.text = g;
        }

        // --- Build & content ---

        private void Refresh()
        {
            // Tear down old cells
            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                if (_cells[i].Root != null) DestroyImmediate(_cells[i].Root.gameObject);
            }
            _cells.Clear();

            // Empty vs populated state
            int distinct = InventoryRuntime.DistinctCount;
            _emptyState.SetActive(distinct == 0);
            _populatedState.SetActive(distinct > 0);

            RefreshKeepsakes();

            if (distinct == 0)
            {
                if (MushroomPreviewer.Instance != null) MushroomPreviewer.Instance.Clear();
                _selectedTitle.text = "";
                _selectedLatin.text = "";
                _selectedCount.text = "";
                _previewMissingNote.gameObject.SetActive(false);
                return;
            }

            foreach (var kv in InventoryRuntime.All)
            {
                var data = InventoryRuntime.ResolveData(kv.Key);
                if (data == null) continue;
                AddCell(data, kv.Value);
            }

            WireCellNavigation();

            // Force-select first cell so the preview pane has content immediately.
            if (_cells.Count > 0 && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_cells[0].Button.gameObject);
                _lastSelected = _cells[0].Button.gameObject;
                SelectCell(0);
            }
        }

        // Bottom-left strip of narrative key items ("KEEPSAKES — Mill Key · Father's Journal").
        // Lives on the panel (not _populatedState) so it shows even with an empty mushroom pouch.
        private void RefreshKeepsakes()
        {
            if (_keepsakesLabel == null) return;
            var ids = Items.KeyItems.All;
            if (ids.Count == 0)
            {
                _keepsakesLabel.gameObject.SetActive(false);
                return;
            }
            _keepsakesLabel.gameObject.SetActive(true);
            var names = new System.Text.StringBuilder();
            names.Append("<color=#9a7b2f>").Append(Hollowfen.Localization.Get("inventory.keepsakes")).Append("</color>   ");
            bool first = true;
            foreach (var id in ids)
            {
                if (!first) names.Append("   ·   ");
                names.Append(Hollowfen.Localization.Get(id + ".name"));
                first = false;
            }
            _keepsakesLabel.text = names.ToString();
        }

        // Auto navigation gets flaky for grid cells inside a clipped ScrollRect — wire explicit grid neighbors.
        // Last row's down nav drops onto the Close button so D-pad/stick can reach it from anywhere in the grid.
        private void WireCellNavigation()
        {
            if (_cells.Count == 0) return;
            int cols = _grid != null ? _grid.constraintCount : 4;

            for (int i = 0; i < _cells.Count; i++)
            {
                var btn = _cells[i].Button;
                if (btn == null) continue;
                var nav = btn.navigation;
                nav.mode = Navigation.Mode.Explicit;

                int col = i % cols;
                nav.selectOnLeft  = (col > 0) ? _cells[i - 1].Button : null;
                nav.selectOnRight = (col < cols - 1 && i + 1 < _cells.Count) ? _cells[i + 1].Button : null;
                nav.selectOnUp    = (i - cols >= 0) ? _cells[i - cols].Button : null;

                if (i + cols < _cells.Count) nav.selectOnDown = _cells[i + cols].Button;
                else if (_closeBtn != null)  nav.selectOnDown = _closeBtn;
                else                         nav.selectOnDown = null;

                btn.navigation = nav;
            }

            if (_closeBtn != null)
            {
                var cn = _closeBtn.navigation;
                cn.mode = Navigation.Mode.Explicit;
                cn.selectOnUp = _cells[_cells.Count - 1].Button;
                cn.selectOnLeft = null;
                cn.selectOnRight = null;
                cn.selectOnDown = null;
                _closeBtn.navigation = cn;
            }
        }

        private void SelectCell(int index)
        {
            if (index < 0 || index >= _cells.Count) return;
            var cell = _cells[index];
            if (_selectedData == cell.Data) return;
            _selectedData = cell.Data;

            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i].Border == null) continue;
                var color = (i == index) ? HollowfenPalette.Gold : new Color(0f, 0f, 0f, 0.10f);
                var edges = _cells[i].Border.GetComponentsInChildren<Image>(true);
                foreach (var img in edges)
                {
                    if (img == _cells[i].Border) continue; // transparent parent
                    img.color = color;
                }
            }

            int count = InventoryRuntime.GetCount(cell.Data);
            _selectedTitle.text = cell.Data.CommonName;
            _selectedLatin.text = cell.Data.LatinName;
            _selectedLatin.gameObject.SetActive(!string.IsNullOrEmpty(cell.Data.LatinName));
            _selectedCount.text = "<size=18>×</size>" + count;

            if (MushroomPreviewer.Instance != null)
            {
                MushroomPreviewer.Instance.Show(cell.Data, false);
                // The preview rig is lazy; Show() must run before its RenderTexture is available.
                if (_previewImage != null)
                    _previewImage.texture = MushroomPreviewer.Instance.RenderTexture;
                _previewMissingNote.gameObject.SetActive(cell.Data.WorldPrefab == null);
            }
        }

        private void AddCell(MushroomFieldGuideData data, int count)
        {
            var root = new GameObject("Cell_" + data.Id, typeof(RectTransform));
            root.transform.SetParent(_gridContent, false);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.04f);

            // Border (selection state)
            var border = UICanvasUtil.NewImage("Border", root.transform, new Color(0f, 0f, 0f, 0.10f), false);
            UICanvasUtil.Stretch((RectTransform)border.transform);
            // Replace flat with a 4-edge frame
            var bImg = border.GetComponent<Image>();
            bImg.color = new Color(0f, 0f, 0f, 0f); // we paint via 4 edge children
            BuildBorderEdges((RectTransform)border.transform, new Color(0f, 0f, 0f, 0.10f));

            // Photo
            var photoGO = new GameObject("Photo", typeof(RectTransform));
            photoGO.transform.SetParent(root.transform, false);
            var photo = photoGO.AddComponent<Image>();
            photo.preserveAspect = true;
            photo.raycastTarget = false;
            photo.sprite = data.Photo;
            photo.color = data.Photo != null ? Color.white : new Color(0f, 0f, 0f, 0.12f);
            var phRT = (RectTransform)photoGO.transform;
            phRT.anchorMin = new Vector2(0f, 1f);
            phRT.anchorMax = new Vector2(1f, 1f);
            phRT.pivot = new Vector2(0.5f, 1f);
            phRT.sizeDelta = new Vector2(-16f, 160f);
            phRT.anchoredPosition = new Vector2(0f, -8f);

            // Count badge (top-right pill)
            var badgeGO = UICanvasUtil.NewImage("Badge", root.transform, HollowfenPalette.Gold, false);
            var bRT = (RectTransform)badgeGO.transform;
            bRT.anchorMin = new Vector2(1f, 1f);
            bRT.anchorMax = new Vector2(1f, 1f);
            bRT.pivot = new Vector2(1f, 1f);
            bRT.sizeDelta = new Vector2(46f, 24f);
            bRT.anchoredPosition = new Vector2(-8f, -8f);
            var badgeTxt = UICanvasUtil.NewEyebrow("Txt", badgeGO.transform,
                "×" + count, 14f, HollowfenPalette.InkDeep, TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(badgeTxt.rectTransform);

            // Name
            var name = UICanvasUtil.NewBody("Name", root.transform, data.CommonName, 16f,
                HollowfenPalette.InkDeep, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.Center);
            var nRT = name.rectTransform;
            nRT.anchorMin = new Vector2(0f, 0f);
            nRT.anchorMax = new Vector2(1f, 0f);
            nRT.pivot = new Vector2(0.5f, 0f);
            nRT.sizeDelta = new Vector2(-12f, 36f);
            nRT.anchoredPosition = new Vector2(0f, 12f);
            name.enableWordWrapping = true;

            // Selectable button
            var btn = root.AddComponent<Button>();
            btn.targetGraphic = bg;
            var cb = btn.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 1f);
            cb.highlightedColor = new Color(1f, 0.95f, 0.85f, 1f);
            cb.selectedColor = new Color(1f, 0.96f, 0.86f, 1f);
            cb.pressedColor = new Color(0.94f, 0.86f, 0.70f, 1f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            int idx = _cells.Count;
            btn.onClick.AddListener(() => SelectCell(idx));

            _cells.Add(new Cell
            {
                Root = (RectTransform)root.transform,
                Button = btn,
                Photo = photo,
                Border = bImg,
                NameText = name,
                CountBadge = badgeTxt,
                Data = data
            });

            // Repaint border via the children we just made — keep ref to the topmost edge for selection color toggle
            // (we re-color all 4 edges via the bImg field — use a simple proxy: store on bImg.color = state, and individually set children)
            // Simpler: cache the four edges as children and recolor on Select.
            // We'll use a small helper to grab them on demand.
        }

        private static void BuildBorderEdges(RectTransform parent, Color color)
        {
            const float t = 1.5f;
            void Edge(string n, Vector2 amin, Vector2 amax, Vector2 piv, Vector2 size, Vector2 pos)
            {
                var go = UICanvasUtil.NewImage(n, parent, color, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = piv;
                rt.sizeDelta = size; rt.anchoredPosition = pos;
            }
            Edge("Top",    new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0, t), Vector2.zero);
            Edge("Bottom", new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,0), new Vector2(0, t), Vector2.zero);
            Edge("Left",   new Vector2(0,0), new Vector2(0,1), new Vector2(0,0.5f), new Vector2(t, 0), Vector2.zero);
            Edge("Right",  new Vector2(1,0), new Vector2(1,1), new Vector2(1,0.5f), new Vector2(t, 0), Vector2.zero);
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            // Scrim
            var scrim = UICanvasUtil.NewImage("Scrim", transform, new Color(0f, 0f, 0f, 0.78f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            // Panel — same parchment journal as InspectScreen
            const float panelW = 1500f;
            const float panelH = 940f;

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.raycastTarget = true;
            // Rounded journal page (batch-47 square sweep — matches InspectScreen).
            panelImg.color = HollowfenPalette.Parchment;
            UICanvasUtil.Roundify(panelImg, 24);

            var panelRT = (RectTransform)panel.transform;
            panelRT.anchorMin = new Vector2(0.5f, 0.5f); panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(panelW, panelH);
            panelRT.anchoredPosition = Vector2.zero;

            // Vignette — inset past the rounded corner so its dark edge doesn't overhang the curve.
            var vignette = UICanvasUtil.NewImage("Vignette", panel.transform, new Color(0f, 0f, 0f, 0.18f), false);
            UICanvasUtil.Stretch((RectTransform)vignette.transform);
            ((RectTransform)vignette.transform).offsetMin = new Vector2(10f, 10f);
            ((RectTransform)vignette.transform).offsetMax = new Vector2(-10f, -10f);
            vignette.GetComponent<Image>().sprite = UICanvasUtil.MakeVerticalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0.00f, new Color(0f, 0f, 0f, 0.32f)),
                new UICanvasUtil.GradientStop(0.18f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(0.82f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(1.00f, new Color(0f, 0f, 0f, 0.28f)),
            }, 256);

            // Frame double-rule (same recipe as InspectScreen) — rounded outlines (batch-47).
            MakeInsetOutline(panelRT, "FrameOuter", 14f, HollowfenPalette.GoldFaint, 1.5f, 16);
            MakeInsetOutline(panelRT, "FrameInner", 22f, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.22f), 1f, 12);

            // Top eyebrow
            var topEyebrow = UICanvasUtil.NewEyebrow("TopEyebrow", panel.transform,
                "FIELD JOURNAL  ·  PROVISIONS", 13f, HollowfenPalette.Gold, TMPro.TextAlignmentOptions.Center);
            var teRT = topEyebrow.rectTransform;
            teRT.anchorMin = new Vector2(0f, 1f); teRT.anchorMax = new Vector2(1f, 1f);
            teRT.pivot = new Vector2(0.5f, 1f);
            teRT.sizeDelta = new Vector2(0f, 18f);
            teRT.anchoredPosition = new Vector2(0f, -22f);

            // Title (left)
            var title = UICanvasUtil.NewHeading("Title", panel.transform, "Provisions", 56f,
                HollowfenPalette.InkDeep, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var tRT = title.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(0f, 1f);
            tRT.pivot = new Vector2(0f, 1f);
            tRT.sizeDelta = new Vector2(700f, 70f);
            tRT.anchoredPosition = new Vector2(56f, -56f);

            // Gold underline
            var underline = UICanvasUtil.NewImage("TitleRule", panel.transform, HollowfenPalette.Gold, false);
            var urRT = (RectTransform)underline.transform;
            urRT.anchorMin = new Vector2(0f, 1f); urRT.anchorMax = new Vector2(0f, 1f);
            urRT.pivot = new Vector2(0f, 1f);
            urRT.sizeDelta = new Vector2(120f, 2f);
            urRT.anchoredPosition = new Vector2(56f, -132f);

            // Stateful containers (toggled by Refresh based on inventory size)
            _emptyState = new GameObject("EmptyState", typeof(RectTransform));
            _emptyState.transform.SetParent(panel.transform, false);
            UICanvasUtil.Stretch((RectTransform)_emptyState.transform);
            var emptyTxt = UICanvasUtil.NewBody("EmptyTxt", _emptyState.transform,
                Hollowfen.Localization.Get("inventory.empty"),
                22f, BodyInk, TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(emptyTxt.rectTransform);
            _emptyText = emptyTxt;

            _populatedState = new GameObject("PopulatedState", typeof(RectTransform));
            _populatedState.transform.SetParent(panel.transform, false);
            UICanvasUtil.Stretch((RectTransform)_populatedState.transform);

            // Keepsakes strip — bottom-left, panel-level so it shows even with an empty pouch.
            _keepsakesLabel = UICanvasUtil.NewBody("Keepsakes", panel.transform, "", 17f,
                BodyInk, TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.BottomLeft);
            _keepsakesLabel.richText = true;
            var kRT = _keepsakesLabel.rectTransform;
            kRT.anchorMin = new Vector2(0f, 0f); kRT.anchorMax = new Vector2(0f, 0f);
            kRT.pivot = new Vector2(0f, 0f);
            kRT.sizeDelta = new Vector2(560f, 24f);
            kRT.anchoredPosition = new Vector2(56f, 36f);
            _keepsakesLabel.gameObject.SetActive(false);

            // === LEFT: GRID with ScrollRect ===
            const float gridLeft = 56f;
            const float gridTop = 156f;
            const float gridW = 752f;
            const float gridH = 660f;

            var scrollGO = new GameObject("Scroll", typeof(RectTransform));
            scrollGO.transform.SetParent(_populatedState.transform, false);
            var scrollRT = (RectTransform)scrollGO.transform;
            scrollRT.anchorMin = new Vector2(0f, 1f); scrollRT.anchorMax = new Vector2(0f, 1f);
            scrollRT.pivot = new Vector2(0f, 1f);
            scrollRT.sizeDelta = new Vector2(gridW, gridH);
            scrollRT.anchoredPosition = new Vector2(gridLeft, -gridTop);
            var scrollMask = scrollGO.AddComponent<RectMask2D>();
            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(scrollGO.transform, false);
            _gridContent = (RectTransform)content.transform;
            _gridContent.anchorMin = new Vector2(0f, 1f); _gridContent.anchorMax = new Vector2(1f, 1f);
            _gridContent.pivot = new Vector2(0f, 1f);
            _gridContent.sizeDelta = new Vector2(0f, 0f); // ContentSizeFitter will manage
            _gridContent.anchoredPosition = Vector2.zero;
            scroll.content = _gridContent;
            scroll.viewport = scrollRT;

            _grid = content.AddComponent<GridLayoutGroup>();
            _grid.cellSize = new Vector2(170f, 220f);
            _grid.spacing = new Vector2(14f, 14f);
            _grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            _grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = 4;
            _grid.padding = new RectOffset(2, 2, 2, 2);

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Keeps the focused cell scrolled into the visible viewport as the user navigates rows.
            scrollGO.AddComponent<Hollowfen.UI.ScrollFocusFollower>();

            // === RIGHT: 3D PREVIEW ===
            const float previewSize = 600f;
            const float previewLeft = gridLeft + gridW + 32f;
            var previewFrame = UICanvasUtil.NewImage("PreviewFrame", _populatedState.transform, HollowfenPalette.GoldFaint, false);
            _previewFrameImage = previewFrame.GetComponent<Image>();
            UICanvasUtil.Roundify(_previewFrameImage, 16);
            var pfRT = (RectTransform)previewFrame.transform;
            pfRT.anchorMin = new Vector2(0f, 1f); pfRT.anchorMax = new Vector2(0f, 1f);
            pfRT.pivot = new Vector2(0f, 1f);
            pfRT.sizeDelta = new Vector2(previewSize + 4f, previewSize + 4f);
            pfRT.anchoredPosition = new Vector2(previewLeft - 2f, -gridTop + 2f);

            var previewBg = UICanvasUtil.NewImage("PreviewBg", _populatedState.transform, HollowfenPalette.Parchment, false);
            UICanvasUtil.Roundify(previewBg.GetComponent<Image>(), 14);
            _previewBgRT = (RectTransform)previewBg.transform;
            _previewBgRT.anchorMin = new Vector2(0f, 1f); _previewBgRT.anchorMax = new Vector2(0f, 1f);
            _previewBgRT.pivot = new Vector2(0f, 1f);
            _previewBgRT.sizeDelta = new Vector2(previewSize, previewSize);
            _previewBgRT.anchoredPosition = new Vector2(previewLeft, -gridTop);

            var previewGO = new GameObject("Preview", typeof(RectTransform));
            previewGO.transform.SetParent(previewBg.transform, false);
            _previewImage = previewGO.AddComponent<RawImage>();
            _previewImage.raycastTarget = false;
            UICanvasUtil.Stretch((RectTransform)previewGO.transform);
            if (MushroomPreviewer.Instance != null) _previewImage.texture = MushroomPreviewer.Instance.RenderTexture;

            // Hint at top of preview
            var hintScrim = UICanvasUtil.NewImage("HintScrim", previewBg.transform, new Color(0f, 0f, 0f, 0.28f), false);
            var hsRT = (RectTransform)hintScrim.transform;
            hsRT.anchorMin = new Vector2(0f, 1f); hsRT.anchorMax = new Vector2(1f, 1f);
            hsRT.pivot = new Vector2(0.5f, 1f);
            hsRT.sizeDelta = new Vector2(0f, 34f);
            hsRT.anchoredPosition = Vector2.zero;
            _hintText = UICanvasUtil.NewBody("Hint", hintScrim.transform,
                "",
                14.5f, HollowfenPalette.Cream, TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(_hintText.rectTransform);

            // "MODEL COMING SOON" overlay (centered in preview, hidden when prefab present)
            _previewMissingNote = UICanvasUtil.NewEyebrow("MissingNote", previewBg.transform,
                "MODEL COMING SOON", 14f, HollowfenPalette.Moss, TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(_previewMissingNote.rectTransform);
            _previewMissingNote.gameObject.SetActive(false);

            // Selected mushroom info under preview
            const float infoTopY = -gridTop - previewSize - 18f;
            _selectedTitle = UICanvasUtil.NewHeading("SelectedTitle", _populatedState.transform, "", 32f,
                HollowfenPalette.InkDeep, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var stRT = _selectedTitle.rectTransform;
            stRT.anchorMin = new Vector2(0f, 1f); stRT.anchorMax = new Vector2(0f, 1f);
            stRT.pivot = new Vector2(0f, 1f);
            stRT.sizeDelta = new Vector2(previewSize - 80f, 38f);
            stRT.anchoredPosition = new Vector2(previewLeft, infoTopY);

            _selectedCount = UICanvasUtil.NewEyebrow("SelectedCount", _populatedState.transform, "", 24f,
                HollowfenPalette.Gold, TMPro.TextAlignmentOptions.TopRight);
            var scRT = _selectedCount.rectTransform;
            scRT.anchorMin = new Vector2(0f, 1f); scRT.anchorMax = new Vector2(0f, 1f);
            scRT.pivot = new Vector2(1f, 1f);
            scRT.sizeDelta = new Vector2(80f, 38f);
            scRT.anchoredPosition = new Vector2(previewLeft + previewSize, infoTopY);
            _selectedCount.richText = true;

            _selectedLatin = UICanvasUtil.NewBody("SelectedLatin", _populatedState.transform, "", 16f,
                HollowfenPalette.Moss, TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.TopLeft);
            var slRT = _selectedLatin.rectTransform;
            slRT.anchorMin = new Vector2(0f, 1f); slRT.anchorMax = new Vector2(0f, 1f);
            slRT.pivot = new Vector2(0f, 1f);
            slRT.sizeDelta = new Vector2(previewSize, 24f);
            slRT.anchoredPosition = new Vector2(previewLeft, infoTopY - 38f);

            // === BOTTOM: CLOSE BUTTON ===
            var btnRow = UICanvasUtil.NewRect("Buttons", panel.transform);
            btnRow.anchorMin = new Vector2(0.5f, 0f); btnRow.anchorMax = new Vector2(0.5f, 0f);
            btnRow.pivot = new Vector2(0.5f, 0f);
            btnRow.sizeDelta = new Vector2(360f, 78f);
            btnRow.anchoredPosition = new Vector2(0f, 36f);

            _closeBtn = MakeCloseButton(btnRow, Hollowfen.Localization.Get("inventory.btn.close"), out _closeGlyph);
            _closeBtn.onClick.AddListener(Close);
        }

        // Rounded inset outline (batch-47) — replaces the old 4-strip square frame.
        private static void MakeInsetOutline(RectTransform parent, string name, float inset, Color color, float thickness, int radius)
        {
            var go = UICanvasUtil.NewImage(name, parent, color, false);
            UICanvasUtil.RoundifyOutline(go.GetComponent<Image>(), radius, thickness);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static Button MakeCloseButton(RectTransform parent, string label, out TMP_Text glyphOut)
        {
            var bgGO = new GameObject("CloseBtn", typeof(RectTransform));
            bgGO.transform.SetParent(parent, false);
            var bgRT = (RectTransform)bgGO.transform;
            bgRT.anchorMin = new Vector2(0.5f, 0.5f); bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.pivot = new Vector2(0.5f, 0.5f);
            bgRT.sizeDelta = new Vector2(316f, 64f);
            bgRT.anchoredPosition = Vector2.zero;
            var img = bgGO.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.15f);
            UICanvasUtil.Roundify(img, 14);

            // Outline — rounded hairline (batch-47).
            var outline = UICanvasUtil.NewImage("Outline", bgRT, HollowfenPalette.GoldFaint, false);
            UICanvasUtil.RoundifyOutline(outline.GetComponent<Image>(), 14, 1.5f);
            UICanvasUtil.Stretch((RectTransform)outline.transform);

            var btn = bgGO.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = img.color;
            cb.highlightedColor = new Color(1f, 1f, 1f, 0.30f);
            cb.selectedColor = new Color(1f, 1f, 1f, 0.35f);
            cb.pressedColor = new Color(1f, 1f, 1f, 0.10f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0.05f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            // Glyph pill
            var glyphGO = UICanvasUtil.NewImage("Glyph", bgRT, new Color(0f, 0f, 0f, 0.10f), false);
            UICanvasUtil.Roundify(glyphGO.GetComponent<Image>(), 10);
            var gRT = (RectTransform)glyphGO.transform;
            gRT.anchorMin = new Vector2(0f, 0.5f); gRT.anchorMax = new Vector2(0f, 0.5f);
            gRT.pivot = new Vector2(0f, 0.5f);
            gRT.sizeDelta = new Vector2(36f, 36f);
            gRT.anchoredPosition = new Vector2(14f, 0f);
            glyphOut = UICanvasUtil.NewBody("GlyphTxt", glyphGO.transform, "?", 22f,
                HollowfenPalette.InkDeep, TMPro.FontStyles.Bold, TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(glyphOut.rectTransform);
            glyphOut.raycastTarget = false;

            // Label
            var lbl = UICanvasUtil.NewEyebrow("Label", bgRT, label, 18f, HollowfenPalette.InkDeep, TMPro.TextAlignmentOptions.Center);
            var lRT = lbl.rectTransform;
            lRT.anchorMin = new Vector2(0f, 0f); lRT.anchorMax = new Vector2(1f, 1f);
            lRT.pivot = new Vector2(0.5f, 0.5f);
            lRT.offsetMin = new Vector2(60f, 0f);
            lRT.offsetMax = new Vector2(-16f, 0f);
            lbl.raycastTarget = false;

            return btn;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                new GameObject("EventSystem", typeof(EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }
            var module = EventSystem.current != null
                ? EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>()
                : null;
            if (module != null) module.deselectOnBackgroundClick = false;
        }
    }
}
