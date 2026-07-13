using Hollowfen.Foraging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    [DisallowMultipleComponent]
    public class InteractionPromptHUD : MonoBehaviour
    {
        [SerializeField, Tooltip("Y offset above the bottom edge, reference 1080.")]
        private float _bottomOffset = 80f;

        [SerializeField, Tooltip("Pill size in reference pixels.")]
        private Vector2 _pillSize = new Vector2(560f, 60f);

        [SerializeField, Range(0f, 1f), Tooltip("Seconds to fade in/out.")]
        private float _fadeSeconds = 0.12f;

        [SerializeField] private string _keyboardGlyph = "[E]";
        // The pad glyph is resolved per-device via ControllerGlyphs (batch-48) — PS pads get
        // the real shape icon sprite, Xbox/Switch their letters. (The old serialized "[△]"
        // rendered as a missing-glyph box.)

        private CanvasGroup _group;
        private TMP_Text _label;
        private float _targetAlpha;
        private bool _built;

        private void Awake()
        {
            BuildIfNeeded();
            _group.alpha = 0f;
            _targetAlpha = 0f;
        }

        private void OnEnable()
        {
            BuildIfNeeded();
            PlayerInteractor.OnFocusChanged += HandleFocus;
            HandleFocus(PlayerInteractor.Current);
        }

        private void OnDisable()
        {
            PlayerInteractor.OnFocusChanged -= HandleFocus;
            _targetAlpha = 0f;
            if (_group != null) _group.alpha = 0f;
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            // Wipe any scene-serialized leftovers from older HUD iterations — a stale sprite-less
            // "Bg" rectangle was rendering as a SQUARE box behind the rounded pill (batch-47 fix).
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, _bottomOffset);
            rt.sizeDelta = _pillSize;

            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Rounded ink-glass pill with hairline stroke
            int radius = Mathf.RoundToInt(_pillSize.y * 0.5f);
            var bgGO = UICanvasUtil.NewImage("Bg", transform, new Color(0.07f, 0.06f, 0.045f, 0.74f), false);
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.sprite = UICanvasUtil.RoundedRect(radius);
            bgImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)bgGO.transform);

            var rule = UICanvasUtil.NewImage("Hairline", transform, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.32f), false);
            var ruleImg = rule.GetComponent<Image>();
            ruleImg.sprite = UICanvasUtil.RoundedOutline(radius, 1.6f);
            ruleImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)rule.transform);

            // Label — keyboard glyph + slash + gamepad glyph + verb + target
            _label = UICanvasUtil.NewBody("Label", transform, "", 22f, HollowfenPalette.Cream,
                TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.Center);
            var lblRT = _label.rectTransform;
            UICanvasUtil.Stretch(lblRT);
            lblRT.offsetMin = new Vector2(20f, 0f);
            lblRT.offsetMax = new Vector2(-20f, -2f);
            _label.enableWordWrapping = false;
            _label.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void HandleFocus(IInteractable focus)
        {
            if (focus == null)
            {
                _targetAlpha = 0f;
                return;
            }
            string verb = Hollowfen.Localization.Get(focus.PromptVerb);
            string target = focus.PromptTarget;
            // Use rich-text color tag on the gold glyph cluster for a tiny lift over cream body.
            // Interact = Player/Interact (buttonNorth) → brand icon/letter when a pad is present.
            string padGlyph = UnityEngine.InputSystem.Gamepad.current != null
                ? " / " + ControllerGlyphs.For(ControllerGlyphs.Face.North)
                : "";
            string glyphs = $"<color=#{ColorUtility.ToHtmlStringRGB(HollowfenPalette.Gold)}>{_keyboardGlyph}{padGlyph}</color>";
            _label.text = $"{glyphs}  {verb} {target}";
            _targetAlpha = 1f;
        }

        private void Update()
        {
            if (_group == null) return;
            if (Mathf.Approximately(_group.alpha, _targetAlpha)) return;
            float speed = _fadeSeconds <= 0f ? 999f : 1f / _fadeSeconds;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Time.unscaledDeltaTime * speed);
        }
    }
}
