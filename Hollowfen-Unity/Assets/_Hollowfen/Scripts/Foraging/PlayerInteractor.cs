using System;
using Hollowfen.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowfen.Foraging
{
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private LayerMask _foragingMask;
        [SerializeField] private float _searchRadius = 3f;
        [SerializeField] private Vector3 _searchOffset = new Vector3(0f, 0.9f, 0f);
        [SerializeField, Tooltip("dot(player.forward, dir-to-target) >= this. -0.2 = ~100° front cone")]
        private float _facingThreshold = -0.2f;

        public static event Action<IInteractable> OnFocusChanged;
        public static IInteractable Current { get; private set; }
        // Set by screens that fully own input (e.g., InspectScreen) to pause focus search + interact.
        public static bool Suspended { get; set; }

        private InputActions _input;
        private readonly Collider[] _hits = new Collider[16];

        private void Awake()
        {
            _input = new InputActions();
            if (_foragingMask.value == 0)
            {
                int layer = LayerMask.NameToLayer("Foraging");
                if (layer >= 0) _foragingMask = 1 << layer;
            }
        }

        private void OnEnable()
        {
            _input.Player.Enable();
            _input.Player.Interact.performed += OnInteract;
        }

        private void OnDisable()
        {
            _input.Player.Interact.performed -= OnInteract;
            _input.Player.Disable();
            SetFocus(null);
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        private void Update()
        {
            if (Suspended) { SetFocus(null); return; }
            Vector3 origin = transform.position + transform.TransformVector(_searchOffset);
            int count = Physics.OverlapSphereNonAlloc(origin, _searchRadius, _hits, _foragingMask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestSqr = float.MaxValue;
            Vector3 fwd = transform.forward;

            for (int i = 0; i < count; i++)
            {
                var col = _hits[i];
                if (col == null) continue;

                var node = col.GetComponentInParent<IInteractable>();
                if (node == null || !node.CanInteract(gameObject)) continue;

                Vector3 to = col.bounds.center - origin;
                float sqr = to.sqrMagnitude;
                Vector3 toFlat = new Vector3(to.x, 0f, to.z);
                if (toFlat.sqrMagnitude > 0.0001f)
                {
                    float dot = Vector3.Dot(new Vector3(fwd.x, 0f, fwd.z).normalized, toFlat.normalized);
                    if (dot < _facingThreshold) continue;
                }

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = node;
                }
            }

            SetFocus(best);
        }

        private void SetFocus(IInteractable next)
        {
            if (ReferenceEquals(next, Current)) return;
            Current = next;
            OnFocusChanged?.Invoke(next);
        }

        private void OnInteract(InputAction.CallbackContext ctx)
        {
            if (Suspended || Current == null) return;
            Current.Interact(gameObject);
            SetFocus(null);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 origin = transform.position + transform.TransformVector(_searchOffset);
            Gizmos.DrawWireSphere(origin, _searchRadius);
        }
    }
}
