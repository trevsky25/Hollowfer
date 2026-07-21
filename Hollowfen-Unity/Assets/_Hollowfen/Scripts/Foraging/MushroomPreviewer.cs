using Hollowfen.Data;
using UnityEngine;

namespace Hollowfen.Foraging
{
    [DefaultExecutionOrder(-50)]
    public class MushroomPreviewer : MonoBehaviour
    {
        public static MushroomPreviewer Instance { get; private set; }

        [SerializeField] private int _renderTextureSize = 1024;
        [SerializeField] private float _rotationSpeedDeg = 25f;
        [SerializeField, Tooltip("Background color of the preview RT — warm cream to read as journal paper.")]
        private Color _backgroundColor = new Color(0.870f, 0.831f, 0.745f, 1f);
        [SerializeField, Tooltip("Silhouette color used for undiscovered mushrooms — near-black walnut.")]
        private Color _silhouetteColor = new Color(0.052f, 0.043f, 0.034f, 1f);
        [SerializeField, Tooltip("Camera ortho size — half-height of the framing in world units.")]
        private float _orthoSize = 0.13f;
        [SerializeField] private float _minOrthoSize = 0.005f;
        [SerializeField] private float _maxOrthoSize = 0.50f;
        [SerializeField, Tooltip("Pitch clamp in degrees (rotation around X).")]
        private float _pitchClampDeg = 80f;

        public bool AutoRotate { get; set; } = true;
        public float OrthoSize
        {
            get { return _cam != null ? _cam.orthographicSize : _orthoSize; }
            set { if (_cam != null) _cam.orthographicSize = Mathf.Clamp(value, _minOrthoSize, _maxOrthoSize); }
        }
        public float MinOrthoSize => _minOrthoSize;
        public float MaxOrthoSize => _maxOrthoSize;

        public void SetBackgroundColor(Color c)
        {
            _backgroundColor = c;
            if (_cam != null) _cam.backgroundColor = c;
        }

        public RenderTexture RenderTexture { get; private set; }

        private Camera _cam;
        private Light _keyLight;
        private Light _fillLight;
        private Transform _mount;
        private GameObject _current;
        private int _previewLayer;
        private float _yawDeg;
        private float _pitchDeg;
        private float _defaultOrthoSize;
        private Material _silhouetteMaterial;

        // Pan state (camera-axis units, world meters). Camera + look target shift together so the
        // gaze stays parallel — true lateral pan, not orbit.
        private Vector3 _camBaseWorldPos;
        private Vector3 _mountWorldPos;
        private Vector2 _panOffset;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _previewLayer = LayerMask.NameToLayer("MushroomPreview");
            // The inspect/inventory canvases start hidden. Building their camera, lights and 1024²
            // render target here adds avoidable work to Scene_Hollowfen activation, so Show() owns
            // first-use initialization instead.
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (RenderTexture != null) { RenderTexture.Release(); Destroy(RenderTexture); }
        }

        private void BuildRig()
        {
            if (_cam != null) return;

            // Camera — position offset; aim is set later via LookAt(mount) once the mount exists.
            var camGO = new GameObject("PreviewCamera");
            camGO.transform.SetParent(transform, false);
            camGO.transform.localPosition = new Vector3(0f, 0.18f, 0.50f);
            _cam = camGO.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = _backgroundColor;
            _cam.orthographic = true;
            _cam.orthographicSize = _orthoSize;
            _cam.cullingMask = _previewLayer >= 0 ? (1 << _previewLayer) : ~0;
            _cam.nearClipPlane = 0.01f;
            _cam.farClipPlane = 5f;
            _cam.allowHDR = false;
            _cam.allowMSAA = false;

            // RenderTexture
            RenderTexture = new RenderTexture(_renderTextureSize, _renderTextureSize, 24, RenderTextureFormat.ARGB32);
            RenderTexture.name = "MushroomPreviewRT";
            RenderTexture.antiAliasing = 4;
            RenderTexture.Create();
            _cam.targetTexture = RenderTexture;
            _defaultOrthoSize = _orthoSize;

            // Key light
            var lightGO = new GameObject("PreviewKeyLight");
            lightGO.transform.SetParent(transform, false);
            lightGO.transform.localRotation = Quaternion.Euler(40f, 200f, 0f);
            _keyLight = lightGO.AddComponent<Light>();
            _keyLight.type = LightType.Directional;
            _keyLight.color = new Color(1f, 0.96f, 0.88f);
            _keyLight.intensity = 1.2f;
            _keyLight.shadows = LightShadows.None;
            _keyLight.cullingMask = _previewLayer >= 0 ? (1 << _previewLayer) : ~0;

            // Fill light
            var fillGO = new GameObject("PreviewFillLight");
            fillGO.transform.SetParent(transform, false);
            fillGO.transform.localRotation = Quaternion.Euler(20f, 50f, 0f);
            _fillLight = fillGO.AddComponent<Light>();
            _fillLight.type = LightType.Directional;
            _fillLight.color = new Color(0.7f, 0.78f, 0.85f);
            _fillLight.intensity = 0.6f;
            _fillLight.shadows = LightShadows.None;
            _fillLight.cullingMask = _previewLayer >= 0 ? (1 << _previewLayer) : ~0;

            // Mount that the spawned mushroom parents under; we rotate this.
            var mountGO = new GameObject("Mount");
            mountGO.transform.SetParent(transform, false);
            _mount = mountGO.transform;

            // Aim the camera at the mount's world position now that it exists.
            camGO.transform.LookAt(_mount.position, Vector3.up);

            // Cache base camera + mount world positions so pan can shift both by the same delta.
            _camBaseWorldPos = camGO.transform.position;
            _mountWorldPos = _mount.position;
            SetRigActive(false);
        }

        private void EnsureRig()
        {
            if (_cam == null) BuildRig();
        }

        private void SetRigActive(bool active)
        {
            if (_cam != null) _cam.enabled = active;
            if (_keyLight != null) _keyLight.enabled = active;
            if (_fillLight != null) _fillLight.enabled = active;
        }

        public void Show(MushroomFieldGuideData data) => Show(data, false);

        public void Show(MushroomFieldGuideData data, bool silhouette)
        {
            Clear();
            if (data == null || data.WorldPrefab == null) return;
            EnsureRig();
            SetRigActive(true);
            _current = Instantiate(data.WorldPrefab, _mount);
            _current.transform.localPosition = Vector3.zero;
            _current.transform.localRotation = Quaternion.identity;
            DisableWorldBehavior(_current);
            SetLayerRecursively(_current, _previewLayer);
            if (silhouette) ApplySilhouette(_current);
            CenterCurrentOnMount();
            ResetView();
        }

        // Each species' Meshy export has its pivot at the BASE of the mesh, not the middle. Without
        // re-centering, the camera (aimed at mount.position) frames the base — leaving the mushroom
        // sitting in the upper portion of the preview. Compute renderer-bounds center and slide the
        // spawned child so bounds.center == mount.position.
        private void CenterCurrentOnMount()
        {
            if (_current == null || _mount == null) return;
            var rends = _current.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            Vector3 delta = _mount.position - b.center;
            _current.transform.position += delta;
        }

        private void ApplySilhouette(GameObject root)
        {
            if (_silhouetteMaterial == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                _silhouetteMaterial = new Material(sh) { name = "M_Silhouette" };
                _silhouetteMaterial.SetColor("_BaseColor", _silhouetteColor);
                _silhouetteMaterial.SetColor("_Color", _silhouetteColor);
            }
            var rends = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                int n = r.sharedMaterials.Length;
                var arr = new Material[n];
                for (int i = 0; i < n; i++) arr[i] = _silhouetteMaterial;
                r.sharedMaterials = arr;
            }
        }

        public void Clear()
        {
            if (_current != null) { Destroy(_current); _current = null; }
            SetRigActive(false);
        }

        public void ResetView()
        {
            _yawDeg = 0f;
            _pitchDeg = 0f;
            _panOffset = Vector2.zero;
            if (_mount != null) _mount.localRotation = Quaternion.identity;
            if (_cam != null)
            {
                _cam.transform.position = _camBaseWorldPos;
                _cam.transform.LookAt(_mountWorldPos, Vector3.up);
            }
            OrthoSize = _defaultOrthoSize;
            AutoRotate = true;
        }

        // yawDeltaDeg: yaw around world Y. pitchDeltaDeg: pitch around world X (clamped).
        public void ApplyRotationDelta(float yawDeltaDeg, float pitchDeltaDeg)
        {
            if (_mount == null) return;
            _yawDeg += yawDeltaDeg;
            _pitchDeg = Mathf.Clamp(_pitchDeg + pitchDeltaDeg, -_pitchClampDeg, _pitchClampDeg);
            _mount.localRotation = Quaternion.Euler(_pitchDeg, _yawDeg, 0f);
            AutoRotate = false;
        }

        // Negative deltaSize zooms in.
        public void ApplyZoomDelta(float deltaSize)
        {
            OrthoSize = OrthoSize + deltaSize;
        }

        // World-units lateral pan. Both the camera and its look target shift by the same vector,
        // keeping the gaze direction unchanged (true pan, no parallax bend).
        // delta.x = camera-right, delta.y = camera-up, world meters per axis.
        public void ApplyPanDelta(Vector2 deltaCameraXY)
        {
            if (_cam == null) return;
            _panOffset += deltaCameraXY;
            Vector3 panWorld = _cam.transform.right * _panOffset.x + _cam.transform.up * _panOffset.y;
            _cam.transform.position = _camBaseWorldPos + panWorld;
            _cam.transform.LookAt(_mountWorldPos + panWorld, Vector3.up);
            // User input on pan disables auto-rotate, same convention as orbit/zoom.
            AutoRotate = false;
        }

        private void Update()
        {
            if (_current == null || _mount == null) return;
            if (!AutoRotate) return;
            _yawDeg += _rotationSpeedDeg * Time.unscaledDeltaTime;
            _mount.localRotation = Quaternion.Euler(_pitchDeg, _yawDeg, 0f);
        }

        // World prefabs carry harvest state, interaction triggers, and sometimes physics helpers.
        // A UI clone must remain a render-only specimen: otherwise its MushroomNode starts at the
        // preview rig's origin, subscribes to gameplay state, and can emit a bogus position-based id.
        private static void DisableWorldBehavior(GameObject root)
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
