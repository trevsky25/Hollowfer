using TMPro;
using Hollowfen.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Wren's pause journal. Builds its UI programmatically (same convention as the menu
    // pages): dim scrim over frozen gameplay, rounded parchment card with a double gold rule,
    // and serif rows. The legacy prefab's authored children are torn down on
    // first init — the prefab only contributes the UIScreen plumbing.
    public class PauseScreen : UIScreen
    {
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _storyButton;
        [SerializeField] private Button _fieldGuideButton;
        [SerializeField] private Button _purseButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _saveGameButton;
        [SerializeField] private Button _quitButton;

        private TMP_Text _saveLabel;
        private TMP_Text _purseLabel;
        private bool _builtRefined;

        public override GameObject DefaultSelected =>
            _resumeButton != null ? _resumeButton.gameObject : base.DefaultSelected;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            BuildRefined();
            if (_resumeButton     != null) _resumeButton.onClick.AddListener(OnResume);
            if (_storyButton      != null) _storyButton.onClick.AddListener(OnStory);
            if (_fieldGuideButton != null) _fieldGuideButton.onClick.AddListener(OnFieldGuide);
            if (_purseButton      != null) _purseButton.onClick.AddListener(OnPurse);
            if (_settingsButton   != null) _settingsButton.onClick.AddListener(OnSettings);
            if (_saveGameButton   != null) _saveGameButton.onClick.AddListener(OnSaveGame);
            if (_quitButton       != null) _quitButton.onClick.AddListener(OnQuit);
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Time.timeScale = 0f;
            if (_saveLabel != null) _saveLabel.text = Localization.Get("ui.pause.save");
            if (_purseLabel != null)
                _purseLabel.text = string.Format(Localization.Get("ui.pause.purse_balance"),
                    CoinPurse.Format(CoinPurse.TotalCopper));
        }

        public override void OnClose()
        {
            base.OnClose();
            Time.timeScale = 1f;
        }

        private void OnResume()
        {
            if (UIManager.Instance != null) UIManager.Instance.Back();
        }

        private void OnStory()
        {
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("story");
        }

        private void OnFieldGuide()
        {
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("field-guide");
        }

        private void OnPurse()
        {
            PurseScreen.OpenFromMenu();
        }

        private void OnSettings()
        {
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("settings");
        }

        private void OnSaveGame()
        {
            Hollowfen.Save.SaveCoordinator.SaveAllWithPlayer();
            Debug.Log($"[Pause] Saved to slot {Hollowfen.Save.SaveManager.ActiveSlot}");
            if (_saveLabel != null) _saveLabel.text = "Saved <sprite name=\"ui_check\">"; // batch-48: ✓ had no font glyph
        }

        private const string MainMenuSceneName = "Scene_MainMenu";

        private void OnQuit()
        {
            ConfirmModal.Show(
                title:   Localization.Get("ui.pause.quit_title"),
                message: Localization.Get("ui.pause.quit_message"),
                onConfirm: () =>
                {
                    Time.timeScale = 1f; // restore before scene load so the next scene starts unpaused
                    if (UIManager.Instance != null)
                        UIManager.Instance.LoadSceneAndOpen(MainMenuSceneName, "main-menu");
                });
        }

        // ----------------- UI BUILDER -----------------

        private void BuildRefined()
        {
            if (_builtRefined) return;
            _builtRefined = true;

            // The prefab root is a plain Transform host; the UI lives under its Canvas child.
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                Debug.LogError("[Pause] No canvas to build into.");
                return;
            }
            var canvasRT = canvas.transform as RectTransform;
            if (canvasRT == null) { Debug.LogError("[Pause] No canvas rect to build into."); return; }

            // The legacy prefab authored this one canvas against 1280×800 while every other
            // Hollowfen screen uses the shared 1920×1080 contract. Standardize the scaler, then
            // preserve the approved card's exact apparent size with a constant presentation
            // transform (the ratio between those two scaler contracts is resolution-independent).
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.Init1080();

            // Tear down the legacy authored card.
            for (int i = canvasRT.childCount - 1; i >= 0; i--)
                DestroyImmediate(canvasRT.GetChild(i).gameObject);

            // Scrim — lets the frozen world read through, dimmed.
            var scrim = UICanvasUtil.NewImage("Scrim", canvasRT, new Color(0.04f, 0.05f, 0.04f, 0.55f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            var presentation = UICanvasUtil.NewRect("PausePresentation", canvasRT);
            presentation.sizeDelta = Vector2.zero;
            presentation.localScale = Vector3.one * 1.423f;

            // Card
            var card = UICanvasUtil.NewRect("Card", presentation);
            card.sizeDelta = new Vector2(470f, 660f);
            // Keep this overlay card shadow-free. The generated full-card SoftShadow rendered as
            // a wide blurred black band through the upper portion of this particular overlay
            // composition. The dimmed world, parchment edge, and double rule already provide
            // sufficient separation without an extra floating image behind the card.
            UICanvasUtil.MakeRoundedPanel(card, HollowfenPalette.Parchment, 22, 0.34f);

            // Inner hairline (double-rule feel)
            var inner = UICanvasUtil.NewImage("InnerRule", card, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.16f), false);
            var innerImg = inner.GetComponent<Image>();
            innerImg.sprite = UICanvasUtil.RoundedOutline(16, 1.4f);
            innerImg.type = Image.Type.Sliced;
            var innerRT = (RectTransform)inner.transform;
            UICanvasUtil.Stretch(innerRT);
            innerRT.offsetMin = new Vector2(10f, 10f);
            innerRT.offsetMax = new Vector2(-10f, -10f);

            // Header
            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", card, Localization.Get("ui.pause.eyebrow"), 13f, HollowfenPalette.Gold, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 18f), new Vector2(0f, -42f));

            var title = UICanvasUtil.NewHeading("Title", card, Localization.Get("ui.pause.title"), 52f, HollowfenPalette.InkDeep, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 72f), new Vector2(0f, -60f));

            var rule = UICanvasUtil.NewImage("TitleRule", card, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.55f), false);
            UICanvasUtil.SetRect((RectTransform)rule.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(56f, 2f), new Vector2(0f, -136f));

            // Rows
            float y = -172f;
            _resumeButton     = BuildRow(card, Localization.Get("ui.pause.resume"), ref y, out _);
            _storyButton      = BuildRow(card, Localization.Get("ui.pause.story"), ref y, out _);
            _fieldGuideButton = BuildRow(card, Localization.Get("ui.pause.field_guide"), ref y, out _);
            _purseButton      = BuildRow(card, Localization.Get("ui.pause.purse"), ref y, out _purseLabel);
            _settingsButton   = BuildRow(card, Localization.Get("ui.pause.settings"), ref y, out _);
            _saveGameButton   = BuildRow(card, Localization.Get("ui.pause.save"), ref y, out _saveLabel);
            y -= 10f; // breathe before the destructive action
            _quitButton       = BuildRow(card, Localization.Get("ui.pause.quit"), ref y, out var quitLabel, danger: true);

            // Hint
            var hint = UICanvasUtil.NewBody("Hint", card, Localization.Get("ui.pause.hint"), 15f,
                new Color(HollowfenPalette.Moss.r, HollowfenPalette.Moss.g, HollowfenPalette.Moss.b, 0.95f),
                FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(0f, 22f));

            WireVerticalNavigation();
        }

        private Button BuildRow(RectTransform card, string label, ref float y, out TMP_Text labelText, bool danger = false)
        {
            var row = UICanvasUtil.NewRect("Btn_" + label.Replace(" ", ""), card);
            UICanvasUtil.SetRect(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(366f, 56f), new Vector2(0f, y));
            y -= 64f;

            // Every row is visually transparent at rest. FocusHighlight is the sole owner of
            // the gold backing, so a previously focused or special-purpose action can never look
            // selected at the same time as the EventSystem's actual selection.
            var fill = row.gameObject.AddComponent<Image>();
            fill.sprite = UICanvasUtil.RoundedRect(12);
            fill.type = Image.Type.Sliced;
            fill.color = new Color(0f, 0f, 0f, 0.001f);
            fill.raycastTarget = true;

            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = fill;

            Color ink = danger ? new Color(0.63f, 0.26f, 0.22f, 1f) : HollowfenPalette.InkDeep;
            labelText = UICanvasUtil.NewHeading("Label", row, label, 25f, ink, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(labelText.rectTransform);

            // Focus glow overlay driven by FocusHighlight (mouse hover + gamepad share state).
            var glowGo = UICanvasUtil.NewImage("FocusGlow", row, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f), false);
            var glowImg = glowGo.GetComponent<Image>();
            glowImg.sprite = UICanvasUtil.RoundedRect(12);
            glowImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)glowGo.transform);

            var fh = row.gameObject.AddComponent<FocusHighlight>();
            fh.Configure(
                glowImg,
                row,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.14f),
                focusedScale: 1.02f,
                swapColor: true,
                swapScale: true,
                underlineText: false);

            return btn;
        }

        private void WireVerticalNavigation()
        {
            Button[] order = { _resumeButton, _storyButton, _fieldGuideButton, _purseButton,
                _settingsButton, _saveGameButton, _quitButton };
            for (int i = 0; i < order.Length; i++)
            {
                if (order[i] == null) continue;
                var nav = order[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = order[(i + order.Length - 1) % order.Length];
                nav.selectOnDown = order[(i + 1) % order.Length];
                order[i].navigation = nav;
            }
        }
    }
}
