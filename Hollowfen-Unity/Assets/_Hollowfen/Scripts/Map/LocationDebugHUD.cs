using UnityEngine;

namespace Hollowfen.Map
{
    public class LocationDebugHUD : MonoBehaviour
    {
        [SerializeField] private string _playerTag = "Player";
        [SerializeField] private KeyCode _markNearestKey = KeyCode.F;
        [SerializeField] private float _maxMarkDistance = 8f;

        private Transform _player;
        private string _lastEvent = "";
        private float _lastEventTime = -10f;

        private void OnEnable()
        {
            LocationRegistry.LocationDiscovered += HandleDiscovered;
            LocationRegistry.RegionChanged += HandleRegion;
        }

        private void OnDisable()
        {
            LocationRegistry.LocationDiscovered -= HandleDiscovered;
            LocationRegistry.RegionChanged -= HandleRegion;
        }

        private void HandleDiscovered(string id)
        {
            _lastEvent = $"Discovered: {id}";
            _lastEventTime = Time.unscaledTime;
        }

        private void HandleRegion(string id)
        {
            _lastEvent = $"Region: {(string.IsNullOrEmpty(id) ? "(none)" : id)}";
            _lastEventTime = Time.unscaledTime;
        }

        private Transform ResolvePlayer()
        {
            if (_player != null) return _player;
            var go = GameObject.FindGameObjectWithTag(_playerTag);
            if (go != null) _player = go.transform;
            return _player;
        }

        private void Update()
        {
            if (!UnityEngine.Input.GetKeyDown(_markNearestKey)) return;
            var p = ResolvePlayer();
            if (p == null) return;
            var nearest = LocationRegistry.FindNearest(p.position, _maxMarkDistance);
            if (nearest != null)
                LocationRegistry.MarkDiscovered(nearest.Id);
        }

        private void OnGUI()
        {
            var p = ResolvePlayer();
            string region = string.IsNullOrEmpty(LocationRegistry.CurrentRegion) ? "(none)" : LocationRegistry.CurrentRegion;
            string nearestId = "(none)";
            float nearestDist = -1f;
            if (p != null)
            {
                var n = LocationRegistry.FindNearest(p.position);
                if (n != null)
                {
                    nearestId = n.Id;
                    nearestDist = Vector3.Distance(p.position, n.WorldPosition);
                }
            }

            GUI.Box(new Rect(10, 10, 360, 110), GUIContent.none);
            GUI.Label(new Rect(20, 16, 340, 22), $"Region: {region}");
            GUI.Label(new Rect(20, 38, 340, 22), $"Nearest: {nearestId}" + (nearestDist >= 0 ? $"  ({nearestDist:F1}m)" : ""));
            GUI.Label(new Rect(20, 60, 340, 22), $"Discovered: {LocationRegistry.DiscoveredCount} / {LocationRegistry.Markers.Count}");
            GUI.Label(new Rect(20, 82, 340, 22), $"[{_markNearestKey}] mark nearest within {_maxMarkDistance:F0}m");
            if (Time.unscaledTime - _lastEventTime < 3f)
                GUI.Label(new Rect(20, 104, 340, 22), _lastEvent);
        }
    }
}
