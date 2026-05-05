using Hollowfen.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowfen.Foraging
{
    // Subscribes to Player/OpenInventory and toggles InventoryScreen. Mirrors MapInputBridge.
    public class InventoryInputBridge : MonoBehaviour
    {
        private InputActions _input;

        private void Awake() { _input = new InputActions(); }
        private void OnDestroy() { _input?.Dispose(); }

        private void OnEnable()
        {
            _input.Player.Enable();
            _input.Player.OpenInventory.performed += OnToggle;
        }

        private void OnDisable()
        {
            _input.Player.OpenInventory.performed -= OnToggle;
            _input.Player.Disable();
        }

        private void OnToggle(InputAction.CallbackContext _)
        {
            // Don't toggle the inventory while the inspect screen owns input — avoids stacking modal screens.
            if (InspectScreen.Instance != null && InspectScreen.Instance.IsOpen) return;
            if (InventoryScreen.Instance == null) return;
            if (InventoryScreen.Instance.IsOpen) InventoryScreen.Instance.Close();
            else InventoryScreen.Instance.Open();
        }
    }
}
