using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hollowfen.UI;

namespace Hollowfen.Map
{
    // Renders LocationRegistry markers as gold dots (and optional labels) over a map RawImage.
    // Place this component on the RawImage GameObject (or on a sibling whose RectTransform fills
    // the same rect as the RawImage). It pools icons lazily and updates positions every LateUpdate
    // by projecting each marker's world position through the supplied camera into the container rect.
    public class LocationMarkerOverlay : MonoBehaviour
    {
        [SerializeField] private Camera _mapCamera;
        [SerializeField] private RectTransform _container;
        [SerializeField] private float _iconSize = 12f;
        [SerializeField, Range(0f, 1f)] private float _undiscoveredAlpha = 0.35f;
        [SerializeField] private bool _showLabels;
        [SerializeField] private float _labelFontSize = 12f;
        [SerializeField] private float _labelOffsetY = -10f;
        [SerializeField] private float _labelWidth = 140f;
        [SerializeField] private bool _hideOutsideRect = true;
        [SerializeField] private bool _includeUndiscovered = true;
        [SerializeField, Tooltip("Multiplier on icon size for the focused marker.")]
        private float _focusedScale = 1.7f;
        [SerializeField, Tooltip("Hit-test radius in pixels (uses the focused icon size when a marker is focused; otherwise uses _iconSize × this).")]
        private float _hitTestPadding = 6f;

        private readonly List<MarkerIcon> _pool = new List<MarkerIcon>();
        private static Sprite _sharedDotSprite;
        private string _focusedId;

        private struct MarkerIcon
        {
            public RectTransform root;
            public Image halo;
            public Image ring;
            public Image dot;
            public TMP_Text label;
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

            for (int i = 0; i < _pool.Count; i++)
            {
                if (i >= markers.Count) { SetActive(_pool[i].root, false); continue; }

                var m = markers[i];
                if (m == null) { SetActive(_pool[i].root, false); continue; }

                bool discovered = LocationRegistry.IsDiscovered(m.Id);
                if (!discovered && !_includeUndiscovered) { SetActive(_pool[i].root, false); continue; }

                Vector3 vp = _mapCamera.WorldToViewportPoint(m.WorldPosition);
                bool insideRect = vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                if (_hideOutsideRect && !insideRect) { SetActive(_pool[i].root, false); continue; }

                SetActive(_pool[i].root, true);
                Rect r = _container.rect;
                _pool[i].root.anchoredPosition = new Vector2(
                    (vp.x - 0.5f) * r.width,
                    (vp.y - 0.5f) * r.height);

                // Cache the marker on the icon so external code (selection / hit-test) can map back.
                var icon = _pool[i];
                icon.marker = m;
                _pool[i] = icon;

                bool isFocused = m.Id == _focusedId;
                bool isWaypoint = LocationRegistry.ActiveWaypoint == m;

                Color fill = HollowfenPalette.Gold;
                if (!discovered) fill.a *= _undiscoveredAlpha;
                if (isFocused) fill = HollowfenPalette.GoldGlow;

                _pool[i].dot.color = fill;
                _pool[i].ring.color = isFocused
                    ? new Color(0.02f, 0.02f, 0.01f, 1f)
                    : new Color(0.05f, 0.04f, 0.02f, 0.92f);

                float sizePx = isFocused ? _iconSize * _focusedScale : _iconSize;
                _pool[i].root.sizeDelta = new Vector2(sizePx, sizePx);

                if (_pool[i].halo != null)
                {
                    if (isWaypoint != _pool[i].halo.gameObject.activeSelf)
                        _pool[i].halo.gameObject.SetActive(isWaypoint);
                }

                if (_pool[i].label != null)
                {
                    bool showLabel = _showLabels && (discovered || isFocused);
                    _pool[i].label.gameObject.SetActive(showLabel);
                    if (showLabel) _pool[i].label.text = ResolveLabel(m.Data);
                    _pool[i].label.color = isFocused ? HollowfenPalette.InkDeep : HollowfenPalette.InkDeep;
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
            if (_sharedDotSprite == null) _sharedDotSprite = BakeCircleSprite(32);

            var root = new GameObject("POI_" + index, typeof(RectTransform));
            root.transform.SetParent(_container, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(_iconSize, _iconSize);

            // Waypoint halo — only visible when this marker is the active waypoint. Slightly larger
            // outer ring in faint gold so it reads as a target reticle. Sibling of Ring so it
            // renders behind the dot but in front of the rendered map texture.
            var haloGO = new GameObject("WaypointHalo", typeof(RectTransform));
            haloGO.transform.SetParent(rt, false);
            var halo = haloGO.AddComponent<Image>();
            halo.sprite = _sharedDotSprite;
            halo.color = new Color(HollowfenPalette.GoldGlow.r, HollowfenPalette.GoldGlow.g, HollowfenPalette.GoldGlow.b, 0.55f);
            halo.raycastTarget = false;
            var haloRT = (RectTransform)haloGO.transform;
            haloRT.anchorMin = new Vector2(0f, 0f);
            haloRT.anchorMax = new Vector2(1f, 1f);
            haloRT.offsetMin = new Vector2(-8f, -8f);
            haloRT.offsetMax = new Vector2(8f, 8f);
            haloGO.SetActive(false);

            var ringGO = new GameObject("Ring", typeof(RectTransform));
            ringGO.transform.SetParent(rt, false);
            var ring = ringGO.AddComponent<Image>();
            ring.sprite = _sharedDotSprite;
            ring.color = new Color(0.05f, 0.04f, 0.02f, 0.92f);
            ring.raycastTarget = false;
            var ringRT = (RectTransform)ringGO.transform;
            ringRT.anchorMin = new Vector2(0f, 0f);
            ringRT.anchorMax = new Vector2(1f, 1f);
            ringRT.offsetMin = new Vector2(-2f, -2f);
            ringRT.offsetMax = new Vector2(2f, 2f);

            var dotGO = new GameObject("Dot", typeof(RectTransform));
            dotGO.transform.SetParent(rt, false);
            var dot = dotGO.AddComponent<Image>();
            dot.sprite = _sharedDotSprite;
            dot.color = HollowfenPalette.Gold;
            dot.raycastTarget = false;
            var dotRT = (RectTransform)dotGO.transform;
            dotRT.anchorMin = Vector2.zero;
            dotRT.anchorMax = Vector2.one;
            dotRT.offsetMin = Vector2.zero;
            dotRT.offsetMax = Vector2.zero;

            TMP_Text label = null;
            if (_showLabels)
            {
                var lblGO = new GameObject("Label", typeof(RectTransform));
                lblGO.transform.SetParent(rt, false);
                label = lblGO.AddComponent<TextMeshProUGUI>();
                label.fontSize = _labelFontSize;
                label.color = HollowfenPalette.InkDeep;
                label.alignment = TextAlignmentOptions.Top;
                label.raycastTarget = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.fontStyle = FontStyles.Italic;
                var lRT = (RectTransform)lblGO.transform;
                lRT.anchorMin = new Vector2(0.5f, 0f);
                lRT.anchorMax = new Vector2(0.5f, 0f);
                lRT.pivot = new Vector2(0.5f, 1f);
                lRT.sizeDelta = new Vector2(_labelWidth, 18f);
                lRT.anchoredPosition = new Vector2(0f, _labelOffsetY);
            }

            return new MarkerIcon { root = rt, halo = halo, ring = ring, dot = dot, label = label };
        }

        private static Sprite BakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float a = Mathf.Clamp01(radius - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
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
