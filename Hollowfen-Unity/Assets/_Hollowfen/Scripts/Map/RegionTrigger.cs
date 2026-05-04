using UnityEngine;

namespace Hollowfen.Map
{
    [RequireComponent(typeof(BoxCollider))]
    public class RegionTrigger : MonoBehaviour
    {
        [SerializeField] private string _regionId;
        [SerializeField] private int _priority;
        [SerializeField] private string _playerTag = "Player";

        public string RegionId => _regionId;
        public int Priority => _priority;

        private void Reset()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(20f, 10f, 20f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (string.IsNullOrEmpty(_regionId)) return;
            if (!other.CompareTag(_playerTag)) return;
            LocationRegistry.PushRegion(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (string.IsNullOrEmpty(_regionId)) return;
            if (!other.CompareTag(_playerTag)) return;
            LocationRegistry.PopRegion(this);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            var prev = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.15f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.7f);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = prev;
        }
    }
}
