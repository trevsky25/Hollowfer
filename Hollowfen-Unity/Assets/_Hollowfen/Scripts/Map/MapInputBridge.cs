using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowfen.Map
{
    public class MapInputBridge : MonoBehaviour
    {
        [SerializeField] private MapScreen _mapScreen;
        [SerializeField] private InputActionAsset _inputAsset;
        [SerializeField] private string _toggleActionPath = "Player/OpenMap";
        [SerializeField] private string _closeActionPath = "UI/Cancel";

        private InputAction _toggleAction;
        private InputAction _closeAction;

        private void OnEnable()
        {
            if (_inputAsset == null) return;
            _toggleAction = _inputAsset.FindAction(_toggleActionPath, false);
            _closeAction = _inputAsset.FindAction(_closeActionPath, false);
            if (_toggleAction != null)
            {
                _toggleAction.performed += OnToggle;
                _toggleAction.Enable();
            }
            if (_closeAction != null)
            {
                _closeAction.performed += OnClose;
                _closeAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (_toggleAction != null) _toggleAction.performed -= OnToggle;
            if (_closeAction != null) _closeAction.performed -= OnClose;
        }

        private void OnToggle(InputAction.CallbackContext _)
        {
            if (_mapScreen != null) _mapScreen.Toggle();
        }

        private void OnClose(InputAction.CallbackContext _)
        {
            if (_mapScreen != null && _mapScreen.IsOpen) _mapScreen.Close();
        }
    }
}
