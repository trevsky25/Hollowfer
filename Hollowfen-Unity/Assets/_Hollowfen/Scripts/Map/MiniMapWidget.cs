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
