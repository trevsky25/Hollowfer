using Hollowfen.Audio;
using UnityEngine;

namespace Hollowfen.Apothecary
{
    /// <summary>
    /// Reuses the purchased double-door leaves as a quiet automatic threshold. The entrance
    /// opens before Wren reaches its solid leaf colliders, stays open through a wider hysteresis
    /// radius, and only closes once she is safely clear on either side.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApothecaryProximityDoor : MonoBehaviour
    {
        [SerializeField] private Transform _leftLeaf;
        [SerializeField] private Transform _rightLeaf;
        [SerializeField] private Vector3 _leftClosedEuler = Vector3.zero;
        [SerializeField] private Vector3 _rightClosedEuler = Vector3.zero;
        [SerializeField] private Vector3 _leftOpenEuler = new Vector3(0f, 250f, 0f);
        [SerializeField] private Vector3 _rightOpenEuler = new Vector3(0f, 110f, 0f);
        [SerializeField, Min(.5f)] private float _openRadius = 4.25f;
        [SerializeField, Min(.5f)] private float _closeRadius = 5.35f;
        [SerializeField, Min(10f)] private float _degreesPerSecond = 125f;
        [SerializeField, Min(.02f)] private float _playerPollSeconds = .10f;

        private Transform _player;
        private bool _targetOpen;
        private float _nextPlayerPoll;

        public Transform LeftLeaf => _leftLeaf;
        public Transform RightLeaf => _rightLeaf;
        public float OpenRadius => _openRadius;
        public float CloseRadius => _closeRadius;
        public bool WantsOpen => _targetOpen;
        public Vector3 LeftClosedEuler => _leftClosedEuler;
        public Vector3 RightClosedEuler => _rightClosedEuler;
        public Vector3 LeftOpenEuler => _leftOpenEuler;
        public Vector3 RightOpenEuler => _rightOpenEuler;

        public void Configure(Transform leftLeaf, Transform rightLeaf,
            Vector3 leftClosedEuler, Vector3 rightClosedEuler,
            Vector3 leftOpenEuler, Vector3 rightOpenEuler,
            float openRadius, float closeRadius, float degreesPerSecond)
        {
            _leftLeaf = leftLeaf;
            _rightLeaf = rightLeaf;
            _leftClosedEuler = leftClosedEuler;
            _rightClosedEuler = rightClosedEuler;
            _leftOpenEuler = leftOpenEuler;
            _rightOpenEuler = rightOpenEuler;
            _openRadius = Mathf.Max(.5f, openRadius);
            _closeRadius = Mathf.Max(_openRadius + .25f, closeRadius);
            _degreesPerSecond = Mathf.Max(10f, degreesPerSecond);
        }

        private void Awake()
        {
            _closeRadius = Mathf.Max(_closeRadius, _openRadius + .25f);
            SetOpenInstant(false);
        }

        private void Update()
        {
            if (_leftLeaf == null || _rightLeaf == null) return;
            if (Time.unscaledTime >= _nextPlayerPoll)
            {
                _nextPlayerPoll = Time.unscaledTime + _playerPollSeconds;
                ResolvePlayer();
                RefreshTarget();
            }

            Quaternion leftTarget = Quaternion.Euler(_targetOpen
                ? _leftOpenEuler : _leftClosedEuler);
            Quaternion rightTarget = Quaternion.Euler(_targetOpen
                ? _rightOpenEuler : _rightClosedEuler);
            float step = _degreesPerSecond * Time.unscaledDeltaTime;
            _leftLeaf.localRotation = Quaternion.RotateTowards(
                _leftLeaf.localRotation, leftTarget, step);
            _rightLeaf.localRotation = Quaternion.RotateTowards(
                _rightLeaf.localRotation, rightTarget, step);
        }

        private void ResolvePlayer()
        {
            if (_player != null) return;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _player = player.transform;
        }

        private void RefreshTarget()
        {
            if (_player == null)
            {
                ChangeTarget(false);
                return;
            }

            Vector3 delta = _player.position - transform.position;
            delta.y = 0f;
            float threshold = _targetOpen ? _closeRadius : _openRadius;
            ChangeTarget(delta.sqrMagnitude <= threshold * threshold);
        }

        private void ChangeTarget(bool open)
        {
            if (_targetOpen == open) return;
            _targetOpen = open;
            GameplaySfx.DoorOpen();
        }

        /// <summary>Used by the deterministic traversal verifier and safe scene setup.</summary>
        public void SetOpenInstant(bool open)
        {
            _targetOpen = open;
            if (_leftLeaf != null)
                _leftLeaf.localRotation = Quaternion.Euler(open
                    ? _leftOpenEuler : _leftClosedEuler);
            if (_rightLeaf != null)
                _rightLeaf.localRotation = Quaternion.Euler(open
                    ? _rightOpenEuler : _rightClosedEuler);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.96f, .72f, .28f, .75f);
            Gizmos.DrawWireSphere(transform.position, _openRadius);
            Gizmos.color = new Color(.45f, .62f, .38f, .45f);
            Gizmos.DrawWireSphere(transform.position, _closeRadius);
        }
    }
}
