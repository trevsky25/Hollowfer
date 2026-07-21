using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hollowfen.Map
{
    [RequireComponent(typeof(Camera))]
    public class MapCamera : MonoBehaviour
    {
        public enum CamMode { FollowPlayer, Free }

        [SerializeField] private string _targetTag = "Player";
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _worldCenter = new Vector3(232f, 33.19f, 310.62f);
        [SerializeField] private float _height = 80f;
        [SerializeField] private float _orthoSize = 150f;
        [SerializeField] private bool _disableFogWhileRendering = true;
        [SerializeField] private bool _useFixedCenter;
        [SerializeField, Min(100f)] private float _farClipPlane = 220f;

        [SerializeField, Tooltip("Runtime RenderTexture size. 2:1 aspect (2048×1024) matches the map zone in MapScreen so the parchment hugs the rendered world without distortion or empty padding.")]
        private Vector2Int _renderTextureSize = new Vector2Int(2048, 1024);
        [SerializeField, Tooltip("Static overhead world image. When assigned, pan and zoom crop this texture instead of rendering the world again.")]
        private Texture2D _bakedMap;

        [Header("Pan / Zoom")]
        [SerializeField, Tooltip("World-space xz rect the VISIBLE FRAME is kept inside (not just the camera center). Auto-tightened to the active Terrain at Awake when one exists.")]
        private Rect _worldBounds = new Rect(0f, 0f, 500f, 500f);
        [SerializeField, Tooltip("Animated ortho size — the actual _orthoSize lerps toward this each frame.")]
        private float _targetOrthoSize = 150f;
        [SerializeField] private float _zoomLerpSpeed = 8f;
        [SerializeField] private float _zoomClose = 60f;
        [SerializeField] private float _zoomRegional = 150f;

        public RenderTexture RenderTexture { get; private set; }
        public CamMode Mode { get; private set; } = CamMode.FollowPlayer;
        public float CurrentOrthoSize => _orthoSize;
        public float TargetOrthoSize => _targetOrthoSize;
        public float ZoomClose => _zoomClose;
        public float ZoomRegional => _zoomRegional;
        public bool IsZoomedClose => Mathf.Approximately(_targetOrthoSize, _zoomClose);
        public bool UsesBakedMap => _bakedMap != null;
        public Texture MapTexture => _bakedMap != null ? _bakedMap : EnsureRenderTexture();
        public Rect CurrentUvRect
        {
            get
            {
                float width = Mathf.Clamp01((_orthoSize * 2f * Aspect) / _worldBounds.width);
                float height = Mathf.Clamp01((_orthoSize * 2f) / _worldBounds.height);
                float centerX = Mathf.InverseLerp(_worldBounds.xMin, _worldBounds.xMax, transform.position.x);
                float centerY = Mathf.InverseLerp(_worldBounds.yMin, _worldBounds.yMax, transform.position.z);
                return new Rect(
                    Mathf.Clamp(centerX - width * 0.5f, 0f, 1f - width),
                    Mathf.Clamp(centerY - height * 0.5f, 0f, 1f - height),
                    width,
                    height);
            }
        }

        private Camera _cam;
        private bool _savedFog;
        private Color _savedFogColor;
        private FogMode _savedFogMode;
        private float _savedFogDensity;
        private float _savedFogStart;
        private float _savedFogEnd;
        private Material _savedSkybox;
        private bool _renderingAllowed;
        private bool _renderRequested;
        private float _nextRenderAt;
        private const float MaxRenderRate = 30f;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            // Backstop tone if a sliver of out-of-world ever shows: muted moss, reads as unmapped land.
            _cam.backgroundColor = new Color(0.16f, 0.18f, 0.12f, 1f);
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.useOcclusionCulling = false;
            _cam.farClipPlane = Mathf.Max(_height + 20f, _farClipPlane);
            _cam.aspect = Aspect;
            var cameraData = _cam.GetUniversalAdditionalCameraData();
            cameraData.renderShadows = false;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // The player's own layer renders as a corrupt magenta blob from top-down — exclude it.
            int foraging = LayerMask.NameToLayer("Foraging");
            if (foraging >= 0) _cam.cullingMask &= ~(1 << foraging);

            // Tighten the clamp rect to the real terrain, and cap the zoom presets so the visible
            // frame (ortho × aspect wide) can never exceed the world — that's what produced the
            // giant black void on the old map.
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                var tp = terrain.GetPosition();
                var ts = terrain.terrainData.size;
                _worldBounds = new Rect(tp.x, tp.z, ts.x, ts.z);
            }
            float maxOrtho = Mathf.Min(_worldBounds.width / (2f * Aspect), _worldBounds.height / 2f);
            _zoomRegional = Mathf.Min(_zoomRegional, maxOrtho);
            _zoomClose = Mathf.Min(_zoomClose, _zoomRegional);
            _orthoSize = Mathf.Min(_orthoSize, maxOrtho);
            _targetOrthoSize = _orthoSize;
            _cam.orthographicSize = _orthoSize;

            // The full-screen map starts closed. Do not allocate its large 2048x1024 render target
            // or render an offscreen camera during scene activation; both are deferred until Open().
            // Clear the legacy project RT assigned in the scene so it cannot render accidentally.
            _cam.targetTexture = null;
            _cam.enabled = false;

            ApplyPosition();
        }

        public RenderTexture EnsureRenderTexture()
        {
            if (RenderTexture != null)
            {
                if (!RenderTexture.IsCreated()) RenderTexture.Create();
                if (_cam != null) _cam.targetTexture = RenderTexture;
                return RenderTexture;
            }

            RenderTexture = new RenderTexture(
                _renderTextureSize.x, _renderTextureSize.y, 24, RenderTextureFormat.ARGB32)
            {
                name = "MapViewRT_Runtime",
                antiAliasing = 1
            };
            RenderTexture.Create();
            if (_cam == null) _cam = GetComponent<Camera>();
            _cam.targetTexture = RenderTexture;
            return RenderTexture;
        }

        public void SetRenderingActive(bool active)
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            _renderingAllowed = active;
            if (!active)
            {
                _cam.enabled = false;
                return;
            }

            if (_bakedMap != null)
            {
                _cam.enabled = false;
                _renderRequested = false;
                return;
            }

            EnsureRenderTexture();
            _renderRequested = true;
            _nextRenderAt = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            if (_cam != null && _cam.targetTexture == RenderTexture) _cam.targetTexture = null;
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
            SetCameraXZ(anchor.x, anchor.z, anchor.y + _height);
        }

        private float Aspect => _renderTextureSize.y > 0
            ? (float)_renderTextureSize.x / _renderTextureSize.y
            : 2f;

        private void SetCameraXZ(float x, float z, float? worldY = null)
        {
            // Clamp the FRAME, not the center: keep [center ± half-extents] inside the world rect.
            // If the frame is wider/taller than the world on an axis, pin to the world's middle.
            float halfW = _orthoSize * Aspect;
            float halfH = _orthoSize;
            float cx = halfW * 2f >= _worldBounds.width
                ? _worldBounds.center.x
                : Mathf.Clamp(x, _worldBounds.xMin + halfW, _worldBounds.xMax - halfW);
            float cz = halfH * 2f >= _worldBounds.height
                ? _worldBounds.center.y
                : Mathf.Clamp(z, _worldBounds.yMin + halfH, _worldBounds.yMax - halfH);
            float cy = worldY.HasValue ? worldY.Value : transform.position.y;
            var next = new Vector3(cx, cy, cz);
            if ((transform.position - next).sqrMagnitude > 0.000001f)
            {
                transform.position = next;
                RequestRender();
            }
        }

        // Public — call once per frame while the map is open and being panned. delta is world-space xz.
        public void Pan(Vector2 worldXZ)
        {
            if (worldXZ.sqrMagnitude <= 0f) return;
            Mode = CamMode.Free;
            var p = transform.position;
            SetCameraXZ(p.x + worldXZ.x, p.z + worldXZ.y);
        }

        // Public — snap camera back to player and resume follow mode.
        public void CenterOnPlayer()
        {
            var t = ResolveTarget();
            Mode = CamMode.FollowPlayer;
            if (t == null) return;
            SetCameraXZ(t.position.x, t.position.z, t.position.y + _height);
        }

        // Public — animated zoom toward the given ortho size. Doesn't change pan mode.
        public void SetTargetOrthoSize(float size)
        {
            float next = Mathf.Max(1f, size);
            if (Mathf.Approximately(_targetOrthoSize, next)) return;
            _targetOrthoSize = next;
            RequestRender();
        }

        public void ToggleZoomPreset()
        {
            SetTargetOrthoSize(IsZoomedClose ? _zoomRegional : _zoomClose);
        }

        private void LateUpdate()
        {
            if (_cam == null || !_renderingAllowed) return;

            if (Mode == CamMode.FollowPlayer)
                ApplyPosition();
            // else: hold current xz; Pan() updates it manually

            if (_cam != null)
            {
                if (!Mathf.Approximately(_orthoSize, _targetOrthoSize))
                {
                    float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.fixedDeltaTime;
                    _orthoSize = Mathf.Lerp(_orthoSize, _targetOrthoSize, 1f - Mathf.Exp(-_zoomLerpSpeed * dt));
                    if (Mathf.Abs(_orthoSize - _targetOrthoSize) < 0.05f) _orthoSize = _targetOrthoSize;
                }
                if (!Mathf.Approximately(_cam.orthographicSize, _orthoSize))
                {
                    _cam.orthographicSize = _orthoSize;
                    RequestRender();
                    // Zooming out grows the frame — re-clamp so the edge never slides off-world.
                    var p = transform.position;
                    SetCameraXZ(p.x, p.z);
                }
            }

            if (_bakedMap == null && _renderRequested && !_cam.enabled && Time.unscaledTime >= _nextRenderAt)
                _cam.enabled = true;
        }

        private void RequestRender()
        {
            _renderRequested = true;
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
            if (_cam != null) _cam.enabled = false;
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
            if (cam != _cam) return;
            if (_disableFogWhileRendering)
            {
                RenderSettings.fog = _savedFog;
                RenderSettings.fogColor = _savedFogColor;
                RenderSettings.fogMode = _savedFogMode;
                RenderSettings.fogDensity = _savedFogDensity;
                RenderSettings.fogStartDistance = _savedFogStart;
                RenderSettings.fogEndDistance = _savedFogEnd;
                RenderSettings.skybox = _savedSkybox;
            }

            // Retain the last map image and stop paying for a second world render while the map is
            // idle. Pan, zoom, recenter, or player-follow movement requests the next frame.
            _cam.enabled = false;
            _renderRequested = false;
            _nextRenderAt = Time.unscaledTime + 1f / MaxRenderRate;
        }

        public void Configure(Vector3 worldCenter, float orthoSize)
        {
            _worldCenter = worldCenter;
            _orthoSize = orthoSize;
            _targetOrthoSize = orthoSize;
            if (_cam == null) _cam = GetComponent<Camera>();
            _cam.orthographicSize = orthoSize;
            transform.position = new Vector3(worldCenter.x, worldCenter.y + _height, worldCenter.z);
            RequestRender();
        }
    }
}
