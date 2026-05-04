using UnityEngine;

namespace Hollowfen.UI
{
    public class UIScreen : MonoBehaviour
    {
        [SerializeField] private string _screenId;
        [SerializeField] private GameObject _defaultSelected;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private bool _isModal;

        public string ScreenId => _screenId;
        public virtual GameObject DefaultSelected => _defaultSelected;
        public CanvasGroup CanvasGroup => _canvasGroup;
        public bool IsModal => _isModal;

        private bool _initialized;

        protected virtual void Awake()
        {
            EnsureInitialized();
        }

        // Called once before the screen is first deactivated by UIManager,
        // so subclasses can wire button onClicks and singleton state without
        // depending on Unity's Awake-pump timing.
        public void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            OnInitialize();
        }

        protected virtual void OnInitialize() { }

        public virtual void OnOpen() { }
        public virtual void OnClose() { }

        public virtual void OnBack()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.Back();
        }
    }
}
