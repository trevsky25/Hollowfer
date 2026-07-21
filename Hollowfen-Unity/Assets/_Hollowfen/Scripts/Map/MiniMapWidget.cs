using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.Map
{
    public class MiniMapWidget : MonoBehaviour
    {
        [SerializeField] private RawImage _mapImage;
        [SerializeField] private RectTransform _headingArrow;
        [SerializeField] private string _targetTag = "Player";
        [SerializeField] private Transform _target;
        [SerializeField] private bool _rotateMapWithPlayer;
        [Header("Baked map")]
        [SerializeField, Tooltip("Static overhead world image. When assigned, the expensive runtime minimap camera is disabled and this image is cropped around the player.")]
        private Texture2D _bakedMap;
        [SerializeField] private Rect _worldBounds = new Rect(0f, 0f, 500f, 500f);
        [SerializeField, Min(10f), Tooltip("Vertical world-space span visible in the minimap. 60 matches the previous orthographic camera size of 30.")]
        private float _viewWorldSize = 60f;

        public bool UsesBakedMap => _bakedMap != null;

        private void Awake()
        {
            if (_bakedMap == null) return;

            if (_mapImage != null)
                _mapImage.texture = _bakedMap;

            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                Vector3 position = terrain.GetPosition();
                Vector3 size = terrain.terrainData.size;
                _worldBounds = new Rect(position.x, position.z, size.x, size.z);
            }

            // The baked image supplies the same view without periodically rendering the complete
            // world a second time. Keep the legacy camera as a fallback for scenes with no bake.
            var miniMapCamera = FindAnyObjectByType<MiniMapCamera>(FindObjectsInactive.Include);
            if (miniMapCamera != null)
                miniMapCamera.UseBakedMap();
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
            float yaw = t.eulerAngles.y;

            if (_bakedMap != null && _mapImage != null &&
                _worldBounds.width > 0.01f && _worldBounds.height > 0.01f)
            {
                float viewWidth = Mathf.Clamp01(_viewWorldSize / _worldBounds.width);
                float viewHeight = Mathf.Clamp01(_viewWorldSize / _worldBounds.height);
                float centerX = Mathf.InverseLerp(_worldBounds.xMin, _worldBounds.xMax, t.position.x);
                float centerY = Mathf.InverseLerp(_worldBounds.yMin, _worldBounds.yMax, t.position.z);
                float x = Mathf.Clamp(centerX - viewWidth * 0.5f, 0f, 1f - viewWidth);
                float y = Mathf.Clamp(centerY - viewHeight * 0.5f, 0f, 1f - viewHeight);
                _mapImage.uvRect = new Rect(x, y, viewWidth, viewHeight);
            }

            if (_rotateMapWithPlayer && _mapImage != null)
            {
                var rt = _mapImage.rectTransform;
                rt.localRotation = Quaternion.Euler(0f, 0f, yaw);
                if (_headingArrow != null)
                    _headingArrow.localRotation = Quaternion.identity;
            }
            else
            {
                if (_mapImage != null)
                    _mapImage.rectTransform.localRotation = Quaternion.identity;
                if (_headingArrow != null)
                    _headingArrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            }
        }
    }
}
