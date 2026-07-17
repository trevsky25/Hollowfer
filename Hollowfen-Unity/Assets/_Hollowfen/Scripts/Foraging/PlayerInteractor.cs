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

        // External "back out of zoom" shortcut: dismiss the current target and refuse to re-focus on it
        // until the player walks out of its trigger and back. Cleared when OverlapSphere stops returning it.
        public static void DismissCurrent()
        {
            if (Current == null) return;
            _suppressed = Current;
            SetFocusStatic(null);
        }

        private static IInteractable _suppressed;

        // Toggles Wren's StarterAssets PlayerInput. Used by modal screens (InspectScreen, InventoryScreen)
        // and the harvest cinematic to block Player/Jump (Space/South) from firing while UI/Submit is bound
        // to the same physical input. Without this, pressing Cross to confirm Forage also queues a jump
        // that fires when timeScale resumes — Wren visibly hops after every harvest.
        public static void SetPlayerInputEnabled(bool enabled)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            var pi = go.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (pi != null) pi.enabled = enabled;
        }

        private InputActions _input;
        private readonly Collider[] _hits = new Collider[16];

        private void Awake()
        {
            EnsureInput();
            if (_foragingMask.value == 0)
            {
                int layer = LayerMask.NameToLayer("Foraging");
                if (layer >= 0) _foragingMask = 1 << layer;
            }
        }

        private void OnEnable()
        {
            // Non-serialized generated input wrappers are cleared by a domain reload while
            // scene objects survive. Recreate the wrapper here as well as in Awake so UI and
            // interaction hotkeys cannot die after a script recompile in Play Mode.
            EnsureInput();
            _input.Player.Enable();
            _input.Player.Interact.performed += OnInteract;
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.Player.Interact.performed -= OnInteract;
                _input.Player.Disable();
            }
            SetFocus(null);
        }

        private void OnDestroy()
        {
            _input?.Dispose();
            _input = null;
        }

        private void EnsureInput()
        {
            if (_input == null) _input = new InputActions();
        }

        private void Update()
        {
            if (Suspended) { SetFocus(null); return; }
            Vector3 origin = transform.position + transform.TransformVector(_searchOffset);
            int count = Physics.OverlapSphereNonAlloc(origin, _searchRadius, _hits, _foragingMask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestSqr = float.MaxValue;
            Vector3 fwd = transform.forward;
            bool suppressedStillVisible = false;

            for (int i = 0; i < count; i++)
            {
                var col = _hits[i];
                if (col == null) continue;

                var node = col.GetComponentInParent<IInteractable>();
                if (node == null || !node.CanInteract(gameObject)) continue;

                // Track whether the dismissed target is still in range — only clear suppression once she walks out.
                if (_suppressed != null && ReferenceEquals(node, _suppressed)) { suppressedStillVisible = true; continue; }

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

            if (!suppressedStillVisible) _suppressed = null;

            SetFocus(best);
        }

        private void SetFocus(IInteractable next) => SetFocusStatic(next);

        private static void SetFocusStatic(IInteractable next)
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
