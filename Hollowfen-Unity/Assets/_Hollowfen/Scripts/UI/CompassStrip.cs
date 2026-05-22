using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hollowfen.Map;

namespace Hollowfen.UI
{
    public class CompassStrip : MonoBehaviour
    {
        [SerializeField] private string _targetTag = "Player";
        [SerializeField] private Transform _target;
        [SerializeField] private RectTransform _container;
        [SerializeField] private Text _markPrefab;
        [SerializeField] private float _visibleAngleRange = 120f;
        [SerializeField] private Color _cardinalColor = new Color(0.96f, 0.86f, 0.62f, 1f);
        [SerializeField] private Color _intercardinalColor = new Color(0.78f, 0.72f, 0.58f, 0.85f);
        [SerializeField, Tooltip("Color of the waypoint diamond. Distinct from cardinal/inter so the eye lands on it.")]
        private Color _waypointColor = new Color(0.965f, 0.812f, 0.475f, 1f); // GoldGlow

        private struct DirMark
        {
            public float Angle;
            public Text Instance;
            public bool IsCardinal;
        }

        private DirMark[] _marks;
        private RectTransform _waypointPip;
        private Image _waypointPipImage;
        private TMP_Text _waypointLabel;

        private void Awake()
        {
            if (_container == null) _container = transform as RectTransform;
            // Even if no cardinal-mark prefab is assigned, we still build the waypoint pip so
            // Phase 3 functionality survives independent of the legacy cardinal-mark wiring.
            if (_markPrefab == null) { BuildWaypointPip(); return; }

            var defs = new (string label, float angle, bool cardinal)[]
            {
                ("N",  0f,   true),
                ("NE", 45f,  false),
                ("E",  90f,  true),
                ("SE", 135f, false),
                ("S",  180f, true),
                ("SW", 225f, false),
                ("W",  270f, true),
                ("NW", 315f, false),
            };

            _marks = new DirMark[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var inst = Instantiate(_markPrefab, _container);
                inst.gameObject.name = "Mark_" + defs[i].label;
                inst.text = defs[i].label;
                inst.color = defs[i].cardinal ? _cardinalColor : _intercardinalColor;
                inst.fontStyle = defs[i].cardinal ? FontStyle.Bold : FontStyle.Normal;
                inst.gameObject.SetActive(true);
                var rt = inst.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                _marks[i] = new DirMark { Angle = defs[i].angle, Instance = inst, IsCardinal = defs[i].cardinal };
            }
            _markPrefab.gameObject.SetActive(false);

            BuildWaypointPip();
        }

        // Builds a small gold diamond pip + italic distance label under the strip. Auto-built so
        // the existing _HUDCanvas/CompassStrip scene object doesn't need re-authoring.
        private void BuildWaypointPip()
        {
            // Diamond pip — slim Image rotated 45°, parented to the strip container so it slides
            // with cardinal marks.
            var pipGO = new GameObject("WaypointPip", typeof(RectTransform));
            pipGO.transform.SetParent(_container, false);
            _waypointPipImage = pipGO.AddComponent<Image>();
            _waypointPipImage.color = _waypointColor;
            _waypointPipImage.raycastTarget = false;
            _waypointPip = (RectTransform)pipGO.transform;
            _waypointPip.anchorMin = new Vector2(0.5f, 0.5f);
            _waypointPip.anchorMax = new Vector2(0.5f, 0.5f);
            _waypointPip.pivot = new Vector2(0.5f, 0.5f);
            _waypointPip.sizeDelta = new Vector2(14f, 14f);
            _waypointPip.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _waypointPip.gameObject.SetActive(false);

            // Distance + name label — italic, gold, anchored below the strip center.
            var lblGO = new GameObject("WaypointLabel", typeof(RectTransform));
            lblGO.transform.SetParent(_container.parent, false); // sibling of strip so labels sit under the strip without being clipped
            _waypointLabel = lblGO.AddComponent<TextMeshProUGUI>();
            _waypointLabel.fontSize = 13f;
            _waypointLabel.color = _waypointColor;
            _waypointLabel.alignment = TextAlignmentOptions.Center;
            _waypointLabel.fontStyle = FontStyles.Italic;
            _waypointLabel.raycastTarget = false;
            _waypointLabel.textWrappingMode = TextWrappingModes.NoWrap;
            var lblRT = (RectTransform)lblGO.transform;
            lblRT.anchorMin = new Vector2(0.5f, 0f);
            lblRT.anchorMax = new Vector2(0.5f, 0f);
            lblRT.pivot = new Vector2(0.5f, 1f);
            lblRT.sizeDelta = new Vector2(360f, 18f);
            // The strip itself is anchored top-center of the HUD canvas; the label goes just under it.
            // Place at the strip's bottom edge: use the strip's own anchored position relative to
            // its parent to compute the right Y offset at runtime.
            lblRT.anchoredPosition = new Vector2(_container.anchoredPosition.x, _container.anchoredPosition.y - _container.rect.height * 0.5f - 4f);
            lblGO.SetActive(false);
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
            if (_marks == null) return;
            var t = ResolveTarget();
            if (t == null) return;
            float yaw = t.eulerAngles.y;
            float halfRange = _visibleAngleRange * 0.5f;
            float pxPerDeg = _container.rect.width / _visibleAngleRange;

            for (int i = 0; i < _marks.Length; i++)
            {
                float rel = Mathf.DeltaAngle(yaw, _marks[i].Angle);
                var inst = _marks[i].Instance;
                if (Mathf.Abs(rel) > halfRange)
                {
                    if (inst.gameObject.activeSelf) inst.gameObject.SetActive(false);
                }
                else
                {
                    if (!inst.gameObject.activeSelf) inst.gameObject.SetActive(true);
                    inst.rectTransform.anchoredPosition = new Vector2(rel * pxPerDeg, 0f);
                }
            }

            UpdateWaypoint(t, yaw, halfRange, pxPerDeg);
        }

        private void UpdateWaypoint(Transform player, float yaw, float halfRange, float pxPerDeg)
        {
            var wp = LocationRegistry.ActiveWaypoint;
            bool show = wp != null && wp.Data != null;

            if (_waypointPip != null && _waypointPip.gameObject.activeSelf != show)
                _waypointPip.gameObject.SetActive(show);
            if (_waypointLabel != null && _waypointLabel.gameObject.activeSelf != show)
                _waypointLabel.gameObject.SetActive(show);

            if (!show) return;

            Vector3 d = wp.WorldPosition - player.position;
            float horiz = Mathf.Sqrt(d.x * d.x + d.z * d.z);
            float bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            if (bearing < 0f) bearing += 360f;
            float rel = Mathf.DeltaAngle(yaw, bearing);

            // If inside the visible cone, slide to position; if outside, clamp to the edge of the cone
            // so the user always sees which side to turn to. Slight color dimming when off-screen.
            float clamped = Mathf.Clamp(rel, -halfRange, halfRange);
            _waypointPip.anchoredPosition = new Vector2(clamped * pxPerDeg, 0f);
            bool onScreen = Mathf.Abs(rel) <= halfRange;
            var c = _waypointColor;
            if (!onScreen) c.a *= 0.55f;
            _waypointPipImage.color = c;

            string name = Hollowfen.Localization.Get(wp.Data.DisplayNameId);
            _waypointLabel.text = string.Format("{0}  ·  {1:0}m", name, horiz);
        }
    }
}
