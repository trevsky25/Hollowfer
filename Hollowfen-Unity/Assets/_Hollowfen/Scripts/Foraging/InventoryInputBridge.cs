using Hollowfen.Input;
using Hollowfen.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowfen.Foraging
{
    // Owns the two journal-family gameplay shortcuts:
    // Inventory/Satchel toggles the provisions screen; Field Guide opens the mushroom journal.
    public class InventoryInputBridge : MonoBehaviour
    {
        private InputActions _input;

        private void Awake() { EnsureInput(); }
        private void OnDestroy() { _input?.Dispose(); _input = null; }

        private void OnEnable()
        {
            // Generated input wrappers are not serialized and can be cleared by a domain reload
            // while this scene object remains enabled. Always heal before binding callbacks.
            EnsureInput();
            _input.Player.Enable();
            _input.Player.OpenInventory.performed += OnToggle;
            _input.Player.OpenFieldGuide.performed += OnOpenFieldGuide;
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.Player.OpenInventory.performed -= OnToggle;
            _input.Player.OpenFieldGuide.performed -= OnOpenFieldGuide;
            _input.Player.Disable();
        }

        private void EnsureInput()
        {
            if (_input == null) _input = new InputActions();
        }

        private void OnToggle(InputAction.CallbackContext _)
        {
            // Don't toggle the inventory while the inspect screen owns input — avoids stacking modal screens.
            if (InspectScreen.Instance != null && InspectScreen.Instance.IsOpen) return;
            if (InventoryScreen.Instance == null) return;
            if (InventoryScreen.Instance.IsOpen) InventoryScreen.Instance.Close();
            else InventoryScreen.Instance.Open();
        }

        private void OnOpenFieldGuide(InputAction.CallbackContext _)
        {
            // D-pad Up also navigates menus. Only treat it as a journal shortcut from
            // unobstructed gameplay so it cannot steal focus inside Pause/Settings/etc.
            var manager = UIManager.Instance;
            if (manager == null || manager.HasOpenScreen) return;
            if (Time.timeScale <= 0f || PlayerInteractor.Suspended) return;
            if (InspectScreen.Instance != null && InspectScreen.Instance.IsOpen) return;
            if (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen) return;

            manager.OpenScreen("field-guide");
        }
    }
}
