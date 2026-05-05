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
        private Transform _mount;
        private GameObject _current;
        private int _previewLayer;
        private float _yawDeg;
        private float _pitchDeg;
        private float _defaultOrthoSize;
        private Material _silhouetteMaterial;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _previewLayer = LayerMask.NameToLayer("MushroomPreview");
            BuildRig();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (RenderTexture != null) { RenderTexture.Release(); Destroy(RenderTexture); }
        }

        private void BuildRig()
        {
            // Camera
            var camGO = new GameObject("PreviewCamera");
            camGO.transform.SetParent(transform, false);
            camGO.transform.localPosition = new Vector3(0f, 0.10f, 0.45f);
            camGO.transform.localRotation = Quaternion.Euler(15f, 180f, 0f);
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
            var key = lightGO.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.96f, 0.88f);
            key.intensity = 1.2f;
            key.shadows = LightShadows.None;
            key.cullingMask = _previewLayer >= 0 ? (1 << _previewLayer) : ~0;

            // Fill light
            var fillGO = new GameObject("PreviewFillLight");
            fillGO.transform.SetParent(transform, false);
            fillGO.transform.localRotation = Quaternion.Euler(20f, 50f, 0f);
            var fill = fillGO.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.7f, 0.78f, 0.85f);
            fill.intensity = 0.6f;
            fill.shadows = LightShadows.None;
            fill.cullingMask = _previewLayer >= 0 ? (1 << _previewLayer) : ~0;

            // Mount that the spawned mushroom parents under; we rotate this.
            var mountGO = new GameObject("Mount");
            mountGO.transform.SetParent(transform, false);
            _mount = mountGO.transform;
        }

        public void Show(MushroomFieldGuideData data) => Show(data, false);

        public void Show(MushroomFieldGuideData data, bool silhouette)
        {
            Clear();
            if (data == null || data.WorldPrefab == null) return;
            _current = Instantiate(data.WorldPrefab, _mount);
            _current.transform.localPosition = Vector3.zero;
            _current.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(_current, _previewLayer);
            if (silhouette) ApplySilhouette(_current);
            ResetView();
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
        }

        public void ResetView()
        {
            _yawDeg = 0f;
            _pitchDeg = 0f;
            if (_mount != null) _mount.localRotation = Quaternion.identity;
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

        private void Update()
        {
            if (_current == null || _mount == null) return;
            if (!AutoRotate) return;
            _yawDeg += _rotationSpeedDeg * Time.unscaledDeltaTime;
            _mount.localRotation = Quaternion.Euler(_pitchDeg, _yawDeg, 0f);
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
