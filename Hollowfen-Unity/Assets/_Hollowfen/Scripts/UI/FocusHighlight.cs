using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Steam Deck-readable focus visual. Animates color, scale, and optional glow
    // when the GameObject becomes the EventSystem's selected object. Gamepad-first;
    // mouse hover routes through the same Select path so a single state drives both.
    //
    // Set the host Button's Transition to None so its built-in color animation
    // doesn't fight this component.
    public class FocusHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [Header("Targets")]
        [SerializeField] private Graphic _targetGraphic;
        [SerializeField] private RectTransform _scaleTarget;
        [SerializeField] private Graphic _glowGraphic;

        [Header("Focused state")]
        [SerializeField] private Color _focusedColor = new Color(0.835f, 0.749f, 0.471f, 1f);
        [SerializeField] private float _focusedScale = 1.08f;

        [Header("Behavior")]
        [SerializeField] private bool _swapColor = true;
        [SerializeField] private bool _swapScale = true;
        [SerializeField] private bool _underlineText;
        [SerializeField] private bool _selectOnHover = true;
        [SerializeField] private float _transitionDuration = 0.12f;

        private Color _baseColor = Color.white;
        private Vector3 _baseScale = Vector3.one;
        private float _baseGlowAlpha;
        private bool _isFocused;
        private float _currentT;
        private Coroutine _anim;
        private System.Reflection.PropertyInfo _textProp;
        private string _baseText;

        // Code-built screens add this component before its target graphics exist.
        // Configure re-captures the resting state after construction so callers no
        // longer need to mutate private fields through reflection.
        public void Configure(
            Graphic targetGraphic,
            RectTransform scaleTarget,
            Color focusedColor,
            float focusedScale = 1.04f,
            bool swapColor = true,
            bool swapScale = true,
            bool underlineText = false,
            Graphic glowGraphic = null)
        {
            _targetGraphic = targetGraphic;
            _scaleTarget = scaleTarget != null ? scaleTarget : transform as RectTransform;
            _focusedColor = focusedColor;
            _focusedScale = focusedScale;
            _swapColor = swapColor;
            _swapScale = swapScale;
            _underlineText = underlineText;
            _glowGraphic = glowGraphic;
            _baseColor = _targetGraphic != null ? _targetGraphic.color : Color.white;
            _baseScale = _scaleTarget != null ? _scaleTarget.localScale : Vector3.one;
            _baseGlowAlpha = _glowGraphic != null ? _glowGraphic.color.a : 0f;
            _textProp = null;
            _baseText = null;
            if (_underlineText && _targetGraphic != null)
            {
                _textProp = _targetGraphic.GetType().GetProperty("text");
                if (_textProp != null) _baseText = _textProp.GetValue(_targetGraphic, null) as string;
            }
            _isFocused = false;
            _currentT = 0f;
            ApplyState(0f);
            if (_glowGraphic != null) SetGlowAlpha(0f);
        }

        private void Awake()
        {
            if (_targetGraphic == null) _targetGraphic = GetComponent<Graphic>();
            if (_scaleTarget == null) _scaleTarget = transform as RectTransform;
            if (_targetGraphic != null) _baseColor = _targetGraphic.color;
            if (_scaleTarget != null) _baseScale = _scaleTarget.localScale;
            if (_glowGraphic != null)
            {
                _baseGlowAlpha = _glowGraphic.color.a;
                SetGlowAlpha(0f);
            }
            if (_underlineText && _targetGraphic != null)
            {
                _textProp = _targetGraphic.GetType().GetProperty("text");
                if (_textProp != null) _baseText = _textProp.GetValue(_targetGraphic, null) as string;

                // Ensure rich-text rendering is enabled so <u>...</u> doesn't render literally.
                // UI.Text uses supportRichText; TMP_Text uses richText.
                var t = _targetGraphic.GetType();
                var richProp = t.GetProperty("supportRichText") ?? t.GetProperty("richText");
                if (richProp != null && richProp.CanWrite) richProp.SetValue(_targetGraphic, true, null);
            }
        }

        private void OnDisable()
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = null;
            _isFocused = false;
            _currentT = 0f;
            ApplyState(0f);
        }

        public void OnSelect(BaseEventData eventData) => SetFocused(true);
        public void OnDeselect(BaseEventData eventData) => SetFocused(false);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_selectOnHover) return;
            var es = EventSystem.current;
            if (es == null) return;
            if (es.currentSelectedGameObject != gameObject)
                es.SetSelectedGameObject(gameObject);
        }

        private void SetFocused(bool focused)
        {
            if (_isFocused == focused) return;
            _isFocused = focused;
            ApplyUnderline(focused);
            if (_anim != null) StopCoroutine(_anim);
            if (!isActiveAndEnabled)
            {
                _currentT = focused ? 1f : 0f;
                ApplyState(_currentT);
                return;
            }
            _anim = StartCoroutine(AnimateTo(focused ? 1f : 0f));
        }

        private void ApplyUnderline(bool focused)
        {
            if (!_underlineText || _textProp == null || _baseText == null) return;
            // Strongly-typed path for UnityEngine.UI.Text — avoids any reflection edge cases.
            var legacy = _targetGraphic as UnityEngine.UI.Text;
            if (legacy != null)
            {
                if (!legacy.supportRichText) legacy.supportRichText = true;
            }
            else
            {
                var t = _targetGraphic.GetType();
                var richProp = t.GetProperty("supportRichText") ?? t.GetProperty("richText");
                if (richProp != null && richProp.CanWrite) richProp.SetValue(_targetGraphic, true, null);
            }
            var newText = focused ? "<u>" + _baseText + "</u>" : _baseText;
            _textProp.SetValue(_targetGraphic, newText, null);
        }

        private IEnumerator AnimateTo(float target)
        {
            if (_transitionDuration <= 0f)
            {
                _currentT = target;
                ApplyState(target);
                _anim = null;
                yield break;
            }
            float start = _currentT;
            float t = 0f;
            while (t < _transitionDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / _transitionDuration);
                _currentT = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, k));
                ApplyState(_currentT);
                yield return null;
            }
            _currentT = target;
            ApplyState(target);
            _anim = null;
        }

        private void ApplyState(float t)
        {
            if (_swapColor && _targetGraphic != null)
                _targetGraphic.color = Color.Lerp(_baseColor, _focusedColor, t);
            if (_swapScale && _scaleTarget != null)
                _scaleTarget.localScale = Vector3.Lerp(_baseScale, _baseScale * _focusedScale, t);
            if (_glowGraphic != null)
                SetGlowAlpha(Mathf.Lerp(0f, _baseGlowAlpha > 0.01f ? _baseGlowAlpha : 1f, t));
        }

        private void SetGlowAlpha(float a)
        {
            var c = _glowGraphic.color;
            c.a = a;
            _glowGraphic.color = c;
        }
    }
}
