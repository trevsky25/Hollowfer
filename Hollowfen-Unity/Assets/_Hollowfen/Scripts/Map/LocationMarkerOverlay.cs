using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hollowfen.UI;

namespace Hollowfen.Map
{
    // Renders LocationRegistry markers as gold pins with readable label chips over a map RawImage.
    // Place this component on the RawImage GameObject (or on a sibling whose RectTransform fills
    // the same rect as the RawImage). It pools icons lazily and updates positions every LateUpdate
    // by projecting each marker's world position through the supplied camera into the container rect.
    //
    // Pin states: discovered (gold dot + dark ring + label chip), undiscovered (dim dot, "?" chip
    // only while focused), focused (enlarged, glow ring), waypoint (pulsing gold halo).
    public class LocationMarkerOverlay : MonoBehaviour
    {
        [SerializeField] private Camera _mapCamera;
        [SerializeField] private RectTransform _container;
        [SerializeField] private float _iconSize = 18f;
        [SerializeField, Range(0f, 1f)] private float _undiscoveredAlpha = 0.4f;
        [SerializeField] private bool _showLabels;
        [SerializeField] private float _labelFontSize = 18f;
        [SerializeField] private float _labelOffsetY = -8f;
        [SerializeField] private bool _hideOutsideRect = true;
        [SerializeField] private bool _includeUndiscovered = true;
        [SerializeField, Tooltip("Multiplier on icon size for the focused marker.")]
        private float _focusedScale = 1.45f;
        [SerializeField, Tooltip("Hit-test radius in pixels (uses the focused icon size when a marker is focused; otherwise uses _iconSize × this).")]
        private float _hitTestPadding = 8f;

        private readonly List<MarkerIcon> _pool = new List<MarkerIcon>();
        private string _focusedId;

        private class MarkerIcon
        {
            public RectTransform root;
            public Image halo;
            public Image ring;
            public Image dot;
            public RectTransform chip;
            public Image chipFill;
            public TMP_Text label;
            public string lastLabel;
            public LocationMarker marker;
        }

        public string FocusedId => _focusedId;
        public IReadOnlyList<LocationMarker> ActiveMarkers => LocationRegistry.Markers;

        private void Awake()
        {
            if (_container == null) _container = transform as RectTransform;
        }

        private void LateUpdate()
        {
            if (_mapCamera == null || _container == null) return;
            var markers = LocationRegistry.Markers;
            EnsurePool(markers.Count);

            float pulse = 1f + 0.14f * Mathf.Sin(Time.unscaledTime * 3.2f);

            for (int i = 0; i < _pool.Count; i++)
            {
                var icon = _pool[i];
                if (i >= markers.Count) { SetActive(icon.root, false); continue; }

                var m = markers[i];
                if (m == null) { SetActive(icon.root, false); continue; }

                bool discovered = LocationRegistry.IsDiscovered(m.Id);
                if (!discovered && !_includeUndiscovered) { SetActive(icon.root, false); continue; }

                Vector3 vp = _mapCamera.WorldToViewportPoint(m.WorldPosition);
                bool insideRect = vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                if (_hideOutsideRect && !insideRect) { SetActive(icon.root, false); continue; }

                SetActive(icon.root, true);
                Rect r = _container.rect;
                icon.root.anchoredPosition = new Vector2(
                    (vp.x - 0.5f) * r.width,
                    (vp.y - 0.5f) * r.height);
                icon.marker = m;

                bool isFocused = m.Id == _focusedId;
                bool isWaypoint = LocationRegistry.ActiveWaypoint == m;

                Color fill = isFocused ? HollowfenPalette.GoldGlow : HollowfenPalette.Gold;
                if (!discovered) fill.a *= _undiscoveredAlpha;
                icon.dot.color = fill;
                icon.ring.color = new Color(0.04f, 0.03f, 0.02f, isFocused ? 1f : 0.9f);

                float sizePx = isFocused ? _iconSize * _focusedScale : _iconSize;
                icon.root.sizeDelta = new Vector2(sizePx, sizePx);

                if (icon.halo != null)
                {
                    if (isWaypoint != icon.halo.gameObject.activeSelf)
                        icon.halo.gameObject.SetActive(isWaypoint);
                    if (isWaypoint)
                        icon.halo.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
                }

                if (icon.chip != null)
                {
                    // Waypoints always carry their name — a quest pointed Wren there by name.
                    bool showLabel = _showLabels && (discovered || isFocused || isWaypoint);
                    if (showLabel != icon.chip.gameObject.activeSelf)
                        icon.chip.gameObject.SetActive(showLabel);
                    if (showLabel)
                    {
                        string text = (discovered || isWaypoint) ? ResolveLabel(m.Data) : "?";
                        if (text != icon.lastLabel)
                        {
                            icon.lastLabel = text;
                            icon.label.text = text;
                        }
                        // Re-measure every frame — TMP preferred sizes are unreliable on the frame
                        // the text is set, so a one-shot measure can bake in garbage. Self-heals.
                        float w = Mathf.Max(40f, icon.label.preferredWidth + 24f);
                        if (Mathf.Abs(icon.chip.sizeDelta.x - w) > 0.5f)
                            icon.chip.sizeDelta = new Vector2(w, 28f);
                        icon.chipFill.color = isFocused
                            ? new Color(0.05f, 0.04f, 0.02f, 0.96f)
                            : new Color(0.05f, 0.04f, 0.02f, 0.78f);
                        icon.label.color = isFocused ? HollowfenPalette.GoldGlow : HollowfenPalette.Cream;
                    }
                }
            }
        }

        // ----- Focus + hit-test public API -----

        public void SetFocusedId(string id)
        {
            _focusedId = id;
        }

        public void ClearFocus()
        {
            _focusedId = null;
        }

        // Returns the LocationMarker whose icon contains the given screen-space point, or null.
        // screenPos should be in pixel coords matching the canvas Screen Space - Overlay convention
        // (origin bottom-left). Uses pooled icon RectTransforms directly so it always sees current positions.
        public LocationMarker HitTestScreenPoint(Vector2 screenPos)
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                var icon = _pool[i];
                if (icon.root == null || !icon.root.gameObject.activeInHierarchy || icon.marker == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(icon.root, screenPos, null))
                    return icon.marker;
                // Also accept a wider tolerance so the user doesn't have to be pixel-perfect.
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(icon.root, screenPos, null, out local))
                {
                    if (local.sqrMagnitude <= (_iconSize * 0.5f + _hitTestPadding) * (_iconSize * 0.5f + _hitTestPadding))
                        return icon.marker;
                }
            }
            return null;
        }

        private static void SetActive(RectTransform rt, bool active)
        {
            if (rt != null && rt.gameObject.activeSelf != active)
                rt.gameObject.SetActive(active);
        }

        private static string ResolveLabel(LocationData data)
        {
            if (data == null) return "";
            if (!string.IsNullOrEmpty(data.DisplayNameId))
            {
                string t = Hollowfen.Localization.Get(data.DisplayNameId);
                if (!string.IsNullOrEmpty(t) && t != data.DisplayNameId) return t;
            }
            return data.Id ?? "";
        }

        private void EnsurePool(int desired)
        {
            while (_pool.Count < desired) _pool.Add(CreateIcon(_pool.Count));
        }

        private MarkerIcon CreateIcon(int index)
        {
            var root = new GameObject("POI_" + index, typeof(RectTransform));
            root.transform.SetParent(_container, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(_iconSize, _iconSize);

            // Waypoint halo — pulsing gold ring, only visible on the active waypoint.
            var haloGO = new GameObject("WaypointHalo", typeof(RectTransform));
            haloGO.transform.SetParent(rt, false);
            var halo = haloGO.AddComponent<Image>();
            halo.sprite = UICanvasUtil.Ring(64, 5f);
            halo.color = new Color(HollowfenPalette.GoldGlow.r, HollowfenPalette.GoldGlow.g, HollowfenPalette.GoldGlow.b, 0.85f);
            halo.raycastTarget = false;
            var haloRT = (RectTransform)haloGO.transform;
            haloRT.anchorMin = new Vector2(0f, 0f);
            haloRT.anchorMax = new Vector2(1f, 1f);
            haloRT.offsetMin = new Vector2(-9f, -9f);
            haloRT.offsetMax = new Vector2(9f, 9f);
            haloGO.SetActive(false);

            var ringGO = new GameObject("Ring", typeof(RectTransform));
            ringGO.transform.SetParent(rt, false);
            var ring = ringGO.AddComponent<Image>();
            ring.sprite = UICanvasUtil.Circle(64);
            ring.color = new Color(0.04f, 0.03f, 0.02f, 0.9f);
            ring.raycastTarget = false;
            var ringRT = (RectTransform)ringGO.transform;
            ringRT.anchorMin = new Vector2(0f, 0f);
            ringRT.anchorMax = new Vector2(1f, 1f);
            ringRT.offsetMin = new Vector2(-2.5f, -2.5f);
            ringRT.offsetMax = new Vector2(2.5f, 2.5f);

            var dotGO = new GameObject("Dot", typeof(RectTransform));
            dotGO.transform.SetParent(rt, false);
            var dot = dotGO.AddComponent<Image>();
            dot.sprite = UICanvasUtil.Circle(64);
            dot.color = HollowfenPalette.Gold;
            dot.raycastTarget = false;
            var dotRT = (RectTransform)dotGO.transform;
            dotRT.anchorMin = Vector2.zero;
            dotRT.anchorMax = Vector2.one;
            dotRT.offsetMin = Vector2.zero;
            dotRT.offsetMax = Vector2.zero;

            RectTransform chip = null;
            Image chipFill = null;
            TMP_Text label = null;
            if (_showLabels)
            {
                // Label chip — rounded ink pill below the pin so names read on any terrain.
                var chipGO = new GameObject("LabelChip", typeof(RectTransform));
                chipGO.transform.SetParent(rt, false);
                chip = (RectTransform)chipGO.transform;
                chip.anchorMin = new Vector2(0.5f, 0f);
                chip.anchorMax = new Vector2(0.5f, 0f);
                chip.pivot = new Vector2(0.5f, 1f);
                chip.sizeDelta = new Vector2(120f, 28f);
                chip.anchoredPosition = new Vector2(0f, _labelOffsetY);
                chipFill = chipGO.AddComponent<Image>();
                chipFill.sprite = UICanvasUtil.RoundedRect(9);
                chipFill.type = Image.Type.Sliced;
                chipFill.color = new Color(0.05f, 0.04f, 0.02f, 0.78f);
                chipFill.raycastTarget = false;

                var lblGO = new GameObject("Label", typeof(RectTransform));
                lblGO.transform.SetParent(chip, false);
                label = lblGO.AddComponent<TextMeshProUGUI>();
                label.font = UICanvasUtil.BodyFont;
                label.fontSize = _labelFontSize;
                label.color = HollowfenPalette.Cream;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                UICanvasUtil.Stretch((RectTransform)lblGO.transform);
            }

            return new MarkerIcon { root = rt, halo = halo, ring = ring, dot = dot, chip = chip, chipFill = chipFill, label = label };
        }

        public void Configure(Camera cam, RectTransform container, float iconSize, bool showLabels, bool includeUndiscovered)
        {
            _mapCamera = cam;
            _container = container;
            _iconSize = iconSize;
            _showLabels = showLabels;
            _includeUndiscovered = includeUndiscovered;
        }
    }
}
