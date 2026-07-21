using Hollowfen.Cinematics;
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
    // Old hand-authored children are torn down on first build. The shipping scene uses the shared
    // baked overhead map; MapCamera still supplies projection math for pins, pan, and zoom.
    public class MapScreen : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private MapCamera _mapCamera;
        [SerializeField] private GameObject _miniMapRoot;
        [SerializeField] private bool _freezeTimeWhileOpen = true;

        [Header("Map art")]
        [SerializeField, Tooltip("Optional parchment texture washed into the side info card.")]
        private Sprite _parchmentSprite;

        [Header("Layout (1920×1080 reference)")]
        [SerializeField, Tooltip("Top chrome bar height. 64 + 56 bottom leaves a 1920×960 map — exactly the RT's 2:1 aspect.")]
        private float _topBarHeight = 64f;
        [SerializeField] private float _bottomBarHeight = 56f;
        [SerializeField] private Vector2 _sideCardSize = new Vector2(380f, 640f);

        [Header("Pan / Zoom")]
        [SerializeField, Tooltip("World units per second of pan at zoom-regional (ortho 150). Pan speed scales with current ortho so close zoom feels equally fast.")]
        private float _panSpeed = 90f;
        [SerializeField] private float _mouseDragPanScale = 1.2f;
        [SerializeField, Tooltip("Deadzone for left-stick pan input.")]
        private float _stickDeadzone = 0.18f;

        private bool _isOpen;
        private bool _built;
        private NarrativePresentationSession.Lease _presentationLease;
        private RawImage _mapImage;
        private Texture _mapTexture;
        private TMP_Text _zoomLabel;
        private TMP_Text _regionLabel;
        private RectTransform _mapZoneRT;
        private Vector2 _lastMousePos;
        private bool _mouseDragging;
        private MiniMapCamera _miniMapCamera;

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
            _miniMapCamera = FindAnyObjectByType<MiniMapCamera>(FindObjectsInactive.Include);
            // If the canvas was saved inactive, Awake only fires during Open()'s SetActive(true) —
            // unconditionally deactivating here would instantly close the map it's opening.
            if (!_isOpen) SetActiveSilent(false);
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            // Allocate/enable the full-map camera only when the map is actually requested. This
            // keeps its large RT and top-down render out of the gameplay scene activation frame.
            if (_mapCamera != null) _mapCamera.SetRenderingActive(true);
            if (_miniMapCamera != null) _miniMapCamera.SetRenderingActive(false);
            BuildIfNeeded();
            _isOpen = true;
            SetActiveSilent(true);
            if (_miniMapRoot != null) _miniMapRoot.SetActive(false);
            var policy = (_freezeTimeWhileOpen
                    ? NarrativePresentationSession.Modal
                    : NarrativePresentationSession.InteractiveNoPause)
                .With(NarrativePresentationSession.Claim.HideGameplayHud);
            _presentationLease = NarrativePresentationSession.Acquire(this, policy);
            // Snap camera to player and regional zoom whenever map opens — predictable starting frame.
            if (_mapCamera != null)
            {
                _mapCamera.CenterOnPlayer();
                _mapCamera.SetTargetOrthoSize(_mapCamera.ZoomRegional);
            }
            RefreshMapTextureFrame();
            RefreshZoomLabel();
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

            // Prefer the nearest DISCOVERED marker that's inside the visible frame, then any
            // visible marker, then anything at all — focusing an off-frame pin is disorienting.
            var cam = _mapCamera != null ? _mapCamera.GetComponent<Camera>() : null;
            LocationMarker bestDiscovered = null, bestVisible = null, bestAny = null;
            float dDiscovered = float.PositiveInfinity, dVisible = float.PositiveInfinity, dAny = float.PositiveInfinity;
            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i]; if (m == null) continue;
                float d = (m.WorldPosition - pp).sqrMagnitude;
                if (d < dAny) { dAny = d; bestAny = m; }
                bool visible = true;
                if (cam != null)
                {
                    var vp = cam.WorldToViewportPoint(m.WorldPosition);
                    visible = vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                }
                if (!visible) continue;
                if (d < dVisible) { dVisible = d; bestVisible = m; }
                if (LocationRegistry.IsDiscovered(m.Id) && d < dDiscovered) { dDiscovered = d; bestDiscovered = m; }
            }
            SetFocus(bestDiscovered ?? bestVisible ?? bestAny);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            SetActiveSilent(false);
            if (_miniMapRoot != null) _miniMapRoot.SetActive(true);
            // Return camera to follow-player so the mini-map / next-open are correct.
            if (_mapCamera != null)
            {
                _mapCamera.CenterOnPlayer();
                _mapCamera.SetRenderingActive(false);
            }
            if (_miniMapCamera != null) _miniMapCamera.SetRenderingActive(true);
            _mouseDragging = false;
            SetFocus(null);
            ReleasePresentation();
        }

        private void OnDestroy()
        {
            if (_miniMapCamera != null) _miniMapCamera.SetRenderingActive(true);
            ReleasePresentation();
        }

        private void ReleasePresentation()
        {
            _presentationLease?.Dispose();
            _presentationLease = null;
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
            RefreshMapTextureFrame();
            RefreshSidePanel();
        }

        private void RefreshMapTextureFrame()
        {
            if (_mapImage == null || _mapCamera == null || !_mapCamera.UsesBakedMap) return;
            if (_mapImage.texture != _mapCamera.MapTexture)
                _mapImage.texture = _mapCamera.MapTexture;
            _mapImage.uvRect = _mapCamera.CurrentUvRect;
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
            _zoomLabel.text = Hollowfen.Localization.Get(_mapCamera.IsZoomedClose
                ? "map.zoom.village" : "map.zoom.regional");
            if (_regionLabel != null)
            {
                string region = LocalizeRegion(LocationRegistry.CurrentRegion);
                _regionLabel.text = region == "—"
                    ? Hollowfen.Localization.Get("map.region.default")
                    : region.ToUpperInvariant();
            }
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
            // The card stays opaque-readable either way; unfocused just dims slightly.
            if (_sidePanelCG != null) _sidePanelCG.alpha = hasFocus ? 1f : 0.92f;

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
                // Disabled reads as a quiet ink ghost on the parchment card, not washed-out gold.
                _sideWaypointBtnBg.color = enabled
                    ? HollowfenPalette.Gold
                    : new Color(HollowfenPalette.InkDeep.r, HollowfenPalette.InkDeep.g, HollowfenPalette.InkDeep.b, 0.10f);
            }
            if (_sideWaypointBtnLabel != null)
                _sideWaypointBtnLabel.color = enabled ? HollowfenPalette.InkDeep : HollowfenPalette.PaperMutedInk;
        }

        private string ComputeDistanceLabel(Vector3 worldPos)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO == null) return "—";
            Vector3 pp = playerGO.transform.position;
            Vector3 d = worldPos - pp;
            float horiz = Mathf.Sqrt(d.x * d.x + d.z * d.z);
            return string.Format(Hollowfen.Localization.Get("map.distance.format"),
                horiz, CardinalDirection(d.x, d.z));
        }

        private static string CardinalDirection(float dx, float dz)
        {
            // Atan2 with (dx, dz): 0° = north (+z), 90° = east (+x), counterclockwise as usual.
            float bearing = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            if (bearing < 0f) bearing += 360f;
            int idx = (int)Mathf.Floor(((bearing + 22.5f) % 360f) / 45f);
            switch (idx)
            {
                case 0: return Hollowfen.Localization.Get("map.cardinal.n");
                case 1: return Hollowfen.Localization.Get("map.cardinal.ne");
                case 2: return Hollowfen.Localization.Get("map.cardinal.e");
                case 3: return Hollowfen.Localization.Get("map.cardinal.se");
                case 4: return Hollowfen.Localization.Get("map.cardinal.s");
                case 5: return Hollowfen.Localization.Get("map.cardinal.sw");
                case 6: return Hollowfen.Localization.Get("map.cardinal.w");
                default: return Hollowfen.Localization.Get("map.cardinal.nw");
            }
        }

        private static string LocalizeRegion(string regionId)
        {
            return RegionCatalog.DisplayName(regionId);
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

            // Pull the static overhead image when available. The runtime RT remains a safe fallback
            // for development scenes that have not been baked yet.
            if (_mapCamera != null)
            {
                _mapTexture = _mapCamera.MapTexture;
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

            // 1) Full-bleed map zone. With a 64px top bar and 56px bottom bar the zone is
            // 1920×960 — exactly the RT's 2:1 aspect, so the world renders pixel-true.
            var mapZone = new GameObject("MapZone", typeof(RectTransform));
            mapZone.transform.SetParent(canvasRT, false);
            var mapZoneRT = (RectTransform)mapZone.transform;
            _mapZoneRT = mapZoneRT;
            mapZoneRT.anchorMin = new Vector2(0f, 0f);
            mapZoneRT.anchorMax = new Vector2(1f, 1f);
            mapZoneRT.pivot = new Vector2(0.5f, 0.5f);
            mapZoneRT.offsetMin = new Vector2(0f, _bottomBarHeight);
            mapZoneRT.offsetMax = new Vector2(0f, -_topBarHeight);

            var mapGO = new GameObject("MapImage", typeof(RectTransform));
            mapGO.transform.SetParent(mapZoneRT, false);
            _mapImage = mapGO.AddComponent<RawImage>();
            _mapImage.raycastTarget = false;
            _mapImage.texture = _mapTexture;
            UICanvasUtil.Stretch((RectTransform)mapGO.transform);

            // Soft edge vignette inside the map so the chrome bars feel seated on it.
            var vigV = UICanvasUtil.NewImage("Vignette.V", mapZoneRT, Color.white, false);
            UICanvasUtil.Stretch((RectTransform)vigV.transform);
            vigV.GetComponent<Image>().sprite = UICanvasUtil.MakeVerticalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0.00f, new Color(0f, 0f, 0f, 0.30f)),
                new UICanvasUtil.GradientStop(0.10f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(0.90f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(1.00f, new Color(0f, 0f, 0f, 0.30f)),
            }, 256);
            var vigH = UICanvasUtil.NewImage("Vignette.H", mapZoneRT, Color.white, false);
            UICanvasUtil.Stretch((RectTransform)vigH.transform);
            vigH.GetComponent<Image>().sprite = UICanvasUtil.MakeHorizontalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0.00f, new Color(0f, 0f, 0f, 0.24f)),
                new UICanvasUtil.GradientStop(0.07f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(0.93f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(1.00f, new Color(0f, 0f, 0f, 0.24f)),
            }, 256);

            // 2) POI overlay + current-location marker. POIs pool into their own container created
            // BEFORE the player marker, so "YOU ARE HERE" always draws above pins/labels.
            var poiRoot = new GameObject("POIRoot", typeof(RectTransform));
            poiRoot.transform.SetParent(mapGO.transform, false);
            UICanvasUtil.Stretch((RectTransform)poiRoot.transform);
            _markerOverlay = mapGO.AddComponent<LocationMarkerOverlay>();
            var mapCam = _mapCamera != null ? _mapCamera.GetComponent<Camera>() : null;
            _markerOverlay.Configure(mapCam, (RectTransform)poiRoot.transform, 18f, showLabels: true, includeUndiscovered: true);

            BuildCurrentLocationMarker((RectTransform)mapGO.transform, mapCam);

            BuildNorthMarker(mapZoneRT);

            // 3) Top chrome bar — ink glass, serif title left, region + zoom chips right.
            var topBar = UICanvasUtil.NewImage("TopBar", canvasRT, new Color(0.07f, 0.06f, 0.045f, 0.94f), true);
            var tbRT = (RectTransform)topBar.transform;
            tbRT.anchorMin = new Vector2(0f, 1f); tbRT.anchorMax = new Vector2(1f, 1f);
            tbRT.pivot = new Vector2(0.5f, 1f);
            tbRT.sizeDelta = new Vector2(0f, _topBarHeight);
            tbRT.anchoredPosition = Vector2.zero;
            BuildBarHairline(tbRT, bottomEdge: true);

            // Fixed offsets — TMP preferred-size queries are unreliable during the same frame the
            // text is created, so don't measure here.
            var title = UICanvasUtil.NewHeading("Title", tbRT, Hollowfen.Localization.Get("map.title"), 32f,
                HollowfenPalette.Parchment, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.Left);
            var tRT = title.rectTransform;
            tRT.anchorMin = new Vector2(0f, 0.5f); tRT.anchorMax = new Vector2(0f, 0.5f);
            tRT.pivot = new Vector2(0f, 0.5f);
            tRT.sizeDelta = new Vector2(230f, 44f);
            tRT.anchoredPosition = new Vector2(28f, -2f);
            title.raycastTarget = false;

            var sep = UICanvasUtil.NewImage("TitleSep", tbRT, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.5f), false);
            var sepRT = (RectTransform)sep.transform;
            sepRT.anchorMin = new Vector2(0f, 0.5f); sepRT.anchorMax = new Vector2(0f, 0.5f);
            sepRT.pivot = new Vector2(0f, 0.5f);
            sepRT.sizeDelta = new Vector2(1.4f, 26f);
            sepRT.anchoredPosition = new Vector2(268f, 0f);

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", tbRT, Hollowfen.Localization.Get("map.eyebrow"), 18f,
                HollowfenPalette.Gold, TMPro.TextAlignmentOptions.Left);
            var eyRT = eyebrow.rectTransform;
            eyRT.anchorMin = new Vector2(0f, 0.5f); eyRT.anchorMax = new Vector2(0f, 0.5f);
            eyRT.pivot = new Vector2(0f, 0.5f);
            eyRT.sizeDelta = new Vector2(420f, 16f);
            eyRT.anchoredPosition = new Vector2(288f, 0f);
            eyebrow.raycastTarget = false;

            _regionLabel = BuildTopChip(tbRT, "RegionChip", Hollowfen.Localization.Get("map.region.default"), HollowfenPalette.Cream, -176f, 210f, false);
            _zoomLabel = BuildTopChip(tbRT, "ZoomChip", Hollowfen.Localization.Get("map.zoom.regional"), HollowfenPalette.GoldGlow, -28f, 132f, true);

            // 4) Bottom control bar — keycap pills + labels, readable at Steam Deck distance.
            var botBar = UICanvasUtil.NewImage("BottomBar", canvasRT, new Color(0.07f, 0.06f, 0.045f, 0.94f), true);
            var bbRT = (RectTransform)botBar.transform;
            bbRT.anchorMin = new Vector2(0f, 0f); bbRT.anchorMax = new Vector2(1f, 0f);
            bbRT.pivot = new Vector2(0.5f, 0f);
            bbRT.sizeDelta = new Vector2(0f, _bottomBarHeight);
            bbRT.anchoredPosition = Vector2.zero;
            BuildBarHairline(bbRT, bottomEdge: false);
            BuildControlHints(bbRT);

            // 5) Side info card — opaque parchment, rounded, shadowed. Sits over the map's right edge.
            BuildSidePanel(mapZoneRT);
            RefreshSidePanel();
        }

        private static void BuildCurrentLocationMarker(RectTransform mapContainer, Camera mapCam)
        {
            // Root tracks Wren's projected position but never rotates, keeping its plate and label
            // readable. The child DirectionGlyph alone follows her heading.
            var markerGO = new GameObject("CurrentLocationMarker", typeof(RectTransform), typeof(CanvasGroup));
            markerGO.transform.SetParent(mapContainer, false);
            var markerRT = (RectTransform)markerGO.transform;
            markerRT.anchorMin = new Vector2(0.5f, 0.5f);
            markerRT.anchorMax = new Vector2(0.5f, 0.5f);
            markerRT.pivot = new Vector2(0.5f, 0.5f);
            markerRT.sizeDelta = new Vector2(38f, 38f);
            markerRT.anchoredPosition = Vector2.zero;

            var pulse = new GameObject("Pulse", typeof(RectTransform)).AddComponent<Image>();
            pulse.transform.SetParent(markerRT, false);
            pulse.sprite = UICanvasUtil.Ring(64, 4f);
            pulse.color = new Color(HollowfenPalette.Sage.r, HollowfenPalette.Sage.g, HollowfenPalette.Sage.b, 0.78f);
            pulse.raycastTarget = false;
            var pulseRT = pulse.rectTransform;
            pulseRT.anchorMin = Vector2.zero;
            pulseRT.anchorMax = Vector2.one;
            pulseRT.offsetMin = new Vector2(-10f, -10f);
            pulseRT.offsetMax = new Vector2(10f, 10f);

            var plate = new GameObject("Plate", typeof(RectTransform)).AddComponent<Image>();
            plate.transform.SetParent(markerRT, false);
            plate.sprite = UICanvasUtil.Circle(64);
            plate.color = new Color(HollowfenPalette.InkDeep.r, HollowfenPalette.InkDeep.g, HollowfenPalette.InkDeep.b, 0.96f);
            plate.raycastTarget = false;
            UICanvasUtil.Stretch(plate.rectTransform);

            var plateRing = new GameObject("PlateRing", typeof(RectTransform)).AddComponent<Image>();
            plateRing.transform.SetParent(markerRT, false);
            plateRing.sprite = UICanvasUtil.Ring(64, 3f);
            plateRing.color = new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.92f);
            plateRing.raycastTarget = false;
            UICanvasUtil.Stretch(plateRing.rectTransform);

            var glyphGO = new GameObject("DirectionGlyph", typeof(RectTransform));
            glyphGO.transform.SetParent(markerRT, false);
            var glyphRT = (RectTransform)glyphGO.transform;
            glyphRT.anchorMin = new Vector2(0.5f, 0.5f);
            glyphRT.anchorMax = new Vector2(0.5f, 0.5f);
            glyphRT.pivot = new Vector2(0.5f, 0.5f);
            glyphRT.sizeDelta = new Vector2(17f, 22f);
            glyphRT.anchoredPosition = new Vector2(0f, 1f);

            var arrowShadow = new GameObject("Shadow", typeof(RectTransform)).AddComponent<UITriangle>();
            arrowShadow.transform.SetParent(glyphRT, false);
            arrowShadow.color = new Color(0f, 0f, 0f, 0.82f);
            arrowShadow.raycastTarget = false;
            var shadowRT = (RectTransform)arrowShadow.transform;
            shadowRT.anchorMin = Vector2.zero;
            shadowRT.anchorMax = Vector2.one;
            shadowRT.offsetMin = new Vector2(-2.5f, -2.5f);
            shadowRT.offsetMax = new Vector2(2.5f, 2.5f);

            var arrow = new GameObject("Arrow", typeof(RectTransform)).AddComponent<UITriangle>();
            arrow.transform.SetParent(glyphRT, false);
            arrow.color = HollowfenPalette.Cream;
            arrow.raycastTarget = false;
            UICanvasUtil.Stretch((RectTransform)arrow.transform);

            var chipGO = new GameObject("CurrentLocationChip", typeof(RectTransform));
            chipGO.transform.SetParent(markerRT, false);
            var chipRT = (RectTransform)chipGO.transform;
            chipRT.anchorMin = new Vector2(0.5f, 0f);
            chipRT.anchorMax = new Vector2(0.5f, 0f);
            chipRT.pivot = new Vector2(0.5f, 1f);
            chipRT.sizeDelta = new Vector2(118f, 26f);
            chipRT.anchoredPosition = new Vector2(0f, -9f);
            var chipFill = chipGO.AddComponent<Image>();
            chipFill.sprite = UICanvasUtil.RoundedRect(9);
            chipFill.type = Image.Type.Sliced;
            chipFill.color = new Color(HollowfenPalette.InkDeep.r, HollowfenPalette.InkDeep.g, HollowfenPalette.InkDeep.b, 0.94f);
            chipFill.raycastTarget = false;

            var chipStroke = UICanvasUtil.NewImage("Stroke", chipRT,
                new Color(HollowfenPalette.Sage.r, HollowfenPalette.Sage.g, HollowfenPalette.Sage.b, 0.8f), false);
            var strokeImage = chipStroke.GetComponent<Image>();
            strokeImage.sprite = UICanvasUtil.RoundedOutline(9, 1.3f);
            strokeImage.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)chipStroke.transform);

            var label = UICanvasUtil.NewEyebrow("Label", chipRT,
                Localization.Get("map.current_location"), 18f, HollowfenPalette.Cream,
                TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
            UICanvasUtil.Stretch(label.rectTransform);

            markerGO.AddComponent<PlayerHeadingArrow>().Configure(mapCam, mapContainer, glyphRT, pulseRT);
        }

        // Thin gold hairline along one edge of a chrome bar.
        private static void BuildBarHairline(RectTransform barRT, bool bottomEdge)
        {
            var line = UICanvasUtil.NewImage("Hairline", barRT,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.35f), false);
            var lRT = (RectTransform)line.transform;
            float y = bottomEdge ? 0f : 1f;
            lRT.anchorMin = new Vector2(0f, y); lRT.anchorMax = new Vector2(1f, y);
            lRT.pivot = new Vector2(0.5f, y);
            lRT.sizeDelta = new Vector2(0f, 1.2f);
            lRT.anchoredPosition = Vector2.zero;
        }

        // Small status chip on the right of the top bar. Returns its label for live updates.
        private static TMP_Text BuildTopChip(RectTransform barRT, string name, string text, Color textColor, float xFromRight, float width, bool goldStroke)
        {
            var chip = new GameObject(name, typeof(RectTransform));
            chip.transform.SetParent(barRT, false);
            var cRT = (RectTransform)chip.transform;
            cRT.anchorMin = new Vector2(1f, 0.5f); cRT.anchorMax = new Vector2(1f, 0.5f);
            cRT.pivot = new Vector2(1f, 0.5f);
            cRT.sizeDelta = new Vector2(width, 32f);
            cRT.anchoredPosition = new Vector2(xFromRight, 0f);
            var fill = chip.AddComponent<Image>();
            fill.sprite = UICanvasUtil.RoundedRect(10);
            fill.type = Image.Type.Sliced;
            fill.color = new Color(0f, 0f, 0f, 0.38f);
            fill.raycastTarget = false;
            var stroke = UICanvasUtil.NewImage("Stroke", cRT, goldStroke
                ? new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.55f)
                : new Color(1f, 1f, 1f, 0.14f), false);
            var stImg = stroke.GetComponent<Image>();
            stImg.sprite = UICanvasUtil.RoundedOutline(10, 1.3f);
            stImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)stroke.transform);
            var label = UICanvasUtil.NewEyebrow("Label", cRT, text, 18f, textColor, TMPro.TextAlignmentOptions.Center);
            label.fontStyle = TMPro.FontStyles.Bold;
            label.raycastTarget = false;
            UICanvasUtil.Stretch(label.rectTransform);
            return label;
        }

        // Keycap + action label groups. Sizing is driven by Unity's layout system (TMP reports
        // preferred width through ILayoutElement once layout runs) — manual measuring at build
        // time bakes in garbage because TMP preferred sizes are unreliable on the creation frame.
        private void BuildControlHints(RectTransform barRT)
        {
            string[][] hints = new string[][]
            {
                new[] { Hollowfen.Localization.Get("map.hint.pan.keys"), Hollowfen.Localization.Get("map.hint.pan") },
                new[] { Hollowfen.Localization.Get("map.hint.select.keys"), Hollowfen.Localization.Get("map.hint.select") },
                new[] { Hollowfen.Localization.Get("map.hint.waypoint.keys"), Hollowfen.Localization.Get("map.hint.waypoint") },
                new[] { Hollowfen.Localization.Get("map.hint.zoom.keys"), Hollowfen.Localization.Get("map.hint.zoom") },
                new[] { Hollowfen.Localization.Get("map.hint.recenter.keys"), Hollowfen.Localization.Get("map.hint.recenter") },
                new[] { Hollowfen.Localization.Get("map.hint.close.keys"), Hollowfen.Localization.Get("map.hint.close") },
            };

            var row = new GameObject("HintRow", typeof(RectTransform));
            row.transform.SetParent(barRT, false);
            var rowRT = (RectTransform)row.transform;
            UICanvasUtil.Stretch(rowRT);
            var rowLG = row.AddComponent<HorizontalLayoutGroup>();
            rowLG.childAlignment = TextAnchor.MiddleCenter;
            rowLG.spacing = 34f;
            rowLG.childControlWidth = true;
            rowLG.childControlHeight = false;
            rowLG.childForceExpandWidth = false;
            rowLG.childForceExpandHeight = false;

            for (int i = 0; i < hints.Length; i++)
            {
                var group = new GameObject("Hint_" + hints[i][1], typeof(RectTransform));
                group.transform.SetParent(rowRT, false);
                var gLG = group.AddComponent<HorizontalLayoutGroup>();
                gLG.childAlignment = TextAnchor.MiddleLeft;
                gLG.spacing = 8f;
                gLG.childControlWidth = true;
                gLG.childControlHeight = false;
                gLG.childForceExpandWidth = false;
                gLG.childForceExpandHeight = false;
                group.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                ((RectTransform)group.transform).sizeDelta = new Vector2(100f, 26f);

                var key = new GameObject("Key", typeof(RectTransform));
                key.transform.SetParent(group.transform, false);
                var kStroke = key.AddComponent<Image>();
                kStroke.sprite = UICanvasUtil.RoundedOutline(8, 1.2f);
                kStroke.type = Image.Type.Sliced;
                kStroke.color = new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.5f);
                kStroke.raycastTarget = false;
                var kLG = key.AddComponent<HorizontalLayoutGroup>();
                kLG.padding = new RectOffset(9, 9, 4, 4);
                kLG.childAlignment = TextAnchor.MiddleCenter;
                kLG.childControlWidth = true;
                kLG.childControlHeight = true;
                kLG.childForceExpandWidth = false;
                kLG.childForceExpandHeight = false;
                key.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                ((RectTransform)key.transform).sizeDelta = new Vector2(60f, 26f);
                var kTxt = UICanvasUtil.NewEyebrow("Txt", key.transform, hints[i][0], 18f, HollowfenPalette.Gold, TMPro.TextAlignmentOptions.Center);
                kTxt.fontStyle = TMPro.FontStyles.Bold;
                kTxt.textWrappingMode = TextWrappingModes.NoWrap;
                kTxt.raycastTarget = false;

                var lbl = UICanvasUtil.NewBody("Lbl", group.transform, hints[i][1], 18f,
                    HollowfenPalette.Cream, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.Left);
                lbl.textWrappingMode = TextWrappingModes.NoWrap;
                lbl.raycastTarget = false;
            }
        }

        private void BuildSidePanel(RectTransform mapZoneRT)
        {
            // Opaque parchment card with rounded corners + drop shadow — the focused-POI reader.
            var panel = new GameObject("SideCard", typeof(RectTransform));
            panel.transform.SetParent(mapZoneRT, false);
            var spRT = (RectTransform)panel.transform;
            spRT.anchorMin = new Vector2(1f, 0.5f);
            spRT.anchorMax = new Vector2(1f, 0.5f);
            spRT.pivot = new Vector2(1f, 0.5f);
            spRT.sizeDelta = _sideCardSize;
            spRT.anchoredPosition = new Vector2(-28f, 0f);

            _sidePanelCG = panel.AddComponent<CanvasGroup>();
            _sidePanelCG.alpha = 1f;
            _sidePanelCG.interactable = true;
            _sidePanelCG.blocksRaycasts = true;

            UICanvasUtil.AddShadow(spRT, 18, 30, 0.5f, -6f);
            var cardImg = UICanvasUtil.MakeRoundedPanel(spRT, HollowfenPalette.Parchment, 18, 0.45f);
            cardImg.raycastTarget = true;
            if (_parchmentSprite != null)
            {
                var wash = UICanvasUtil.NewImage("ParchmentWash", spRT, new Color(1f, 1f, 1f, 0.45f), false);
                var washImg = wash.GetComponent<Image>();
                washImg.sprite = _parchmentSprite;
                washImg.type = Image.Type.Simple;
                UICanvasUtil.Stretch((RectTransform)wash.transform);
                wash.transform.SetSiblingIndex(1);
            }

            float padX = 28f;
            float topY = -30f;
            Color ink = HollowfenPalette.InkDeep;
            Color inkSoft = new Color(0.27f, 0.22f, 0.15f, 1f);

            _sideEyebrow = UICanvasUtil.NewEyebrow("Eyebrow", spRT,
                Hollowfen.Localization.Get("map.eyebrow.landmark"), 18f, HollowfenPalette.PaperAccentInk, TMPro.TextAlignmentOptions.TopLeft);
            var eRT = _sideEyebrow.rectTransform;
            eRT.anchorMin = new Vector2(0f, 1f); eRT.anchorMax = new Vector2(1f, 1f);
            eRT.pivot = new Vector2(0.5f, 1f);
            eRT.sizeDelta = new Vector2(-padX * 2f, 14f);
            eRT.anchoredPosition = new Vector2(0f, topY);
            _sideEyebrow.fontStyle = TMPro.FontStyles.Bold;
            _sideEyebrow.raycastTarget = false;

            _sideTitle = UICanvasUtil.NewHeading("Title", spRT, Hollowfen.Localization.Get("map.title"), 30f,
                ink, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var tRT = _sideTitle.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0.5f, 1f);
            tRT.sizeDelta = new Vector2(-padX * 2f, 76f);
            tRT.anchoredPosition = new Vector2(0f, topY - 20f);
            _sideTitle.textWrappingMode = TextWrappingModes.Normal;
            _sideTitle.raycastTarget = false;

            var rule = UICanvasUtil.NewImage("Rule", spRT, HollowfenPalette.Gold, false);
            var rRT = (RectTransform)rule.transform;
            rRT.anchorMin = new Vector2(0f, 1f); rRT.anchorMax = new Vector2(0f, 1f);
            rRT.pivot = new Vector2(0f, 1f);
            rRT.sizeDelta = new Vector2(72f, 2f);
            rRT.anchoredPosition = new Vector2(padX, topY - 98f);

            _sideBody = UICanvasUtil.NewBody("Body", spRT, "", 20f,
                inkSoft, TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.TopLeft);
            var bRT = _sideBody.rectTransform;
            bRT.anchorMin = new Vector2(0f, 1f); bRT.anchorMax = new Vector2(1f, 1f);
            bRT.pivot = new Vector2(0.5f, 1f);
            bRT.sizeDelta = new Vector2(-padX * 2f, 200f);
            bRT.anchoredPosition = new Vector2(0f, topY - 116f);
            _sideBody.textWrappingMode = TextWrappingModes.Normal;
            _sideBody.lineSpacing = 4f;
            _sideBody.raycastTarget = false;

            var sep = UICanvasUtil.NewImage("Sep", spRT, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.3f), false);
            var sepRT = (RectTransform)sep.transform;
            sepRT.anchorMin = new Vector2(0f, 1f); sepRT.anchorMax = new Vector2(1f, 1f);
            sepRT.pivot = new Vector2(0.5f, 1f);
            sepRT.sizeDelta = new Vector2(-padX * 2f, 1f);
            sepRT.anchoredPosition = new Vector2(0f, topY - 330f);

            // Stat rows: DISTANCE and REGION side by side.
            _sideDistanceLabel = BuildStatLabel(spRT, "DistLabel", Hollowfen.Localization.Get("map.label.distance"), padX, topY - 350f);
            _sideDistanceValue = BuildStatValue(spRT, "DistVal", padX, topY - 366f, ink);
            BuildStatLabel(spRT, "RegLabel", Hollowfen.Localization.Get("map.label.region"), padX + 170f, topY - 350f);
            _sideRegionValue = BuildStatValue(spRT, "RegVal", padX + 170f, topY - 366f, ink);

            // Waypoint button — solid gold rounded, clickable, Enter/A also triggers it.
            var btnGO = new GameObject("WaypointBtn", typeof(RectTransform));
            btnGO.transform.SetParent(spRT, false);
            _sideWaypointBtnBg = btnGO.AddComponent<Image>();
            _sideWaypointBtnBg.sprite = UICanvasUtil.RoundedRect(12);
            _sideWaypointBtnBg.type = Image.Type.Sliced;
            _sideWaypointBtnBg.color = HollowfenPalette.Gold;
            _sideWaypointBtnBg.raycastTarget = true;
            var btnRT = (RectTransform)btnGO.transform;
            btnRT.anchorMin = new Vector2(0.5f, 0f); btnRT.anchorMax = new Vector2(0.5f, 0f);
            btnRT.pivot = new Vector2(0.5f, 0f);
            btnRT.sizeDelta = new Vector2(_sideCardSize.x - padX * 2f, 48f);
            btnRT.anchoredPosition = new Vector2(0f, 26f);
            var btn = btnGO.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(ToggleWaypointOnFocused);

            _sideWaypointBtnLabel = UICanvasUtil.NewEyebrow("Label", btnRT,
                Hollowfen.Localization.Get("map.btn.waypoint"), 18f,
                HollowfenPalette.InkDeep, TMPro.TextAlignmentOptions.Center);
            _sideWaypointBtnLabel.fontStyle = TMPro.FontStyles.Bold;
            _sideWaypointBtnLabel.raycastTarget = false;
            UICanvasUtil.Stretch(_sideWaypointBtnLabel.rectTransform);
        }

        private static TMP_Text BuildStatLabel(RectTransform parent, string name, string text, float x, float y)
        {
            var t = UICanvasUtil.NewEyebrow(name, parent, text, 18f, HollowfenPalette.PaperMutedInk, TMPro.TextAlignmentOptions.TopLeft);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(160f, 14f);
            rt.anchoredPosition = new Vector2(x, y);
            t.fontStyle = TMPro.FontStyles.Bold;
            t.raycastTarget = false;
            return t;
        }

        private static TMP_Text BuildStatValue(RectTransform parent, string name, float x, float y, Color color)
        {
            var t = UICanvasUtil.NewBody(name, parent, "—", 18f, color, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(160f, 24f);
            rt.anchoredPosition = new Vector2(x, y);
            t.raycastTarget = false;
            return t;
        }

        private static void BuildNorthMarker(RectTransform mapZoneRT)
        {
            // Compact "N" chip with an up-notch, top-center inside the map. The map always renders
            // +Z up, so one cue is enough.
            var pill = new GameObject("North", typeof(RectTransform));
            pill.transform.SetParent(mapZoneRT, false);
            var pRT = (RectTransform)pill.transform;
            pRT.anchorMin = new Vector2(0.5f, 1f);
            pRT.anchorMax = new Vector2(0.5f, 1f);
            pRT.pivot = new Vector2(0.5f, 1f);
            pRT.sizeDelta = new Vector2(40f, 32f);
            pRT.anchoredPosition = new Vector2(0f, -14f);
            var fill = pill.AddComponent<Image>();
            fill.sprite = UICanvasUtil.RoundedRect(10);
            fill.type = Image.Type.Sliced;
            fill.color = new Color(0.05f, 0.04f, 0.02f, 0.8f);
            fill.raycastTarget = false;

            var notch = new GameObject("Notch", typeof(RectTransform)).AddComponent<UITriangle>();
            notch.transform.SetParent(pRT, false);
            notch.color = HollowfenPalette.Gold;
            notch.raycastTarget = false;
            var nRT = (RectTransform)notch.transform;
            nRT.anchorMin = new Vector2(0.5f, 1f); nRT.anchorMax = new Vector2(0.5f, 1f);
            nRT.pivot = new Vector2(0.5f, 0f);
            nRT.sizeDelta = new Vector2(10f, 6f);
            nRT.anchoredPosition = new Vector2(0f, 1f);

            var txt = UICanvasUtil.NewEyebrow("Txt", pRT, Hollowfen.Localization.Get("map.cardinal.n"), 18f,
                HollowfenPalette.GoldGlow, TMPro.TextAlignmentOptions.Center);
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
