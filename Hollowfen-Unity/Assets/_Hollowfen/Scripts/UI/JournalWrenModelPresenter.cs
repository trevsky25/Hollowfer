using System.Collections.Generic;
using Hollowfen.Data;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    /// <summary>
    /// Visual-only, isolated character study rig for Wren's journal page.
    /// It owns its RenderTexture, cloned preview materials, animation graph,
    /// lights, and lifecycle; no player/input/controller component is instantiated.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public class JournalWrenModelPresenter : MonoBehaviour, IDragHandler, IScrollHandler, IPointerDownHandler
    {
        private const string PreviewLayerName = "JournalPreview";
        private const int PreviewRenderingLayer = 1 << 7;
        private const float DragDegreesPerPixel = 0.24f;
        private const float MinPitch = -28f;
        private const float MaxPitch = 32f;
        private const float MinZoomScale = 0.52f;
        private const float MaxZoomScale = 1.34f;
        private const float FramingPadding = 1.045f;
        private static int _nextStage;

        private readonly List<Material> _previewMaterials = new List<Material>();
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
        private CharacterProfileData _profile;
        private PlayableGraph _animationGraph;
        private AnimationClipPlayable _idlePlayable;
        private float _yaw;
        private float _pitch;
        private float _baseOrthoSize = 1f;
        private float _zoomScale = 1f;
        private float _autoRotateSpeed = 7f;
        private bool _autoRotate = true;
        private int _textureWidth = 768;
        private int _textureHeight = 896;

        public bool HasModel => _profile != null && _profile.JournalModelPrefab != null && _current != null;
        public RenderTexture Texture => _renderTexture;
        public float ZoomScale => _zoomScale;
        public Vector2 Rotation => new Vector2(_yaw, _pitch);
        public int RendererCount => _current != null ? _current.GetComponentsInChildren<Renderer>(true).Length : 0;
        public bool IsAnimating => _animationGraph.IsValid() && _idlePlayable.IsValid()
            && _rigRoot != null && _rigRoot.activeInHierarchy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _nextStage = 0;
        }

        public void Configure(int textureWidth, int textureHeight, float autoRotateSpeed = 7f)
        {
            _textureWidth = Mathf.Clamp(textureWidth, 256, 1024);
            _textureHeight = Mathf.Clamp(textureHeight, 256, 1024);
            _autoRotateSpeed = Mathf.Max(0f, autoRotateSpeed);
            CacheImage();
            _image.raycastTarget = true;
        }

        public void SetProfile(CharacterProfileData profile)
        {
            CacheImage();
            if (_profile == profile && _current != null)
            {
                SetRigActive(isActiveAndEnabled);
                _image.enabled = true;
                return;
            }

            ClearCurrent();
            _profile = profile;
            if (profile == null || profile.JournalModelPrefab == null)
            {
                _image.enabled = false;
                return;
            }

            EnsureRig();
            if (_mount == null)
            {
                _image.enabled = false;
                return;
            }

            _mount.localRotation = Quaternion.identity;
            _current = Instantiate(profile.JournalModelPrefab, _mount);
            _current.name = "Wren_JournalStudy";
            _current.transform.localPosition = Vector3.zero;
            _current.transform.localRotation = Quaternion.identity;
            _current.transform.localScale = Vector3.one;
            DisableWorldBehavior(_current);
            SetLayerRecursively(_current, LayerMask.NameToLayer(PreviewLayerName));
            foreach (Renderer renderer in _current.GetComponentsInChildren<Renderer>(true))
            {
                renderer.renderingLayerMask = PreviewRenderingLayer;
                var skinned = renderer as SkinnedMeshRenderer;
                if (skinned != null) skinned.updateWhenOffscreen = true;
            }
            ApplyPreviewMaterials(_current, profile.JournalExposure);
            StartIdle(profile.JournalIdleClip);
            CenterAndFrame();
            ResetView();
            _image.enabled = true;
            SetRigActive(isActiveAndEnabled);
        }

        public void ApplyRotationDelta(float yawDelta, float pitchDelta)
        {
            if (_mount == null) return;
            _yaw += yawDelta;
            _pitch = Mathf.Clamp(_pitch + pitchDelta, MinPitch, MaxPitch);
            _mount.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _autoRotate = false;
        }

        // Negative values zoom in, matching the mushroom detail presenter.
        public void ApplyZoomDelta(float delta)
        {
            if (_camera == null) return;
            _zoomScale = Mathf.Clamp(_zoomScale * Mathf.Exp(delta), MinZoomScale, MaxZoomScale);
            _camera.orthographicSize = _baseOrthoSize * _zoomScale;
            _autoRotate = false;
        }

        public void ResetView()
        {
            _yaw = -8f;
            _pitch = -2f;
            _zoomScale = 1f;
            _autoRotate = _autoRotateSpeed > 0f;
            if (_mount != null) _mount.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            if (_camera != null) _camera.orthographicSize = _baseOrthoSize;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _autoRotate = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            ApplyRotationDelta(
                eventData.delta.x * DragDegreesPerPixel,
                -eventData.delta.y * DragDegreesPerPixel);
            eventData.Use();
        }

        public void OnScroll(PointerEventData eventData)
        {
            float wheelStep = Mathf.Clamp(eventData.scrollDelta.y, -1f, 1f);
            if (Mathf.Abs(wheelStep) < 0.01f) return;
            ApplyZoomDelta(-wheelStep * 0.13f);
            eventData.Use();
        }

        private void Update()
        {
            if (_current == null || _mount == null || !_autoRotate) return;
            _yaw += _autoRotateSpeed * Time.unscaledDeltaTime;
            _mount.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
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

        private void EnsureRig()
        {
            if (_rigRoot != null) return;
            int layer = LayerMask.NameToLayer(PreviewLayerName);
            if (layer < 0)
            {
                Debug.LogError("[JournalWrenModelPresenter] Missing JournalPreview layer.");
                return;
            }

            int stageIndex = _nextStage++;
            Vector3 origin = new Vector3(3000f + stageIndex * 12f, -3000f, 3000f);
            _rigRoot = new GameObject("JournalWrenPreviewRig_" + stageIndex);
            _rigRoot.transform.position = origin;
            DontDestroyOnLoad(_rigRoot);

            var mountGo = new GameObject("Mount");
            mountGo.transform.SetParent(_rigRoot.transform, false);
            _mount = mountGo.transform;

            var cameraGo = new GameObject("Camera");
            cameraGo.transform.SetParent(_rigRoot.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0.08f, 4.5f);
            cameraGo.transform.LookAt(_mount.position, Vector3.up);
            _camera = cameraGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.orthographic = true;
            _camera.orthographicSize = 1f;
            _camera.cullingMask = 1 << layer;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 12f;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;

            _renderTexture = new RenderTexture(_textureWidth, _textureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "JournalWrenRT_" + stageIndex,
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _renderTexture.Create();
            _camera.targetTexture = _renderTexture;
            _image.texture = _renderTexture;
            _image.color = Color.white;
            _image.raycastTarget = true;

            _key = BuildPointLight("Key", new Vector3(-1.9f, 2.6f, 2.3f),
                new Color(1f, 0.93f, 0.82f), 5.0f, 7.5f, layer);
            _fill = BuildPointLight("Fill", new Vector3(2.1f, 1.55f, 2.1f),
                new Color(0.72f, 0.84f, 1f), 2.35f, 7f, layer);
            _rim = BuildPointLight("Rim", new Vector3(1.65f, 2.45f, -1.8f),
                new Color(1f, 0.78f, 0.54f), 2.8f, 6.5f, layer);
            _bounce = BuildPointLight("Bounce", new Vector3(-0.65f, 0.22f, 1.25f),
                new Color(0.76f, 0.86f, 1f), 1.15f, 5f, layer);
        }

        private Light BuildPointLight(string lightName, Vector3 position, Color color, float intensity, float range, int layer)
        {
            var go = new GameObject(lightName);
            go.transform.SetParent(_rigRoot.transform, false);
            go.transform.localPosition = position;
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

        private void StartIdle(AnimationClip clip)
        {
            DestroyAnimationGraph();
            Animator animator = _current != null ? _current.GetComponentInChildren<Animator>(true) : null;
            if (animator == null || clip == null) return;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;

            _animationGraph = PlayableGraph.Create("WrenJournalIdle");
            _animationGraph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
            _idlePlayable = AnimationClipPlayable.Create(_animationGraph, clip);
            _idlePlayable.SetApplyFootIK(true);
            _idlePlayable.SetTime(0.85f);
            var output = AnimationPlayableOutput.Create(_animationGraph, "WrenJournalIdleOutput", animator);
            output.SetSourcePlayable(_idlePlayable);
            _animationGraph.Play();
            _animationGraph.Evaluate(0f);
        }

        private void CenterAndFrame()
        {
            if (_current == null || _mount == null || _camera == null) return;
            Renderer[] renderers = _current.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            _current.transform.position += _mount.position - bounds.center;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            _camera.transform.LookAt(_mount.position, Vector3.up);
            float aspect = _renderTexture != null && _renderTexture.height > 0
                ? (float)_renderTexture.width / _renderTexture.height
                : 1f;
            float horizontalRadius = Mathf.Sqrt(
                bounds.extents.x * bounds.extents.x + bounds.extents.z * bounds.extents.z);
            _baseOrthoSize = Mathf.Max(
                Mathf.Max(0.05f, bounds.extents.y),
                horizontalRadius / Mathf.Max(0.05f, aspect)) * FramingPadding;
            _camera.orthographicSize = _baseOrthoSize;
        }

        private void ApplyPreviewMaterials(GameObject root, float exposure)
        {
            float ambient = Mathf.Clamp(exposure, 0f, 0.4f);
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
                        clone = new Material(source) { name = source.name + "_WrenJournalPreview" };
                        Texture albedo = source.GetTexture("_BaseMap");
                        clone.SetColor("_BaseColor", new Color(1.08f, 1.08f, 1.08f, 1f));
                        if (albedo != null && ambient > 0f)
                        {
                            clone.SetTexture("_EmissionMap", albedo);
                            clone.SetColor("_EmissionColor", new Color(ambient, ambient, ambient, 1f));
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

        private void ClearCurrent()
        {
            DestroyAnimationGraph();
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

        private void DestroyAnimationGraph()
        {
            if (_animationGraph.IsValid()) _animationGraph.Destroy();
        }

        private void SetRigActive(bool active)
        {
            if (_rigRoot != null) _rigRoot.SetActive(active);
            if (_animationGraph.IsValid())
            {
                if (active) _animationGraph.Play();
                else _animationGraph.Stop();
            }
            if (_camera != null) _camera.enabled = active;
            if (_key != null) _key.enabled = active;
            if (_fill != null) _fill.enabled = active;
            if (_rim != null) _rim.enabled = active;
            if (_bounce != null) _bounce.enabled = active;
        }

        private void CacheImage()
        {
            if (_image == null) _image = GetComponent<RawImage>();
        }

        private static void DisableWorldBehavior(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
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
