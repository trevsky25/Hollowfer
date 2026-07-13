using Hollowfen.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class MushroomDetailScreen : UIScreen
    {
        private static readonly Color BgColor      = new Color(0.020f, 0.031f, 0.020f, 1f);
        private static readonly Color HeadingColor = new Color(0.961f, 0.925f, 0.855f, 1f);
        private static readonly Color BodyColor    = new Color(0.961f, 0.925f, 0.855f, 0.92f);
        private static readonly Color SubtleColor  = new Color(0.961f, 0.925f, 0.855f, 0.70f);
        private static readonly Color FaintColor   = new Color(0.961f, 0.925f, 0.855f, 0.50f);
        private static readonly Color GoldColor    = new Color(0.851f, 0.741f, 0.427f, 1f);
        private static readonly Color GoldFaint    = new Color(0.851f, 0.741f, 0.427f, 0.18f);
        private static readonly Color CardBg       = new Color(0.078f, 0.110f, 0.086f, 0.55f);

        private Image _hero;
        private Image _chipBg;
        private TMP_Text _chipLabel;
        private TMP_Text _commonName;
        private TMP_Text _latinName;
        private TMP_Text _description;
        private TMP_Text _habitatText;
        private TMP_Text _seasonText;
        private TMP_Text _lookalikesText;
        private TMP_Text _notesBody;
        private RectTransform _featuresList;
        private Button _backButton;
        private bool _built;

        public override GameObject DefaultSelected => _backButton != null ? _backButton.gameObject : base.DefaultSelected;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;
            try { BuildLayout(); _built = true; }
            catch (System.Exception e) { Debug.LogError("[MushroomDetailScreen] OnInitialize failed: " + e); }
        }

        public void SetEntry(MushroomFieldGuideData entry)
        {
            if (!_built) { BuildLayout(); _built = true; }
            if (entry == null) return;
            _hero.sprite = entry.Photo;
            _hero.color = entry.Photo != null ? Color.white : new Color(0.10f, 0.10f, 0.10f, 1f);
            var ed = HollowfenPalette.Edibility(entry.Edibility);
            _chipBg.color = ed;
            _chipLabel.text = entry.EdibilityLabel.ToUpperInvariant();
            _commonName.text = entry.CommonName;
            _latinName.text = entry.LatinName;
            _description.text = entry.Description;
            _habitatText.text = entry.Habitat;
            _seasonText.text = entry.Season;
            _lookalikesText.text = entry.Lookalikes;
            _notesBody.text = entry.Notes;

            for (int i = _featuresList.childCount - 1; i >= 0; i--)
                Destroy(_featuresList.GetChild(i).gameObject);

            if (entry.IdFeatures != null)
                foreach (var feature in entry.IdFeatures)
                    if (!string.IsNullOrEmpty(feature)) BuildFeatureRow(_featuresList, feature);
        }

        private void BuildLayout()
        {
            EnsureCanvas();

            var bg = UICanvasUtil.NewImage("BG", transform, BgColor, true);
            UICanvasUtil.Stretch(bg.GetComponent<RectTransform>());

            // Hero photo (top-left)
            var heroRt = UICanvasUtil.NewRect("Hero", transform);
            UICanvasUtil.SetRect(heroRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(840f, 480f), new Vector2(-470f, -70f));
            _hero = heroRt.gameObject.AddComponent<Image>();
            _hero.preserveAspect = true;
            _hero.raycastTarget = false;

            // Right header column
            var headerRt = UICanvasUtil.NewRect("Header", transform);
            UICanvasUtil.SetRect(headerRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(740f, 480f), new Vector2(450f, -70f));
            var headerVlg = headerRt.gameObject.AddComponent<VerticalLayoutGroup>();
            headerVlg.spacing = 16f;
            headerVlg.childForceExpandWidth = true;
            headerVlg.childForceExpandHeight = false;
            headerVlg.childAlignment = TextAnchor.UpperLeft;

            // Edibility chip
            var chipRt = UICanvasUtil.NewRect("EdibilityChip", headerRt);
            chipRt.sizeDelta = new Vector2(260f, 38f);
            _chipBg = chipRt.gameObject.AddComponent<Image>();
            _chipBg.raycastTarget = false;
            UICanvasUtil.Roundify(_chipBg, 19); // full pill at 38px height (batch-47)
            var chipLE = chipRt.gameObject.AddComponent<LayoutElement>();
            chipLE.minHeight = 38f; chipLE.preferredHeight = 38f; chipLE.flexibleWidth = 0f; chipLE.preferredWidth = 260f;
            _chipLabel = UICanvasUtil.NewEyebrow("Label", chipRt, "", 13f, BgColor, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(_chipLabel.rectTransform);

            _commonName = UICanvasUtil.NewHeading("CommonName", headerRt, "", 56f, HeadingColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            AttachLayout(_commonName.gameObject, 80f);

            _latinName = UICanvasUtil.NewBody("LatinName", headerRt, "", 22f, GoldColor, FontStyles.Italic);
            AttachLayout(_latinName.gameObject, 32f);

            _description = UICanvasUtil.NewBody("Description", headerRt, "", 18f, BodyColor);
            AttachLayout(_description.gameObject, 130f);

            // Meta strip
            var meta = UICanvasUtil.NewRect("MetaStrip", headerRt);
            var metaLayout = meta.gameObject.AddComponent<VerticalLayoutGroup>();
            metaLayout.spacing = 8f;
            metaLayout.childForceExpandWidth = true;
            metaLayout.childForceExpandHeight = false;
            var metaLE = meta.gameObject.AddComponent<LayoutElement>();
            metaLE.minHeight = 110f; metaLE.flexibleHeight = 1f;

            _habitatText    = MakeMetaRow(meta, "HABITAT");
            _seasonText     = MakeMetaRow(meta, "SEASON");
            _lookalikesText = MakeMetaRow(meta, "LOOK-ALIKES");

            // Bottom row: ID features + Notes
            var bottom = UICanvasUtil.NewRect("Bottom", transform);
            UICanvasUtil.SetRect(bottom, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1640f, 380f), new Vector2(0f, 80f));
            var bhlg = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            bhlg.spacing = 30f;
            bhlg.childForceExpandWidth = true;
            bhlg.childForceExpandHeight = true;

            // Features
            var featuresCard = UICanvasUtil.NewRect("FeaturesCard", bottom);
            var fcImg = featuresCard.gameObject.AddComponent<Image>();
            fcImg.color = CardBg;
            fcImg.raycastTarget = false;
            UICanvasUtil.Roundify(fcImg, 14); // batch-47 square sweep
            var fcBorder = UICanvasUtil.NewImage("Hairline", featuresCard, GoldFaint, false);
            UICanvasUtil.RoundifyOutline(fcBorder.GetComponent<Image>(), 14, 1.5f);
            UICanvasUtil.Stretch((RectTransform)fcBorder.transform);

            var featInner = UICanvasUtil.NewRect("Inner", featuresCard);
            featInner.anchorMin = Vector2.zero; featInner.anchorMax = Vector2.one;
            featInner.offsetMin = new Vector2(30f, 28f); featInner.offsetMax = new Vector2(-30f, -28f);
            var featVlg = featInner.gameObject.AddComponent<VerticalLayoutGroup>();
            featVlg.spacing = 10f;
            featVlg.childForceExpandWidth = true;
            featVlg.childForceExpandHeight = false;

            var featHeader = UICanvasUtil.NewEyebrow("Header", featInner, "Identifying features", 12f, GoldColor);
            AttachLayout(featHeader.gameObject, 22f);

            var listRt = UICanvasUtil.NewRect("List", featInner);
            _featuresList = listRt;
            var listVlg = listRt.gameObject.AddComponent<VerticalLayoutGroup>();
            listVlg.spacing = 6f;
            listVlg.childForceExpandWidth = true;
            listVlg.childForceExpandHeight = false;
            var listLE = listRt.gameObject.AddComponent<LayoutElement>();
            listLE.flexibleHeight = 1f; listLE.minHeight = 100f;

            // Notes
            var notesCard = UICanvasUtil.NewRect("NotesCard", bottom);
            var ncImg = notesCard.gameObject.AddComponent<Image>();
            ncImg.color = CardBg;
            ncImg.raycastTarget = false;
            UICanvasUtil.Roundify(ncImg, 14); // batch-47 square sweep
            var ncBorder = UICanvasUtil.NewImage("Hairline", notesCard, GoldFaint, false);
            UICanvasUtil.RoundifyOutline(ncBorder.GetComponent<Image>(), 14, 1.5f);
            UICanvasUtil.Stretch((RectTransform)ncBorder.transform);

            var notesInner = UICanvasUtil.NewRect("Inner", notesCard);
            notesInner.anchorMin = Vector2.zero; notesInner.anchorMax = Vector2.one;
            notesInner.offsetMin = new Vector2(30f, 28f); notesInner.offsetMax = new Vector2(-30f, -28f);
            var notesVlg = notesInner.gameObject.AddComponent<VerticalLayoutGroup>();
            notesVlg.spacing = 10f;
            notesVlg.childForceExpandWidth = true;
            notesVlg.childForceExpandHeight = false;

            var notesHeader = UICanvasUtil.NewEyebrow("Header", notesInner, "Forager's note", 12f, GoldColor);
            AttachLayout(notesHeader.gameObject, 22f);

            _notesBody = UICanvasUtil.NewBody("Body", notesInner, "", 18f, BodyColor, FontStyles.Italic);
            var notesBodyLE = _notesBody.gameObject.AddComponent<LayoutElement>();
            notesBodyLE.flexibleHeight = 1f; notesBodyLE.minHeight = 100f;

            // Back button
            var backRt = UICanvasUtil.NewRect("BackButton", transform);
            UICanvasUtil.SetRect(backRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(220f, 60f), new Vector2(40f, 40f));
            var backImg = backRt.gameObject.AddComponent<Image>();
            backImg.color = new Color(0f, 0f, 0f, 0f);
            _backButton = backRt.gameObject.AddComponent<Button>();
            _backButton.transition = Selectable.Transition.None;
            _backButton.targetGraphic = backImg;
            _backButton.onClick.AddListener(() => { if (UIManager.Instance != null) UIManager.Instance.Back(); });
            backRt.gameObject.AddComponent<FocusHighlight>();
            var backLabel = UICanvasUtil.NewBody("Label", backRt, "← Back", 22f, HeadingColor, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(backLabel.rectTransform);
        }

        private TMP_Text MakeMetaRow(Transform parent, string label)
        {
            var row = UICanvasUtil.NewRect("Meta_" + label, parent);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childAlignment = TextAnchor.UpperLeft;
            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.minHeight = 26f; rowLE.preferredHeight = 26f; rowLE.flexibleWidth = 1f;

            var lab = UICanvasUtil.NewEyebrow("Label", row, label, 11f, FaintColor);
            var labLE = lab.gameObject.AddComponent<LayoutElement>();
            labLE.minWidth = 130f; labLE.preferredWidth = 130f;

            var value = UICanvasUtil.NewBody("Value", row, "", 17f, BodyColor);
            var valueLE = value.gameObject.AddComponent<LayoutElement>();
            valueLE.flexibleWidth = 1f;
            return value;
        }

        private void BuildFeatureRow(Transform parent, string text)
        {
            var row = UICanvasUtil.NewRect("Feature", parent);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childAlignment = TextAnchor.UpperLeft;
            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.minHeight = 26f; rowLE.preferredHeight = -1f; rowLE.flexibleWidth = 1f;

            var dot = UICanvasUtil.NewBody("Dot", row, "•", 16f, GoldColor, FontStyles.Bold);
            var dotLE = dot.gameObject.AddComponent<LayoutElement>();
            dotLE.preferredWidth = 14f; dotLE.minWidth = 14f;

            var t = UICanvasUtil.NewBody("Text", row, text, 17f, BodyColor);
            var tLE = t.gameObject.AddComponent<LayoutElement>();
            tLE.flexibleWidth = 1f;
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
