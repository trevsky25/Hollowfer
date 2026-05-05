using Hollowfen.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class FieldGuideScreen : UIScreen
    {
        [SerializeField] private MushroomFieldGuideDatabase _database;
        [SerializeField] private MushroomDetailScreen _detailScreen;

        private static readonly Color BgColor       = new Color(0.020f, 0.031f, 0.020f, 1f);
        private static readonly Color CardBgColor   = new Color(0.078f, 0.110f, 0.086f, 0.78f);
        private static readonly Color CardBorder    = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color HeadingColor  = new Color(0.961f, 0.925f, 0.855f, 1f);
        private static readonly Color SubtleColor   = new Color(0.961f, 0.925f, 0.855f, 0.62f);
        private static readonly Color FaintColor    = new Color(0.961f, 0.925f, 0.855f, 0.45f);
        private static readonly Color GoldColor     = new Color(0.851f, 0.741f, 0.427f, 1f);

        private GameObject _firstCell;
        private bool _built;
        private RectTransform _gridContent;

        public override GameObject DefaultSelected => _firstCell != null ? _firstCell : base.DefaultSelected;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;
            try { BuildLayout(); PopulateGrid(); _built = true; }
            catch (System.Exception e) { Debug.LogError("[FieldGuideScreen] OnInitialize failed: " + e); }
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

            var header = UICanvasUtil.NewRect("Header", col);
            header.anchorMin = new Vector2(0f, 1f); header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 156f);

            var title = UICanvasUtil.NewHeading("Title", header, "Field Guide", 92f, HeadingColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 100f), Vector2.zero);

            int n = _database != null ? _database.Count : 0;
            var counter = UICanvasUtil.NewBody("Counter", header, $"{n} of {n} species recorded. Click a thumbnail to see ID features, habitat, and lookalikes.", 20f, SubtleColor, FontStyles.Italic);
            UICanvasUtil.SetRect(counter.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 28f), new Vector2(0f, -106f));

            var rule = UICanvasUtil.NewImage("Rule", header, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.18f), false);
            UICanvasUtil.SetRect(rule.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(0f, -148f));

            var scrollGo = UICanvasUtil.NewRect("Scroll", col);
            scrollGo.anchorMin = new Vector2(0f, 0f); scrollGo.anchorMax = new Vector2(1f, 1f);
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
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            scroll.content = content;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(363f, 400f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(0, 0, 26, 100);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _gridContent = content;
        }

        private void PopulateGrid()
        {
            if (_database == null || _gridContent == null) return;
            foreach (var entry in _database.Entries)
            {
                if (entry == null) continue;
                var go = BuildCardCell(_gridContent, entry);
                if (_firstCell == null) _firstCell = go;
            }
        }

        private GameObject BuildCardCell(Transform parent, MushroomFieldGuideData entry)
        {
            var go = UICanvasUtil.NewRect("Card_" + entry.Id, parent).gameObject;
            var img = go.AddComponent<Image>();
            img.color = CardBgColor;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = CardBorder;
            outline.effectDistance = new Vector2(1f, -1f);

            var fh = go.AddComponent<FocusHighlight>();

            // Photo (top 220 of 360)
            var thumbRt = UICanvasUtil.NewRect("Thumb", go.transform);
            UICanvasUtil.SetRect(thumbRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 220f), Vector2.zero);
            var thumbImg = thumbRt.gameObject.AddComponent<Image>();
            thumbImg.sprite = entry.Photo;
            thumbImg.preserveAspect = false;
            thumbImg.raycastTarget = false;

            // Body
            var body = UICanvasUtil.NewRect("Body", go.transform);
            UICanvasUtil.SetRect(body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 180f), new Vector2(0f, -220f));
            body.offsetMin = new Vector2(22f, body.offsetMin.y);
            body.offsetMax = new Vector2(-22f, body.offsetMax.y);

            var nameText = UICanvasUtil.NewHeading("Name", body, entry.CommonName, 30f, HeadingColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 38f), new Vector2(0f, -10f));

            var latinText = UICanvasUtil.NewBody("Latin", body, entry.LatinName, 17f, FaintColor, FontStyles.Italic);
            UICanvasUtil.SetRect(latinText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 24f), new Vector2(0f, -52f));

            var chipColor = HollowfenPalette.Edibility(entry.Edibility);
            var dotRt = UICanvasUtil.NewRect("Dot", body);
            UICanvasUtil.SetRect(dotRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, 10f), new Vector2(0f, -92f));
            var dot = dotRt.gameObject.AddComponent<Image>();
            dot.color = chipColor;
            dot.raycastTarget = false;

            var chipText = UICanvasUtil.NewEyebrow("EdibilityLabel", body, entry.EdibilityLabel, 14f, chipColor);
            UICanvasUtil.SetRect(chipText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 22f), new Vector2(20f, -88f));

            // Focus glow
            var glowRt = UICanvasUtil.NewRect("FocusGlow", go.transform);
            UICanvasUtil.Stretch(glowRt);
            var glowImg = glowRt.gameObject.AddComponent<Image>();
            glowImg.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0f);
            glowImg.raycastTarget = false;

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
            // Snap the glow to the resting (transparent) state immediately so the
            // first frame doesn't paint the cached _baseColor over the card.
            glowImg.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0f);

            var cell = go.AddComponent<MushroomCardCell>();
            cell.Bind(entry, OnCellClicked);
            btn.onClick.AddListener(cell.HandleClick);
            return go;
        }

        private void OnCellClicked(MushroomFieldGuideData entry)
        {
            if (_detailScreen != null) _detailScreen.SetEntry(entry);
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("mushroom-detail");
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
