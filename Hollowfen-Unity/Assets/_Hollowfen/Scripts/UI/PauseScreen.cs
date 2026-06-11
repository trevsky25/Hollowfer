using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Wren's pause journal. Builds its UI programmatically (same convention as the menu
    // pages): dim scrim over frozen gameplay, rounded parchment card with soft shadow and
    // gold hairline, serif rows. The legacy prefab's authored children are torn down on
    // first init — the prefab only contributes the UIScreen plumbing.
    public class PauseScreen : UIScreen
    {
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _storyButton;
        [SerializeField] private Button _fieldGuideButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _saveGameButton;
        [SerializeField] private Button _quitButton;

        private TMP_Text _saveLabel;
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
            if (_settingsButton   != null) _settingsButton.onClick.AddListener(OnSettings);
            if (_saveGameButton   != null) _saveGameButton.onClick.AddListener(OnSaveGame);
            if (_quitButton       != null) _quitButton.onClick.AddListener(OnQuit);
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Time.timeScale = 0f;
            if (_saveLabel != null) _saveLabel.text = "Save Game";
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

        private void OnSettings()
        {
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("settings");
        }

        private void OnSaveGame()
        {
            Hollowfen.Save.SaveCoordinator.SaveAllWithPlayer();
            Debug.Log($"[Pause] Saved to slot {Hollowfen.Save.SaveManager.ActiveSlot}");
            if (_saveLabel != null) _saveLabel.text = "Saved ✓";
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
            var canvasRT = canvas != null ? (RectTransform)canvas.transform : transform as RectTransform;
            if (canvasRT == null) { Debug.LogError("[Pause] No canvas rect to build into."); return; }

            // Tear down the legacy authored card.
            for (int i = canvasRT.childCount - 1; i >= 0; i--)
                DestroyImmediate(canvasRT.GetChild(i).gameObject);

            // Scrim — lets the frozen world read through, dimmed.
            var scrim = UICanvasUtil.NewImage("Scrim", canvasRT, new Color(0.04f, 0.05f, 0.04f, 0.55f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            // Card
            var card = UICanvasUtil.NewRect("Card", canvasRT);
            card.sizeDelta = new Vector2(470f, 660f);
            UICanvasUtil.AddShadow(card, 26, 34, 0.45f, -10f);
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
            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", card, "HOLLOWFEN", 12f, HollowfenPalette.Gold, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 18f), new Vector2(0f, -42f));

            var title = UICanvasUtil.NewHeading("Title", card, "Paused", 52f, HollowfenPalette.InkDeep, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 62f), new Vector2(0f, -64f));

            var rule = UICanvasUtil.NewImage("TitleRule", card, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.55f), false);
            UICanvasUtil.SetRect((RectTransform)rule.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(56f, 2f), new Vector2(0f, -136f));

            // Rows
            float y = -172f;
            _resumeButton     = BuildRow(card, "Resume", ref y, accent: false, out _);
            _storyButton      = BuildRow(card, "Story", ref y, accent: false, out _);
            _fieldGuideButton = BuildRow(card, "Field Guide", ref y, accent: false, out _);
            _settingsButton   = BuildRow(card, "Settings", ref y, accent: false, out _);
            _saveGameButton   = BuildRow(card, "Save Game", ref y, accent: true, out _saveLabel);
            y -= 10f; // breathe before the destructive action
            _quitButton       = BuildRow(card, "Quit to Main Menu", ref y, accent: false, out var quitLabel, danger: true);

            // Hint
            var hint = UICanvasUtil.NewBody("Hint", card, "ESC to resume", 13f,
                new Color(HollowfenPalette.Moss.r, HollowfenPalette.Moss.g, HollowfenPalette.Moss.b, 0.8f),
                FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(0f, 22f));

            WireVerticalNavigation();
        }

        private Button BuildRow(RectTransform card, string label, ref float y, bool accent, out TMP_Text labelText, bool danger = false)
        {
            var row = UICanvasUtil.NewRect("Btn_" + label.Replace(" ", ""), card);
            UICanvasUtil.SetRect(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(366f, 56f), new Vector2(0f, y));
            y -= 64f;

            // Resting fill: transparent; focus paints it. Accent rows get a faint gold wash.
            var fill = row.gameObject.AddComponent<Image>();
            fill.sprite = UICanvasUtil.RoundedRect(12);
            fill.type = Image.Type.Sliced;
            fill.color = accent
                ? new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.10f)
                : new Color(0f, 0f, 0f, 0.001f);
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
            var fhT = typeof(FocusHighlight);
            System.Action<string, object> setF = (n, v) =>
            {
                var f = fhT.GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (f != null) f.SetValue(fh, v);
            };
            setF("_targetGraphic", glowImg);
            setF("_baseColor", new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f));
            setF("_focusedColor", new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.14f));
            setF("_focusedScale", 1.02f);
            setF("_swapColor", true);
            setF("_swapScale", true);
            setF("_underlineText", false);
            glowImg.color = new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f);

            return btn;
        }

        private void WireVerticalNavigation()
        {
            Button[] order = { _resumeButton, _storyButton, _fieldGuideButton, _settingsButton, _saveGameButton, _quitButton };
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
