using System.Collections.Generic;
using Hollowfen.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Isolated off-screen specimen rig used by Field Guide cards and the detail leaf.
    // Each presenter owns its RenderTexture so multiple modeled species can rotate at once.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public class JournalMushroomModelPresenter : MonoBehaviour, IDragHandler, IScrollHandler, IPointerDownHandler
    {
        private const string PreviewLayerName = "JournalPreview";
        private const int PreviewRenderingLayer = 1 << 7;
        private const float DragDegreesPerPixel = 0.28f;
        private const float MinPitch = -78f;
        private const float MaxPitch = 78f;
        private const float MinZoomScale = 0.34f;
        private const float MaxZoomScale = 1.35f;
        private const float DetailFramingPadding = 1.02f;
        private static int _nextStage;

        private RawImage _image;
        private GameObject _rigRoot;
        private Transform _mount;
        private Camera _camera;
        private Light _key;
        private Light _fill;
        private Light _rim;
        private Light _bounce;
        private RenderTexture _renderTexture;
        private GameObject _current;
        private readonly List<Material> _previewMaterials = new List<Material>();
        private MushroomFieldGuideData _entry;
        private int _textureSize = 384;
        private float _rotationSpeed = 18f;
        private Color _background = Color.clear;
        private float _yaw;
        private float _pitch;
        private float _baseOrthoSize = 0.16f;
        private float _zoomScale = 1f;
        private bool _interactive;
        private bool _autoRotate = true;

        public bool HasModel => _entry != null && _entry.JournalPreviewPrefab != null && _current != null;
        public RenderTexture Texture => _renderTexture;
        public bool Interactive => _interactive;
        public float ZoomScale => _zoomScale;
        public Vector2 Rotation => new Vector2(_yaw, _pitch);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _nextStage = 0;
        }

        public void Configure(int textureSize, Color background, float rotationSpeed = 18f, bool interactive = false)
        {
            _textureSize = Mathf.Clamp(textureSize, 128, 1024);
            _background = background;
            _rotationSpeed = rotationSpeed;
            _interactive = interactive;
            CacheImage();
            _image.raycastTarget = interactive;
        }

        public void SetEntry(MushroomFieldGuideData entry)
        {
            CacheImage();
            if (_entry == entry && _current != null)
            {
                SetRigActive(isActiveAndEnabled);
                _image.enabled = true;
                return;
            }

            ClearCurrent();
            _entry = entry;
            if (entry == null || entry.JournalPreviewPrefab == null)
            {
                _image.enabled = false;
                return;
            }

            EnsureRig();
            _mount.localRotation = Quaternion.identity;
            _current = Instantiate(entry.JournalPreviewPrefab, _mount);
            _current.name = entry.Id + "_JournalSpecimen";
            _current.transform.localPosition = Vector3.zero;
            _current.transform.localRotation = Quaternion.identity;
            DisableWorldBehavior(_current);
            SetLayerRecursively(_current, LayerMask.NameToLayer(PreviewLayerName));
            foreach (var renderer in _current.GetComponentsInChildren<Renderer>(true))
                renderer.renderingLayerMask = (uint)PreviewRenderingLayer;
            ApplyPreviewMaterials(_current, entry.JournalExposure);
            CenterAndFrame();
            ResetView();
            _image.enabled = true;
            SetRigActive(isActiveAndEnabled);
        }

        // Used by the detail leaf's gamepad path. Pointer drag and wheel route through
        // the event-system callbacks below so cards can keep this presenter non-interactive.
        public void ApplyRotationDelta(float yawDelta, float pitchDelta)
        {
            if (!_interactive || _mount == null) return;
            _yaw += yawDelta;
            _pitch = Mathf.Clamp(_pitch + pitchDelta, MinPitch, MaxPitch);
            _mount.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _autoRotate = false;
        }

        // Negative values zoom in. Multiplicative scaling keeps the response consistent
        // across mushrooms with different automatically-computed framing sizes.
        public void ApplyZoomDelta(float delta)
        {
            if (!_interactive || _camera == null) return;
            _zoomScale = Mathf.Clamp(_zoomScale * Mathf.Exp(delta), MinZoomScale, MaxZoomScale);
            _camera.orthographicSize = _baseOrthoSize * _zoomScale;
            _autoRotate = false;
        }

        public void ResetView()
        {
            _yaw = _entry != null ? Mathf.Abs(_entry.Id.GetHashCode() % 360) : 0f;
            _pitch = 0f;
            _zoomScale = 1f;
            _autoRotate = _rotationSpeed > 0f;
            if (_mount != null) _mount.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_camera != null) _camera.orthographicSize = _baseOrthoSize;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactive || eventData.button != PointerEventData.InputButton.Left) return;
            _autoRotate = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_interactive || eventData.button != PointerEventData.InputButton.Left) return;
            ApplyRotationDelta(
                eventData.delta.x * DragDegreesPerPixel,
                -eventData.delta.y * DragDegreesPerPixel);
            eventData.Use();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!_interactive) return;
            float wheelStep = Mathf.Clamp(eventData.scrollDelta.y, -1f, 1f);
            if (Mathf.Abs(wheelStep) < 0.01f) return;
            ApplyZoomDelta(-wheelStep * 0.13f);
            eventData.Use();
        }

        public void Clear()
        {
            ClearCurrent();
            _entry = null;
            if (_image != null) _image.enabled = false;
        }

        private void OnEnable()
        {
            SetRigActive(_current != null);
        }

        private void OnDisable()
        {
            SetRigActive(false);
        }

        private void OnDestroy()
        {
            ClearCurrent();
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }
            if (_rigRoot != null) Destroy(_rigRoot);
        }

        private void Update()
        {
            if (_current == null || _mount == null) return;
            if (!_autoRotate) return;
            _yaw += _rotationSpeed * Time.unscaledDeltaTime;
            _mount.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void EnsureRig()
        {
            if (_rigRoot != null) return;
            int layer = LayerMask.NameToLayer(PreviewLayerName);
            if (layer < 0)
            {
                Debug.LogError("[JournalMushroomModelPresenter] Missing JournalPreview layer.");
                return;
            }

            int stageIndex = _nextStage++;
            Vector3 origin = new Vector3(2000f + stageIndex * 8f, -2000f, 2000f);
            _rigRoot = new GameObject("JournalPreviewRig_" + stageIndex);
            _rigRoot.transform.position = origin;
            DontDestroyOnLoad(_rigRoot);

            var mountGo = new GameObject("Mount");
            mountGo.transform.SetParent(_rigRoot.transform, false);
            _mount = mountGo.transform;

            var cameraGo = new GameObject("Camera");
            cameraGo.transform.SetParent(_rigRoot.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0.18f, 0.52f);
            cameraGo.transform.LookAt(_mount.position, Vector3.up);
            _camera = cameraGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(_background.r, _background.g, _background.b, 0f);
            _camera.orthographic = true;
            _camera.orthographicSize = 0.16f;
            _camera.cullingMask = 1 << layer;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 4f;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;

            _renderTexture = new RenderTexture(_textureSize, _textureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "JournalMushroomRT_" + stageIndex,
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _renderTexture.Create();
            _camera.targetTexture = _renderTexture;
            _image.texture = _renderTexture;
            _image.color = Color.white;
            _image.raycastTarget = _interactive;

            // Four local point lights give each isolated specimen a stable studio treatment.
            // Their short ranges keep neighboring preview rigs from lighting one another.
            _key = BuildPointLight("Key", new Vector3(-0.38f, 0.44f, 0.34f),
                new Color(1f, 0.93f, 0.82f), 4.00f, 1.4f, layer);
            _fill = BuildPointLight("Fill", new Vector3(0.36f, 0.22f, 0.28f),
                new Color(0.76f, 0.84f, 0.96f), 1.80f, 1.2f, layer);
            _rim = BuildPointLight("Rim", new Vector3(0.28f, 0.42f, -0.30f),
                new Color(1f, 0.82f, 0.60f), 2.00f, 1.1f, layer);
            _bounce = BuildPointLight("Bounce", new Vector3(-0.10f, -0.24f, 0.16f),
                new Color(0.82f, 0.88f, 1f), 0.90f, 0.9f, layer);
        }

        private Light BuildPointLight(string name, Vector3 localPosition, Color color, float intensity, float range, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_rigRoot.transform, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << layer;
            light.renderingLayerMask = PreviewRenderingLayer;
            return light;
        }

        private void CenterAndFrame()
        {
            if (_current == null || _mount == null || _camera == null) return;
            var renderers = _current.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            _current.transform.position += _mount.position - bounds.center;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            _camera.transform.LookAt(_mount.position, Vector3.up);
            _baseOrthoSize = _interactive
                ? DetailOrthoSize(bounds)
                : Mathf.Max(0.02f, bounds.extents.magnitude) * 1.14f;
            _camera.orthographicSize = _baseOrthoSize;
        }

        private float DetailOrthoSize(Bounds bounds)
        {
            // Fit the yawing specimen to the camera plane instead of enclosing it in a
            // three-dimensional sphere. The horizontal radius remains safe throughout
            // auto-rotation, while the tighter projected fit makes the detail study larger.
            Vector3 extents = bounds.extents;
            float horizontalRadius = Mathf.Sqrt(extents.x * extents.x + extents.z * extents.z);
            Vector3 cameraUp = _camera.transform.up;
            Vector3 cameraRight = _camera.transform.right;
            float halfHeight = Mathf.Abs(cameraUp.y) * extents.y
                + Mathf.Sqrt(cameraUp.x * cameraUp.x + cameraUp.z * cameraUp.z) * horizontalRadius;
            float halfWidth = Mathf.Abs(cameraRight.y) * extents.y
                + Mathf.Sqrt(cameraRight.x * cameraRight.x + cameraRight.z * cameraRight.z) * horizontalRadius;
            float aspect = _renderTexture != null && _renderTexture.height > 0
                ? (float)_renderTexture.width / _renderTexture.height
                : 1f;
            return Mathf.Max(0.02f, Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.01f, aspect)))
                * DetailFramingPadding;
        }

        private void ClearCurrent()
        {
            if (_current != null)
            {
                _current.SetActive(false);
                Destroy(_current);
                _current = null;
            }
            foreach (Material material in _previewMaterials)
                if (material != null) Destroy(material);
            _previewMaterials.Clear();
            SetRigActive(false);
        }

        private void SetRigActive(bool active)
        {
            if (_rigRoot != null) _rigRoot.SetActive(active);
            if (_camera != null) _camera.enabled = active;
            if (_key != null) _key.enabled = active;
            if (_fill != null) _fill.enabled = active;
            if (_rim != null) _rim.enabled = active;
            if (_bounce != null) _bounce.enabled = active;
        }

        private void CacheImage()
        {
            if (_image == null) _image = GetComponent<RawImage>();
            if (_image != null) _image.raycastTarget = _interactive;
        }

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

        private void ApplyPreviewMaterials(GameObject root, float journalExposure)
        {
            // The Meshy albedos were authored for a bright outdoor environment. A small
            // preview-only ambient copy preserves their color while making them legible on
            // the journal's near-black paper. Runtime clones keep gameplay materials untouched.
            float exposure = Mathf.Clamp(journalExposure, 0.15f, 1.10f);
            var clones = new Dictionary<Material, Material>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] sources = renderer.sharedMaterials;
                var assigned = new Material[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    Material source = sources[i];
                    if (source == null) continue;
                    Material clone;
                    if (!clones.TryGetValue(source, out clone))
                    {
                        clone = new Material(source) { name = source.name + "_JournalPreview" };
                        Texture albedo = source.GetTexture("_BaseMap");
                        clone.SetColor("_BaseColor", new Color(1.20f, 1.20f, 1.20f, 1f));
                        if (albedo != null)
                        {
                            clone.SetTexture("_EmissionMap", albedo);
                            clone.SetColor("_EmissionColor", new Color(exposure, exposure, exposure, 1f));
                            clone.EnableKeyword("_EMISSION");
                        }
                        clones.Add(source, clone);
                        _previewMaterials.Add(clone);
                    }
                    assigned[i] = clone;
                }
                renderer.sharedMaterials = assigned;
            }
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null || layer < 0) return;
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
