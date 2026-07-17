using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(AspectRatioFitter))]
    public class JournalArtPresenter : MonoBehaviour
    {
        private Image _image;
        private AspectRatioFitter _fitter;
        private GameObject _missingVisual;

        public RectTransform Frame => transform.parent as RectTransform;
        public Image Image => _image;

        private void Awake()
        {
            Cache();
        }

        public void Configure(bool cover)
        {
            Cache();
            _image.preserveAspect = false;
            _image.raycastTarget = false;
            _fitter.aspectMode = cover
                ? AspectRatioFitter.AspectMode.EnvelopeParent
                : AspectRatioFitter.AspectMode.FitInParent;
        }

        public void SetMissingVisual(GameObject missingVisual)
        {
            _missingVisual = missingVisual;
            if (_missingVisual != null) _missingVisual.SetActive(_image == null || _image.sprite == null);
        }

        public void SetSprite(Sprite sprite, Color missingColor)
        {
            Cache();
            _image.sprite = sprite;
            _fitter.aspectRatio = sprite != null && sprite.rect.height > 0f
                ? sprite.rect.width / sprite.rect.height
                : 1f;
            _image.color = sprite != null ? Color.white : missingColor;
            if (_missingVisual != null) _missingVisual.SetActive(sprite == null);
        }

        public void SetTint(Color tint)
        {
            Cache();
            _image.color = tint;
        }

        public static JournalArtPresenter Create(string name, Transform parent, bool cover, Color frameColor)
        {
            var frame = UICanvasUtil.NewRect(name, parent);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = frameColor;
            frameImage.raycastTarget = false;
            frame.gameObject.AddComponent<RectMask2D>();

            var art = UICanvasUtil.NewRect("Art", frame);
            UICanvasUtil.Stretch(art);
            var image = art.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            var presenter = art.gameObject.AddComponent<JournalArtPresenter>();
            presenter.Configure(cover);
            return presenter;
        }

        private void Cache()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_fitter == null) _fitter = GetComponent<AspectRatioFitter>();
        }
    }
}
