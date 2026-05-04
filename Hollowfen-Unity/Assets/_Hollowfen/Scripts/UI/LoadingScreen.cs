using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class LoadingScreen : UIScreen
    {
        [SerializeField] private Text _label;
        [SerializeField] private string _baseText = "Traveling to Hollowfen";
        [SerializeField] private float _dotInterval = 0.4f;

        private Coroutine _dotAnim;

        public override void OnOpen()
        {
            base.OnOpen();
            if (_dotAnim != null) StopCoroutine(_dotAnim);
            _dotAnim = StartCoroutine(AnimateDots());
        }

        public override void OnClose()
        {
            base.OnClose();
            if (_dotAnim != null) { StopCoroutine(_dotAnim); _dotAnim = null; }
            if (_label != null) _label.text = _baseText;
        }

        private IEnumerator AnimateDots()
        {
            int dots = 0;
            while (true)
            {
                if (_label != null) _label.text = _baseText + new string('.', dots);
                dots = (dots + 1) % 4;
                yield return new WaitForSecondsRealtime(_dotInterval);
            }
        }
    }
}
