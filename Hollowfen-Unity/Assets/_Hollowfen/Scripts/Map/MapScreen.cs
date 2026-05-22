using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.Map
{
    // Full-screen map screen. Builds its UI programmatically on first Open() to match the parchment
    // journal aesthetic used by InspectScreen / InventoryScreen — header band with serif title, gold
    // double-rule frame, vignette, and 8 cardinal compass markers in gold pills around the map area.
    //
    // Old hand-authored children are torn down on first build; the existing MapImage's RenderTexture
    // (assigned to MapCamera.targetTexture) is captured and re-applied to the new RawImage.
    public class MapScreen : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private MapCamera _mapCamera;
        [SerializeField] private GameObject _miniMapRoot;
        [SerializeField] private bool _freezeTimeWhileOpen = true;

        [Header("Map art")]
        [SerializeField, Tooltip("Optional parchment background sprite. Falls back to flat HollowfenPalette.Parchment.")]
        private Sprite _parchmentSprite;

        [Header("Layout")]
        [SerializeField] private Vector2 _panelSize = new Vector2(1820f, 1020f);
        [SerializeField, Tooltip("Map zone (RawImage) size — match the MapCamera RT aspect for non-stretched rendering. 2:1 ratio matches the default 2048×1024 RT.")]
        private Vector2 _mapZoneSize = new Vector2(1640f, 820f);
        [SerializeField] private float _headerHeight = 150f;
        [SerializeField] private float _hintHeight = 50f;

        [Header("Pan / Zoom")]
        [SerializeField, Tooltip("World units per second of pan at zoom-regional (ortho 150). Pan speed scales with current ortho so close zoom feels equally fast.")]
        private float _panSpeed = 90f;
        [SerializeField] private float _mouseDragPanScale = 1.2f;
        [SerializeField, Tooltip("Deadzone for left-stick pan input.")]
        private float _stickDeadzone = 0.18f;

        private bool _isOpen;
        private bool _built;
        private float _previousTimeScale = 1f;
        private RawImage _mapImage;
        private Texture _mapTexture;
        private TMP_Text _zoomLabel;
        private TMP_Text _hintText;
        private RectTransform _mapZoneRT;
        private Vector2 _lastMousePos;
        private bool _mouseDragging;

        // Phase 2: side panel + focus
        private LocationMarkerOverlay _markerOverlay;
        private LocationMarker _focusedMarker;
        private CanvasGroup _sidePanelCG;
        private TMP_Text _sideEyebrow;
        private TMP_Text _sideTitle;
        private TMP_Text _sideBody;
        private TMP_Text _sideDistanceLabel;
        private TMP_Text _sideDistanceValue;
        private TMP_Text _sideRegionValue;
        private TMP_Text _sideWaypointBtnLabel;
        private Image _sideWaypointBtnBg;
        private float _selectionRepeatTimer;

        public bool IsOpen => _isOpen;
        public LocationMarker FocusedMarker => _focusedMarker;

        private void Awake()
        {
            if (_root == null) _root = gameObject;
            SetActiveSilent(false);
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            BuildIfNeeded();
            _isOpen = true;
            SetActiveSilent(true);
            if (_miniMapRoot != null) _miniMapRoot.SetActive(false);
            // Snap camera to player and regional zoom whenever map opens — predictable starting frame.
            if (_mapCamera != null)
            {
                _mapCamera.CenterOnPlayer();
                _mapCamera.SetTargetOrthoSize(_mapCamera.ZoomRegional);
            }
            RefreshZoomLabel();
            if (_freezeTimeWhileOpen)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            AutoFocusClosestMarker();
            RefreshSidePanel();
        }

        // Picks the nearest marker to the player (preferring discovered) and focuses it so the
        // "Set Waypoint" action has a target the moment the map opens — no manual selection
        // step required for keyboard / gamepad users.
        private void AutoFocusClosestMarker()
        {
            var markers = LocationRegistry.Markers;
            if (markers == null || markers.Count == 0) return;
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO == null) return;
            Vector3 pp = playerGO.transform.position;

            LocationMarker bestDiscovered = null, bestAny = null;
            float dDiscovered = float.PositiveInfinity, dAny = float.PositiveInfinity;
            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i]; if (m == null) continue;
                float d = (m.WorldPosition - pp).sqrMagnitude;
                if (d < dAny) { dAny = d; bestAny = m; }
                if (LocationRegistry.IsDiscovered(m.Id) && d < dDiscovered) { dDiscovered = d; bestDiscovered = m; }
            }
            SetFocus(bestDiscovered ?? bestAny);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            SetActiveSilent(false);
            if (_miniMapRoot != null) _miniMapRoot.SetActive(true);
            // Return camera to follow-player so the mini-map / next-open are correct.
            if (_mapCamera != null) _mapCamera.CenterOnPlayer();
            _mouseDragging = false;
            SetFocus(null);
            if (_freezeTimeWhileOpen)
                Time.timeScale = _previousTimeScale;
        }

        private void Update()
        {
            if (!_isOpen || _mapCamera == null) return;

            float dt = Time.unscaledDeltaTime;

            // ---- Pan: left-stick + WASD (no arrows — arrows do POI selection) + middle-mouse-drag ----
            Vector2 stick = ReadStick();
            Vector2 keyboard = ReadKeyboardPan();
            Vector2 panInput = stick + keyboard;
            if (panInput.sqrMagnitude > 1f) panInput.Normalize();

            if (panInput.sqrMagnitude > 0.0001f)
            {
                float orthoScale = _mapCamera.CurrentOrthoSize / _mapCamera.ZoomRegional;
                Vector2 worldDelta = panInput * (_panSpeed * orthoScale * dt);
                _mapCamera.Pan(worldDelta);
            }

            HandleMouseDragPan();

            // ---- POI selection: D-pad / arrow keys / mouse hover/click ----
            Vector2 selDir;
            if (ReadSelectionPressed(out selDir))
                CycleFocusInDirection(selDir);
            HandleMousePointing();

            // ---- Waypoint set/clear: A / Cross / Space / Enter ----
            if (ReadWaypointPressed())
                ToggleWaypointOnFocused();

            // ---- Zoom toggle ----
            if (ReadZoomTogglePressed())
                _mapCamera.ToggleZoomPreset();

            // ---- Recenter ----
            if (ReadRecenterPressed())
            {
                _mapCamera.CenterOnPlayer();
                RefreshZoomLabel();
            }

            RefreshZoomLabel();
            RefreshSidePanel();
        }

        // -------- Input helpers (unscaled, polling Input System devices directly) --------

        private Vector2 ReadStick()
        {
            var pad = Gamepad.current;
            if (pad == null) return Vector2.zero;
            var v = pad.leftStick.ReadValue();
            return v.sqrMagnitude < _stickDeadzone * _stickDeadzone ? Vector2.zero : v;
        }

        private Vector2 ReadKeyboardPan()
        {
            // WASD pans the camera. Arrow keys are reserved for POI selection.
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            Vector2 v = Vector2.zero;
            if (kb.wKey.isPressed) v.y += 1f;
            if (kb.sKey.isPressed) v.y -= 1f;
            if (kb.dKey.isPressed) v.x += 1f;
            if (kb.aKey.isPressed) v.x -= 1f;
            return v;
        }

        private bool ReadSelectionPressed(out Vector2 dir)
        {
            dir = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame)    dir = Vector2.up;
                else if (kb.downArrowKey.wasPressedThisFrame)  dir = Vector2.down;
                else if (kb.leftArrowKey.wasPressedThisFrame)  dir = Vector2.left;
                else if (kb.rightArrowKey.wasPressedThisFrame) dir = Vector2.right;
            }
            if (dir == Vector2.zero)
            {
                var pad = Gamepad.current;
                if (pad != null)
                {
                    if (pad.dpad.up.wasPressedThisFrame)    dir = Vector2.up;
                    else if (pad.dpad.down.wasPressedThisFrame)  dir = Vector2.down;
                    else if (pad.dpad.left.wasPressedThisFrame)  dir = Vector2.left;
                    else if (pad.dpad.right.wasPressedThisFrame) dir = Vector2.right;
                }
            }
            return dir != Vector2.zero;
        }

        private void HandleMousePointing()
        {
            var mouse = Mouse.current;
            if (mouse == null || _markerOverlay == null) return;
            Vector2 pos = mouse.position.ReadValue();

            // Only hover-select when the cursor is inside the map zone.
            if (_mapZoneRT == null ||
                !RectTransformUtility.RectangleContainsScreenPoint(_mapZoneRT, pos, null))
            {
                return;
            }

            var hit = _markerOverlay.HitTestScreenPoint(pos);
            if (hit != null && hit != _focusedMarker)
                SetFocus(hit);

            // Left-click: keep the focused POI (clicking a POI is the same as hovering for now;
            // Phase 3 will repurpose click as "set waypoint" or use the side-panel button).
            if (mouse.leftButton.wasPressedThisFrame && hit != null)
                SetFocus(hit);
        }

        private void HandleMouseDragPan()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();
            if (mouse.middleButton.wasPressedThisFrame)
            {
                _mouseDragging = true;
                _lastMousePos = pos;
                return;
            }
            if (mouse.middleButton.wasReleasedThisFrame)
            {
                _mouseDragging = false;
            }
            if (_mouseDragging && mouse.middleButton.isPressed)
            {
                Vector2 deltaPx = pos - _lastMousePos;
                _lastMousePos = pos;
                if (_mapZoneRT != null && _mapZoneRT.rect.width > 1f)
                {
                    // Convert pixel delta to world delta: each pixel = (ortho * 2) / rect.width meters.
                    float worldPerPx = (_mapCamera.CurrentOrthoSize * 2f) / _mapZoneRT.rect.width;
                    // Drag direction is inverted (drag right = world moves left under cursor)
                    _mapCamera.Pan(-deltaPx * worldPerPx * _mouseDragPanScale);
                }
            }
        }

        private bool ReadZoomTogglePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.tabKey.wasPressedThisFrame || kb.equalsKey.wasPressedThisFrame || kb.minusKey.wasPressedThisFrame))
                return true;
            var pad = Gamepad.current;
            if (pad != null && (pad.rightShoulder.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame))
                return true;
            var mouse = Mouse.current;
            if (mouse != null && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f)
                return true;
            return false;
        }

        private bool ReadRecenterPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.homeKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame))
                return true;
            var pad = Gamepad.current;
            if (pad != null && pad.rightStickButton.wasPressedThisFrame) return true;
            return false;
        }

        private bool ReadWaypointPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                return true;
            var pad = Gamepad.current;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame) return true;
            return false;
        }

        private void ToggleWaypointOnFocused()
        {
            if (_focusedMarker == null || _focusedMarker.Data == null) return;
            // Only allow setting a waypoint on a discovered location. Clearing is always allowed.
            bool discovered = LocationRegistry.IsDiscovered(_focusedMarker.Id);
            if (!discovered && LocationRegistry.ActiveWaypoint != _focusedMarker) return;
            LocationRegistry.ToggleWaypoint(_focusedMarker);
        }

        private void RefreshZoomLabel()
        {
            if (_zoomLabel == null || _mapCamera == null) return;
            _zoomLabel.text = _mapCamera.IsZoomedClose ? "VILLAGE" : "REGIONAL";
        }

        // ---------- Phase 2: focus + side panel ----------

        private void SetFocus(LocationMarker m)
        {
            if (m == _focusedMarker) return;
            _focusedMarker = m;
            if (_markerOverlay != null)
            {
                if (m != null) _markerOverlay.SetFocusedId(m.Id);
                else _markerOverlay.ClearFocus();
            }
        }

        // Snap focus to the marker that's farthest along `dir` from the currently focused marker
        // (or from screen center if nothing is focused), filtered to those whose angular bearing
        // from the origin is within ~60° of the pressed direction.
        private void CycleFocusInDirection(Vector2 dir)
        {
            var markers = LocationRegistry.Markers;
            if (markers == null || markers.Count == 0 || _mapCamera == null) return;
            var cam = _mapCamera.GetComponent<Camera>();
            if (cam == null) return;

            // Origin in viewport space: focused marker's projection, or screen center if none.
            Vector2 origin = new Vector2(0.5f, 0.5f);
            if (_focusedMarker != null)
            {
                var vp = cam.WorldToViewportPoint(_focusedMarker.WorldPosition);
                origin = new Vector2(vp.x, vp.y);
            }

            dir = dir.normalized;
            LocationMarker best = null;
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i];
                if (m == null || m == _focusedMarker) continue;
                var vp = cam.WorldToViewportPoint(m.WorldPosition);
                Vector2 candidate = new Vector2(vp.x, vp.y);
                Vector2 delta = candidate - origin;
                if (delta.sqrMagnitude < 1e-6f) continue;
                Vector2 deltaN = delta.normalized;
                float dot = Vector2.Dot(deltaN, dir);
                if (dot < 0.35f) continue; // too far off the pressed direction
                // Score: prefer aligned candidates that are also CLOSE (so we step neighbor-by-neighbor).
                float perpDist = Mathf.Abs(delta.x * dir.y - delta.y * dir.x);
                float forwardDist = Mathf.Max(0.0001f, Vector2.Dot(delta, dir));
                float score = forwardDist + perpDist * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = m;
                }
            }

            if (best != null) SetFocus(best);
            else if (_focusedMarker == null)
            {
                // No directional match from center — focus the closest marker as a starting point.
                LocationMarker closest = null;
                float closestDist = float.PositiveInfinity;
                for (int i = 0; i < markers.Count; i++)
                {
                    var m = markers[i]; if (m == null) continue;
                    var vp = cam.WorldToViewportPoint(m.WorldPosition);
                    float d = ((Vector2)vp - origin).sqrMagnitude;
                    if (d < closestDist) { closestDist = d; closest = m; }
                }
                if (closest != null) SetFocus(closest);
            }
        }

        private void RefreshSidePanel()
        {
            if (_sideTitle == null) return;

            bool hasFocus = _focusedMarker != null && _focusedMarker.Data != null;
            float targetAlpha = hasFocus ? 1f : 0.55f;
            if (_sidePanelCG != null) _sidePanelCG.alpha = targetAlpha;

            if (!hasFocus)
            {
                _sideEyebrow.text = ""; // placeholder, hide the eyebrow line
                _sideTitle.text = Hollowfen.Localization.Get("map.placeholder.title");
                _sideBody.text = Hollowfen.Localization.Get("map.placeholder.body");
                _sideDistanceValue.text = "—";
                _sideRegionValue.text = string.IsNullOrEmpty(LocationRegistry.CurrentRegion) ? "—" : LocalizeRegion(LocationRegistry.CurrentRegion);
                SetWaypointButton(false, Hollowfen.Localization.Get("map.btn.waypoint_disabled"));
                return;
            }

            var data = _focusedMarker.Data;
            bool discovered = LocationRegistry.IsDiscovered(_focusedMarker.Id);

            _sideEyebrow.text = discovered
                ? Hollowfen.Localization.Get("map.eyebrow.landmark")
                : Hollowfen.Localization.Get("map.eyebrow.unknown");

            if (discovered)
            {
                _sideTitle.text = Hollowfen.Localization.Get(data.DisplayNameId);
                _sideBody.text = Hollowfen.Localization.Get(data.ShortDescriptionId);
            }
            else
            {
                _sideTitle.text = Hollowfen.Localization.Get("map.unknown.title");
                _sideBody.text = Hollowfen.Localization.Get("map.unknown.body");
            }

            _sideDistanceValue.text = ComputeDistanceLabel(_focusedMarker.WorldPosition);
            _sideRegionValue.text = LocalizeRegion(data.RegionId);

            bool isCurrentWaypoint = LocationRegistry.ActiveWaypoint == _focusedMarker;
            string label;
            if (isCurrentWaypoint) label = Hollowfen.Localization.Get("map.btn.waypoint_clear");
            else if (!discovered)  label = Hollowfen.Localization.Get("map.btn.waypoint_disabled");
            else                   label = Hollowfen.Localization.Get("map.btn.waypoint");

            SetWaypointButton(discovered || isCurrentWaypoint, label);
        }

        private void SetWaypointButton(bool enabled, string label)
        {
            if (_sideWaypointBtnLabel != null) _sideWaypointBtnLabel.text = label;
            if (_sideWaypointBtnBg != null)
            {
                _sideWaypointBtnBg.color = enabled
                    ? HollowfenPalette.Gold
                    : new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.25f);
            }
            if (_sideWaypointBtnLabel != null)
                _sideWaypointBtnLabel.color = enabled ? HollowfenPalette.InkDeep : HollowfenPalette.Moss;
        }

        private string ComputeDistanceLabel(Vector3 worldPos)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO == null) return "—";
            Vector3 pp = playerGO.transform.position;
            Vector3 d = worldPos - pp;
            float horiz = Mathf.Sqrt(d.x * d.x + d.z * d.z);
            return string.Format("{0:0}m {1}", horiz, CardinalDirection(d.x, d.z));
        }

        private static string CardinalDirection(float dx, float dz)
        {
            // Atan2 with (dx, dz): 0° = north (+z), 90° = east (+x), counterclockwise as usual.
            float bearing = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            if (bearing < 0f) bearing += 360f;
            int idx = (int)Mathf.Floor(((bearing + 22.5f) % 360f) / 45f);
            switch (idx)
            {
                case 0: return "N";
                case 1: return "NE";
                case 2: return "E";
                case 3: return "SE";
                case 4: return "S";
                case 5: return "SW";
                case 6: return "W";
                default: return "NW";
            }
        }

        private static string LocalizeRegion(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return "—";
            switch (regionId)
            {
                case "village":  return "Hollowfen Village";
                case "old_wood": return "The Old Wood";
                case "wend":     return "The Wend";
                default:         return regionId;
            }
        }

        private void SetActiveSilent(bool active)
        {
            if (_root != null && _root.activeSelf != active)
                _root.SetActive(active);
        }

        // ----------------- UI BUILDER -----------------

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            // Pull the runtime RT from MapCamera (created at the configured landscape aspect).
            // Falls back to the project RT asset if MapCamera's runtime RT isn't ready yet.
            if (_mapCamera != null)
            {
                _mapTexture = _mapCamera.RenderTexture;
                if (_mapTexture == null)
                {
                    var camComp = _mapCamera.GetComponent<Camera>();
                    if (camComp != null) _mapTexture = camComp.targetTexture;
                }
            }
            if (_mapTexture == null)
            {
                var oldRaw = _root.GetComponentInChildren<RawImage>(true);
                if (oldRaw != null) _mapTexture = oldRaw.texture;
            }

            // Tear down all children of the canvas. The MapScreen component lives on the canvas itself
            // so we don't touch _root.
            for (int i = _root.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_root.transform.GetChild(i).gameObject);
            }

            BuildUI();
        }

        private void BuildUI()
        {
            var canvasRT = (RectTransform)_root.transform;

            // 1) Full-screen scrim — heavier so the unused margins around the page feel deliberate
            // (the page sits on a dark surface) rather than reading as accidental black bars.
            var scrim = UICanvasUtil.NewImage("Scrim", canvasRT, new Color(0.04f, 0.03f, 0.02f, 0.96f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            // 2) Center panel with parchment background
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasRT, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.raycastTarget = true;
            if (_parchmentSprite != null)
            {
                panelImg.sprite = _parchmentSprite;
                panelImg.color = Color.white;
                panelImg.preserveAspect = false;
                panelImg.type = Image.Type.Simple;
            }
            else
            {
                panelImg.color = HollowfenPalette.Parchment;
            }
            var panelRT = (RectTransform)panel.transform;
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = _panelSize;
            panelRT.anchoredPosition = Vector2.zero;

            // 3) Vignette overlay (top + bottom darker)
            var vignette = UICanvasUtil.NewImage("Vignette", panelRT, Color.white, false);
            UICanvasUtil.Stretch((RectTransform)vignette.transform);
            vignette.GetComponent<Image>().sprite = UICanvasUtil.MakeVerticalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0.00f, new Color(0f, 0f, 0f, 0.32f)),
                new UICanvasUtil.GradientStop(0.18f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(0.82f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(1.00f, new Color(0f, 0f, 0f, 0.28f)),
            }, 256);

            // 4) Double-rule frame
            BuildFrame(panelRT, 14f, HollowfenPalette.GoldFaint, 1.5f);
            BuildFrame(panelRT, 22f, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.22f), 1f);

            // 5) Header band: eyebrow + title + gold rule, anchored to panel TOP so we never overlap the map.
            float panelHalfH = _panelSize.y * 0.5f;

            var topEyebrow = UICanvasUtil.NewEyebrow("TopEyebrow", panelRT,
                "FIELD JOURNAL  ·  CARTOGRAPHY", 13f, HollowfenPalette.Gold, TMPro.TextAlignmentOptions.Center);
            var teRT = topEyebrow.rectTransform;
            teRT.anchorMin = new Vector2(0f, 1f); teRT.anchorMax = new Vector2(1f, 1f);
            teRT.pivot = new Vector2(0.5f, 1f);
            teRT.sizeDelta = new Vector2(0f, 18f);
            teRT.anchoredPosition = new Vector2(0f, -28f);

            var title = UICanvasUtil.NewHeading("Title", panelRT, "Hollowfen", 56f,
                HollowfenPalette.InkDeep, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.Center);
            var tRT = title.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0.5f, 1f);
            tRT.sizeDelta = new Vector2(0f, 64f);
            tRT.anchoredPosition = new Vector2(0f, -50f);

            var underline = UICanvasUtil.NewImage("TitleRule", panelRT, HollowfenPalette.Gold, false);
            var urRT = (RectTransform)underline.transform;
            urRT.anchorMin = new Vector2(0.5f, 1f); urRT.anchorMax = new Vector2(0.5f, 1f);
            urRT.pivot = new Vector2(0.5f, 1f);
            urRT.sizeDelta = new Vector2(140f, 2f);
            urRT.anchoredPosition = new Vector2(0f, -120f);

            // 6) Map zone — landscape RawImage centered horizontally, offset down so it clears the
            // header band above and leaves room for the hint band below.
            float mapYOffset = -((_headerHeight - _hintHeight) * 0.5f);
            var mapZone = new GameObject("MapZone", typeof(RectTransform));
            mapZone.transform.SetParent(panelRT, false);
            var mapZoneRT = (RectTransform)mapZone.transform;
            _mapZoneRT = mapZoneRT;
            mapZoneRT.anchorMin = new Vector2(0.5f, 0.5f);
            mapZoneRT.anchorMax = new Vector2(0.5f, 0.5f);
            mapZoneRT.pivot = new Vector2(0.5f, 0.5f);
            mapZoneRT.sizeDelta = _mapZoneSize;
            mapZoneRT.anchoredPosition = new Vector2(0f, mapYOffset);

            // Subtle gold inset frame around the map (separates map from parchment)
            var mapFrame = UICanvasUtil.NewImage("MapFrame", mapZoneRT, HollowfenPalette.GoldFaint, false);
            var mfRT = (RectTransform)mapFrame.transform;
            mfRT.anchorMin = new Vector2(0f, 0f); mfRT.anchorMax = new Vector2(1f, 1f);
            mfRT.pivot = new Vector2(0.5f, 0.5f);
            mfRT.offsetMin = new Vector2(-3f, -3f);
            mfRT.offsetMax = new Vector2(3f, 3f);

            // The actual map image
            var mapGO = new GameObject("MapImage", typeof(RectTransform));
            mapGO.transform.SetParent(mapZoneRT, false);
            _mapImage = mapGO.AddComponent<RawImage>();
            _mapImage.raycastTarget = false;
            _mapImage.texture = _mapTexture;
            UICanvasUtil.Stretch((RectTransform)mapGO.transform);

            // POI overlay — gold dots + italic labels for every registered LocationMarker.
            _markerOverlay = mapGO.AddComponent<LocationMarkerOverlay>();
            var mapCam = _mapCamera != null ? _mapCamera.GetComponent<Camera>() : null;
            _markerOverlay.Configure(mapCam, (RectTransform)mapGO.transform, 16f, showLabels: true, includeUndiscovered: true);

            // Player heading triangle — tracks Wren's projected position on the map AND rotates
            // with her facing direction. Parented to MapImage so it sits above the rendered terrain
            // but below any future overlay UI.
            var arrowGO = new GameObject("HeadingArrow", typeof(RectTransform));
            arrowGO.transform.SetParent(mapGO.transform, false);
            var tri = arrowGO.AddComponent<UITriangle>();
            tri.color = HollowfenPalette.GoldGlow;
            tri.raycastTarget = false;
            var heading = arrowGO.AddComponent<PlayerHeadingArrow>();
            heading.Configure(mapCam, (RectTransform)mapGO.transform);
            var arRT = (RectTransform)arrowGO.transform;
            arRT.anchorMin = new Vector2(0.5f, 0.5f); arRT.anchorMax = new Vector2(0.5f, 0.5f);
            arRT.pivot = new Vector2(0.5f, 0.5f);
            arRT.sizeDelta = new Vector2(24f, 30f);
            arRT.anchoredPosition = Vector2.zero;

            // 7) North label — single, minimal orientation cue. The map is always rendered
            // with world +Z up (no camera rotation), so a single "N" pill at the top of the
            // map rect is enough. The full perimeter cardinal markers were removed because
            // they collided with the title and added clutter.
            BuildNorthMarker(mapZoneRT);

            // 8) Hint band at panel bottom — pan/zoom/recenter/close controls.
            _hintText = UICanvasUtil.NewBody("Hint", panelRT,
                "Stick · WASD — pan     Arrows · D-pad — select     A · Enter — waypoint     R1 · Tab — zoom     R3 · Home — recenter     M · B · Esc — close",
                11f,
                HollowfenPalette.Moss, TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.Center);
            var hintRT = _hintText.rectTransform;
            hintRT.anchorMin = new Vector2(0f, 0f); hintRT.anchorMax = new Vector2(1f, 0f);
            hintRT.pivot = new Vector2(0.5f, 0f);
            hintRT.sizeDelta = new Vector2(0f, 22f);
            hintRT.anchoredPosition = new Vector2(0f, 38f);

            // 9) Zoom indicator — small pill in the upper-right of the map zone showing current preset.
            var zoomPill = UICanvasUtil.NewImage("ZoomPill", mapZoneRT,
                new Color(0.05f, 0.04f, 0.02f, 0.7f), false);
            var zpRT = (RectTransform)zoomPill.transform;
            zpRT.anchorMin = new Vector2(1f, 1f); zpRT.anchorMax = new Vector2(1f, 1f);
            zpRT.pivot = new Vector2(1f, 1f);
            zpRT.sizeDelta = new Vector2(120f, 28f);
            zpRT.anchoredPosition = new Vector2(-12f, -12f);

            _zoomLabel = UICanvasUtil.NewEyebrow("ZoomLabel", zpRT, "REGIONAL", 12f,
                HollowfenPalette.GoldGlow, TMPro.TextAlignmentOptions.Center);
            _zoomLabel.fontStyle = TMPro.FontStyles.Bold;
            _zoomLabel.raycastTarget = false;
            UICanvasUtil.Stretch(_zoomLabel.rectTransform);

            // 10) Side panel — focused POI info, overlays the right ~320px of the map.
            BuildSidePanel(mapZoneRT);
            RefreshSidePanel();
        }

        private void BuildSidePanel(RectTransform mapZoneRT)
        {
            // Container
            var panel = new GameObject("SidePanel", typeof(RectTransform));
            panel.transform.SetParent(mapZoneRT, false);
            var spRT = (RectTransform)panel.transform;
            spRT.anchorMin = new Vector2(1f, 0.5f);
            spRT.anchorMax = new Vector2(1f, 0.5f);
            spRT.pivot = new Vector2(1f, 0.5f);
            spRT.sizeDelta = new Vector2(340f, 720f);
            spRT.anchoredPosition = new Vector2(-16f, 0f);

            _sidePanelCG = panel.AddComponent<CanvasGroup>();
            _sidePanelCG.alpha = 0.55f;
            _sidePanelCG.interactable = true;
            _sidePanelCG.blocksRaycasts = false;

            // Background: deep ink with subtle gradient
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.04f, 0.02f, 0.86f);
            bg.raycastTarget = false;

            // Thin gold border (4 rules)
            BuildFrame(spRT, 6f, HollowfenPalette.GoldFaint, 1.2f);

            float padX = 22f;
            float topY = -28f;

            // Eyebrow
            _sideEyebrow = UICanvasUtil.NewEyebrow("Eyebrow", spRT,
                "LANDMARK", 11f, HollowfenPalette.Gold, TMPro.TextAlignmentOptions.TopLeft);
            var eRT = _sideEyebrow.rectTransform;
            eRT.anchorMin = new Vector2(0f, 1f); eRT.anchorMax = new Vector2(1f, 1f);
            eRT.pivot = new Vector2(0.5f, 1f);
            eRT.sizeDelta = new Vector2(-padX * 2f, 14f);
            eRT.anchoredPosition = new Vector2(0f, topY);
            _sideEyebrow.fontStyle = TMPro.FontStyles.Bold;

            // Title (serif, large)
            _sideTitle = UICanvasUtil.NewHeading("Title", spRT, "Hollowfen", 28f,
                HollowfenPalette.GoldGlow, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var tRT = _sideTitle.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0.5f, 1f);
            tRT.sizeDelta = new Vector2(-padX * 2f, 72f);
            tRT.anchoredPosition = new Vector2(0f, topY - 22f);
            _sideTitle.textWrappingMode = TextWrappingModes.Normal;

            // Gold underline rule
            var rule = UICanvasUtil.NewImage("Rule", spRT, HollowfenPalette.Gold, false);
            var rRT = (RectTransform)rule.transform;
            rRT.anchorMin = new Vector2(0f, 1f); rRT.anchorMax = new Vector2(0f, 1f);
            rRT.pivot = new Vector2(0f, 1f);
            rRT.sizeDelta = new Vector2(72f, 2f);
            rRT.anchoredPosition = new Vector2(padX, topY - 96f);

            // Body (italic)
            _sideBody = UICanvasUtil.NewBody("Body", spRT,
                "", 14f,
                HollowfenPalette.Parchment, TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.TopLeft);
            var bRT = _sideBody.rectTransform;
            bRT.anchorMin = new Vector2(0f, 1f); bRT.anchorMax = new Vector2(1f, 1f);
            bRT.pivot = new Vector2(0.5f, 1f);
            bRT.sizeDelta = new Vector2(-padX * 2f, 220f);
            bRT.anchoredPosition = new Vector2(0f, topY - 112f);
            _sideBody.textWrappingMode = TextWrappingModes.Normal;

            // Separator
            var sep = UICanvasUtil.NewImage("Sep", spRT, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.18f), false);
            var sepRT = (RectTransform)sep.transform;
            sepRT.anchorMin = new Vector2(0f, 1f); sepRT.anchorMax = new Vector2(1f, 1f);
            sepRT.pivot = new Vector2(0.5f, 1f);
            sepRT.sizeDelta = new Vector2(-padX * 2f, 1f);
            sepRT.anchoredPosition = new Vector2(0f, topY - 342f);

            // Distance row
            _sideDistanceLabel = UICanvasUtil.NewEyebrow("DistLabel", spRT,
                Hollowfen.Localization.Get("map.label.distance"), 10f, HollowfenPalette.Moss, TMPro.TextAlignmentOptions.TopLeft);
            var dlRT = _sideDistanceLabel.rectTransform;
            dlRT.anchorMin = new Vector2(0f, 1f); dlRT.anchorMax = new Vector2(0f, 1f);
            dlRT.pivot = new Vector2(0f, 1f);
            dlRT.sizeDelta = new Vector2(120f, 14f);
            dlRT.anchoredPosition = new Vector2(padX, topY - 358f);

            _sideDistanceValue = UICanvasUtil.NewBody("DistVal", spRT, "—", 15f,
                HollowfenPalette.Cream, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var dvRT = _sideDistanceValue.rectTransform;
            dvRT.anchorMin = new Vector2(0f, 1f); dvRT.anchorMax = new Vector2(0f, 1f);
            dvRT.pivot = new Vector2(0f, 1f);
            dvRT.sizeDelta = new Vector2(220f, 22f);
            dvRT.anchoredPosition = new Vector2(padX, topY - 374f);

            // Region row
            var regionLabel = UICanvasUtil.NewEyebrow("RegLabel", spRT,
                Hollowfen.Localization.Get("map.label.region"), 10f, HollowfenPalette.Moss, TMPro.TextAlignmentOptions.TopLeft);
            var rlRT = regionLabel.rectTransform;
            rlRT.anchorMin = new Vector2(0f, 1f); rlRT.anchorMax = new Vector2(0f, 1f);
            rlRT.pivot = new Vector2(0f, 1f);
            rlRT.sizeDelta = new Vector2(120f, 14f);
            rlRT.anchoredPosition = new Vector2(padX, topY - 410f);

            _sideRegionValue = UICanvasUtil.NewBody("RegVal", spRT, "—", 15f,
                HollowfenPalette.Cream, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var rvRT = _sideRegionValue.rectTransform;
            rvRT.anchorMin = new Vector2(0f, 1f); rvRT.anchorMax = new Vector2(0f, 1f);
            rvRT.pivot = new Vector2(0f, 1f);
            rvRT.sizeDelta = new Vector2(280f, 22f);
            rvRT.anchoredPosition = new Vector2(padX, topY - 426f);

            // Waypoint button (placeholder for Phase 3)
            var btnGO = new GameObject("WaypointBtn", typeof(RectTransform));
            btnGO.transform.SetParent(spRT, false);
            _sideWaypointBtnBg = btnGO.AddComponent<Image>();
            _sideWaypointBtnBg.color = HollowfenPalette.Gold;
            _sideWaypointBtnBg.raycastTarget = false;
            var btnRT = (RectTransform)btnGO.transform;
            btnRT.anchorMin = new Vector2(0.5f, 0f); btnRT.anchorMax = new Vector2(0.5f, 0f);
            btnRT.pivot = new Vector2(0.5f, 0f);
            btnRT.sizeDelta = new Vector2(260f, 44f);
            btnRT.anchoredPosition = new Vector2(0f, 24f);

            _sideWaypointBtnLabel = UICanvasUtil.NewEyebrow("Label", btnRT,
                Hollowfen.Localization.Get("map.btn.waypoint"), 13f,
                HollowfenPalette.InkDeep, TMPro.TextAlignmentOptions.Center);
            _sideWaypointBtnLabel.fontStyle = TMPro.FontStyles.Bold;
            _sideWaypointBtnLabel.raycastTarget = false;
            UICanvasUtil.Stretch(_sideWaypointBtnLabel.rectTransform);
        }

        private static void BuildNorthMarker(RectTransform mapZoneRT)
        {
            // Small gold "N" pill anchored INSIDE the top edge of the map zone (not outside, so it
            // never collides with the title). Sits ~12px below the gold frame.
            var pill = UICanvasUtil.NewImage("North", mapZoneRT, new Color(0.05f, 0.04f, 0.02f, 0.78f), false);
            var pRT = (RectTransform)pill.transform;
            pRT.anchorMin = new Vector2(0.5f, 1f);
            pRT.anchorMax = new Vector2(0.5f, 1f);
            pRT.pivot = new Vector2(0.5f, 1f);
            pRT.sizeDelta = new Vector2(38f, 28f);
            pRT.anchoredPosition = new Vector2(0f, -12f);

            var txt = UICanvasUtil.NewEyebrow("Txt", pRT, "N", 14f,
                HollowfenPalette.GoldGlow, TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(txt.rectTransform);
            txt.fontStyle = TMPro.FontStyles.Bold;
            txt.raycastTarget = false;
        }

        private static void BuildCompassMarker(RectTransform mapZoneRT, string label, Vector2 mapAnchor, Vector2 outwardOffset, bool isCardinal)
        {
            // Pill background + uppercase gold/cream label. Anchored to a point on the map perimeter,
            // pushed outward by `outwardOffset`. Pivot mirrors the anchor so the pill sits cleanly
            // outside the map rect (e.g. anchored at top-center → pivot bottom-center → grows upward).
            var pill = UICanvasUtil.NewImage("Compass_" + label, mapZoneRT, new Color(0f, 0f, 0f, 0.55f), false);
            var pRT = (RectTransform)pill.transform;
            pRT.anchorMin = mapAnchor;
            pRT.anchorMax = mapAnchor;
            pRT.pivot = new Vector2(
                outwardOffset.x > 0.01f ? 0f : (outwardOffset.x < -0.01f ? 1f : 0.5f),
                outwardOffset.y > 0.01f ? 0f : (outwardOffset.y < -0.01f ? 1f : 0.5f));
            pRT.sizeDelta = isCardinal ? new Vector2(36f, 30f) : new Vector2(40f, 30f);
            pRT.anchoredPosition = outwardOffset;

            var txt = UICanvasUtil.NewEyebrow("Txt", pRT, label,
                isCardinal ? 16f : 13f,
                isCardinal ? HollowfenPalette.GoldGlow : HollowfenPalette.Gold,
                TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(txt.rectTransform);
            txt.fontStyle = TMPro.FontStyles.Bold;
            txt.raycastTarget = false;
        }

        private static void BuildFrame(RectTransform panelRT, float inset, Color color, float thickness)
        {
            var top = UICanvasUtil.NewImage("Frame.Top", panelRT, color, false);
            var tr = (RectTransform)top.transform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-inset * 2f, thickness); tr.anchoredPosition = new Vector2(0f, -inset);
            var bot = UICanvasUtil.NewImage("Frame.Bot", panelRT, color, false);
            var br = (RectTransform)bot.transform;
            br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(1f, 0f); br.pivot = new Vector2(0.5f, 0f);
            br.sizeDelta = new Vector2(-inset * 2f, thickness); br.anchoredPosition = new Vector2(0f, inset);
            var left = UICanvasUtil.NewImage("Frame.Left", panelRT, color, false);
            var lr = (RectTransform)left.transform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(0f, 1f); lr.pivot = new Vector2(0f, 0.5f);
            lr.sizeDelta = new Vector2(thickness, -inset * 2f); lr.anchoredPosition = new Vector2(inset, 0f);
            var right = UICanvasUtil.NewImage("Frame.Right", panelRT, color, false);
            var rr = (RectTransform)right.transform;
            rr.anchorMin = new Vector2(1f, 0f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(1f, 0.5f);
            rr.sizeDelta = new Vector2(thickness, -inset * 2f); rr.anchoredPosition = new Vector2(-inset, 0f);
        }
    }
}
