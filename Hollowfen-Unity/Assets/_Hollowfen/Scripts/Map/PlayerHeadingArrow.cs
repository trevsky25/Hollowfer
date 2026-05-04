using UnityEngine;

namespace Hollowfen.Map
{
    public class PlayerHeadingArrow : MonoBehaviour
    {
        [SerializeField] private string _targetTag = "Player";
        [SerializeField] private Transform _target;

        private RectTransform _rt;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
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
            _rt.localRotation = Quaternion.Euler(0f, 0f, -t.eulerAngles.y);
        }
    }
}
