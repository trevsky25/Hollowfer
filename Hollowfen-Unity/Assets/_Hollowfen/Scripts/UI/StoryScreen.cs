using System.Collections.Generic;
using Hollowfen.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class StoryScreen : UIScreen
    {
        [SerializeField] private StoryCardDatabase _database;
        [SerializeField] private StoryDetailScreen _detailScreen;

        private static readonly Color BgColor       = new Color(0.020f, 0.031f, 0.020f, 1f);
        private static readonly Color CardBgColor   = new Color(0.078f, 0.110f, 0.086f, 0.78f);
        private static readonly Color CardBorder    = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color HeadingColor  = new Color(0.961f, 0.925f, 0.855f, 1f);
        private static readonly Color BodyColor     = new Color(0.961f, 0.925f, 0.855f, 0.92f);
        private static readonly Color SubtleColor   = new Color(0.961f, 0.925f, 0.855f, 0.62f);
        private static readonly Color FaintColor    = new Color(0.961f, 0.925f, 0.855f, 0.45f);
        private static readonly Color GoldColor     = new Color(0.851f, 0.741f, 0.427f, 1f);
        private static readonly Color GoldFaint     = new Color(0.851f, 0.741f, 0.427f, 0.40f);

        private GameObject _firstCard;
        private bool _built;
        private RectTransform _scrollContent;

        public override GameObject DefaultSelected => _firstCard != null ? _firstCard : base.DefaultSelected;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;
            try { BuildLayout(); PopulateCards(); _built = true; }
            catch (System.Exception e) { Debug.LogError("[StoryScreen] OnInitialize failed: " + e); }
        }

        private void BuildLayout()
        {
            EnsureCanvas();

            var bg = UICanvasUtil.NewImage("BG", transform, BgColor, true);
            UICanvasUtil.Stretch(bg.GetComponent<RectTransform>());

            var col = UICanvasUtil.NewRect("Col", transform);
            col.anchorMin = new Vector2(0.5f, 0f);
            col.anchorMax = new Vector2(0.5f, 1f);
            col.pivot = new Vector2(0.5f, 0.5f);
            col.sizeDelta = new Vector2(1500f, 0f);
            col.offsetMin = new Vector2(col.offsetMin.x, 60f);
            col.offsetMax = new Vector2(col.offsetMax.x, -60f);

            // Header
            var header = UICanvasUtil.NewRect("Header", col);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 156f);

            var title = UICanvasUtil.NewHeading("Title", header, "Story", 92f, HeadingColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 100f), Vector2.zero);

            _counterText = UICanvasUtil.NewBody("Counter", header, BuildCounterCopy(), 20f, SubtleColor, FontStyles.Italic);
            UICanvasUtil.SetRect(_counterText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 28f), new Vector2(0f, -106f));

            var headerRule = UICanvasUtil.NewImage("HeaderRule", header, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.18f), false);
            UICanvasUtil.SetRect(headerRule.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(0f, -148f));

            // Scroll
            var scrollGo = UICanvasUtil.NewRect("Scroll", col);
            scrollGo.anchorMin = new Vector2(0f, 0f);
            scrollGo.anchorMax = new Vector2(1f, 1f);
            scrollGo.pivot = new Vector2(0.5f, 0.5f);
            scrollGo.offsetMin = Vector2.zero;
            scrollGo.offsetMax = new Vector2(0f, -170f);

            var scrollImg = scrollGo.gameObject.AddComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0f);
            scrollImg.raycastTarget = false;

            var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 50f;
            scrollGo.gameObject.AddComponent<ScrollFocusFollower>();

            var viewport = UICanvasUtil.NewRect("Viewport", scrollGo);
            UICanvasUtil.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = UICanvasUtil.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            scroll.content = content;

            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 26, 100);
            vlg.spacing = 22f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollContent = content;
        }

        // (cell, card) pairs for progression gating — refreshed every open.
        private readonly List<(GameObject go, StoryCardData card)> _builtCells = new List<(GameObject, StoryCardData)>();
        private TMP_Text _counterText;

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshLockStates();
        }

        // Web-parity "Locked Memory" treatment: darkened art, masked copy, click disabled.
        private void RefreshLockStates()
        {
            if (_counterText != null) _counterText.text = BuildCounterCopy();
            foreach (var (go, card) in _builtCells)
            {
                if (go == null || card == null) continue;
                bool locked = !Hollowfen.Quests.QuestManager.IsStoryCardUnlocked(card.Id);

                var thumb = go.transform.Find("Thumb");
                if (thumb != null) thumb.GetComponent<Image>().color =
                    locked ? new Color(0.14f, 0.12f, 0.10f, 1f) : Color.white;

                var body = go.transform.Find("Body");
                if (body == null) continue;
                var eyebrow = body.Find("Eyebrow")?.GetComponent<TMP_Text>();
                var title = body.Find("Title")?.GetComponent<TMP_Text>();
                var subtitle = body.Find("Subtitle")?.GetComponent<TMP_Text>();
                if (eyebrow != null)
                {
                    eyebrow.text = locked ? "LOCKED MEMORY" : (card.Scene ?? "").ToUpperInvariant();
                    eyebrow.color = locked ? FaintColor : GoldColor;
                }
                if (title != null) title.text = locked ? "· · ·" : card.Title;
                if (subtitle != null) subtitle.text = locked ? "A memory yet to be made." : card.Subtitle;
            }
        }

        private void PopulateCards()
        {
            if (_database == null || _scrollContent == null) return;

            var groups = new List<(string Act, List<StoryCardData> Cards)>();
            string lastAct = null;
            foreach (var c in _database.Cards)
            {
                if (c == null) continue;
                if (c.Act != lastAct)
                {
                    groups.Add((c.Act, new List<StoryCardData>()));
                    lastAct = c.Act;
                }
                groups[groups.Count - 1].Cards.Add(c);
            }

            foreach (var g in groups)
            {
                BuildActHeader(_scrollContent, g.Act, g.Cards.Count);
                var grid = BuildGrid(_scrollContent, g.Act);
                foreach (var card in g.Cards)
                {
                    var go = BuildCardCell(grid, card);
                    if (_firstCard == null) _firstCard = go;
                }
            }
        }

        private void BuildActHeader(Transform parent, string act, int count)
        {
            var row = UICanvasUtil.NewRect("ActHeader_" + act, parent);
            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 44f; rowLE.minHeight = 44f;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(2, 4, 12, 12);
            hlg.spacing = 18f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth = false;

            var label = UICanvasUtil.NewEyebrow("Label", row, act, 14f, GoldColor);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 22f; labelLE.minHeight = 22f;
            labelLE.preferredWidth = -1f;

            var rule = UICanvasUtil.NewImage("Rule", row, GoldFaint, false);
            var ruleLE = rule.AddComponent<LayoutElement>();
            ruleLE.preferredHeight = 1f; ruleLE.minHeight = 1f; ruleLE.flexibleWidth = 1f;

            var countT = UICanvasUtil.NewEyebrow("Count", row, count + " CARDS", 13f, FaintColor, TextAlignmentOptions.Right);
            var countLE = countT.gameObject.AddComponent<LayoutElement>();
            countLE.preferredHeight = 22f; countLE.minHeight = 22f;
            countLE.preferredWidth = 130f;
        }

        private RectTransform BuildGrid(Transform parent, string act)
        {
            var gridGo = UICanvasUtil.NewRect("Grid_" + act, parent);
            var grid = gridGo.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(489f, 400f);
            grid.spacing = new Vector2(16f, 16f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            var fitter = gridGo.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return gridGo;
        }

        private GameObject BuildCardCell(Transform parent, StoryCardData card)
        {
            var go = UICanvasUtil.NewRect("Card_" + card.Id, parent).gameObject;
            var img = go.AddComponent<Image>();
            img.color = CardBgColor;
            img.raycastTarget = true;
            // Rounded card face (batch-47 square sweep). The art thumb is inset below so the
            // rounded corners frame it like a mounted journal photograph — no stencil Mask
            // (URP UI + Mask rendered the fill cyan; gotcha logged).
            UICanvasUtil.Roundify(img, 14);

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            // Rounded hairline border (batch-47) — the uGUI Outline component washes a
            // sliced sprite gray (it re-draws the whole 9-slice 4x offset).
            var borderGo = UICanvasUtil.NewImage("Hairline", go.transform, CardBorder, false);
            UICanvasUtil.RoundifyOutline(borderGo.GetComponent<Image>(), 14, 1.5f);
            UICanvasUtil.Stretch((RectTransform)borderGo.transform);

            var fh = go.AddComponent<FocusHighlight>();

            // Image fills the top portion (220 of 400), inset 8px so the card's rounded
            // corners frame it (batch-47).
            var thumbRt = UICanvasUtil.NewRect("Thumb", go.transform);
            UICanvasUtil.SetRect(thumbRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-16f, 212f), new Vector2(0f, -8f));
            var thumbImg = thumbRt.gameObject.AddComponent<Image>();
            thumbImg.sprite = card.Image;
            thumbImg.preserveAspect = false;
            thumbImg.raycastTarget = false;

            // Body container
            var body = UICanvasUtil.NewRect("Body", go.transform);
            UICanvasUtil.SetRect(body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 180f), new Vector2(0f, -220f));
            body.offsetMin = new Vector2(24f, body.offsetMin.y);
            body.offsetMax = new Vector2(-24f, body.offsetMax.y);

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", body, card.Scene, 12f, GoldColor);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 22f), new Vector2(0f, -18f));

            var title = UICanvasUtil.NewHeading("Title", body, card.Title, 36f, HeadingColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 46f), new Vector2(0f, -46f));
            title.overflowMode = TextOverflowModes.Truncate;

            var subtitle = UICanvasUtil.NewBody("Subtitle", body, card.Subtitle, 17f, SubtleColor);
            UICanvasUtil.SetRect(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 70f), new Vector2(0f, -98f));
            subtitle.overflowMode = TextOverflowModes.Truncate;

            // Focus glow overlay (gold tint fades in)
            var glowRt = UICanvasUtil.NewRect("FocusGlow", go.transform);
            UICanvasUtil.Stretch(glowRt);
            var glowImg = glowRt.gameObject.AddComponent<Image>();
            glowImg.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0f);
            glowImg.raycastTarget = false;
            UICanvasUtil.Roundify(glowImg, 14); // glow follows the card shape (batch-47)

            var fhT = typeof(FocusHighlight);
            System.Action<string,object> setF = (string n, object v) => {
                var f = fhT.GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (f != null) f.SetValue(fh, v);
            };
            setF("_targetGraphic", glowImg);
            // Override the auto-captured _baseColor (which got the card's bg color
            // because FocusHighlight.Awake fired before we re-pointed _targetGraphic).
            setF("_baseColor", new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0f));
            setF("_focusedColor", new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.10f));
            setF("_focusedScale", 1.02f);
            setF("_swapColor", true);
            setF("_swapScale", true);
            setF("_underlineText", false);
            // Snap the glow to the resting (transparent) state immediately.
            glowImg.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0f);

            var cell = go.AddComponent<StoryCardCell>();
            cell.Bind(card, OnCardClicked);
            btn.onClick.AddListener(cell.HandleClick);
            _builtCells.Add((go, card));
            return go;
        }

        private void OnCardClicked(StoryCardData card)
        {
            if (!Hollowfen.Quests.QuestManager.IsStoryCardUnlocked(card.Id)) return;
            if (_detailScreen != null) _detailScreen.SetCard(card, _database);
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("story-detail");
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

        private string BuildCounterCopy()
        {
            int n = _database != null ? _database.Count : 0;
            int unlocked = 0;
            if (_database != null)
                foreach (var c in _database.Cards)
                    if (c != null && Hollowfen.Quests.QuestManager.IsStoryCardUnlocked(c.Id)) unlocked++;
            return $"{unlocked} of {n} memories recorded. Four acts and four possible endings — Hollowfen as Wren lives it.";
        }
    }
}
