using UnityEngine;

namespace Hollowfen.Map
{
    [RequireComponent(typeof(Camera))]
    public class MiniMapCamera : MonoBehaviour
    {
        [SerializeField] private string _targetTag = "Player";
        [SerializeField] private Transform _target;
        [SerializeField] private float _height = 60f;
        [SerializeField] private float _orthoSize = 30f;
        [SerializeField] private bool _rotateWithTarget;

        private Camera _cam;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = _orthoSize;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 1f);
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
            if (t == null) return;
            var p = t.position;
            transform.position = new Vector3(p.x, p.y + _height, p.z);
            float yaw = _rotateWithTarget ? t.eulerAngles.y : 0f;
            transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            if (_cam != null && !Mathf.Approximately(_cam.orthographicSize, _orthoSize))
                _cam.orthographicSize = _orthoSize;
        }
    }
}
