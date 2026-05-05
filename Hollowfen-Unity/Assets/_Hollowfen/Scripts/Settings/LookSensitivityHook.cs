using StarterAssets;
using UnityEngine;

namespace Hollowfen.Settings
{
    // Runs after Update so input events for the current frame have arrived,
    // and before LateUpdate (default order 0) so ThirdPersonController.CameraRotation
    // sees the scaled value. Scales StarterAssetsInputs.look in place each frame.
    [DefaultExecutionOrder(-100)]
    public class LookSensitivityHook : MonoBehaviour
    {
        [SerializeField] private StarterAssetsInputs _inputs;

        private void Awake()
        {
            if (_inputs == null) _inputs = GetComponent<StarterAssetsInputs>();
        }

        private void LateUpdate()
        {
            if (_inputs == null) return;
            _inputs.look *= GameSettings.LookSensitivity;
        }
    }
}
