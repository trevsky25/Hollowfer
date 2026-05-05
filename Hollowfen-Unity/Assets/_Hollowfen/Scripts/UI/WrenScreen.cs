using Hollowfen.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class WrenScreen : UIScreen
    {
        [SerializeField] private CharacterProfileData _profile;

        private static readonly Color BgColor       = new Color(0.020f, 0.031f, 0.020f, 1f);
        private static readonly Color HeadingColor  = new Color(0.961f, 0.925f, 0.855f, 1f);
        private static readonly Color BodyColor     = new Color(0.961f, 0.925f, 0.855f, 0.92f);
        private static readonly Color SubtleColor   = new Color(0.961f, 0.925f, 0.855f, 0.70f);
        private static readonly Color FaintColor    = new Color(0.961f, 0.925f, 0.855f, 0.50f);
        private static readonly Color GoldColor     = new Color(0.851f, 0.741f, 0.427f, 1f);
        private static readonly Color GoldFaint     = new Color(0.851f, 0.741f, 0.427f, 0.16f);
        private static readonly Color GoldBorder    = new Color(0.851f, 0.741f, 0.427f, 0.20f);
        private static readonly Color GoldStrong    = new Color(0.851f, 0.741f, 0.427f, 0.55f);
        private static readonly Color CardBg        = new Color(0.078f, 0.110f, 0.086f, 0.55f);
        private static readonly Color StatBg        = new Color(0.031f, 0.047f, 0.039f, 0.50f);
        private static readonly Color StatBorder    = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color HeroPanelTop  = new Color(0.086f, 0.121f, 0.094f, 0.76f);
        private static readonly Color HeroPanelBot  = new Color(0.047f, 0.071f, 0.055f, 0.90f);
        private static readonly Color KitTileBg     = new Color(0.031f, 0.047f, 0.039f, 0.42f);
        private static readonly Color KitTileBorder = new Color(0.851f, 0.741f, 0.427f, 0.14f);

        private bool _built;
        private GameObject _firstSelectable;

        public override GameObject DefaultSelected => _firstSelectable != null ? _firstSelectable : base.DefaultSelected;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;
            try { BuildLayout(); _built = true; }
            catch (System.Exception e) { Debug.LogError("[WrenScreen] OnInitialize failed: " + e); }
        }

        private void BuildLayout()
        {
            EnsureCanvas();

            var bg = UICanvasUtil.NewImage("BG", transform, BgColor, true);
            UICanvasUtil.Stretch(bg.GetComponent<RectTransform>());

            // Scrollable centered column
            var scrollGo = UICanvasUtil.NewRect("Scroll", transform);
            UICanvasUtil.Stretch(scrollGo);
            var scrollImg = scrollGo.gameObject.AddComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0f);
            scrollImg.raycastTarget = false;
            var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scrollGo.gameObject.AddComponent<ScrollFocusFollower>();

            var viewport = UICanvasUtil.NewRect("Viewport", scrollGo);
            UICanvasUtil.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = UICanvasUtil.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(1480f, 0f);
            content.anchoredPosition = Vector2.zero;
            scroll.content = content;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 60, 100);
            vlg.spacing = 24f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildHero(content);
            BuildKitStrip(content);
            BuildBodyCards(content);
            BuildPullquote(content);
        }

        private void BuildHero(Transform parent)
        {
            var hero = UICanvasUtil.NewRect("Hero", parent);
            var heroLE = hero.gameObject.AddComponent<LayoutElement>();
            heroLE.preferredHeight = 600f; heroLE.minHeight = 540f;
            var hlg = hero.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 28f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // PORTRAIT
            var portrait = UICanvasUtil.NewRect("Portrait", hero);
            var portraitLE = portrait.gameObject.AddComponent<LayoutElement>();
            portraitLE.preferredWidth = 820f; portraitLE.flexibleWidth = 0f;
            var pImg = portrait.gameObject.AddComponent<Image>();
            pImg.preserveAspect = false;
            pImg.raycastTarget = false;
            if (_profile != null) pImg.sprite = _profile.HeroPortrait;
            var pBorder = portrait.gameObject.AddComponent<Outline>();
            pBorder.effectColor = GoldBorder;
            pBorder.effectDistance = new Vector2(1f, -1f);

            // INFO PANEL
            var info = UICanvasUtil.NewRect("Info", hero);
            var infoLE = info.gameObject.AddComponent<LayoutElement>();
            infoLE.flexibleWidth = 1f; infoLE.minWidth = 420f;

            var stops = new[]
            {
                new UICanvasUtil.GradientStop(0f, HeroPanelBot),
                new UICanvasUtil.GradientStop(1f, HeroPanelTop),
            };
            var infoBg = UICanvasUtil.NewRect("BG", info);
            UICanvasUtil.Stretch(infoBg);
            var infoBgImg = infoBg.gameObject.AddComponent<Image>();
            infoBgImg.sprite = UICanvasUtil.MakeVerticalGradient(stops, 256);
            infoBgImg.type = Image.Type.Simple;
            infoBgImg.preserveAspect = false;
            infoBgImg.raycastTarget = false;
            var infoBorder = info.gameObject.AddComponent<Outline>();
            infoBorder.effectColor = GoldFaint;
            infoBorder.effectDistance = new Vector2(1f, -1f);

            var inner = UICanvasUtil.NewRect("Inner", info);
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(36f, 30f); inner.offsetMax = new Vector2(-36f, -30f);
            var innerVlg = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            innerVlg.spacing = 12f;
            innerVlg.childForceExpandWidth = true;
            innerVlg.childForceExpandHeight = false;
            innerVlg.childAlignment = TextAnchor.UpperLeft;

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", inner, _profile != null ? _profile.Role : "PROTAGONIST", 13f, GoldColor);
            AttachLayout(eyebrow.gameObject, 22f);

            var name = UICanvasUtil.NewHeading("Name", inner, _profile != null ? _profile.CharacterName : "Wren Tobin", 88f, HeadingColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            AttachLayout(name.gameObject, 200f);

            var tagline = UICanvasUtil.NewBody("Tagline", inner, _profile != null ? _profile.Tagline : "", 18f, SubtleColor, FontStyles.Italic);
            AttachLayout(tagline.gameObject, 60f);

            var leadGap = UICanvasUtil.NewRect("LeadGap", inner);
            var leadGapLE = leadGap.gameObject.AddComponent<LayoutElement>();
            leadGapLE.preferredHeight = 8f; leadGapLE.minHeight = 8f;

            var lead = UICanvasUtil.NewBody("Lead", inner, _profile != null ? _profile.LeadParagraph : "", 17f, BodyColor);
            AttachLayout(lead.gameObject, 200f);

            var statSpacer = UICanvasUtil.NewRect("StatSpacer", inner);
            var statSpacerLE = statSpacer.gameObject.AddComponent<LayoutElement>();
            statSpacerLE.flexibleHeight = 1f; statSpacerLE.minHeight = 8f;

            // 2x2 stats grid
            var statsRoot = UICanvasUtil.NewRect("Stats", inner);
            var statsLE = statsRoot.gameObject.AddComponent<LayoutElement>();
            statsLE.preferredHeight = 168f; statsLE.minHeight = 168f;
            var grid = statsRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(228f, 78f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            BuildStat(statsRoot, "AGE",      _profile != null ? _profile.Age      : "");
            BuildStat(statsRoot, "HOME",     _profile != null ? _profile.Home     : "");
            BuildStat(statsRoot, "WORK",     _profile != null ? _profile.Work     : "");
            BuildStat(statsRoot, "KEEPSAKE", _profile != null ? _profile.Keepsake : "");
        }

        private void BuildStat(Transform parent, string label, string value)
        {
            var card = UICanvasUtil.NewRect("Stat_" + label, parent).gameObject;
            var img = card.AddComponent<Image>();
            img.color = StatBg;
            img.raycastTarget = false;
            var outline = card.AddComponent<Outline>();
            outline.effectColor = StatBorder;
            outline.effectDistance = new Vector2(1f, -1f);

            var inner = UICanvasUtil.NewRect("Inner", card.transform);
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(14f, 12f); inner.offsetMax = new Vector2(-14f, -12f);
            var vlg = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var labelText = UICanvasUtil.NewEyebrow("Label", inner, label, 10f, FaintColor);
            AttachLayout(labelText.gameObject, 16f);

            var valueText = UICanvasUtil.NewBody("Value", inner, value, 19f, GoldColor, FontStyles.Bold);
            AttachLayout(valueText.gameObject, 32f);
        }

        private void BuildKitStrip(Transform parent)
        {
            var row = UICanvasUtil.NewRect("KitStrip", parent);
            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 132f; rowLE.minHeight = 110f;
            var grid = row.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(354f, 110f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            if (_profile != null && _profile.KitItems != null)
                foreach (var k in _profile.KitItems) BuildKitTile(row, k.Name, k.OneLine);
        }

        private void BuildKitTile(Transform parent, string name, string oneLine)
        {
            var tile = UICanvasUtil.NewRect("Kit_" + name, parent).gameObject;
            var img = tile.AddComponent<Image>();
            img.color = KitTileBg;
            img.raycastTarget = false;
            var outline = tile.AddComponent<Outline>();
            outline.effectColor = KitTileBorder;
            outline.effectDistance = new Vector2(1f, -1f);

            var inner = UICanvasUtil.NewRect("Inner", tile.transform);
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(16f, 14f); inner.offsetMax = new Vector2(-16f, -14f);
            var vlg = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var nameText = UICanvasUtil.NewEyebrow("Name", inner, name, 11f, GoldColor);
            AttachLayout(nameText.gameObject, 18f);

            var lineText = UICanvasUtil.NewBody("Line", inner, oneLine, 15f, BodyColor);
            AttachLayout(lineText.gameObject, 60f);
        }

        private void BuildBodyCards(Transform parent)
        {
            var row = UICanvasUtil.NewRect("BodyRow", parent);
            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 280f; rowLE.minHeight = 220f;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            BuildBodyCard(row, "Background", _profile != null ? _profile.BackgroundParagraph : "");
            BuildBodyCard(row, "How she sees the world", _profile != null ? _profile.PerspectiveParagraph : "");
        }

        private void BuildBodyCard(Transform parent, string heading, string body)
        {
            var card = UICanvasUtil.NewRect("Body_" + heading, parent).gameObject;
            var img = card.AddComponent<Image>();
            img.color = CardBg;
            img.raycastTarget = false;
            var outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.06f);
            outline.effectDistance = new Vector2(1f, -1f);

            var inner = UICanvasUtil.NewRect("Inner", card.transform);
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(24f, 22f); inner.offsetMax = new Vector2(-24f, -20f);
            var vlg = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var head = UICanvasUtil.NewEyebrow("Header", inner, heading, 11f, GoldColor);
            AttachLayout(head.gameObject, 18f);

            var bodyText = UICanvasUtil.NewBody("Body", inner, body, 16f, BodyColor);
            var le = bodyText.gameObject.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f; le.minHeight = 120f;
        }

        private void BuildPullquote(Transform parent)
        {
            var card = UICanvasUtil.NewRect("Pullquote", parent).gameObject;
            var bg = card.AddComponent<Image>();
            bg.color = CardBg;
            bg.raycastTarget = false;

            var border = UICanvasUtil.NewImage("LeftBorder", card.transform, GoldStrong, false);
            UICanvasUtil.SetRect(border.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(3f, 0f), Vector2.zero);

            var inner = UICanvasUtil.NewRect("Inner", card.transform);
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(32f, 26f); inner.offsetMax = new Vector2(-32f, -26f);

            var quote = _profile != null ? "“" + _profile.Pullquote + "”" : "";
            var quoteText = UICanvasUtil.NewBody("Quote", inner, quote, 22f, BodyColor, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(quoteText.rectTransform);

            var le = card.AddComponent<LayoutElement>();
            le.preferredHeight = 120f; le.minHeight = 90f;
        }

        private static void AttachLayout(GameObject go, float minHeight)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = minHeight;
            le.preferredHeight = minHeight;
            le.flexibleWidth = 1f;
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
