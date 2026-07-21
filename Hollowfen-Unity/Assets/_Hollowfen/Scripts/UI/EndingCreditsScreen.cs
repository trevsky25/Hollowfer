using System;
using Hollowfen.Cinematics;
using Hollowfen.Data;
using Hollowfen.Foraging;
using Hollowfen.Quests;
using Hollowfen.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    /// <summary>
    /// Final illustrated credit page. It is created once at runtime, registered with UIManager,
    /// and therefore inherits the normal pointer, keyboard, controller, focus, and transition flow.
    /// </summary>
    public sealed class EndingCreditsScreen : UIScreen
    {
        private const string RuntimeScreenId = "ending-credits";
        private static EndingCreditsScreen _instance;

        private EndingData _ending;
        private Action _onDismissed;
        private NarrativePresentationSession.Lease _presentationLease;
        private Canvas _canvas;
        private Image _art;
        private AspectRatioFitter _artFitter;
        private TMP_Text _title;
        private TMP_Text _subtitle;
        private TMP_Text _note;
        private Button _returnButton;
        private Button _remainButton;
        private bool _dismissalCommitted;

        public override GameObject DefaultSelected => _returnButton != null ? _returnButton.gameObject : base.DefaultSelected;

        public static bool Show(EndingData ending, Action onDismissed)
        {
            if (ending == null || UIManager.Instance == null) return false;
            if (_instance == null)
            {
                var go = new GameObject("EndingCreditsScreen", typeof(RectTransform));
                go.transform.SetParent(UIManager.Instance.transform, false);
                _instance = go.AddComponent<EndingCreditsScreen>();
                UIManager.Instance.RegisterScreen(_instance);
            }
            _instance.Configure(ending, onDismissed);
            UIManager.Instance.OpenScreen(RuntimeScreenId);
            return true;
        }

        protected override void OnInitialize()
        {
            Build();
            ConfigureRuntimeScreen(RuntimeScreenId, _returnButton != null ? _returnButton.gameObject : null,
                GetComponent<CanvasGroup>(), false);
        }

        private void Configure(EndingData ending, Action onDismissed)
        {
            _ending = ending;
            _onDismissed = onDismissed;
            _dismissalCommitted = false;
            var card = ending.StoryCard;
            if (_art != null)
            {
                _art.sprite = card != null ? card.Image : null;
                _art.gameObject.SetActive(_art.sprite != null);
                if (_art.sprite != null && _artFitter != null)
                    _artFitter.aspectRatio = Mathf.Max(0.01f, _art.sprite.rect.width / _art.sprite.rect.height);
            }
            if (_title != null) _title.text = card != null ? JournalText.StoryTitle(card) : JournalText.EndingChoice(ending);
            if (_subtitle != null) _subtitle.text = card != null ? JournalText.StorySubtitle(card) : JournalText.EndingContext(ending);
            string note = card != null ? JournalText.StoryNote(card) : "";
            if (_note != null) _note.text = !string.IsNullOrWhiteSpace(note)
                ? string.Format(Localization.Get("format.quote"), note)
                : JournalText.EndingContext(ending);
        }

        public override void OnOpen()
        {
            if (_presentationLease == null)
                _presentationLease = NarrativePresentationSession.AcquireIfGameplay(
                    this, NarrativePresentationSession.Modal);
            if (_canvas != null) _canvas.sortingOrder = 9000;
        }

        public override void OnClose()
        {
            ReleasePresentation();
            NotifyDismissed();
        }

        private void OnDestroy()
        {
            ReleasePresentation();
            if (_instance == this) _instance = null;
        }

        private void ReleasePresentation()
        {
            _presentationLease?.Dispose();
            _presentationLease = null;
        }

        public override void OnBack() => RemainInHollowfen();

        private void ReturnToMenu()
        {
            UISfx.Confirm();
            CommitDismissal();
            NotifyDismissed();
            UIManager.Instance?.LoadSceneAndOpen("Scene_MainMenu", "main-menu");
        }

        private void RemainInHollowfen()
        {
            CommitDismissal();
            if (UIManager.Instance != null && UIManager.Instance.TopScreen == this)
                UIManager.Instance.Back();
        }

        private void CommitDismissal()
        {
            if (_dismissalCommitted) return;
            _dismissalCommitted = EndingResolver.MarkPresentationSeen();
            if (!_dismissalCommitted)
                Debug.LogError("[Ending] Could not persist finale acknowledgement.");
        }

        private void NotifyDismissed()
        {
            var done = _onDismissed;
            _onDismissed = null;
            done?.Invoke();
        }

        private void Build()
        {
            var group = gameObject.AddComponent<CanvasGroup>();
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameObject.AddComponent<CanvasScaler>().Init1080();
            gameObject.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;

            var viewport = UICanvasUtil.NewRect("EndingPainting", root);
            UICanvasUtil.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var artGo = UICanvasUtil.NewImage("Art", viewport, Color.white, false);
            _art = artGo.GetComponent<Image>();
            var artRt = (RectTransform)artGo.transform;
            artRt.anchorMin = artRt.anchorMax = new Vector2(0.5f, 0.5f);
            artRt.pivot = new Vector2(0.5f, 0.5f);
            artRt.sizeDelta = new Vector2(1920f, 1080f);
            _artFitter = artGo.AddComponent<AspectRatioFitter>();
            _artFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            var shade = UICanvasUtil.NewImage("InkGrade", root, Color.white, false);
            var shadeImage = shade.GetComponent<Image>();
            shadeImage.sprite = UICanvasUtil.MakeHorizontalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0f, new Color(0.025f, 0.030f, 0.022f, 0.98f)),
                new UICanvasUtil.GradientStop(0.50f, new Color(0.025f, 0.030f, 0.022f, 0.88f)),
                new UICanvasUtil.GradientStop(0.78f, new Color(0.025f, 0.030f, 0.022f, 0.24f)),
                new UICanvasUtil.GradientStop(1f, new Color(0.025f, 0.030f, 0.022f, 0.12f)),
            });
            UICanvasUtil.Stretch((RectTransform)shade.transform);

            var column = UICanvasUtil.NewRect("CreditsColumn", root);
            column.anchorMin = new Vector2(0f, 0f); column.anchorMax = new Vector2(0f, 1f);
            column.pivot = new Vector2(0f, 0.5f); column.sizeDelta = new Vector2(930f, -120f);
            column.anchoredPosition = new Vector2(100f, 0f);

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", column, Localization.Get("ending.credits.eyebrow"), 18f,
                HollowfenPalette.Gold, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 28f), new Vector2(0f, -28f));

            _title = UICanvasUtil.NewHeading("EndingTitle", column, "", 64f, HollowfenPalette.Cream,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-40f, 94f), new Vector2(0f, -76f));

            _subtitle = UICanvasUtil.NewBody("Subtitle", column, "", 24f, HollowfenPalette.GoldGlow,
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(_subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-60f, 58f), new Vector2(0f, -180f));

            _note = UICanvasUtil.NewBody("WrenNote", column, "", 20f, new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.84f),
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            _note.lineSpacing = 8f;
            UICanvasUtil.SetRect(_note.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-80f, 176f), new Vector2(0f, -258f));

            var saved = UICanvasUtil.NewEyebrow("Saved", column, Localization.Get("ending.credits.saved"), 18f,
                HollowfenPalette.Sage, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(saved.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 28f), new Vector2(0f, 214f));

            var creditsHeading = UICanvasUtil.NewHeading("CreditsHeading", column, Localization.Get("ending.credits.heading"), 27f,
                HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(creditsHeading.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 34f), new Vector2(0f, 162f));

            string creditCopy = string.Join("\n", new[]
            {
                Localization.Get("credits.sub"),
                Localization.Get("credits.copyright"),
                Localization.Get("credits.engine"),
                Localization.Get("credits.fonts"),
                Localization.Get("credits.thanks"),
            });
            var credits = UICanvasUtil.NewBody("Credits", column, creditCopy, 20f,
                new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.70f),
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            credits.lineSpacing = 6f;
            UICanvasUtil.SetRect(credits.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(-70f, 116f), new Vector2(0f, 38f));

            _returnButton = BuildButton(root, "ReturnToMenu", Localization.Get("ending.credits.return"), new Vector2(1640f, 92f), true);
            _remainButton = BuildButton(root, "Remain", Localization.Get("ending.credits.remain"), new Vector2(1300f, 92f), false);
            _returnButton.onClick.AddListener(ReturnToMenu);
            _remainButton.onClick.AddListener(RemainInHollowfen);

            var returnNav = _returnButton.navigation; returnNav.mode = Navigation.Mode.Explicit;
            returnNav.selectOnLeft = _remainButton; returnNav.selectOnRight = _remainButton; _returnButton.navigation = returnNav;
            var remainNav = _remainButton.navigation; remainNav.mode = Navigation.Mode.Explicit;
            remainNav.selectOnLeft = _returnButton; remainNav.selectOnRight = _returnButton; _remainButton.navigation = remainNav;

            var hint = UICanvasUtil.NewBody("Hint", root, Localization.Get("ending.credits.hint"), 18f,
                new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.62f),
                FontStyles.Italic, TextAlignmentOptions.BottomRight);
            UICanvasUtil.SetRect(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(620f, 24f), new Vector2(-80f, 28f));
        }

        private static Button BuildButton(Transform parent, string name, string labelText, Vector2 position, bool accent)
        {
            var rt = UICanvasUtil.NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300f, 58f);
            rt.anchoredPosition = position;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = UICanvasUtil.RoundedRect(15);
            image.type = Image.Type.Sliced;
            image.color = accent ? HollowfenPalette.Gold : new Color(0.10f, 0.11f, 0.09f, 0.94f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = accent ? HollowfenPalette.GoldGlow : new Color(0.18f, 0.20f, 0.15f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(colors.highlightedColor, Color.black, 0.16f);
            button.colors = colors;
            var label = UICanvasUtil.NewBody("Label", rt, labelText, 18f,
                accent ? HollowfenPalette.InkDeep : HollowfenPalette.Cream, FontStyles.Bold, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(label.rectTransform);
            return button;
        }
    }
}
