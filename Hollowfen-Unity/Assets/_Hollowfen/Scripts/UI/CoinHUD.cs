using Hollowfen.Items;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Small parchment pill in the bottom-left HUD corner showing Wren's purse ("3s 2c").
    // Hidden until she's earned her first coin — coins don't exist in Hollowfen's story
    // until Marra's kitchen sells a bowl. Builds itself programmatically like the other HUD bits.
    public class CoinHUD : MonoBehaviour
    {
        [SerializeField] private Sprite _parchmentSprite;

        private GameObject _pill;
        private TMP_Text _label;
        private TMP_Text _hint;
        private Button _button;
        private CanvasGroup _sourceHudGroup;
        private CanvasGroup _pillGroup;
        private InputAction _openPurse;
        private bool _built;

        private void OnEnable()
        {
            CoinPurse.OnChanged += HandleChanged;
            EnsureInput();
            _openPurse.Enable();
        }

        private void OnDisable()
        {
            CoinPurse.OnChanged -= HandleChanged;
            _openPurse?.Disable();
        }

        private void OnDestroy()
        {
            if (_openPurse != null)
            {
                _openPurse.performed -= OnOpenPurse;
                _openPurse.Dispose();
                _openPurse = null;
            }
        }

        private void Start()
        {
            Refresh(CoinPurse.TotalCopper);
        }

        private void HandleChanged(int totalCopper) => Refresh(totalCopper);

        private void Update()
        {
            if (_pillGroup == null) return;

            // CoinHUD lives under the deliberately passive HUD CanvasGroup. This one pill opts
            // out so it can be a real Button, then mirrors the parent's presentation state.
            float sourceAlpha = _sourceHudGroup != null ? _sourceHudGroup.alpha : 1f;
            _pillGroup.alpha = sourceAlpha;
            bool pointerAvailable = sourceAlpha > 0.95f && Cursor.visible &&
                (UIManager.Instance == null || !UIManager.Instance.HasOpenScreen);
            _pillGroup.interactable = pointerAvailable;
            _pillGroup.blocksRaycasts = pointerAvailable;

            if (_hint != null)
            {
                string next = Localization.Get(ControllerGlyphs.IsGamepadActive
                    ? "hud.purse.gamepad" : "hud.purse.keyboard");
                if (_hint.text != next) _hint.text = next;
            }
        }

        private void EnsureInput()
        {
            if (_openPurse != null) return;
            _openPurse = new InputAction("Open Wren's Purse", InputActionType.Button);
            _openPurse.AddBinding("<Keyboard>/p");
            _openPurse.AddBinding("<Gamepad>/leftStickPress");
            _openPurse.performed += OnOpenPurse;
        }

        private void OnOpenPurse(InputAction.CallbackContext _) => PurseScreen.ToggleFromHud();

        private void Refresh(int totalCopper)
        {
            if (totalCopper <= 0)
            {
                if (_pill != null) _pill.SetActive(false);
                return;
            }
            BuildIfNeeded();
            _pill.SetActive(true);
            _label.text = CoinPurse.Format(totalCopper);
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            var canvasRT = (RectTransform)transform;
            _sourceHudGroup = GetComponentInParent<CanvasGroup>();
            _pill = new GameObject("CoinPill", typeof(RectTransform));
            _pill.transform.SetParent(canvasRT, false);
            var rt = (RectTransform)_pill.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(274f, 48f);
            rt.anchoredPosition = new Vector2(24f, 24f);

            var bg = _pill.AddComponent<Image>();
            bg.sprite = UICanvasUtil.RoundedRect(24);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.07f, 0.06f, 0.045f, 0.72f);
            bg.raycastTarget = true;

            _button = _pill.AddComponent<Button>();
            _button.transition = Selectable.Transition.None;
            _button.targetGraphic = bg;
            _button.onClick.AddListener(PurseScreen.ToggleFromHud);

            _pillGroup = _pill.AddComponent<CanvasGroup>();
            _pillGroup.ignoreParentGroups = true;

            var rim = UICanvasUtil.NewImage("Rim", rt, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.32f), false);
            var rimImg = rim.GetComponent<Image>();
            rimImg.sprite = UICanvasUtil.RoundedOutline(24, 1.6f);
            rimImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)rim.transform);

            var glyph = UICanvasUtil.NewHeading("Glyph", rt, "<sprite name=\"coin\">", 20f, HollowfenPalette.Gold, FontStyles.Normal, TextAlignmentOptions.Center); // batch-48: ◉ had no font glyph
            var gRT = glyph.rectTransform;
            gRT.anchorMin = new Vector2(0f, 0f); gRT.anchorMax = new Vector2(0f, 1f);
            gRT.pivot = new Vector2(0f, 0.5f);
            gRT.sizeDelta = new Vector2(36f, 0f);
            gRT.anchoredPosition = new Vector2(8f, 0f);

            _label = UICanvasUtil.NewHeading("Amount", rt, "", 24f, HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            var lRT = _label.rectTransform;
            lRT.anchorMin = new Vector2(0f, 0f); lRT.anchorMax = new Vector2(1f, 1f);
            lRT.pivot = new Vector2(0.5f, 0.5f);
            lRT.offsetMin = new Vector2(48f, 0f);
            lRT.offsetMax = new Vector2(-124f, 0f);

            var divider = UICanvasUtil.NewImage("Divider", rt,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.22f), false);
            var dRT = (RectTransform)divider.transform;
            dRT.anchorMin = new Vector2(1f, 0.5f); dRT.anchorMax = new Vector2(1f, 0.5f);
            dRT.pivot = new Vector2(1f, 0.5f);
            dRT.sizeDelta = new Vector2(1f, 24f);
            dRT.anchoredPosition = new Vector2(-112f, 0f);

            _hint = UICanvasUtil.NewEyebrow("PurseHint", rt, Localization.Get("hud.purse.keyboard"), 18f,
                HollowfenPalette.Gold, TextAlignmentOptions.Center);
            var hRT = _hint.rectTransform;
            hRT.anchorMin = new Vector2(1f, 0f); hRT.anchorMax = new Vector2(1f, 1f);
            hRT.pivot = new Vector2(1f, 0.5f);
            hRT.sizeDelta = new Vector2(104f, 0f);
            hRT.anchoredPosition = new Vector2(-6f, 0f);

            var focus = _pill.AddComponent<FocusHighlight>();
            focus.Configure(bg, rt, new Color(0.12f, 0.11f, 0.075f, 0.94f), 1.025f);
        }
    }
}
