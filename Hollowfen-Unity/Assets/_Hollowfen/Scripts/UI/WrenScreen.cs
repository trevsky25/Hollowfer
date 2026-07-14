using System.Collections.Generic;
using Hollowfen.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Batch-61 "Forager's Dossier": full-bleed hero painting with a slow Ken Burns
    // drift + scroll parallax, a left identity column over the art, then dossier
    // cards, kit strip, and a Field Study plate gallery (turnaround sheet, three
    // figure studies, knife plate) closed by the pullquote. Every section carries
    // an invisible focus waypoint so a pad walks the page top-to-bottom and
    // ScrollFocusFollower glides between sections.
    public class WrenScreen : UIScreen
    {
        [SerializeField] private CharacterProfileData _profile;

        private static readonly Color BgColor      = new Color(0.020f, 0.031f, 0.020f, 1f);
        private static readonly Color HeadingColor = new Color(0.961f, 0.925f, 0.855f, 1f);
        private static readonly Color BodyColor    = new Color(0.961f, 0.925f, 0.855f, 0.92f);
        private static readonly Color SubtleColor  = new Color(0.961f, 0.925f, 0.855f, 0.72f);
        private static readonly Color FaintColor   = new Color(0.961f, 0.925f, 0.855f, 0.45f);
        private static readonly Color GoldColor    = new Color(0.851f, 0.741f, 0.427f, 1f);
        private static readonly Color GoldBright   = new Color(0.94f, 0.85f, 0.55f, 1f);
        private static readonly Color GoldBorder   = new Color(0.851f, 0.741f, 0.427f, 0.22f);
        private static readonly Color GoldStrong   = new Color(0.851f, 0.741f, 0.427f, 0.60f);
        private static readonly Color CardBg       = new Color(0.043f, 0.063f, 0.047f, 0.72f);
        private static readonly Color PlateBg      = new Color(0.035f, 0.051f, 0.039f, 0.88f);
        private static readonly Color KitTileBg    = new Color(0.031f, 0.047f, 0.039f, 0.60f);
        private static readonly Color HairlineDim  = new Color(0.851f, 0.741f, 0.427f, 0.16f);

        private const float ContentWidth = 1680f;

        private bool _built;
        private GameObject _firstSelectable;
        private ScrollRect _scroll;
        private RectTransform _heroDrift;
        private Image _deepScrim;
        private readonly List<Selectable> _waypoints = new List<Selectable>();

        public override GameObject DefaultSelected => _firstSelectable != null ? _firstSelectable : base.DefaultSelected;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;
            try { BuildLayout(); _built = true; }
            catch (System.Exception e) { Debug.LogError("[WrenScreen] OnInitialize failed: " + e); }
        }

        public override void OnOpen()
        {
            base.OnOpen();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        private void Update()
        {
            // Slow breathe + drift on the hero painting; deepen the scrim and let the
            // painting lag slightly behind the scroll so the page reads with depth.
            if (_heroDrift == null) return;
            float t = Time.unscaledTime;
            float s = 1.05f + 0.028f * Mathf.Sin(t * 0.07f);
            float x = 14f * Mathf.Sin(t * 0.045f + 1.3f);
            float scrolled = _scroll != null && _scroll.content != null ? Mathf.Max(0f, _scroll.content.anchoredPosition.y) : 0f;
            float y = Mathf.Min(90f, scrolled * 0.07f);
            _heroDrift.localScale = new Vector3(s, s, 1f);
            _heroDrift.anchoredPosition = new Vector2(x, y);
            if (_deepScrim != null)
            {
                var c = _deepScrim.color;
                c.a = Mathf.Clamp01((scrolled - 380f) / 720f) * 0.78f;
                _deepScrim.color = c;
            }
        }

        private void BuildLayout()
        {
            EnsureCanvas();

            var bg = UICanvasUtil.NewImage("BG", transform, BgColor, false);
            UICanvasUtil.Stretch(bg.GetComponent<RectTransform>());

            BuildHeroBackdrop();
            BuildScrims();

            // Scroll layer above the fixed backdrop
            var scrollGo = UICanvasUtil.NewRect("Scroll", transform);
            UICanvasUtil.Stretch(scrollGo);
            var scrollImg = scrollGo.gameObject.AddComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0f);
            scrollImg.raycastTarget = true; // wheel/drag scrolling anywhere on the page
            _scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 42f;
            scrollGo.gameObject.AddComponent<ScrollFocusFollower>();

            var viewport = UICanvasUtil.NewRect("Viewport", scrollGo);
            UICanvasUtil.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _scroll.viewport = viewport;

            var content = UICanvasUtil.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(ContentWidth, 0f);
            content.anchoredPosition = Vector2.zero;
            _scroll.content = content;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 150, 150);
            vlg.spacing = 34f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var identity = BuildIdentity(content);
            Selectable bgCard, perspCard;
            BuildDossierRow(content, out bgCard, out perspCard);
            var kit = BuildKitSection(content);
            BuildSectionHeader(content, "Field study · Character sheet", GoldColor);
            var plate1 = BuildStudySheetPlate(content);
            var figures = BuildFigureRow(content);
            var knife = BuildKnifeQuoteRow(content);
            BuildSectionHeader(content, "Hollowfen · The Failing Village", FaintColor);

            var close = BuildCloseButton();

            WireNavigation(close, identity, bgCard, perspCard, kit, plate1, figures, knife);
            _firstSelectable = identity.gameObject;

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        // ---------- Fixed backdrop ----------

        private void BuildHeroBackdrop()
        {
            var driftRoot = UICanvasUtil.NewRect("HeroDrift", transform);
            UICanvasUtil.Stretch(driftRoot);
            _heroDrift = driftRoot;

            var hero = UICanvasUtil.NewRect("Hero", driftRoot);
            UICanvasUtil.Stretch(hero);
            var img = hero.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = false;
            var sprite = _profile != null ? _profile.HeroPortrait : null;
            img.sprite = sprite;
            var arf = hero.gameObject.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            arf.aspectRatio = sprite != null ? sprite.rect.width / sprite.rect.height : 16f / 9f;
        }

        private void BuildScrims()
        {
            // Left column shade so the identity text reads over the painting
            var leftStops = new[]
            {
                new UICanvasUtil.GradientStop(0f,    new Color(BgColor.r, BgColor.g, BgColor.b, 0.92f)),
                new UICanvasUtil.GradientStop(0.32f, new Color(BgColor.r, BgColor.g, BgColor.b, 0.62f)),
                new UICanvasUtil.GradientStop(0.62f, new Color(BgColor.r, BgColor.g, BgColor.b, 0.10f)),
                new UICanvasUtil.GradientStop(1f,    new Color(BgColor.r, BgColor.g, BgColor.b, 0f)),
            };
            var left = UICanvasUtil.NewRect("ScrimLeft", transform);
            UICanvasUtil.Stretch(left);
            var leftImg = left.gameObject.AddComponent<Image>();
            leftImg.sprite = UICanvasUtil.MakeHorizontalGradient(leftStops, 512);
            leftImg.type = Image.Type.Simple;
            leftImg.preserveAspect = false;
            leftImg.raycastTarget = false;

            // Bottom shade for content scrolling up over the painting
            var bottomStops = new[]
            {
                new UICanvasUtil.GradientStop(0f,    new Color(BgColor.r, BgColor.g, BgColor.b, 0.94f)),
                new UICanvasUtil.GradientStop(0.55f, new Color(BgColor.r, BgColor.g, BgColor.b, 0.42f)),
                new UICanvasUtil.GradientStop(1f,    new Color(BgColor.r, BgColor.g, BgColor.b, 0f)),
            };
            var bottom = UICanvasUtil.NewRect("ScrimBottom", transform);
            UICanvasUtil.SetRect(bottom, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 520f), Vector2.zero);
            var bottomImg = bottom.gameObject.AddComponent<Image>();
            bottomImg.sprite = UICanvasUtil.MakeVerticalGradient(bottomStops, 256);
            bottomImg.preserveAspect = false;
            bottomImg.raycastTarget = false;

            // Thin top shade so the close glyph always reads
            var topStops = new[]
            {
                new UICanvasUtil.GradientStop(0f, new Color(BgColor.r, BgColor.g, BgColor.b, 0f)),
                new UICanvasUtil.GradientStop(1f, new Color(BgColor.r, BgColor.g, BgColor.b, 0.55f)),
            };
            var top = UICanvasUtil.NewRect("ScrimTop", transform);
            UICanvasUtil.SetRect(top, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 180f), Vector2.zero);
            var topImg = top.gameObject.AddComponent<Image>();
            topImg.sprite = UICanvasUtil.MakeVerticalGradient(topStops, 128);
            topImg.preserveAspect = false;
            topImg.raycastTarget = false;

            // Near-black wash that fades in as the reader scrolls into the plates
            var deep = UICanvasUtil.NewImage("DeepScrim", transform, new Color(BgColor.r, BgColor.g, BgColor.b, 0f), false);
            UICanvasUtil.Stretch(deep.GetComponent<RectTransform>());
            _deepScrim = deep.GetComponent<Image>();
        }

        // ---------- Identity ----------

        private Selectable BuildIdentity(Transform parent)
        {
            var block = UICanvasUtil.NewRect("Identity", parent);
            Pin(block.gameObject, ContentWidth, 660f);

            var column = UICanvasUtil.NewRect("Column", block);
            column.anchorMin = new Vector2(0f, 0f);
            column.anchorMax = new Vector2(0f, 1f);
            column.pivot = new Vector2(0f, 0.5f);
            column.anchoredPosition = new Vector2(48f, 0f);
            column.sizeDelta = new Vector2(860f, 0f);
            var vlg = column.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            // forceExpandWidth would stretch the pinned gold rule to full column width
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            string eyebrowText = (_profile != null ? _profile.Role : "Protagonist") + "  ·  " + (_profile != null ? _profile.Home : "Hollowfen");
            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", column, eyebrowText, 14f, GoldColor);
            Pin(eyebrow.gameObject, 0f, 26f, true);

            var name = UICanvasUtil.NewHeading("Name", column, _profile != null ? _profile.CharacterName : "Wren Tobin", 118f, HeadingColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Pin(name.gameObject, 0f, 150f, true);

            var tagline = UICanvasUtil.NewBody("Tagline", column, _profile != null ? _profile.Tagline : "", 21f, SubtleColor, FontStyles.Italic);
            Pin(tagline.gameObject, 0f, 44f, true);

            var rule = UICanvasUtil.NewImage("Rule", column, GoldStrong, false);
            Pin(rule, 150f, 2f);

            var lead = UICanvasUtil.NewBody("Lead", column, _profile != null ? _profile.LeadParagraph : "", 18f, BodyColor);
            Pin(lead.gameObject, 0f, 170f, true);

            var stats = UICanvasUtil.NewRect("Stats", column);
            Pin(stats.gameObject, 0f, 96f, true);
            var hlg = stats.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 22f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            BuildStat(stats, "Age",      _profile != null ? _profile.Age      : "");
            BuildStatDivider(stats);
            BuildStat(stats, "Home",     _profile != null ? _profile.Home     : "");
            BuildStatDivider(stats);
            BuildStat(stats, "Work",     _profile != null ? _profile.Work     : "");
            BuildStatDivider(stats);
            BuildStat(stats, "Keepsake", _profile != null ? _profile.Keepsake : "");

            return AddWaypoint(block.gameObject, rule.GetComponent<Image>(), GoldBright);
        }

        private void BuildStat(Transform parent, string label, string value)
        {
            var cell = UICanvasUtil.NewRect("Stat_" + label, parent);
            var le = cell.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = label == "Keepsake" ? 220f : 160f;
            le.flexibleWidth = 0f;
            var vlg = cell.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(0, 0, 14, 10);
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var labelText = UICanvasUtil.NewEyebrow("Label", cell, label, 11f, FaintColor);
            Pin(labelText.gameObject, 0f, 18f, true);

            var valueText = UICanvasUtil.NewHeading("Value", cell, value, 25f, GoldColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            valueText.enableWordWrapping = false;
            Pin(valueText.gameObject, 0f, 36f, true);
        }

        private void BuildStatDivider(Transform parent)
        {
            var div = UICanvasUtil.NewImage("Divider", parent, GoldBorder, false);
            var le = div.AddComponent<LayoutElement>();
            le.preferredWidth = 1f;
            le.preferredHeight = 60f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;
        }

        // ---------- Dossier cards ----------

        private void BuildDossierRow(Transform parent, out Selectable bgCard, out Selectable perspCard)
        {
            var row = UICanvasUtil.NewRect("DossierRow", parent);
            Pin(row.gameObject, ContentWidth, 252f);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24f;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            bgCard    = BuildDossierCard(row, "Background", _profile != null ? _profile.BackgroundParagraph : "");
            perspCard = BuildDossierCard(row, "How she sees the world", _profile != null ? _profile.PerspectiveParagraph : "");
        }

        private Selectable BuildDossierCard(Transform parent, string heading, string body)
        {
            var card = UICanvasUtil.NewRect("Card_" + heading, parent).gameObject;
            var img = card.AddComponent<Image>();
            img.color = CardBg;
            img.raycastTarget = true;
            UICanvasUtil.Roundify(img, 16);
            var hairline = UICanvasUtil.NewImage("Hairline", card.transform, HairlineDim, false);
            UICanvasUtil.RoundifyOutline(hairline.GetComponent<Image>(), 16, 1.4f);
            UICanvasUtil.Stretch((RectTransform)hairline.transform);

            var inner = UICanvasUtil.NewRect("Inner", card.transform);
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(34f, 26f); inner.offsetMax = new Vector2(-34f, -28f);
            var vlg = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14f;
            vlg.childForceExpandWidth = false; // keep the pinned rule at its own width
            vlg.childForceExpandHeight = false;

            var head = UICanvasUtil.NewEyebrow("Header", inner, heading, 12f, GoldColor);
            Pin(head.gameObject, 0f, 20f, true);

            var rule = UICanvasUtil.NewImage("Rule", inner, GoldBorder, false);
            Pin(rule, 56f, 1f);

            var bodyText = UICanvasUtil.NewBody("Body", inner, body, 16.5f, BodyColor);
            var le = bodyText.gameObject.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f; le.minHeight = 140f; le.flexibleWidth = 1f;

            return AddWaypoint(card, hairline.GetComponent<Image>(), GoldStrong);
        }

        // ---------- Kit ----------

        private Selectable BuildKitSection(Transform parent)
        {
            var section = UICanvasUtil.NewRect("KitSection", parent);
            Pin(section.gameObject, ContentWidth, 172f);
            var vlg = section.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            BuildSectionHeaderInto(section, "She carries", GoldColor, out Image headerRule);

            var row = UICanvasUtil.NewRect("Tiles", section);
            Pin(row.gameObject, 0f, 126f, true);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 18f;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            if (_profile != null && _profile.KitItems != null)
                foreach (var k in _profile.KitItems) BuildKitTile(row, k.Name, k.OneLine);

            return AddWaypoint(section.gameObject, headerRule, GoldStrong);
        }

        private void BuildKitTile(Transform parent, string name, string oneLine)
        {
            var tile = UICanvasUtil.NewRect("Kit_" + name, parent).gameObject;
            var img = tile.AddComponent<Image>();
            img.color = KitTileBg;
            img.raycastTarget = false;
            UICanvasUtil.Roundify(img, 12);
            var hairline = UICanvasUtil.NewImage("Hairline", tile.transform, HairlineDim, false);
            UICanvasUtil.RoundifyOutline(hairline.GetComponent<Image>(), 12, 1.2f);
            UICanvasUtil.Stretch((RectTransform)hairline.transform);

            var inner = UICanvasUtil.NewRect("Inner", tile.transform);
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(22f, 16f); inner.offsetMax = new Vector2(-22f, -18f);
            var vlg = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var nameText = UICanvasUtil.NewEyebrow("Name", inner, name, 11.5f, GoldColor);
            Pin(nameText.gameObject, 0f, 18f, true);

            var lineText = UICanvasUtil.NewBody("Line", inner, oneLine, 15.5f, BodyColor);
            Pin(lineText.gameObject, 0f, 48f, true);
        }

        // ---------- Field study plates ----------

        private Selectable BuildStudySheetPlate(Transform parent)
        {
            var panel = UICanvasUtil.NewRect("Plate_StudySheet", parent).gameObject;
            Pin(panel, 1500f, 1042f);
            var img = panel.AddComponent<Image>();
            img.color = PlateBg;
            img.raycastTarget = true;
            UICanvasUtil.Roundify(img, 10);
            var hairline = UICanvasUtil.NewImage("Hairline", panel.transform, HairlineDim, false);
            UICanvasUtil.RoundifyOutline(hairline.GetComponent<Image>(), 10, 1.4f);
            UICanvasUtil.Stretch((RectTransform)hairline.transform);

            var art = UICanvasUtil.NewRect("Art", panel.transform);
            UICanvasUtil.SetRect(art, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1440f, 960f), new Vector2(0f, -22f));
            var artImg = art.gameObject.AddComponent<Image>();
            artImg.sprite = _profile != null ? _profile.StudySheet : null;
            artImg.preserveAspect = true;
            artImg.raycastTarget = false;

            BuildPlateCaption(panel.transform, "Study sheet · Wren Tobin", "Plate I");
            return AddWaypoint(panel, hairline.GetComponent<Image>(), GoldStrong);
        }

        private Selectable BuildFigureRow(Transform parent)
        {
            var row = UICanvasUtil.NewRect("Plate_Figures", parent);
            Pin(row.gameObject, 1500f, 748f);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            Image firstHairline = null;
            var plates = new[]
            {
                new { Sprite = _profile != null ? _profile.FigureFront : null,        Caption = "Plate II · Front" },
                new { Sprite = _profile != null ? _profile.FigureBack : null,         Caption = "Plate III · Back" },
                new { Sprite = _profile != null ? _profile.FigureThreeQuarter : null, Caption = "Plate IV · Three-quarter" },
            };
            foreach (var p in plates)
            {
                var hl = BuildFigureTile(row, p.Sprite, p.Caption);
                if (firstHairline == null) firstHairline = hl;
            }

            return AddWaypoint(row.gameObject, firstHairline, GoldStrong);
        }

        private Image BuildFigureTile(Transform parent, Sprite sprite, string caption)
        {
            var tile = UICanvasUtil.NewRect("Figure_" + caption, parent).gameObject;
            var img = tile.AddComponent<Image>();
            img.color = PlateBg;
            img.raycastTarget = false;
            UICanvasUtil.Roundify(img, 10);
            var hairline = UICanvasUtil.NewImage("Hairline", tile.transform, HairlineDim, false);
            UICanvasUtil.RoundifyOutline(hairline.GetComponent<Image>(), 10, 1.2f);
            UICanvasUtil.Stretch((RectTransform)hairline.transform);

            var art = UICanvasUtil.NewRect("Art", tile.transform);
            UICanvasUtil.SetRect(art, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(440f, 646f), new Vector2(0f, -22f));
            var artImg = art.gameObject.AddComponent<Image>();
            artImg.sprite = sprite;
            artImg.preserveAspect = true;
            artImg.raycastTarget = false;

            var cap = UICanvasUtil.NewEyebrow("Caption", tile.transform, caption, 11.5f, GoldColor, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(cap.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(0f, 26f));
            return hairline.GetComponent<Image>();
        }

        private Selectable BuildKnifeQuoteRow(Transform parent)
        {
            var row = UICanvasUtil.NewRect("Plate_KnifeQuote", parent);
            Pin(row.gameObject, ContentWidth, 620f);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // Knife plate
            var knife = UICanvasUtil.NewRect("Knife", row).gameObject;
            var knifeLe = knife.AddComponent<LayoutElement>();
            knifeLe.preferredWidth = 900f; knifeLe.flexibleWidth = 0f;
            var kImg = knife.AddComponent<Image>();
            kImg.color = PlateBg;
            kImg.raycastTarget = false;
            UICanvasUtil.Roundify(kImg, 10);
            var kHairline = UICanvasUtil.NewImage("Hairline", knife.transform, HairlineDim, false);
            UICanvasUtil.RoundifyOutline(kHairline.GetComponent<Image>(), 10, 1.2f);
            UICanvasUtil.Stretch((RectTransform)kHairline.transform);

            var art = UICanvasUtil.NewRect("Art", knife.transform);
            UICanvasUtil.SetRect(art, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(840f, 540f), new Vector2(0f, -18f));
            var artImg = art.gameObject.AddComponent<Image>();
            artImg.sprite = _profile != null ? _profile.KnifePlate : null;
            artImg.preserveAspect = true;
            artImg.raycastTarget = false;

            BuildPlateCaption(knife.transform, "The forager's knife", "Plate V");

            // Pullquote
            var quote = UICanvasUtil.NewRect("Quote", row).gameObject;
            var quoteLe = quote.AddComponent<LayoutElement>();
            quoteLe.flexibleWidth = 1f;
            var qImg = quote.AddComponent<Image>();
            qImg.color = CardBg;
            qImg.raycastTarget = true;
            UICanvasUtil.Roundify(qImg, 16);
            var qHairline = UICanvasUtil.NewImage("Hairline", quote.transform, HairlineDim, false);
            UICanvasUtil.RoundifyOutline(qHairline.GetComponent<Image>(), 16, 1.4f);
            UICanvasUtil.Stretch((RectTransform)qHairline.transform);

            // body font — the “ glyph is known-good there (old screen rendered it)
            var mark = UICanvasUtil.NewBody("Mark", quote.transform, "“", 150f, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.35f), FontStyles.Italic, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(mark.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140f, 150f), new Vector2(40f, -18f));

            var quoteText = UICanvasUtil.NewBody("Text", quote.transform,
                _profile != null ? _profile.Pullquote : "", 23f, BodyColor, FontStyles.Italic, TextAlignmentOptions.Center);
            var qtRt = quoteText.rectTransform;
            qtRt.anchorMin = Vector2.zero; qtRt.anchorMax = Vector2.one;
            qtRt.offsetMin = new Vector2(70f, 90f); qtRt.offsetMax = new Vector2(-70f, -120f);

            var attribution = UICanvasUtil.NewEyebrow("Attribution", quote.transform,
                "— " + (_profile != null ? _profile.CharacterName : "Wren Tobin"), 12f, GoldColor, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(attribution.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(0f, 44f));

            return AddWaypoint(row.gameObject, qHairline.GetComponent<Image>(), GoldStrong);
        }

        private void BuildPlateCaption(Transform panel, string left, string right)
        {
            var leftCap = UICanvasUtil.NewEyebrow("CaptionL", panel, left, 12f, GoldColor);
            UICanvasUtil.SetRect(leftCap.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(900f, 24f), new Vector2(34f, 20f));

            var rightCap = UICanvasUtil.NewEyebrow("CaptionR", panel, right, 12f, FaintColor, TextAlignmentOptions.Right);
            UICanvasUtil.SetRect(rightCap.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(300f, 24f), new Vector2(-34f, 20f));
        }

        // ---------- Section headers / footer ----------

        private void BuildSectionHeader(Transform parent, string title, Color color)
        {
            var header = UICanvasUtil.NewRect("Section_" + title, parent);
            Pin(header.gameObject, ContentWidth, 26f);
            BuildHeaderRow(header, title, color, out _);
        }

        private void BuildSectionHeaderInto(Transform parent, string title, Color color, out Image leadingRule)
        {
            var header = UICanvasUtil.NewRect("Section_" + title, parent);
            Pin(header.gameObject, 0f, 26f, true);
            leadingRule = BuildHeaderRow(header, title, color, out _);
        }

        // Centered eyebrow flanked by hairline rules. Returns the leading rule image.
        private Image BuildHeaderRow(RectTransform header, string title, Color color, out TMP_Text label)
        {
            var hlg = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 26f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var ruleL = UICanvasUtil.NewImage("RuleL", header, GoldBorder, false);
            var ruleLLe = ruleL.AddComponent<LayoutElement>();
            ruleLLe.flexibleWidth = 1f; ruleLLe.preferredHeight = 1f;

            label = UICanvasUtil.NewEyebrow("Title", header, title, 13f, color, TextAlignmentOptions.Center);
            var labelLe = label.gameObject.AddComponent<LayoutElement>();
            labelLe.preferredHeight = 24f;

            var ruleR = UICanvasUtil.NewImage("RuleR", header, GoldBorder, false);
            var ruleRLe = ruleR.AddComponent<LayoutElement>();
            ruleRLe.flexibleWidth = 1f; ruleRLe.preferredHeight = 1f;

            return ruleL.GetComponent<Image>();
        }

        // ---------- Close / navigation / waypoints ----------

        private Selectable BuildCloseButton()
        {
            var closeRt = UICanvasUtil.NewRect("CloseButton", transform);
            UICanvasUtil.SetRect(closeRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(48f, 48f), new Vector2(-32f, -32f));
            var closeImg = closeRt.gameObject.AddComponent<Image>();
            closeImg.color = new Color(0f, 0f, 0f, 0f);
            var btn = closeRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = closeImg;
            btn.onClick.AddListener(() => { if (UIManager.Instance != null) UIManager.Instance.Back(); });
            var closeText = UICanvasUtil.NewBody("X", closeRt, "<sprite name=\"ui_x\">", 32f, SubtleColor, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(closeText.rectTransform);
            var fh = closeRt.gameObject.AddComponent<FocusHighlight>();
            ConfigureFocusHighlight(fh, closeText, closeRt, GoldColor, 1.12f);
            return btn;
        }

        // Invisible full-section selectable; focusing it lights the section's gold
        // hairline and lets ScrollFocusFollower bring the section into view.
        private Selectable AddWaypoint(GameObject host, Graphic highlightTarget, Color focusedColor)
        {
            var img = host.GetComponent<Image>();
            if (img == null)
            {
                img = host.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0f);
            }
            img.raycastTarget = true;

            var sel = host.AddComponent<Selectable>();
            sel.transition = Selectable.Transition.None;

            if (highlightTarget != null)
            {
                var fh = host.AddComponent<FocusHighlight>();
                ConfigureFocusHighlight(fh, highlightTarget, null, focusedColor, 1f);
            }
            _waypoints.Add(sel);
            return sel;
        }

        private void WireNavigation(Selectable close, Selectable identity, Selectable bgCard, Selectable perspCard,
                                    Selectable kit, Selectable plate1, Selectable figures, Selectable knife)
        {
            SetNav(close,     up: knife,    down: identity, left: null,    right: null);
            SetNav(identity,  up: close,    down: bgCard,   left: null,    right: null);
            SetNav(bgCard,    up: identity, down: kit,      left: null,    right: perspCard);
            SetNav(perspCard, up: identity, down: kit,      left: bgCard,  right: null);
            SetNav(kit,       up: bgCard,   down: plate1,   left: null,    right: null);
            SetNav(plate1,    up: kit,      down: figures,  left: null,    right: null);
            SetNav(figures,   up: plate1,   down: knife,    left: null,    right: null);
            SetNav(knife,     up: figures,  down: close,    left: null,    right: null);
        }

        private static void SetNav(Selectable s, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            if (s == null) return;
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = up; nav.selectOnDown = down; nav.selectOnLeft = left; nav.selectOnRight = right;
            s.navigation = nav;
        }

        private static void ConfigureFocusHighlight(FocusHighlight fh, Graphic target, RectTransform scaleTarget, Color focused, float focusedScale)
        {
            var t = typeof(FocusHighlight);
            System.Action<string, object> setF = (string n, object v) =>
            {
                var f = t.GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (f != null) f.SetValue(fh, v);
            };
            setF("_targetGraphic", target);
            // leave _scaleTarget on Awake's default (own transform) when we don't scale,
            // so FocusHighlight never dereferences a null scale target
            if (scaleTarget != null) setF("_scaleTarget", scaleTarget);
            setF("_focusedColor", focused);
            setF("_focusedScale", focusedScale);
            setF("_swapColor", true);
            setF("_swapScale", scaleTarget != null);
            setF("_underlineText", false);
            setF("_selectOnHover", true);
            setF("_transitionDuration", 0.12f);
            // FocusHighlight.Awake may have cached _baseColor off the wrong graphic
            // before the reflection re-point (ui-framework gotcha) — re-cache it.
            if (target != null) setF("_baseColor", target.color);
        }

        // ---------- Layout helpers ----------

        // Pin a fixed layout size. Pass stretchWidth=true to keep width driven by the
        // parent layout group and only pin the height.
        private static void Pin(GameObject go, float width, float height, bool stretchWidth = false)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            if (stretchWidth) { le.flexibleWidth = 1f; }
            else if (width > 0f) { le.minWidth = width; le.preferredWidth = width; le.flexibleWidth = 0f; }
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
