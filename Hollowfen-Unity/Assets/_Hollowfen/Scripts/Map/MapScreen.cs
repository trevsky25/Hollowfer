using UnityEngine;

namespace Hollowfen.Map
{
    public class MapScreen : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private MapCamera _mapCamera;
        [SerializeField] private GameObject _miniMapRoot;
        [SerializeField] private bool _freezeTimeWhileOpen = true;

        private bool _isOpen;
        private float _previousTimeScale = 1f;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_root == null) _root = gameObject;
            SetActiveSilent(false);
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            SetActiveSilent(true);
            if (_miniMapRoot != null) _miniMapRoot.SetActive(false);
            if (_freezeTimeWhileOpen)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            SetActiveSilent(false);
            if (_miniMapRoot != null) _miniMapRoot.SetActive(true);
            if (_freezeTimeWhileOpen)
                Time.timeScale = _previousTimeScale;
        }

        private void SetActiveSilent(bool active)
        {
            if (_root != null && _root.activeSelf != active)
                _root.SetActive(active);
        }
    }
}
