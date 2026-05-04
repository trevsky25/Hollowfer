using UnityEngine;

namespace Hollowfen.Map
{
    [DisallowMultipleComponent]
    public class LocationMarker : MonoBehaviour
    {
        [SerializeField] private LocationData _data;

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

        private void OnDrawGizmos()
        {
            Gizmos.color = LocationRegistry.IsDiscovered(Id) ? new Color(1f, 0.85f, 0.3f, 0.9f) : new Color(0.6f, 0.7f, 0.6f, 0.7f);
            Gizmos.DrawSphere(transform.position + Vector3.up * 1.5f, 0.5f);
            Gizmos.color = new Color(0f, 0f, 0f, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
        }
    }
}
