using Hollowfen.Audio;
using UnityEngine;

namespace Hollowfen.Apothecary
{
    /// <summary>
    /// Drives the purchased rear chain gate directly so it can reverse safely at any point in
    /// its travel. The original showcase clips jump when interrupted; this keeps the authored
    /// gate, chain, and moving collider together while Wren passes beneath it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApothecaryChainDoor : MonoBehaviour
    {
        [SerializeField] private Transform _gate;
        [SerializeField] private Vector3 _closedLocalPosition;
        [SerializeField] private Vector3 _openLocalPosition = new Vector3(0f, 2.2f, 0f);
        [SerializeField, Min(.5f)] private float _openRadius = 3.85f;
        [SerializeField, Min(.5f)] private float _closeRadius = 5f;
        [SerializeField, Min(.1f)] private float _openSpeed = 2.25f;
        [SerializeField, Min(.1f)] private float _closeSpeed = 1.7f;
        [SerializeField, Min(.02f)] private float _playerPollSeconds = .10f;

        private Animator _showcaseAnimator;
        private Transform _player;
        private bool _targetOpen;
        private float _nextPlayerPoll;

        public Transform Gate => _gate;
        public float OpenRadius => _openRadius;
        public float CloseRadius => _closeRadius;
        public bool WantsOpen => _targetOpen;

        public void Configure(Transform gate, Vector3 closedLocalPosition,
            Vector3 openLocalPosition, float openRadius, float closeRadius,
            float openSpeed, float closeSpeed)
        {
            _gate = gate;
            _closedLocalPosition = closedLocalPosition;
            _openLocalPosition = openLocalPosition;
            _openRadius = Mathf.Max(.5f, openRadius);
            _closeRadius = Mathf.Max(_openRadius + .25f, closeRadius);
            _openSpeed = Mathf.Max(.1f, openSpeed);
            _closeSpeed = Mathf.Max(.1f, closeSpeed);
        }

        private void Awake()
        {
            _closeRadius = Mathf.Max(_closeRadius, _openRadius + .25f);
            _showcaseAnimator = GetComponent<Animator>();
            if (_showcaseAnimator != null) _showcaseAnimator.enabled = false;
            SetOpenInstant(false);
        }

        private void Update()
        {
            if (_gate == null) return;
            if (Time.unscaledTime >= _nextPlayerPoll)
            {
                _nextPlayerPoll = Time.unscaledTime + _playerPollSeconds;
                ResolvePlayer();
                RefreshTarget();
            }

            Vector3 target = _targetOpen ? _openLocalPosition : _closedLocalPosition;
            float speed = _targetOpen ? _openSpeed : _closeSpeed;
            _gate.localPosition = Vector3.MoveTowards(_gate.localPosition, target,
                speed * Time.unscaledDeltaTime);
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

        public void SetOpenInstant(bool open)
        {
            _targetOpen = open;
            if (_gate != null)
                _gate.localPosition = open ? _openLocalPosition : _closedLocalPosition;
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
