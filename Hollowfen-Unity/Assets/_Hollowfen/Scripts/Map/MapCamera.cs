using UnityEngine;
using UnityEngine.Rendering;

namespace Hollowfen.Map
{
    [RequireComponent(typeof(Camera))]
    public class MapCamera : MonoBehaviour
    {
        [SerializeField] private string _targetTag = "Player";
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _worldCenter = new Vector3(232f, 33.19f, 310.62f);
        [SerializeField] private float _height = 80f;
        [SerializeField] private float _orthoSize = 150f;
        [SerializeField] private bool _disableFogWhileRendering = true;
        [SerializeField] private bool _useFixedCenter;

        [SerializeField, Tooltip("Runtime RenderTexture size. 2:1 aspect (2048×1024) matches the map zone in MapScreen so the parchment hugs the rendered world without distortion or empty padding.")]
        private Vector2Int _renderTextureSize = new Vector2Int(2048, 1024);

        public RenderTexture RenderTexture { get; private set; }

        private Camera _cam;
        private bool _savedFog;
        private Color _savedFogColor;
        private FogMode _savedFogMode;
        private float _savedFogDensity;
        private float _savedFogStart;
        private float _savedFogEnd;
        private Material _savedSkybox;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = _orthoSize;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 1f);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Create a runtime RT at the configured landscape aspect. Overrides any project RT asset
            // assigned in the inspector — that asset can stay orphaned (no longer referenced).
            RenderTexture = new RenderTexture(_renderTextureSize.x, _renderTextureSize.y, 24, RenderTextureFormat.ARGB32);
            RenderTexture.name = "MapViewRT_Runtime";
            RenderTexture.antiAliasing = 4;
            RenderTexture.Create();
            _cam.targetTexture = RenderTexture;

            ApplyPosition();
        }

        private void OnDestroy()
        {
            if (RenderTexture != null) { RenderTexture.Release(); Destroy(RenderTexture); RenderTexture = null; }
        }

        private Transform ResolveTarget()
        {
            if (_target != null) return _target;
            if (string.IsNullOrEmpty(_targetTag)) return null;
            var go = GameObject.FindGameObjectWithTag(_targetTag);
            if (go != null) _target = go.transform;
            return _target;
        }

        private void ApplyPosition()
        {
            Vector3 anchor = _worldCenter;
            if (!_useFixedCenter)
            {
                var t = ResolveTarget();
                if (t != null) anchor = t.position;
            }
            transform.position = new Vector3(anchor.x, anchor.y + _height, anchor.z);
        }

        private void LateUpdate()
        {
            ApplyPosition();
            if (_cam != null && !Mathf.Approximately(_cam.orthographicSize, _orthoSize))
                _cam.orthographicSize = _orthoSize;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += HandleBeginRender;
            RenderPipelineManager.endCameraRendering += HandleEndRender;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginRender;
            RenderPipelineManager.endCameraRendering -= HandleEndRender;
        }

        private void HandleBeginRender(ScriptableRenderContext _, Camera cam)
        {
            if (cam != _cam || !_disableFogWhileRendering) return;
            _savedFog = RenderSettings.fog;
            _savedFogColor = RenderSettings.fogColor;
            _savedFogMode = RenderSettings.fogMode;
            _savedFogDensity = RenderSettings.fogDensity;
            _savedFogStart = RenderSettings.fogStartDistance;
            _savedFogEnd = RenderSettings.fogEndDistance;
            _savedSkybox = RenderSettings.skybox;
            RenderSettings.fog = false;
        }

        private void HandleEndRender(ScriptableRenderContext _, Camera cam)
        {
            if (cam != _cam || !_disableFogWhileRendering) return;
            RenderSettings.fog = _savedFog;
            RenderSettings.fogColor = _savedFogColor;
            RenderSettings.fogMode = _savedFogMode;
            RenderSettings.fogDensity = _savedFogDensity;
            RenderSettings.fogStartDistance = _savedFogStart;
            RenderSettings.fogEndDistance = _savedFogEnd;
            RenderSettings.skybox = _savedSkybox;
        }

        public void Configure(Vector3 worldCenter, float orthoSize)
        {
            _worldCenter = worldCenter;
            _orthoSize = orthoSize;
            if (_cam == null) _cam = GetComponent<Camera>();
            _cam.orthographicSize = orthoSize;
            transform.position = new Vector3(worldCenter.x, worldCenter.y + _height, worldCenter.z);
        }
    }
}
