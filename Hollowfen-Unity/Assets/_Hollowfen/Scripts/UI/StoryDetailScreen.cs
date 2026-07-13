using Hollowfen.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class StoryDetailScreen : UIScreen
    {
        private static readonly Color BgColor       = new Color(0.020f, 0.031f, 0.020f, 1f);
        private static readonly Color HeadingColor  = new Color(0.961f, 0.925f, 0.855f, 1f);
        private static readonly Color BodyColor     = new Color(0.961f, 0.925f, 0.855f, 0.95f);
        private static readonly Color SubtleColor   = new Color(0.961f, 0.925f, 0.855f, 0.78f);
        private static readonly Color GoldColor     = new Color(0.851f, 0.741f, 0.427f, 1f);
        private static readonly Color GoldDim       = new Color(0.851f, 0.741f, 0.427f, 0.62f);
        private static readonly Color SeparatorCol  = new Color(0.961f, 0.925f, 0.855f, 0.10f);

        private Image _hero;
        private Image _gradient;
        private TMP_Text _eyebrow;
        private TMP_Text _title;
        private TMP_Text _subtitle;
        private TMP_Text _body;
        private TMP_Text _wrenNote;
        private RectTransform _beatsContainer;
        private Button _closeButton;
        private Button _prevButton;
        private Button _nextButton;
        private TMP_Text _prevLabel;
        private TMP_Text _nextLabel;
        private TMP_Text _pageIndicator;
        private bool _built;
        private StoryCardData _current;
        private StoryCardDatabase _database;

        public override GameObject DefaultSelected => _closeButton != null ? _closeButton.gameObject : base.DefaultSelected;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;
            try { BuildLayout(); _built = true; }
            catch (System.Exception e) { Debug.LogError("[StoryDetailScreen] OnInitialize failed: " + e); }
        }

        public void SetCard(StoryCardData card, StoryCardDatabase database = null)
        {
            if (!_built) { BuildLayout(); _built = true; }
            _current = card;
            if (database != null) _database = database;
            if (card == null) return;

            _hero.sprite = card.Image;
            _hero.color = card.Image != null ? Color.white : new Color(0.10f, 0.10f, 0.10f, 1f);
            _eyebrow.text = (card.Act + " · " + card.Scene).ToUpperInvariant();
            _title.text = card.Title;
            _subtitle.text = card.Subtitle;
            _body.text = card.Body;
            _wrenNote.text = card.WrenNote;

            for (int i = _beatsContainer.childCount - 1; i >= 0; i--)
                Destroy(_beatsContainer.GetChild(i).gameObject);

            if (card.Beats != null)
                foreach (var beat in card.Beats)
                    if (!string.IsNullOrEmpty(beat)) BuildBeatRow(_beatsContainer, beat);

            UpdatePrevNextNav();
        }

        private void BuildLayout()
        {
            EnsureCanvas();

            var bg = UICanvasUtil.NewImage("BG", transform, BgColor, true);
            UICanvasUtil.Stretch(bg.GetComponent<RectTransform>());

            // Full-bleed hero image
            var heroGo = UICanvasUtil.NewRect("Hero", transform);
            UICanvasUtil.Stretch(heroGo);
            _hero = heroGo.gameObject.AddComponent<Image>();
            _hero.preserveAspect = false;
            _hero.raycastTarget = false;

            // Bottom-up gradient (reads through the image at top, dark at bottom)
            var stops = new[]
            {
                new UICanvasUtil.GradientStop(0f,    new Color(0.020f, 0.031f, 0.020f, 0.96f)),
                new UICanvasUtil.GradientStop(0.18f, new Color(0.020f, 0.031f, 0.020f, 0.88f)),
                new UICanvasUtil.GradientStop(0.36f, new Color(0.020f, 0.031f, 0.020f, 0.62f)),
                new UICanvasUtil.GradientStop(0.55f, new Color(0.020f, 0.031f, 0.020f, 0.18f)),
                new UICanvasUtil.GradientStop(0.78f, new Color(0.020f, 0.031f, 0.020f, 0.0f)),
                new UICanvasUtil.GradientStop(1f,    new Color(0.020f, 0.031f, 0.020f, 0.20f)),
            };
            var gradGo = UICanvasUtil.NewRect("Gradient", transform);
            UICanvasUtil.Stretch(gradGo);
            _gradient = gradGo.gameObject.AddComponent<Image>();
            _gradient.sprite = UICanvasUtil.MakeVerticalGradient(stops, 512);
            _gradient.raycastTarget = false;
            _gradient.preserveAspect = false;
            _gradient.type = Image.Type.Simple;

            // Dedicated content scrim (bottom 600 px) for legibility
            var scrimStops = new[]
            {
                new UICanvasUtil.GradientStop(0f,    new Color(0.020f, 0.031f, 0.020f, 0.82f)),
                new UICanvasUtil.GradientStop(0.65f, new Color(0.020f, 0.031f, 0.020f, 0.65f)),
                new UICanvasUtil.GradientStop(1f,    new Color(0.020f, 0.031f, 0.020f, 0.0f)),
            };
            var scrimGo = UICanvasUtil.NewRect("ContentScrim", transform);
            UICanvasUtil.SetRect(scrimGo, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 600f), Vector2.zero);
            var scrimImg = scrimGo.gameObject.AddComponent<Image>();
            scrimImg.sprite = UICanvasUtil.MakeVerticalGradient(scrimStops, 256);
            scrimImg.raycastTarget = false;
            scrimImg.preserveAspect = false;

            // Close button
            var closeRt = UICanvasUtil.NewRect("CloseButton", transform);
            UICanvasUtil.SetRect(closeRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(48f, 48f), new Vector2(-32f, -32f));
            var closeImg = closeRt.gameObject.AddComponent<Image>();
            closeImg.color = new Color(0f, 0f, 0f, 0f);
            _closeButton = closeRt.gameObject.AddComponent<Button>();
            _closeButton.transition = Selectable.Transition.None;
            _closeButton.targetGraphic = closeImg;
            _closeButton.onClick.AddListener(() => { if (UIManager.Instance != null) UIManager.Instance.Back(); });
            var closeText = UICanvasUtil.NewBody("X", closeRt, "<sprite name=\"ui_x\">", 32f, SubtleColor, FontStyles.Normal, TextAlignmentOptions.Center); // batch-48: ✕ had no font glyph
            UICanvasUtil.Stretch(closeText.rectTransform);
            var closeFh = closeRt.gameObject.AddComponent<FocusHighlight>();
            ConfigureFocusHighlight(closeFh, closeText, closeRt as RectTransform, GoldColor, 1.12f);

            // Bottom content panel
            var content = UICanvasUtil.NewRect("Content", transform);
            UICanvasUtil.SetRect(content, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1640f, 480f), new Vector2(0f, 36f));

            var topRule = UICanvasUtil.NewImage("TopRule", content, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.22f), false);
            UICanvasUtil.SetRect(topRule.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), Vector2.zero);

            // 3-col content row
            var row = UICanvasUtil.NewRect("Row", content);
            UICanvasUtil.SetRect(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 360f), new Vector2(0f, -38f));
            var rowHlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowHlg.padding = new RectOffset(0, 0, 0, 0);
            rowHlg.spacing = 38f;
            rowHlg.childAlignment = TextAnchor.UpperLeft;
            rowHlg.childForceExpandWidth = false;
            rowHlg.childForceExpandHeight = true;

            // LEFT — heading column
            var leftCol = UICanvasUtil.NewRect("LeftCol", row);
            var leftLE = leftCol.gameObject.AddComponent<LayoutElement>();
            leftLE.preferredWidth = 420f; leftLE.flexibleWidth = 0f;
            var leftVlg = leftCol.gameObject.AddComponent<VerticalLayoutGroup>();
            leftVlg.spacing = 16f;
            leftVlg.padding = new RectOffset(0, 0, 0, 0);
            leftVlg.childForceExpandWidth = true;
            leftVlg.childForceExpandHeight = false;
            leftVlg.childAlignment = TextAnchor.UpperLeft;

            _eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", leftCol, "", 13f, GoldColor);
            AttachLayout(_eyebrow.gameObject, 22f);

            _title = UICanvasUtil.NewHeading("Title", leftCol, "", 64f, HeadingColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            AttachLayout(_title.gameObject, 150f);

            _subtitle = UICanvasUtil.NewBody("Subtitle", leftCol, "", 17f, SubtleColor, FontStyles.Italic);
            AttachLayout(_subtitle.gameObject, 70f);

            // MIDDLE — body with vertical separators
            var midColWrap = UICanvasUtil.NewRect("MidColWrap", row);
            var midLE = midColWrap.gameObject.AddComponent<LayoutElement>();
            midLE.preferredWidth = 640f; midLE.flexibleWidth = 1f;
            var sepL = UICanvasUtil.NewImage("SepL", midColWrap, SeparatorCol, false);
            UICanvasUtil.SetRect(sepL.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(1f, 0f), Vector2.zero);
            var sepR = UICanvasUtil.NewImage("SepR", midColWrap, SeparatorCol, false);
            UICanvasUtil.SetRect(sepR.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(1f, 0f), Vector2.zero);
            var midInner = UICanvasUtil.NewRect("Inner", midColWrap);
            midInner.anchorMin = Vector2.zero; midInner.anchorMax = Vector2.one;
            midInner.offsetMin = new Vector2(34f, 4f); midInner.offsetMax = new Vector2(-34f, 0f);
            _body = UICanvasUtil.NewBody("Body", midInner, "", 19f, BodyColor);
            UICanvasUtil.Stretch(_body.rectTransform);

            // RIGHT — Wren note + beats
            var rightCol = UICanvasUtil.NewRect("RightCol", row);
            var rightLE = rightCol.gameObject.AddComponent<LayoutElement>();
            rightLE.preferredWidth = 440f; rightLE.flexibleWidth = 0f;
            var rightVlg = rightCol.gameObject.AddComponent<VerticalLayoutGroup>();
            rightVlg.spacing = 18f;
            rightVlg.padding = new RectOffset(0, 0, 0, 0);
            rightVlg.childForceExpandWidth = true;
            rightVlg.childForceExpandHeight = false;
            rightVlg.childAlignment = TextAnchor.UpperLeft;

            // Note with gold left border
            var noteWrap = UICanvasUtil.NewRect("NoteWrap", rightCol);
            var noteWrapLE = noteWrap.gameObject.AddComponent<LayoutElement>();
            noteWrapLE.preferredHeight = 130f; noteWrapLE.minHeight = 80f; noteWrapLE.flexibleHeight = 0f;

            var noteBorder = UICanvasUtil.NewImage("LeftBorder", noteWrap, GoldDim, false);
            UICanvasUtil.SetRect(noteBorder.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f), Vector2.zero);

            var noteInner = UICanvasUtil.NewRect("Inner", noteWrap);
            noteInner.anchorMin = Vector2.zero; noteInner.anchorMax = Vector2.one;
            noteInner.offsetMin = new Vector2(20f, 0f); noteInner.offsetMax = new Vector2(0f, 0f);
            _wrenNote = UICanvasUtil.NewBody("Note", noteInner, "", 17f, BodyColor, FontStyles.Italic);
            UICanvasUtil.Stretch(_wrenNote.rectTransform);

            // Beats list
            var beatsList = UICanvasUtil.NewRect("BeatsList", rightCol);
            _beatsContainer = beatsList;
            var beatsVlg = beatsList.gameObject.AddComponent<VerticalLayoutGroup>();
            beatsVlg.spacing = 8f;
            beatsVlg.childForceExpandWidth = true;
            beatsVlg.childForceExpandHeight = false;
            var beatsLE = beatsList.gameObject.AddComponent<LayoutElement>();
            beatsLE.flexibleHeight = 1f;
            beatsLE.minHeight = 100f;

            // Bottom nav
            var nav = UICanvasUtil.NewRect("Nav", content);
            UICanvasUtil.SetRect(nav, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), Vector2.zero);
            var navTopLine = UICanvasUtil.NewImage("Line", nav, SeparatorCol, false);
            UICanvasUtil.SetRect(navTopLine.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), Vector2.zero);

            _prevButton = BuildNavButton(nav, "PrevButton", true, out _prevLabel);
            ((RectTransform)_prevButton.transform).anchorMin = new Vector2(0f, 0f);
            ((RectTransform)_prevButton.transform).anchorMax = new Vector2(0f, 1f);
            ((RectTransform)_prevButton.transform).pivot = new Vector2(0f, 0.5f);
            ((RectTransform)_prevButton.transform).sizeDelta = new Vector2(380f, 0f);
            ((RectTransform)_prevButton.transform).anchoredPosition = Vector2.zero;
            _prevButton.onClick.AddListener(GoPrev);

            _pageIndicator = UICanvasUtil.NewBody("PageIndicator", nav, "", 16f, SubtleColor, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_pageIndicator.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(220f, 0f), Vector2.zero);

            _nextButton = BuildNavButton(nav, "NextButton", false, out _nextLabel);
            ((RectTransform)_nextButton.transform).anchorMin = new Vector2(1f, 0f);
            ((RectTransform)_nextButton.transform).anchorMax = new Vector2(1f, 1f);
            ((RectTransform)_nextButton.transform).pivot = new Vector2(1f, 0.5f);
            ((RectTransform)_nextButton.transform).sizeDelta = new Vector2(380f, 0f);
            ((RectTransform)_nextButton.transform).anchoredPosition = Vector2.zero;
            _nextButton.onClick.AddListener(GoNext);
        }

        private void BuildBeatRow(Transform parent, string text)
        {
            var row = UICanvasUtil.NewRect("Beat", parent);
            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.minHeight = 26f; rowLE.preferredHeight = -1f; rowLE.flexibleWidth = 1f;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.padding = new RectOffset(2, 0, 0, 0);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.UpperLeft;

            var dot = UICanvasUtil.NewBody("Dot", row, "•", 16f, GoldDim, FontStyles.Bold);
            var dotLE = dot.gameObject.AddComponent<LayoutElement>();
            dotLE.preferredWidth = 16f; dotLE.minWidth = 16f; dotLE.preferredHeight = 26f;

            var t = UICanvasUtil.NewBody("Text", row, text, 16f, BodyColor);
            var tLE = t.gameObject.AddComponent<LayoutElement>();
            tLE.flexibleWidth = 1f;
        }

        private Button BuildNavButton(Transform parent, string name, bool isLeft, out TMP_Text labelOut)
        {
            var rt = UICanvasUtil.NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.childAlignment = isLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            TMP_Text label;
            if (isLeft)
            {
                var arrow = UICanvasUtil.NewBody("Arrow", rt, "←", 24f, GoldDim, FontStyles.Normal, TextAlignmentOptions.Left);
                AttachLayoutW(arrow.gameObject, 28f);

                var stack = UICanvasUtil.NewRect("Stack", rt);
                var stackVlg = stack.gameObject.AddComponent<VerticalLayoutGroup>();
                stackVlg.childAlignment = TextAnchor.MiddleLeft;
                stackVlg.childForceExpandWidth = true;
                stackVlg.childForceExpandHeight = false;
                stackVlg.spacing = 4f;
                var stackLE = stack.gameObject.AddComponent<LayoutElement>();
                stackLE.preferredWidth = 320f;
                var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", stack, "PREVIOUS", 11f, SubtleColor);
                AttachLayout(eyebrow.gameObject, 16f);
                label = UICanvasUtil.NewHeading("Label", stack, "", 20f, HeadingColor, FontStyles.Italic, TextAlignmentOptions.TopLeft);
                AttachLayout(label.gameObject, 28f);
            }
            else
            {
                var stack = UICanvasUtil.NewRect("Stack", rt);
                var stackVlg = stack.gameObject.AddComponent<VerticalLayoutGroup>();
                stackVlg.childAlignment = TextAnchor.MiddleRight;
                stackVlg.childForceExpandWidth = true;
                stackVlg.childForceExpandHeight = false;
                stackVlg.spacing = 4f;
                var stackLE = stack.gameObject.AddComponent<LayoutElement>();
                stackLE.preferredWidth = 320f;
                var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", stack, "NEXT", 11f, SubtleColor, TextAlignmentOptions.Right);
                AttachLayout(eyebrow.gameObject, 16f);
                label = UICanvasUtil.NewHeading("Label", stack, "", 20f, HeadingColor, FontStyles.Italic, TextAlignmentOptions.TopRight);
                AttachLayout(label.gameObject, 28f);

                var arrow = UICanvasUtil.NewBody("Arrow", rt, "→", 24f, GoldDim, FontStyles.Normal, TextAlignmentOptions.Right);
                AttachLayoutW(arrow.gameObject, 28f);
            }

            var fh = rt.gameObject.AddComponent<FocusHighlight>();
            ConfigureFocusHighlight(fh, label, rt as RectTransform, GoldColor, 1.04f);
            labelOut = label;
            return btn;
        }

        private void GoPrev()
        {
            if (_database == null || _current == null) return;
            int idx = IndexOf(_current);
            if (idx <= 0) return;
            SetCard(_database.Cards[idx - 1], _database);
        }

        private void GoNext()
        {
            if (_database == null || _current == null) return;
            int idx = IndexOf(_current);
            if (idx < 0 || idx >= _database.Count - 1) return;
            SetCard(_database.Cards[idx + 1], _database);
        }

        private int IndexOf(StoryCardData c)
        {
            if (_database == null) return -1;
            for (int i = 0; i < _database.Count; i++)
                if (_database.Cards[i] == c) return i;
            return -1;
        }

        private void UpdatePrevNextNav()
        {
            int idx = IndexOf(_current);
            int total = _database != null ? _database.Count : 0;

            bool hasPrev = idx > 0;
            bool hasNext = idx >= 0 && idx < total - 1;

            if (_prevButton != null) _prevButton.interactable = hasPrev;
            if (_nextButton != null) _nextButton.interactable = hasNext;
            if (_prevLabel != null) _prevLabel.text = hasPrev ? _database.Cards[idx - 1].Title : "";
            if (_nextLabel != null) _nextLabel.text = hasNext ? _database.Cards[idx + 1].Title : "";
            if (_pageIndicator != null && idx >= 0 && total > 0)
                _pageIndicator.text = (idx + 1) + " of " + total;
        }

        private static void AttachLayout(GameObject go, float minHeight)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = minHeight;
            le.preferredHeight = minHeight;
            le.flexibleWidth = 1f;
        }

        private static void AttachLayoutW(GameObject go, float minWidth)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = minWidth; le.preferredWidth = minWidth;
            le.flexibleHeight = 1f;
        }

        private static void ConfigureFocusHighlight(FocusHighlight fh, Graphic target, RectTransform scaleTarget, Color focused, float focusedScale)
        {
            var t = typeof(FocusHighlight);
            System.Action<string,object> setF = (string n, object v) => {
                var f = t.GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (f != null) f.SetValue(fh, v);
            };
            setF("_targetGraphic", target);
            setF("_scaleTarget", scaleTarget);
            setF("_focusedColor", focused);
            setF("_focusedScale", focusedScale);
            setF("_swapColor", true);
            setF("_swapScale", true);
            setF("_underlineText", false);
            setF("_selectOnHover", true);
            setF("_transitionDuration", 0.12f);
        }

        private void EnsureCanvas()
        {
            if (GetComponent<Canvas>() == null)
            {
                var c = gameObject.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                gameObject.AddComponent<CanvasScaler>().Init1080();
                gameObject.AddComponent<GraphicRaycaster>();
            }
            var rt = transform as RectTransform;
            if (rt != null) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
        }
    }
}
