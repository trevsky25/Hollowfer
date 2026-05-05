using Hollowfen.Input;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowfen.Foraging
{
    // Cinematic "discovery shot" — when the PlayerInteractor focuses a mushroom, this Cinemachine camera
    // raises its priority above the third-person follow cam, framing the mushroom as the hero of the shot.
    // CinemachineBrain handles the blend automatically.
    //
    // The cam follows the mushroom (so it stays anchored as Wren walks past), with a fixed offset that
    // sits the camera above and slightly off-axis from the mushroom. Aim is a hard look-at on the mushroom.
    [RequireComponent(typeof(CinemachineCamera))]
    public class MushroomFocusCamera : MonoBehaviour
    {
        public static MushroomFocusCamera Instance { get; private set; }

        // Set true while the harvest cinematic owns the camera — orbit input is suppressed and the
        // cinematic coroutine writes FollowOffset directly.
        public bool IsHarvestCinematicActive { get; set; }
        public CinemachineFollow Follow => _follow;
        public Vector3 BaseOffset => _baseOffset;
        [SerializeField, Tooltip("Priority while no mushroom is focused — must be lower than the player follow cam.")]
        private int _idlePriority = 5;
        [SerializeField, Tooltip("Priority while a mushroom is focused — higher than the player follow cam (typically 10).")]
        private int _activePriority = 20;

        [SerializeField, Tooltip("Camera position offset from the focused mushroom (world space).")]
        private Vector3 _focusOffset = new Vector3(0.6f, 0.55f, -1.1f);

        [SerializeField, Tooltip("Field of view while focused. Lower = tighter zoom-in.")]
        private float _focusFOV = 32f;

        [SerializeField, Tooltip("Cinemachine blend time (seconds) overrides the brain's default for this transition.")]
        private float _blendSeconds = 0.55f;

        [Header("Manual orbit while zoomed")]
        [SerializeField, Tooltip("Right-stick yaw rotation: degrees per second at full deflection.")]
        private float _gamepadYawSpeed = 90f;
        [SerializeField, Tooltip("Right-stick pitch rotation: degrees per second at full deflection.")]
        private float _gamepadPitchSpeed = 60f;
        [SerializeField, Tooltip("Mouse RMB drag yaw: degrees per pixel.")]
        private float _mouseYawSpeed = 0.30f;
        [SerializeField, Tooltip("Mouse RMB drag pitch: degrees per pixel.")]
        private float _mousePitchSpeed = 0.20f;
        [SerializeField] private float _pitchMin = -10f;
        [SerializeField] private float _pitchMax = 60f;

        private CinemachineCamera _cam;
        private CinemachineFollow _follow;
        private Transform _focusedTarget;
        private Vector3 _baseOffset;
        private float _orbitYaw;
        private float _orbitPitch;
        private InputActions _input;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _cam = GetComponent<CinemachineCamera>();
            _follow = GetComponent<CinemachineFollow>();
            _baseOffset = _focusOffset;

            // Apply tunables in case the GameObject was authored programmatically without inspector values
            if (_follow != null) _follow.FollowOffset = _baseOffset;
            var lens = _cam.Lens;
            lens.FieldOfView = _focusFOV;
            _cam.Lens = lens;
            _cam.Priority = _idlePriority;

            // Pull the brain's default blend down to ours so the zoom feels snappy.
            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            if (brain != null)
            {
                brain.DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.EaseInOut, _blendSeconds);
            }

            _input = new InputActions();
        }

        private void OnEnable()
        {
            PlayerInteractor.OnFocusChanged += HandleFocusChanged;
            HandleFocusChanged(PlayerInteractor.Current);
            if (_input != null)
            {
                _input.UI.Enable();
                _input.UI.Cancel.performed += OnCancel;
            }
        }

        private void OnDisable()
        {
            PlayerInteractor.OnFocusChanged -= HandleFocusChanged;
            if (_input != null)
            {
                _input.UI.Cancel.performed -= OnCancel;
                _input.UI.Disable();
            }
            ClearFocus();
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        private void HandleFocusChanged(IInteractable focus)
        {
            // Cast to MonoBehaviour to grab the transform — every concrete IInteractable today is a MB.
            var mb = focus as MonoBehaviour;
            if (mb == null)
            {
                ClearFocus();
                return;
            }
            _focusedTarget = mb.transform;
            _cam.Follow = _focusedTarget;
            _cam.LookAt = _focusedTarget;
            _cam.Priority = _activePriority;
            // Reset orbit so each new target starts from the configured hero shot
            _orbitYaw = 0f;
            _orbitPitch = 0f;
            if (_follow != null) _follow.FollowOffset = _baseOffset;
        }

        private void ClearFocus()
        {
            _focusedTarget = null;
            if (_cam != null) _cam.Priority = _idlePriority;
        }

        // Public version for the harvest cinematic — clears the cam target before the mushroom
        // GameObject is deactivated, preventing a 1-frame "tracking inactive transform" warning.
        public void ReleaseFocus()
        {
            ClearFocus();
        }

        // While zoomed, allow the player to orbit the camera around the mushroom.
        // Right stick (gamepad) or RMB drag (mouse) feeds yaw + clamped pitch.
        // Inactive when no focus, when InspectScreen is open, or when InventoryScreen is open.
        private void Update()
        {
            if (_focusedTarget == null || _follow == null) return;
            if (InspectScreen.Instance != null && InspectScreen.Instance.IsOpen) return;
            if (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen) return;
            if (IsHarvestCinematicActive) return; // cinematic owns FollowOffset

            float dt = Time.deltaTime;
            float yawDelta = 0f, pitchDelta = 0f;

            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 rs = pad.rightStick.ReadValue();
                if (rs.sqrMagnitude > 0.0025f)
                {
                    yawDelta   += rs.x * _gamepadYawSpeed * dt;
                    pitchDelta -= rs.y * _gamepadPitchSpeed * dt;
                }
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                if (d.sqrMagnitude > 0.01f)
                {
                    yawDelta   += d.x * _mouseYawSpeed;
                    pitchDelta -= d.y * _mousePitchSpeed;
                }
            }

            if (Mathf.Abs(yawDelta) < 0.0001f && Mathf.Abs(pitchDelta) < 0.0001f) return;

            _orbitYaw += yawDelta;
            _orbitPitch = Mathf.Clamp(_orbitPitch + pitchDelta, _pitchMin, _pitchMax);
            // Apply yaw first (around world Y), then pitch on the resulting orbit axis.
            Quaternion yawRot   = Quaternion.AngleAxis(_orbitYaw, Vector3.up);
            Quaternion pitchRot = Quaternion.AngleAxis(_orbitPitch, yawRot * Vector3.right);
            _follow.FollowOffset = (pitchRot * yawRot) * _baseOffset;
        }

        // Circle / Esc backs out of the cinematic when the inspect/inventory screens aren't already
        // claiming Cancel. Suppresses re-focus on the same mushroom until the player walks out and back.
        private void OnCancel(InputAction.CallbackContext _)
        {
            if (_focusedTarget == null) return;
            if (InspectScreen.Instance != null && InspectScreen.Instance.IsOpen) return;
            if (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen) return;
            PlayerInteractor.DismissCurrent();
        }
    }
}
