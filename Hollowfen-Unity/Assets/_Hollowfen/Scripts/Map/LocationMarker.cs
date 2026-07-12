using UnityEngine;

namespace Hollowfen.Map
{
    [DisallowMultipleComponent]
    public class LocationMarker : MonoBehaviour
    {
        [SerializeField] private LocationData _data;
        [SerializeField, Tooltip("Walking within this range discovers the location (names it on the map). 0 = never auto-discover.")]
        private float _discoverRadius = 20f;

        private static Transform _player;
        private float _nextCheck;

        public LocationData Data => _data;
        public string Id => _data != null ? _data.Id : null;
        public Vector3 WorldPosition => transform.position;

        private void OnEnable()
        {
            if (_data == null) return;
            LocationRegistry.RegisterMarker(this);
        }

        private void OnDisable()
        {
            if (_data == null) return;
            LocationRegistry.UnregisterMarker(this);
        }

        private void Update()
        {
            // Proximity discovery, throttled — 8 markers × 2 checks/sec is free.
            // (Don't disable the component once discovered: OnDisable would unregister the marker.)
            if (_discoverRadius <= 0f || _data == null) return;
            if (LocationRegistry.IsDiscovered(Id)) return;
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.5f;

            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go == null) return;
                _player = go.transform;
            }
            Vector3 d = _player.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= _discoverRadius * _discoverRadius)
            {
                LocationRegistry.MarkDiscovered(Id);
                Debug.Log("[Location] Discovered: " + Id);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = LocationRegistry.IsDiscovered(Id) ? new Color(1f, 0.85f, 0.3f, 0.9f) : new Color(0.6f, 0.7f, 0.6f, 0.7f);
            Gizmos.DrawSphere(transform.position + Vector3.up * 1.5f, 0.5f);
            Gizmos.color = new Color(0f, 0f, 0f, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
        }
    }
}
