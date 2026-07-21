using Hollowfen.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Cinematic memory reader: aspect-safe full-bleed art plus one readable journal
    // leaf. Secondary notes are a deliberate layer and paging only traverses cards
    // permitted by progression.
    public class StoryDetailScreen : UIScreen
    {
        private static readonly Color BgColor = HollowfenPalette.JournalBackdrop;
        private static readonly Color PanelColor = new Color(0.026f, 0.040f, 0.030f, 0.965f);
        private static readonly Color Cream = new Color(0.961f, 0.925f, 0.855f, 1f);
        private static readonly Color Body = new Color(0.961f, 0.925f, 0.855f, 0.92f);
        private static readonly Color Subtle = new Color(0.961f, 0.925f, 0.855f, 0.66f);
        private static readonly Color Gold = new Color(0.851f, 0.741f, 0.427f, 1f);
        private static readonly Color Hairline = HollowfenPalette.DividerLine;

        private JournalArtPresenter _hero;
        private TMP_Text _eyebrow;
        private TMP_Text _title;
        private TMP_Text _subtitle;
        private TMP_Text _body;
        private TMP_Text _wrenNote;
        private RectTransform _beatsContainer;
        private GameObject _annotationsRoot;
        private ScrollRect _readerScroll;
        private Button _closeButton;
        private Button _annotationsButton;
        private TMP_Text _annotationsLabel;
        private Button _prevButton;
        private Button _nextButton;
        private TMP_Text _prevLabel;
        private TMP_Text _nextLabel;
        private TMP_Text _pageIndicator;
        private bool _annotationsVisible;
        private bool _built;
        private StoryCardData _current;
        private StoryCardDatabase _database;

        public override GameObject DefaultSelected => _closeButton != null ? _closeButton.gameObject : base.DefaultSelected;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;
            try
            {
                BuildLayout();
                _built = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[StoryDetailScreen] OnInitialize failed: " + e);
            }
        }

        public override void OnBack()
        {
            if (_annotationsVisible)
            {
                SetAnnotationsVisible(false, true);
                return;
            }
            base.OnBack();
        }

        public void SetCard(StoryCardData card, StoryCardDatabase database = null)
        {
            if (!_built)
            {
                BuildLayout();
                _built = true;
            }
            if (database != null) _database = database;
            if (card == null || !IsAvailable(card)) return;

            _current = card;
            _hero.SetSprite(card.Image, new Color(0.09f, 0.10f, 0.08f, 1f));
            _eyebrow.text = string.Format(Localization.Get("format.pair"),
                JournalText.StoryAct(card), JournalText.StoryScene(card)).ToUpperInvariant();
            _title.text = JournalText.StoryTitle(card);
            _subtitle.text = JournalText.StorySubtitle(card);
            _body.text = JournalText.StoryBody(card);
            _wrenNote.text = string.IsNullOrEmpty(JournalText.StoryNote(card))
                ? Localization.Get("journal.story.no_note")
                : JournalText.StoryNote(card);

            ClearBeatRows();
            if (card.Beats != null)
            {
                for (int i = 0; i < card.Beats.Length; i++)
                {
                    string beat = JournalText.StoryBeat(card, i);
                    if (!string.IsNullOrEmpty(beat)) BuildBeatRow(_beatsContainer, beat);
                }
            }

            SetAnnotationsVisible(false, false);
            _readerScroll.verticalNormalizedPosition = 1f;
            UpdatePrevNextNav();
            Canvas.ForceUpdateCanvases();
        }

        private void BuildLayout()
        {
            EnsureCanvas();

            var bg = UICanvasUtil.NewImage("BG", transform, BgColor, true);
            UICanvasUtil.Stretch(bg.GetComponent<RectTransform>());

            _hero = JournalArtPresenter.Create("HeroFrame", transform, true, BgColor);
            UICanvasUtil.Stretch(_hero.Frame);

            var shadeStops = new[]
            {
                new UICanvasUtil.GradientStop(0f, new Color(BgColor.r, BgColor.g, BgColor.b, 0.82f)),
                new UICanvasUtil.GradientStop(0.38f, new Color(BgColor.r, BgColor.g, BgColor.b, 0.16f)),
                new UICanvasUtil.GradientStop(1f, new Color(BgColor.r, BgColor.g, BgColor.b, 0.10f))
            };
            var shade = UICanvasUtil.NewRect("BottomShade", transform);
            UICanvasUtil.SetRect(shade, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 360f), Vector2.zero);
            var shadeImage = shade.gameObject.AddComponent<Image>();
            shadeImage.sprite = UICanvasUtil.MakeVerticalGradient(shadeStops, 256);
            shadeImage.raycastTarget = false;

            var panel = UICanvasUtil.NewRect("ReaderLeaf", transform);
            UICanvasUtil.SetRect(panel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(660f, 948f), new Vector2(-48f, 0f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = true;
            UICanvasUtil.Roundify(panelImage, 18);
            JournalChrome.AddStructuralBorder(panel, 18, 0.10f);

            BuildIdentity(panel);
            BuildReader(panel);
            BuildControls(panel);

            _closeButton = JournalChrome.BuildCloseButton(transform, () =>
            {
                if (UIManager.Instance != null) UIManager.Instance.Back();
            });
            JournalChrome.BuildBottomHint(transform, "journal.hint.reader");
            UpdateControlNavigation();
        }

        private void BuildIdentity(RectTransform panel)
        {
            var identity = UICanvasUtil.NewRect("Identity", panel);
            identity.anchorMin = new Vector2(0f, 1f);
            identity.anchorMax = new Vector2(1f, 1f);
            identity.pivot = new Vector2(0.5f, 1f);
            identity.offsetMin = new Vector2(42f, identity.offsetMin.y);
            identity.offsetMax = new Vector2(-42f, identity.offsetMax.y);
            identity.sizeDelta = new Vector2(identity.sizeDelta.x, 248f);

            _eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", identity, "", 18f, Gold);
            UICanvasUtil.SetRect(_eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 22f), new Vector2(0f, -30f));

            _title = UICanvasUtil.NewHeading("Title", identity, "", 58f, Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 118f), new Vector2(0f, -62f));
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 38f;
            _title.fontSizeMax = 58f;

            _subtitle = UICanvasUtil.NewBody("Subtitle", identity, "", 18f, Subtle, FontStyles.Italic);
            UICanvasUtil.SetRect(_subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 48f), new Vector2(0f, -184f));

            var rule = UICanvasUtil.NewImage("Rule", identity, Hairline, false);
            UICanvasUtil.SetRect(rule.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), Vector2.zero);
        }

        private void BuildReader(RectTransform panel)
        {
            var scrollRt = UICanvasUtil.NewRect("ReadingScroll", panel);
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(42f, 176f);
            scrollRt.offsetMax = new Vector2(-42f, -260f);
            var scrollImage = scrollRt.gameObject.AddComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0f);
            scrollImage.raycastTarget = true;
            _readerScroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            _readerScroll.horizontal = false;
            _readerScroll.vertical = true;
            _readerScroll.movementType = ScrollRect.MovementType.Clamped;
            _readerScroll.scrollSensitivity = 42f;
            scrollRt.gameObject.AddComponent<ScrollFocusFollower>();

            var viewport = UICanvasUtil.NewRect("Viewport", scrollRt);
            UICanvasUtil.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _readerScroll.viewport = viewport;

            var content = UICanvasUtil.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            _readerScroll.content = content;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 8, 0, 34);
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _body = UICanvasUtil.NewBody("Body", content, "", 22f, Body);
            _body.lineSpacing = 8f;
            JournalChrome.FitText(_body, 180f);

            _annotationsRoot = UICanvasUtil.NewRect("Annotations", content).gameObject;
            var annotationLayout = _annotationsRoot.AddComponent<VerticalLayoutGroup>();
            annotationLayout.padding = new RectOffset(22, 10, 24, 18);
            annotationLayout.spacing = 16f;
            annotationLayout.childForceExpandWidth = true;
            annotationLayout.childForceExpandHeight = false;
            var annotationBg = _annotationsRoot.AddComponent<Image>();
            annotationBg.color = new Color(Gold.r, Gold.g, Gold.b, 0.075f);
            UICanvasUtil.Roundify(annotationBg, 12);
            var annotationFitter = _annotationsRoot.AddComponent<ContentSizeFitter>();
            annotationFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var noteTitle = UICanvasUtil.NewEyebrow("Title", _annotationsRoot.transform, Localization.Get("journal.story.annotations"), 18f, Gold);
            JournalChrome.FitText(noteTitle, 22f);
            _wrenNote = UICanvasUtil.NewBody("WrenNote", _annotationsRoot.transform, "", 19f, Body, FontStyles.Italic);
            _wrenNote.lineSpacing = 7f;
            JournalChrome.FitText(_wrenNote, 70f);

            _beatsContainer = UICanvasUtil.NewRect("Beats", _annotationsRoot.transform);
            var beatsLayout = _beatsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            beatsLayout.spacing = 10f;
            beatsLayout.childForceExpandWidth = true;
            beatsLayout.childForceExpandHeight = false;
            var beatsFitter = _beatsContainer.gameObject.AddComponent<ContentSizeFitter>();
            beatsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void BuildControls(RectTransform panel)
        {
            var annotationRt = UICanvasUtil.NewRect("AnnotationsButton", panel);
            UICanvasUtil.SetRect(annotationRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-84f, 50f), new Vector2(0f, 104f));
            var annotationImage = annotationRt.gameObject.AddComponent<Image>();
            annotationImage.color = new Color(Gold.r, Gold.g, Gold.b, 0.08f);
            UICanvasUtil.Roundify(annotationImage, 12);
            _annotationsButton = annotationRt.gameObject.AddComponent<Button>();
            _annotationsButton.transition = Selectable.Transition.None;
            _annotationsButton.targetGraphic = annotationImage;
            _annotationsButton.onClick.AddListener(() => SetAnnotationsVisible(!_annotationsVisible, true));
            _annotationsLabel = UICanvasUtil.NewEyebrow("Label", annotationRt, Localization.Get("journal.story.annotations"), 18f, Gold, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(_annotationsLabel.rectTransform);
            var annotationFocus = annotationRt.gameObject.AddComponent<FocusHighlight>();
            annotationFocus.Configure(annotationImage, annotationRt, new Color(Gold.r, Gold.g, Gold.b, 0.22f), 1.02f);

            var nav = UICanvasUtil.NewRect("Nav", panel);
            UICanvasUtil.SetRect(nav, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-84f, 70f), new Vector2(0f, 24f));
            var rule = UICanvasUtil.NewImage("Rule", nav, Hairline, false);
            UICanvasUtil.SetRect(rule.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), Vector2.zero);

            _prevButton = BuildNavButton(nav, "Prev", true, out _prevLabel);
            var prevRt = (RectTransform)_prevButton.transform;
            prevRt.anchorMin = new Vector2(0f, 0f);
            prevRt.anchorMax = new Vector2(0.42f, 1f);
            prevRt.offsetMin = Vector2.zero;
            prevRt.offsetMax = Vector2.zero;
            _prevButton.onClick.AddListener(GoPrev);

            _pageIndicator = UICanvasUtil.NewBody("Page", nav, "", 18f, Subtle, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_pageIndicator.rectTransform, new Vector2(0.42f, 0f), new Vector2(0.58f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            _nextButton = BuildNavButton(nav, "Next", false, out _nextLabel);
            var nextRt = (RectTransform)_nextButton.transform;
            nextRt.anchorMin = new Vector2(0.58f, 0f);
            nextRt.anchorMax = new Vector2(1f, 1f);
            nextRt.offsetMin = Vector2.zero;
            nextRt.offsetMax = Vector2.zero;
            _nextButton.onClick.AddListener(GoNext);
        }

        private Button BuildNavButton(Transform parent, string name, bool previous, out TMP_Text label)
        {
            var rt = UICanvasUtil.NewRect(name + "Button", parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;

            var direction = UICanvasUtil.NewEyebrow("Direction", rt,
                Localization.Get(previous ? "journal.previous" : "journal.next"), 18f, Subtle,
                previous ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
            UICanvasUtil.SetRect(direction.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(previous ? 0f : 1f, 1f), new Vector2(0f, 18f), new Vector2(0f, -10f));
            label = UICanvasUtil.NewHeading("Label", rt, "", 19f, Cream, FontStyles.Italic,
                previous ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight);
            UICanvasUtil.SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(previous ? 0f : 1f, 0.5f), new Vector2(-4f, -22f), new Vector2(previous ? 4f : -4f, 0f));
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 19f;
            var focus = rt.gameObject.AddComponent<FocusHighlight>();
            focus.Configure(label, rt, Gold, 1.035f);
            return button;
        }

        private void BuildBeatRow(Transform parent, string copy)
        {
            var row = UICanvasUtil.NewRect("Beat", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var rowFitter = row.gameObject.AddComponent<ContentSizeFitter>();
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var dot = UICanvasUtil.NewBody("Dot", row, "•", 16f, Gold, FontStyles.Bold);
            var dotLayout = dot.gameObject.AddComponent<LayoutElement>();
            dotLayout.preferredWidth = 14f;
            var text = UICanvasUtil.NewBody("Text", row, copy, 17f, Body);
            JournalChrome.FitText(text, 24f);
        }

        private void ClearBeatRows()
        {
            if (_beatsContainer == null) return;
            for (int i = _beatsContainer.childCount - 1; i >= 0; i--)
            {
                var child = _beatsContainer.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private void SetAnnotationsVisible(bool visible, bool moveFocus)
        {
            _annotationsVisible = visible;
            if (_annotationsRoot != null) _annotationsRoot.SetActive(visible);
            if (_annotationsLabel != null)
                _annotationsLabel.text = Localization.Get(visible ? "journal.story.hide_annotations" : "journal.story.annotations").ToUpperInvariant();
            if (visible && _readerScroll != null) _readerScroll.verticalNormalizedPosition = 0f;
            if (moveFocus && UnityEngine.EventSystems.EventSystem.current != null && _annotationsButton != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(_annotationsButton.gameObject);
        }

        private void GoPrev()
        {
            int index = AdjacentIndex(-1);
            if (index >= 0) SetCard(_database.Cards[index], _database);
        }

        private void GoNext()
        {
            int index = AdjacentIndex(1);
            if (index >= 0) SetCard(_database.Cards[index], _database);
        }

        private int AdjacentIndex(int direction)
        {
            if (_database == null || _current == null) return -1;
            int current = JournalNavigation.FindIndex(_database.Cards, _current);
            return JournalNavigation.FindAdjacentAvailable(_database.Cards, current, direction, IsAvailable);
        }

        private void UpdatePrevNextNav()
        {
            int current = _database != null ? JournalNavigation.FindIndex(_database.Cards, _current) : -1;
            int previous = AdjacentIndex(-1);
            int next = AdjacentIndex(1);
            int total = _database != null ? JournalNavigation.CountAvailable(_database.Cards, IsAvailable) : 0;
            int position = _database != null ? JournalNavigation.AvailablePosition(_database.Cards, current, IsAvailable) : -1;

            _prevButton.interactable = previous >= 0;
            _nextButton.interactable = next >= 0;
            _prevLabel.text = previous >= 0 ? JournalText.StoryTitle(_database.Cards[previous]) : "";
            _nextLabel.text = next >= 0 ? JournalText.StoryTitle(_database.Cards[next]) : "";
            _pageIndicator.text = position > 0
                ? string.Format(Localization.Get("journal.page"), position, total)
                : "";
            UpdateControlNavigation();
        }

        private void UpdateControlNavigation()
        {
            if (_closeButton == null || _annotationsButton == null || _prevButton == null || _nextButton == null) return;
            Selectable bottom = _prevButton.interactable ? _prevButton : (_nextButton.interactable ? _nextButton : _annotationsButton);
            JournalChrome.SetNavigation(_closeButton, bottom, _annotationsButton);
            JournalChrome.SetNavigation(_annotationsButton, _closeButton, bottom);
            JournalChrome.SetNavigation(_prevButton, _annotationsButton, _closeButton, null, _nextButton.interactable ? _nextButton : null);
            JournalChrome.SetNavigation(_nextButton, _annotationsButton, _closeButton, _prevButton.interactable ? _prevButton : null, null);
        }

        private static bool IsAvailable(StoryCardData card)
        {
            return card != null && Hollowfen.Quests.QuestManager.IsStoryCardUnlocked(card.Id);
        }

        private void EnsureCanvas()
        {
            if (GetComponent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                gameObject.AddComponent<CanvasScaler>().Init1080();
                gameObject.AddComponent<GraphicRaycaster>();
            }
            var rt = transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }
    }
}
