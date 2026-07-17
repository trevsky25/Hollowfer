using System.Collections.Generic;
using Hollowfen.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class MushroomDetailScreen : UIScreen
    {
        private static readonly Color Bg = HollowfenPalette.JournalBackdrop;
        private static readonly Color Panel = HollowfenPalette.SurfaceBase;
        private static readonly Color Cream = new Color(0.961f, 0.925f, 0.855f, 1f);
        private static readonly Color Body = new Color(0.961f, 0.925f, 0.855f, 0.91f);
        private static readonly Color Subtle = new Color(0.961f, 0.925f, 0.855f, 0.62f);
        private static readonly Color Faint = new Color(0.961f, 0.925f, 0.855f, 0.44f);
        private static readonly Color Gold = new Color(0.851f, 0.741f, 0.427f, 1f);
        private static readonly Color Hairline = HollowfenPalette.DividerLine;
        private const float GamepadRotateSpeed = 125f;
        private const float GamepadZoomSpeed = 1.15f;

        private JournalMushroomModelPresenter _model;
        private TMP_Text _modelPending;
        private TMP_Text _modelCaption;
        private JournalArtPresenter _photo;
        private TMP_Text _missingPhoto;
        private TMP_Text _credit;
        private TMP_Text _name;
        private TMP_Text _latin;
        private TMP_Text _edibility;
        private TMP_Text _description;
        private TMP_Text _habitat;
        private TMP_Text _season;
        private TMP_Text _lookalikes;
        private TMP_Text _notes;
        private RectTransform _features;
        private readonly List<Selectable> _waypoints = new List<Selectable>();
        private Button _closeButton;
        private Button _prevButton;
        private Button _nextButton;
        private TMP_Text _prevLabel;
        private TMP_Text _nextLabel;
        private TMP_Text _page;
        private ScrollRect _scroll;
        private bool _built;
        private MushroomFieldGuideData _current;
        private MushroomFieldGuideDatabase _database;

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
                Debug.LogError("[MushroomDetailScreen] OnInitialize failed: " + e);
            }
        }

        private void Update()
        {
            if (!_built || _model == null || !_model.HasModel) return;

            // Right-stick orbit does not compete with the UI navigation map, while the
            // triggers provide an analog zoom that continues to work with time paused.
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                float dt = Time.unscaledDeltaTime;
                Vector2 orbit = pad.rightStick.ReadValue();
                if (orbit.sqrMagnitude > 0.0025f)
                {
                    _model.ApplyRotationDelta(
                        orbit.x * GamepadRotateSpeed * dt,
                        -orbit.y * GamepadRotateSpeed * dt);
                }

                float zoom = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                if (Mathf.Abs(zoom) > 0.05f)
                    _model.ApplyZoomDelta(-zoom * GamepadZoomSpeed * dt);

                if (pad.rightStickButton.wasPressedThisFrame)
                    _model.ResetView();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                _model.ResetView();
        }

        public void SetEntry(MushroomFieldGuideData entry, MushroomFieldGuideDatabase database = null)
        {
            if (!_built)
            {
                BuildLayout();
                _built = true;
            }
            if (database != null) _database = database;
            if (entry == null || !IsAvailable(entry)) return;
            _current = entry;

            _model.SetEntry(entry);
            bool hasModel = entry.JournalPreviewPrefab != null;
            _modelPending.gameObject.SetActive(!hasModel);
            _modelCaption.gameObject.SetActive(hasModel);
            _photo.SetSprite(entry.Photo, HollowfenPalette.SurfaceQuiet);
            _missingPhoto.gameObject.SetActive(entry.Photo == null);
            _credit.text = string.IsNullOrEmpty(JournalText.MushroomCredit(entry))
                ? ""
                : string.Format(Localization.Get("journal.field.photo_credit"), JournalText.MushroomCredit(entry));
            _name.text = JournalText.MushroomName(entry);
            _latin.text = JournalText.MushroomLatin(entry);
            _edibility.text = JournalText.MushroomEdibility(entry).ToUpperInvariant();
            _edibility.color = HollowfenPalette.Edibility(entry.Edibility);
            _description.text = JournalText.MushroomDescription(entry);
            _habitat.text = JournalText.MushroomHabitat(entry);
            _season.text = JournalText.MushroomSeason(entry);
            _lookalikes.text = JournalText.MushroomLookalikes(entry);
            _notes.text = JournalText.MushroomNotes(entry);

            ClearFeatureRows();
            if (entry.IdFeatures != null)
            {
                for (int i = 0; i < entry.IdFeatures.Length; i++)
                {
                    string feature = JournalText.MushroomFeature(entry, i);
                    if (!string.IsNullOrEmpty(feature)) BuildFeatureRow(_features, feature);
                }
            }
            _scroll.verticalNormalizedPosition = 1f;
            UpdatePaging();
            Canvas.ForceUpdateCanvases();
        }

        private void BuildLayout()
        {
            EnsureCanvas();
            var bg = UICanvasUtil.NewImage("BG", transform, Bg, true);
            UICanvasUtil.Stretch(bg.GetComponent<RectTransform>());

            var page = UICanvasUtil.NewRect("SpecimenSpread", transform);
            UICanvasUtil.SetRect(page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1600f, 900f), Vector2.zero);

            BuildModelLeaf(page);
            BuildReadingLeaf(page);

            _closeButton = JournalChrome.BuildCloseButton(transform, () =>
            {
                if (UIManager.Instance != null) UIManager.Instance.Back();
            });
            JournalChrome.BuildBottomHint(transform, "journal.hint.specimen");
            WireNavigation();
        }

        private void BuildModelLeaf(RectTransform page)
        {
            var left = UICanvasUtil.NewRect("ModelLeaf", page);
            UICanvasUtil.SetRect(left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(680f, 850f), new Vector2(0f, 0f));
            var leftImage = left.gameObject.AddComponent<Image>();
            leftImage.color = Panel;
            UICanvasUtil.Roundify(leftImage, 18);
            JournalChrome.AddStructuralBorder(left, 18, 0.10f);

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", left, Localization.Get("journal.field.model_badge"), 12f, Gold, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-48f, 24f), new Vector2(0f, -28f));

            var frame = UICanvasUtil.NewRect("ModelFrame", left);
            UICanvasUtil.SetRect(frame, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(620f, 620f), new Vector2(0f, -66f));
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = Color.clear;
            frameImage.raycastTarget = false;
            frame.gameObject.AddComponent<RectMask2D>();

            JournalChrome.AddSpecimenHalo(frame, new Vector2(540f, 250f), new Vector2(0f, -96f));
            var modelRt = UICanvasUtil.NewRect("Model", frame);
            UICanvasUtil.Stretch(modelRt);
            var raw = modelRt.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            _model = modelRt.gameObject.AddComponent<JournalMushroomModelPresenter>();
            _model.Configure(768, Color.clear, 18f, true);

            _modelPending = UICanvasUtil.NewBody("ModelPending", frame, Localization.Get("journal.field.model_pending"), 22f, Faint, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_modelPending.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-120f, 80f), Vector2.zero);
            _modelCaption = UICanvasUtil.NewBody("ModelCaption", left, Localization.Get("journal.field.model_caption"), 15f, Subtle, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_modelCaption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-48f, 42f), new Vector2(0f, 28f));
        }

        private void BuildReadingLeaf(RectTransform page)
        {
            var right = UICanvasUtil.NewRect("ReadingLeaf", page);
            UICanvasUtil.SetRect(right, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(880f, 850f), Vector2.zero);
            var rightImage = right.gameObject.AddComponent<Image>();
            rightImage.color = Panel;
            rightImage.raycastTarget = true;
            UICanvasUtil.Roundify(rightImage, 18);
            JournalChrome.AddStructuralBorder(right, 18, 0.10f);

            var identity = UICanvasUtil.NewRect("Identity", right);
            identity.anchorMin = new Vector2(0f, 1f);
            identity.anchorMax = new Vector2(1f, 1f);
            identity.pivot = new Vector2(0.5f, 1f);
            identity.offsetMin = new Vector2(42f, identity.offsetMin.y);
            identity.offsetMax = new Vector2(-42f, identity.offsetMax.y);
            identity.sizeDelta = new Vector2(identity.sizeDelta.x, 194f);
            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", identity, Localization.Get("journal.field.title"), 12f, Gold);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 22f), new Vector2(0f, -26f));
            _name = UICanvasUtil.NewHeading("Name", identity, "", 58f, Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(_name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 92f), new Vector2(0f, -54f));
            _name.enableAutoSizing = true;
            _name.fontSizeMin = 36f;
            _name.fontSizeMax = 58f;
            _latin = UICanvasUtil.NewBody("Latin", identity, "", 19f, Subtle, FontStyles.Italic);
            UICanvasUtil.SetRect(_latin.rectTransform, new Vector2(0f, 1f), new Vector2(0.65f, 1f), new Vector2(0f, 1f), new Vector2(0f, 28f), new Vector2(0f, -150f));
            _edibility = UICanvasUtil.NewEyebrow("Edibility", identity, "", 13f, Gold, TextAlignmentOptions.Right);
            UICanvasUtil.SetRect(_edibility.rectTransform, new Vector2(0.62f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 28f), new Vector2(0f, -150f));
            var rule = UICanvasUtil.NewImage("Rule", identity, Hairline, false);
            UICanvasUtil.SetRect(rule.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), Vector2.zero);

            BuildScroll(right);
            BuildNav(right);
        }

        private void BuildScroll(RectTransform right)
        {
            var scrollRt = UICanvasUtil.NewRect("Scroll", right);
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(42f, 92f);
            scrollRt.offsetMax = new Vector2(-42f, -212f);
            var scrollImage = scrollRt.gameObject.AddComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0f);
            scrollImage.raycastTarget = true;
            _scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 42f;
            scrollRt.gameObject.AddComponent<ScrollFocusFollower>();

            var viewport = UICanvasUtil.NewRect("Viewport", scrollRt);
            UICanvasUtil.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _scroll.viewport = viewport;
            var content = UICanvasUtil.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            _scroll.content = content;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 8, 0, 38);
            layout.spacing = 20f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var descriptionSection = BuildSection(content, null, out Image descriptionRule);
            var descriptionRow = UICanvasUtil.NewRect("DescriptionRow", descriptionSection);
            var rowLayout = descriptionRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 22f;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            var rowFitter = descriptionRow.gameObject.AddComponent<ContentSizeFitter>();
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var copyColumn = UICanvasUtil.NewRect("Copy", descriptionRow);
            var copyColumnLayout = copyColumn.gameObject.AddComponent<LayoutElement>();
            copyColumnLayout.flexibleWidth = 1f;
            copyColumnLayout.minWidth = 430f;
            var copyStack = copyColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            copyStack.childForceExpandWidth = true;
            copyStack.childForceExpandHeight = false;
            var copyFitter = copyColumn.gameObject.AddComponent<ContentSizeFitter>();
            copyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _description = UICanvasUtil.NewBody("Description", copyColumn, "", 21f, Body);
            _description.lineSpacing = 7f;
            JournalChrome.FitText(_description, 250f);

            var photoColumn = UICanvasUtil.NewRect("FieldPhotoColumn", descriptionRow);
            var photoColumnLayout = photoColumn.gameObject.AddComponent<LayoutElement>();
            photoColumnLayout.minWidth = 230f;
            photoColumnLayout.preferredWidth = 230f;
            photoColumnLayout.preferredHeight = 270f;
            var photoHeading = UICanvasUtil.NewEyebrow("Heading", photoColumn, Localization.Get("journal.field.photo_heading"), 10f, Gold, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(photoHeading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 20f), Vector2.zero);
            _photo = JournalArtPresenter.Create("FieldPhoto", photoColumn, true, HollowfenPalette.SurfaceQuiet);
            UICanvasUtil.SetRect(_photo.Frame, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(220f, 190f), new Vector2(0f, -30f));
            _missingPhoto = UICanvasUtil.NewBody("Missing", _photo.Frame, Localization.Get("journal.field.missing_photo"), 15f, Faint, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_missingPhoto.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-30f, -20f), Vector2.zero);
            _credit = UICanvasUtil.NewBody("Credit", photoColumn, "", 12f, Subtle, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_credit.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), Vector2.zero);
            _waypoints.Add(MakeWaypoint(descriptionSection.gameObject, descriptionRule));

            var meta = BuildSection(content, null, out Image metaRule);
            var metaRow = UICanvasUtil.NewRect("MetaRow", meta);
            var metaLayout = metaRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            metaLayout.spacing = 22f;
            metaLayout.childForceExpandWidth = true;
            metaLayout.childForceExpandHeight = false;
            var metaFitter = metaRow.gameObject.AddComponent<ContentSizeFitter>();
            metaFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _habitat = BuildMeta(metaRow, "journal.field.habitat");
            _season = BuildMeta(metaRow, "journal.field.season");
            _waypoints.Add(MakeWaypoint(meta.gameObject, metaRule));

            var featureSection = BuildSection(content, "journal.field.features", out Image featureRule);
            _features = UICanvasUtil.NewRect("Rows", featureSection);
            var featureLayout = _features.gameObject.AddComponent<VerticalLayoutGroup>();
            featureLayout.spacing = 10f;
            featureLayout.childForceExpandWidth = true;
            featureLayout.childForceExpandHeight = false;
            var featureFitter = _features.gameObject.AddComponent<ContentSizeFitter>();
            featureFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _waypoints.Add(MakeWaypoint(featureSection.gameObject, featureRule));

            var lookSection = BuildSection(content, "journal.field.lookalikes", out Image lookRule);
            _lookalikes = UICanvasUtil.NewBody("Body", lookSection, "", 18f, Body);
            JournalChrome.FitText(_lookalikes, 60f);
            _waypoints.Add(MakeWaypoint(lookSection.gameObject, lookRule));

            var noteSection = BuildSection(content, "journal.field.note", out Image noteRule);
            _notes = UICanvasUtil.NewBody("Body", noteSection, "", 18f, Body, FontStyles.Italic);
            JournalChrome.FitText(_notes, 70f);
            _waypoints.Add(MakeWaypoint(noteSection.gameObject, noteRule));
        }

        private RectTransform BuildSection(Transform parent, string headingKey, out Image focusRule)
        {
            var section = UICanvasUtil.NewRect("Section", parent);
            var bgImage = section.gameObject.AddComponent<Image>();
            bgImage.color = HollowfenPalette.SurfaceQuiet;
            bgImage.raycastTarget = false;
            UICanvasUtil.Roundify(bgImage, 10);
            var layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 22, 20, 20);
            layout.spacing = 12f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = section.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (!string.IsNullOrEmpty(headingKey))
            {
                var heading = UICanvasUtil.NewEyebrow("Heading", section, Localization.Get(headingKey), 12f, Gold);
                JournalChrome.FitText(heading, 22f);
            }
            var ruleGo = UICanvasUtil.NewImage("FocusRule", section,
                new Color(Gold.r, Gold.g, Gold.b, 0.10f), false);
            var ruleLayout = ruleGo.AddComponent<LayoutElement>();
            ruleLayout.ignoreLayout = true;
            var ruleRt = ruleGo.GetComponent<RectTransform>();
            ruleRt.anchorMin = new Vector2(0f, 0f);
            ruleRt.anchorMax = new Vector2(0f, 1f);
            ruleRt.pivot = new Vector2(0f, 0.5f);
            ruleRt.sizeDelta = new Vector2(2f, 0f);
            ruleRt.anchoredPosition = Vector2.zero;
            focusRule = ruleGo.GetComponent<Image>();
            return section;
        }

        private TMP_Text BuildMeta(Transform parent, string key)
        {
            var cell = UICanvasUtil.NewRect("Meta", parent);
            var layout = cell.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = cell.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var label = UICanvasUtil.NewEyebrow("Label", cell, Localization.Get(key), 11f, Faint);
            JournalChrome.FitText(label, 20f);
            var value = UICanvasUtil.NewBody("Value", cell, "", 18f, Body);
            JournalChrome.FitText(value, 48f);
            return value;
        }

        private Selectable MakeWaypoint(GameObject root, Image focusRule)
        {
            var selectable = root.AddComponent<Selectable>();
            selectable.transition = Selectable.Transition.None;
            var focus = root.AddComponent<FocusHighlight>();
            focus.Configure(focusRule, root.transform as RectTransform, new Color(Gold.r, Gold.g, Gold.b, 0.88f), 1f, true, false);
            return selectable;
        }

        private void BuildNav(RectTransform right)
        {
            var nav = UICanvasUtil.NewRect("Nav", right);
            UICanvasUtil.SetRect(nav, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-84f, 68f), new Vector2(0f, 18f));
            var line = UICanvasUtil.NewImage("Line", nav, Hairline, false);
            UICanvasUtil.SetRect(line.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), Vector2.zero);
            _prevButton = BuildNavButton(nav, true, out _prevLabel);
            var prevRt = (RectTransform)_prevButton.transform;
            prevRt.anchorMin = new Vector2(0f, 0f);
            prevRt.anchorMax = new Vector2(0.42f, 1f);
            prevRt.offsetMin = Vector2.zero;
            prevRt.offsetMax = Vector2.zero;
            _prevButton.onClick.AddListener(GoPrev);
            _page = UICanvasUtil.NewBody("Page", nav, "", 15f, Subtle, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_page.rectTransform, new Vector2(0.42f, 0f), new Vector2(0.58f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _nextButton = BuildNavButton(nav, false, out _nextLabel);
            var nextRt = (RectTransform)_nextButton.transform;
            nextRt.anchorMin = new Vector2(0.58f, 0f);
            nextRt.anchorMax = new Vector2(1f, 1f);
            nextRt.offsetMin = Vector2.zero;
            nextRt.offsetMax = Vector2.zero;
            _nextButton.onClick.AddListener(GoNext);
        }

        private Button BuildNavButton(Transform parent, bool previous, out TMP_Text label)
        {
            var rt = UICanvasUtil.NewRect(previous ? "PrevButton" : "NextButton", parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            var direction = UICanvasUtil.NewEyebrow("Direction", rt, Localization.Get(previous ? "journal.previous" : "journal.next"), 10f, Subtle,
                previous ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
            UICanvasUtil.SetRect(direction.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(previous ? 0f : 1f, 1f), new Vector2(0f, 18f), new Vector2(0f, -8f));
            label = UICanvasUtil.NewHeading("Label", rt, "", 18f, Cream, FontStyles.Italic,
                previous ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight);
            UICanvasUtil.SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(previous ? 0f : 1f, 0.5f), new Vector2(0f, -20f), Vector2.zero);
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = 18f;
            var focus = rt.gameObject.AddComponent<FocusHighlight>();
            focus.Configure(label, rt, Gold, 1.035f);
            return button;
        }

        private void BuildFeatureRow(Transform parent, string copy)
        {
            var row = UICanvasUtil.NewRect("Feature", parent);
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 12f;
            horizontal.childAlignment = TextAnchor.UpperLeft;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;
            var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var dot = UICanvasUtil.NewBody("Dot", row, "•", 16f, Gold, FontStyles.Bold);
            var dotLayout = dot.gameObject.AddComponent<LayoutElement>();
            dotLayout.preferredWidth = 14f;
            var text = UICanvasUtil.NewBody("Text", row, copy, 17f, Body);
            JournalChrome.FitText(text, 24f);
        }

        private void ClearFeatureRows()
        {
            if (_features == null) return;
            for (int i = _features.childCount - 1; i >= 0; i--)
            {
                var child = _features.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private void GoPrev()
        {
            int index = AdjacentIndex(-1);
            if (index >= 0) SetEntry(_database.Entries[index], _database);
        }

        private void GoNext()
        {
            int index = AdjacentIndex(1);
            if (index >= 0) SetEntry(_database.Entries[index], _database);
        }

        private int AdjacentIndex(int direction)
        {
            if (_database == null || _current == null) return -1;
            int current = JournalNavigation.FindIndex(_database.Entries, _current);
            return JournalNavigation.FindAdjacentAvailable(_database.Entries, current, direction, IsAvailable);
        }

        private void UpdatePaging()
        {
            int current = _database != null ? JournalNavigation.FindIndex(_database.Entries, _current) : -1;
            int previous = AdjacentIndex(-1);
            int next = AdjacentIndex(1);
            int total = _database != null ? JournalNavigation.CountAvailable(_database.Entries, IsAvailable) : 0;
            int position = _database != null ? JournalNavigation.AvailablePosition(_database.Entries, current, IsAvailable) : -1;
            _prevButton.interactable = previous >= 0;
            _nextButton.interactable = next >= 0;
            _prevLabel.text = previous >= 0 ? JournalText.MushroomName(_database.Entries[previous]) : "";
            _nextLabel.text = next >= 0 ? JournalText.MushroomName(_database.Entries[next]) : "";
            _page.text = position > 0 ? string.Format(Localization.Get("journal.page"), position, total) : "";
            WireNavigation();
        }

        private void WireNavigation()
        {
            if (_closeButton == null) return;
            if (_waypoints.Count == 0)
            {
                JournalChrome.SetNavigation(_closeButton, _prevButton, _prevButton);
                return;
            }
            JournalChrome.SetNavigation(_closeButton, _waypoints[_waypoints.Count - 1], _waypoints[0]);
            for (int i = 0; i < _waypoints.Count; i++)
            {
                Selectable up = i == 0 ? _closeButton : _waypoints[i - 1];
                Selectable down = i == _waypoints.Count - 1
                    ? (_prevButton != null && _prevButton.interactable ? _prevButton : _nextButton)
                    : _waypoints[i + 1];
                JournalChrome.SetNavigation(_waypoints[i], up, down);
            }
            if (_prevButton != null)
                JournalChrome.SetNavigation(_prevButton, _waypoints[_waypoints.Count - 1], _closeButton, null, _nextButton != null && _nextButton.interactable ? _nextButton : null);
            if (_nextButton != null)
                JournalChrome.SetNavigation(_nextButton, _waypoints[_waypoints.Count - 1], _closeButton, _prevButton != null && _prevButton.interactable ? _prevButton : null, null);
        }

        private static bool IsAvailable(MushroomFieldGuideData entry)
        {
            return entry != null && Hollowfen.Foraging.MushroomDiscovery.IsDiscovered(entry.Id);
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
