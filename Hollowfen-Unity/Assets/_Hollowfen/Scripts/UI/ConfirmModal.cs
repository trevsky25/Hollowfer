using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Reusable confirm/cancel dialog. One instance per UIManager (registered as
    // a UIScreen with IsModal=true so the underlying screen stays visible).
    //
    // Call site:
    //   ConfirmModal.Show("Delete Save?", "This cannot be undone.",
    //       onConfirm: () => SaveManager.DeleteSlot(slot));
    public class ConfirmModal : UIScreen
    {
        public static ConfirmModal Instance { get; private set; }

        [SerializeField] private Text _titleText;
        [SerializeField] private Text _messageText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        private Action _onConfirm;
        private Action _onCancel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_confirmButton != null) _confirmButton.onClick.AddListener(HandleConfirm);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(HandleCancel);

            // Lock D-pad navigation to the two modal buttons only — prevents focus
            // jumping to whatever Selectables are visible in the underlying screen.
            if (_cancelButton != null && _confirmButton != null)
            {
                _cancelButton.navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnRight = _confirmButton
                };
                _confirmButton.navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnLeft = _cancelButton
                };
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static bool Show(string title, string message, Action onConfirm, Action onCancel = null)
        {
            if (Instance == null)
            {
                Debug.LogError("[ConfirmModal] No instance registered with UIManager.");
                return false;
            }
            if (UIManager.Instance == null)
            {
                Debug.LogError("[ConfirmModal] UIManager.Instance missing.");
                return false;
            }
            Instance.Configure(title, message, onConfirm, onCancel);
            UIManager.Instance.OpenScreen(Instance.ScreenId);
            return true;
        }

        public void Configure(string title, string message, Action onConfirm, Action onCancel)
        {
            if (_titleText != null) _titleText.text = title;
            if (_messageText != null) _messageText.text = message;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
        }

        public override void OnBack() => HandleCancel();

        private void HandleConfirm()
        {
            var cb = _onConfirm;
            _onConfirm = null;
            _onCancel = null;
            cb?.Invoke();
            UIManager.Instance?.Back();
        }

        private void HandleCancel()
        {
            var cb = _onCancel;
            _onConfirm = null;
            _onCancel = null;
            cb?.Invoke();
            UIManager.Instance?.Back();
        }
    }
}
