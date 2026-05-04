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
        public GameObject DefaultSelected => _defaultSelected;
        public CanvasGroup CanvasGroup => _canvasGroup;
        public bool IsModal => _isModal;

        public virtual void OnOpen() { }
        public virtual void OnClose() { }

        public virtual void OnBack()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.Back();
        }
    }
}
