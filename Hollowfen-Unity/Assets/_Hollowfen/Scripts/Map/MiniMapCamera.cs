using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
        [SerializeField, Min(1f), Tooltip("The scenery is static, so the retained minimap texture does not need a full world render every gameplay frame.")]
        private float _refreshRate = 10f;
        [SerializeField, Min(80f)] private float _farClipPlane = 160f;

        private Camera _cam;
        private bool _renderingAllowed = true;
        private float _nextRenderAt;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = _orthoSize;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.16f, 0.18f, 0.12f, 1f);
            _cam.farClipPlane = Mathf.Max(_height + 20f, _farClipPlane);
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.useOcclusionCulling = false;
            var cameraData = _cam.GetUniversalAdditionalCameraData();
            cameraData.renderShadows = false;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            // The player's own layer renders as a corrupt magenta blob from top-down — exclude it
            // (the gold heading arrow marks Wren on the widget instead).
            int foraging = LayerMask.NameToLayer("Foraging");
            if (foraging >= 0) _cam.cullingMask &= ~(1 << foraging);

            // The retained RenderTexture makes a continuous secondary world render unnecessary.
            // LateUpdate enables one refresh frame; the render-pipeline callback disables it again.
            _cam.enabled = false;
            _nextRenderAt = Time.unscaledTime;
        }

        private void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
            _nextRenderAt = Time.unscaledTime;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            if (_cam != null) _cam.enabled = false;
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

            if (_renderingAllowed && _cam != null && !_cam.enabled && Time.unscaledTime >= _nextRenderAt)
                _cam.enabled = true;
        }

        public void SetRenderingActive(bool active)
        {
            _renderingAllowed = active;
            if (_cam == null) _cam = GetComponent<Camera>();
            if (!active)
            {
                _cam.enabled = false;
                return;
            }
            _nextRenderAt = Time.unscaledTime;
        }

        public void UseBakedMap()
        {
            _renderingAllowed = false;
            if (_cam == null) _cam = GetComponent<Camera>();
            _cam.enabled = false;
            enabled = false;
        }

        private void HandleEndCameraRendering(ScriptableRenderContext _, Camera renderedCamera)
        {
            if (renderedCamera != _cam || _cam == null) return;
            _cam.enabled = false;
            _nextRenderAt = Time.unscaledTime + 1f / Mathf.Max(1f, _refreshRate);
        }
    }
}
