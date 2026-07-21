using UnityEngine;

namespace Hollowfen.Map
{
    // Rotates a UI rect to match the player's Y heading. Optionally also REPOSITIONS the arrow
    // to the player's projected location on a map (used by the full-screen map once it can pan
    // away from the player). When _mapCamera and _container are set, the arrow tracks the
    // player's position through the camera viewport; otherwise the arrow's position stays put.
    public class PlayerHeadingArrow : MonoBehaviour
    {
        [SerializeField] private string _targetTag = "Player";
        [SerializeField] private Transform _target;
        [SerializeField] private Camera _mapCamera;
        [SerializeField] private RectTransform _container;
        [SerializeField] private bool _hideWhenOffMap = true;
        [SerializeField, Tooltip("Optional child that rotates with Wren's heading. When omitted, the whole marker rotates (legacy mini-map behavior).")]
        private RectTransform _headingGlyph;
        [SerializeField, Tooltip("Optional ring that pulses independently of the directional glyph.")]
        private RectTransform _pulseRing;
        [SerializeField] private float _pulseAmount = 0.1f;
        [SerializeField] private float _pulseSpeed = 3.8f;

        private RectTransform _rt;
        private CanvasGroup _cg;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
        }

        public void Configure(Camera mapCamera, RectTransform container,
                              RectTransform headingGlyph = null, RectTransform pulseRing = null)
        {
            _mapCamera = mapCamera;
            _container = container;
            _headingGlyph = headingGlyph;
            _pulseRing = pulseRing;
        }

        private Transform ResolveTarget()
        {
            if (_target != null) return _target;
            if (string.IsNullOrEmpty(_targetTag)) return null;
            var go = GameObject.FindGameObjectWithTag(_targetTag);
            if (go != null) _target = go.transform;
            return _target;
        }

        private void LateUpdate()
        {
            var t = ResolveTarget();
            if (t == null || _rt == null) return;

            // Full-map current-location markers keep their plate, pulse, and label upright; only
            // the nested arrow rotates. Existing mini-map arrows omit _headingGlyph and retain the
            // legacy whole-rect behavior.
            RectTransform rotatingPart = _headingGlyph != null ? _headingGlyph : _rt;
            rotatingPart.localRotation = Quaternion.Euler(0f, 0f, -t.eulerAngles.y);
            if (_headingGlyph != null && _rt.localRotation != Quaternion.identity)
                _rt.localRotation = Quaternion.identity;

            if (_pulseRing != null)
            {
                float scale = 1f + _pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _pulseSpeed));
                _pulseRing.localScale = new Vector3(scale, scale, 1f);
            }

            if (_mapCamera == null || _container == null) return;

            Vector3 vp = _mapCamera.WorldToViewportPoint(t.position);
            bool inView = vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
            SetVisible(inView || !_hideWhenOffMap);
            if (!inView && _hideWhenOffMap) return;

            Rect r = _container.rect;
            _rt.anchoredPosition = new Vector2(
                (vp.x - 0.5f) * r.width,
                (vp.y - 0.5f) * r.height);
        }

        private void SetVisible(bool visible)
        {
            // No ?? here — it bypasses Unity's overloaded null check and returns the
            // "missing component" stub, which then throws on every .alpha access.
            if (_cg == null)
            {
                _cg = gameObject.GetComponent<CanvasGroup>();
                if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
            }
            float a = visible ? 1f : 0f;
            if (!Mathf.Approximately(_cg.alpha, a)) _cg.alpha = a;
        }
    }
}
