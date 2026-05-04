using UnityEngine;
using UnityEngine.UI;

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

        private struct DirMark
        {
            public float Angle;
            public Text Instance;
            public bool IsCardinal;
        }

        private DirMark[] _marks;

        private void Awake()
        {
            if (_container == null) _container = transform as RectTransform;
            if (_markPrefab == null) return;

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
        }
    }
}
