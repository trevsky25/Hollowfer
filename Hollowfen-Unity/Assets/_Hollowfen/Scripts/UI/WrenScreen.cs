using System;
using System.Collections;
using System.Collections.Generic;
using Hollowfen.Data;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    /// <summary>
    /// Main-menu character archive. The class and screen id intentionally retain
    /// their legacy names so the existing Main Menu scene needs no migration.
    /// </summary>
    public class WrenScreen : UIScreen
    {
        [SerializeField] private CharacterProfileData _profile;
        [SerializeField] private CharacterProfileDatabase _database;

        private enum PeopleFilter
        {
            All,
            Story,
            Village,
        }

        private sealed class RosterRow
        {
            public CharacterProfileData Profile;
            public GameObject Root;
            public Button Button;
            public Image Face;
            public Image SelectionRail;
            public TMP_Text Number;
            public TMP_Text Name;
            public TMP_Text Subtitle;
        }

        private static readonly Color Bg = HollowfenPalette.JournalBackdrop;
        private static readonly Color Cream = HollowfenPalette.Cream;
        private static readonly Color Gold = HollowfenPalette.Gold;
        private static readonly Color GoldDim = HollowfenPalette.DividerLine;
        // This archive is dense and is commonly previewed at Steam Deck / scaled 4K sizes.
        // Keep semantic copy comfortably above the decorative microcopy contrast floor.
        private static readonly Color Body = new Color(Cream.r, Cream.g, Cream.b, 0.94f);
        private static readonly Color Subtle = new Color(Cream.r, Cream.g, Cream.b, 0.78f);
        private static readonly Color Faint = new Color(Cream.r, Cream.g, Cream.b, 0.68f);
        private static readonly Color Panel = new Color(0.026f, 0.043f, 0.033f, 0.94f);
        private static readonly Color PanelRaised = new Color(0.038f, 0.059f, 0.044f, 0.96f);
        private static readonly Color PanelQuiet = new Color(0.030f, 0.049f, 0.037f, 0.82f);

        private const float GamepadRotateSpeed = 112f;
        private const float GamepadZoomSpeed = 1.05f;

        private readonly List<CharacterProfileData> _allProfiles = new List<CharacterProfileData>();
        private readonly List<CharacterProfileData> _visibleProfiles = new List<CharacterProfileData>();
        private readonly List<RosterRow> _rosterRows = new List<RosterRow>();
        private readonly Button[] _filterButtons = new Button[3];
        private readonly Image[] _filterFaces = new Image[3];
        private readonly Image[] _filterRules = new Image[3];

        private PeopleFilter _filter;
        private CharacterProfileData _selected;
        private bool _built;
        private bool _isOpen;
        private Coroutine _resourceCleanup;
        private CharacterProfileData _visualizedProfile;

        private JournalArtPresenter _backdropArt;
        private RectTransform _backdropDrift;
        private TMP_Text _archiveCount;
        private TMP_Text _rosterCount;
        private RectTransform _rosterContent;
        private ScrollRect _rosterScroll;
        private GameObject _firstSelectable;

        private JournalWrenModelPresenter _model;
        private TMP_Text _modelBadge;
        private TMP_Text _modelEdition;
        private TMP_Text _modelName;
        private TMP_Text _modelCounter;
        private TMP_Text _modelPending;
        private Button _previousButton;
        private Button _nextButton;

        private ScrollRect _detailScroll;
        private Selectable _detailWaypoint;
        private RectTransform _detailContent;
        private RectTransform _detailBodyRoot;
        private Button _closeButton;

        public override GameObject DefaultSelected
        {
            get
            {
                RosterRow selected = FindRow(_selected);
                if (selected != null && selected.Root.activeInHierarchy) return selected.Root;
                for (int i = 0; i < _rosterRows.Count; i++)
                    if (_rosterRows[i].Root.activeInHierarchy) return _rosterRows[i].Root;
                return _firstSelectable != null ? _firstSelectable : base.DefaultSelected;
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;

            try
            {
                LoadProfiles();
                BuildLayout();
                _built = true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[PeopleOfHollowfen] Failed to build archive: " + exception);
            }
        }

        public override void OnOpen()
        {
            base.OnOpen();
            _isOpen = true;
            ApplyFilter(_filter, false);
            if (_selected == null && _allProfiles.Count > 0) _selected = _allProfiles[0];
            SelectProfile(_selected, true);
            if (_rosterScroll != null) ScrollRosterToSelected();
        }

        public override void OnClose()
        {
            _isOpen = false;
            if (_resourceCleanup != null)
            {
                StopCoroutine(_resourceCleanup);
                _resourceCleanup = null;
            }
            if (_model != null) _model.ReleaseResources();
            _visualizedProfile = null;
            if (_backdropArt != null) _backdropArt.SetSprite(null, Bg);
            ReleaseDetailVisuals();
            Resources.UnloadUnusedAssets();
            base.OnClose();
        }

        private void Update()
        {
            if (!_built || !_isOpen) return;
            HandleArchiveInput();
            HandleDetailScrollInput();
            HandleModelInput();
            AnimateBackdrop();
        }

        private void LoadProfiles()
        {
            _allProfiles.Clear();
            if (_database == null) _database = CharacterProfileDatabase.LoadFallback();
            if (_database != null)
            {
                IReadOnlyList<CharacterProfileData> profiles = _database.Profiles;
                for (int i = 0; i < profiles.Count; i++)
                {
                    CharacterProfileData candidate = profiles[i];
                    if (candidate != null && !_allProfiles.Contains(candidate)) _allProfiles.Add(candidate);
                }
            }

            if (_allProfiles.Count == 0 && _profile != null) _allProfiles.Add(_profile);
            _allProfiles.Sort((left, right) =>
            {
                int order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.CharacterName, right.CharacterName, StringComparison.OrdinalIgnoreCase);
            });

            if (_profile != null)
            {
                for (int i = 0; i < _allProfiles.Count; i++)
                {
                    if (_allProfiles[i] == _profile || _allProfiles[i].Id == _profile.Id)
                    {
                        _selected = _allProfiles[i];
                        break;
                    }
                }
            }
            if (_selected == null && _allProfiles.Count > 0) _selected = _allProfiles[0];
        }

        private void BuildLayout()
        {
            EnsureCanvas();

            GameObject background = UICanvasUtil.NewImage("ArchiveBackdrop", transform, Bg, false);
            UICanvasUtil.Stretch(background.GetComponent<RectTransform>());
            BuildBackdropArt();
            BuildAtmosphere();
            BuildHeader();

            RectTransform main = UICanvasUtil.NewRect("PeopleArchive", transform);
            main.anchorMin = Vector2.zero;
            main.anchorMax = Vector2.one;
            main.offsetMin = new Vector2(44f, 64f);
            main.offsetMax = new Vector2(-44f, -174f);
            var layout = main.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            BuildRosterPanel(main);
            BuildModelPanel(main);
            BuildDetailPanel(main);

            _closeButton = JournalChrome.BuildCloseButton(transform, () =>
            {
                if (UIManager.Instance != null) UIManager.Instance.Back();
            });
            JournalChrome.BuildBottomHint(transform, "journal.hint.wren");

            ApplyFilter(PeopleFilter.All, false);
            RefreshSelectionChrome();
            RefreshModelLabels();
            BuildClosedDetailMessage();
        }

        private void BuildBackdropArt()
        {
            _backdropDrift = UICanvasUtil.NewRect("PortraitWash", transform);
            UICanvasUtil.Stretch(_backdropDrift);
            _backdropArt = JournalArtPresenter.Create("Portrait", _backdropDrift, true, Bg);
            UICanvasUtil.Stretch(_backdropArt.Frame);
            _backdropArt.SetSprite(null, Bg);
            _backdropArt.SetTint(new Color(0.50f, 0.62f, 0.53f, 0.10f));
        }

        private void BuildAtmosphere()
        {
            var horizontalStops = new[]
            {
                new UICanvasUtil.GradientStop(0f, new Color(Bg.r, Bg.g, Bg.b, 0.98f)),
                new UICanvasUtil.GradientStop(0.34f, new Color(Bg.r, Bg.g, Bg.b, 0.78f)),
                new UICanvasUtil.GradientStop(0.68f, new Color(Bg.r, Bg.g, Bg.b, 0.62f)),
                new UICanvasUtil.GradientStop(1f, new Color(Bg.r, Bg.g, Bg.b, 0.96f)),
            };
            RectTransform veil = UICanvasUtil.NewRect("ArchiveVeil", transform);
            UICanvasUtil.Stretch(veil);
            Image veilImage = veil.gameObject.AddComponent<Image>();
            veilImage.sprite = UICanvasUtil.MakeHorizontalGradient(horizontalStops, 512);
            veilImage.raycastTarget = false;

            GameObject glow = UICanvasUtil.NewImage("HeaderGlow", transform,
                new Color(Gold.r, Gold.g, Gold.b, 0.065f), false);
            Image glowImage = glow.GetComponent<Image>();
            glowImage.sprite = UICanvasUtil.RadialGlow();
            UICanvasUtil.SetRect(glow.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(1180f, 500f), new Vector2(-160f, 130f));

            GameObject bottom = UICanvasUtil.NewImage("BottomVeil", transform,
                new Color(Bg.r, Bg.g, Bg.b, 0.55f), false);
            UICanvasUtil.SetRect(bottom.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 82f), Vector2.zero);
        }

        private void BuildHeader()
        {
            TMP_Text eyebrow = UICanvasUtil.NewEyebrow(
                "Eyebrow", transform, Localization.Get("journal.people.eyebrow"), 17.5f, Gold);
            UICanvasUtil.SetRect(eyebrow.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(660f, 22f), new Vector2(50f, -24f));

            TMP_Text title = UICanvasUtil.NewHeading(
                "Title", transform, Localization.Get("journal.people.title"), 64f, Cream,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(title.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(920f, 78f), new Vector2(48f, -48f));

            int story = CountCategory(CharacterProfileData.CharacterCategory.Story);
            int village = _allProfiles.Count - story;
            string summary = string.Format(
                Localization.Get("journal.people.summary"), _allProfiles.Count, story, village);
            TMP_Text summaryText = UICanvasUtil.NewBody(
                "Summary", transform, summary, 20f, Subtle);
            UICanvasUtil.SetRect(summaryText.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(1160f, 34f), new Vector2(51f, -125f));

            _archiveCount = UICanvasUtil.NewEyebrow(
                "ArchiveCount", transform,
                string.Format(Localization.Get("journal.people.archive_count"), _allProfiles.Count),
                17.5f, Subtle, TextAlignmentOptions.Right);
            UICanvasUtil.SetRect(_archiveCount.rectTransform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(430f, 24f), new Vector2(-104f, -126f));

            GameObject rule = UICanvasUtil.NewImage("HeaderRule", transform, GoldDim, false);
            UICanvasUtil.SetRect(rule.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-88f, 1f), new Vector2(0f, -162f));
        }

        private void BuildRosterPanel(Transform parent)
        {
            RectTransform panel = BuildPanel("RosterPanel", parent, 408f, 0f, 0f);

            TMP_Text heading = UICanvasUtil.NewEyebrow(
                "Heading", panel, Localization.Get("journal.people.roster"), 17.5f, Gold);
            heading.characterSpacing = 14f;
            UICanvasUtil.SetRect(heading.rectTransform,
                new Vector2(0f, 1f), new Vector2(0.68f, 1f), new Vector2(0f, 1f),
                new Vector2(-40f, 22f), new Vector2(22f, -19f));

            _rosterCount = UICanvasUtil.NewEyebrow(
                "Count", panel, "", 17.5f, Faint, TextAlignmentOptions.Right);
            _rosterCount.characterSpacing = 14f;
            UICanvasUtil.SetRect(_rosterCount.rectTransform,
                new Vector2(0.72f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, 22f), new Vector2(-20f, -19f));

            RectTransform tabs = UICanvasUtil.NewRect("Filters", panel);
            UICanvasUtil.SetRect(tabs,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-40f, 48f), new Vector2(0f, -54f));
            BuildFilterButton(tabs, PeopleFilter.All, 0, Localization.Get("journal.people.filter.all"));
            BuildFilterButton(tabs, PeopleFilter.Story, 1, Localization.Get("journal.people.filter.story"));
            BuildFilterButton(tabs, PeopleFilter.Village, 2, Localization.Get("journal.people.filter.village"));

            RectTransform scrollRoot = UICanvasUtil.NewRect("RosterScroll", panel);
            scrollRoot.anchorMin = Vector2.zero;
            scrollRoot.anchorMax = Vector2.one;
            scrollRoot.offsetMin = new Vector2(14f, 16f);
            scrollRoot.offsetMax = new Vector2(-14f, -112f);
            Image blocker = scrollRoot.gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            blocker.raycastTarget = true;
            _rosterScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            _rosterScroll.horizontal = false;
            _rosterScroll.vertical = true;
            _rosterScroll.movementType = ScrollRect.MovementType.Clamped;
            _rosterScroll.scrollSensitivity = 34f;
            scrollRoot.gameObject.AddComponent<ScrollFocusFollower>();

            RectTransform viewport = UICanvasUtil.NewRect("Viewport", scrollRoot);
            UICanvasUtil.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _rosterScroll.viewport = viewport;

            _rosterContent = UICanvasUtil.NewRect("Content", viewport);
            _rosterContent.anchorMin = new Vector2(0f, 1f);
            _rosterContent.anchorMax = new Vector2(1f, 1f);
            _rosterContent.pivot = new Vector2(0.5f, 1f);
            _rosterContent.sizeDelta = Vector2.zero;
            _rosterContent.anchoredPosition = Vector2.zero;
            var list = _rosterContent.gameObject.AddComponent<VerticalLayoutGroup>();
            list.padding = new RectOffset(3, 3, 3, 3);
            list.spacing = 8f;
            list.childAlignment = TextAnchor.UpperCenter;
            list.childControlWidth = true;
            list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            var fitter = _rosterContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _rosterScroll.content = _rosterContent;

            for (int i = 0; i < _allProfiles.Count; i++) BuildRosterRow(_allProfiles[i], i);
        }

        private void BuildFilterButton(RectTransform parent, PeopleFilter filter, int index, string labelCopy)
        {
            float third = 1f / 3f;
            RectTransform root = UICanvasUtil.NewRect(filter + "Filter", parent);
            root.anchorMin = new Vector2(index * third, 0f);
            root.anchorMax = new Vector2((index + 1) * third, 1f);
            root.offsetMin = new Vector2(index == 0 ? 0f : 4f, 0f);
            root.offsetMax = new Vector2(index == 2 ? 0f : -4f, 0f);

            Image face = root.gameObject.AddComponent<Image>();
            face.color = PanelQuiet;
            face.raycastTarget = true;
            UICanvasUtil.Roundify(face, 8);
            Button button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = face;
            button.onClick.AddListener(() => ApplyFilter(filter, true));

            TMP_Text label = UICanvasUtil.NewEyebrow(
                "Label", root, labelCopy, 17.5f, Subtle, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(label.rectTransform);
            Image rule = UICanvasUtil.NewImage("ActiveRule", root, Gold, false).GetComponent<Image>();
            UICanvasUtil.SetRect(rule.rectTransform,
                new Vector2(0.2f, 0f), new Vector2(0.8f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 2f), new Vector2(0f, 4f));

            _filterButtons[index] = button;
            _filterFaces[index] = face;
            _filterRules[index] = rule;
            JournalChrome.AddSurfaceFocus(root.gameObject, 8, 1.01f);
        }

        private void BuildRosterRow(CharacterProfileData profile, int index)
        {
            RectTransform root = UICanvasUtil.NewRect("Portrait_" + profile.Id, _rosterContent);
            Pin(root.gameObject, 0f, 90f, true);
            Image face = root.gameObject.AddComponent<Image>();
            face.color = PanelQuiet;
            face.raycastTarget = true;
            UICanvasUtil.Roundify(face, 11);
            Button button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = face;
            CharacterProfileData captured = profile;
            button.onClick.AddListener(() => SelectProfile(captured, true));

            GameObject medallion = UICanvasUtil.NewImage(
                "NumberMedallion", root, CategoryWash(profile.Category), false);
            Image medallionImage = medallion.GetComponent<Image>();
            UICanvasUtil.Roundify(medallionImage, 9);
            UICanvasUtil.SetRect(medallion.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(56f, 56f), new Vector2(13f, 0f));
            TMP_Text number = UICanvasUtil.NewHeading(
                "Number", medallion.transform, (index + 1).ToString("00"), 21f, Gold,
                FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(number.rectTransform);

            TMP_Text name = UICanvasUtil.NewHeading(
                "Name", root, ProfileName(profile), 24f, Cream,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            name.enableAutoSizing = true;
            name.fontSizeMin = 19f;
            name.fontSizeMax = 24f;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            UICanvasUtil.SetRect(name.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-104f, 34f), new Vector2(82f, -12f));

            string subtitleCopy = JournalText.Character(profile, "role", profile.Role);
            TMP_Text subtitle = UICanvasUtil.NewBody(
                "Subtitle", root, subtitleCopy, 18f, Body);
            subtitle.textWrappingMode = TextWrappingModes.NoWrap;
            subtitle.overflowMode = TextOverflowModes.Ellipsis;
            UICanvasUtil.SetRect(subtitle.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                new Vector2(-104f, 28f), new Vector2(82f, 11f));

            Image rail = UICanvasUtil.NewImage("SelectedRail", root, Gold, false).GetComponent<Image>();
            UICanvasUtil.Roundify(rail, 2);
            UICanvasUtil.SetRect(rail.rectTransform,
                new Vector2(0f, 0.16f), new Vector2(0f, 0.84f), new Vector2(0f, 0.5f),
                new Vector2(3f, 0f), Vector2.zero);
            rail.color = new Color(Gold.r, Gold.g, Gold.b, 0f);

            JournalChrome.AddSurfaceFocus(root.gameObject, 11, 1.008f);
            var row = new RosterRow
            {
                Profile = profile,
                Root = root.gameObject,
                Button = button,
                Face = face,
                SelectionRail = rail,
                Number = number,
                Name = name,
                Subtitle = subtitle,
            };
            _rosterRows.Add(row);
            if (_firstSelectable == null) _firstSelectable = root.gameObject;
        }

        private void BuildModelPanel(Transform parent)
        {
            RectTransform panel = BuildPanel("CharacterStage", parent, 540f, 0f, 0f);

            _modelBadge = UICanvasUtil.NewEyebrow(
                "Badge", panel, Localization.Get("journal.people.model_badge"), 17.5f, Gold);
            _modelBadge.characterSpacing = 14f;
            UICanvasUtil.SetRect(_modelBadge.rectTransform,
                new Vector2(0f, 1f), new Vector2(0.55f, 1f), new Vector2(0f, 1f),
                new Vector2(-32f, 22f), new Vector2(22f, -20f));

            _modelEdition = UICanvasUtil.NewEyebrow(
                "Edition", panel, "", 15.5f, Subtle, TextAlignmentOptions.Right);
            _modelEdition.characterSpacing = 14f;
            UICanvasUtil.SetRect(_modelEdition.rectTransform,
                new Vector2(0.55f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-22f, 22f), new Vector2(-22f, -20f));

            RectTransform stage = UICanvasUtil.NewRect("ModelStage", panel);
            stage.anchorMin = new Vector2(0f, 0f);
            stage.anchorMax = new Vector2(1f, 1f);
            stage.offsetMin = new Vector2(18f, 106f);
            stage.offsetMax = new Vector2(-18f, -52f);
            Image stageFace = stage.gameObject.AddComponent<Image>();
            stageFace.color = new Color(0.016f, 0.029f, 0.022f, 0.92f);
            UICanvasUtil.Roundify(stageFace, 170);
            JournalChrome.AddStructuralBorder(stage, 170, 0.14f);
            stage.gameObject.AddComponent<RectMask2D>();

            JournalChrome.AddSpecimenHalo(stage, new Vector2(500f, 520f), new Vector2(0f, -74f));
            GameObject axis = UICanvasUtil.NewImage(
                "Ground", stage, new Color(Gold.r, Gold.g, Gold.b, 0.22f), false);
            UICanvasUtil.SetRect(axis.GetComponent<RectTransform>(),
                new Vector2(0.13f, 0f), new Vector2(0.87f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 1f), new Vector2(0f, 66f));

            RectTransform modelRoot = UICanvasUtil.NewRect("SelectedCharacterModel", stage);
            UICanvasUtil.Stretch(modelRoot);
            modelRoot.offsetMin = new Vector2(8f, 28f);
            modelRoot.offsetMax = new Vector2(-8f, -18f);
            RawImage raw = modelRoot.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = true;
            _model = modelRoot.gameObject.AddComponent<JournalWrenModelPresenter>();
            _model.Configure(896, 1024, 6f);

            _modelPending = UICanvasUtil.NewBody(
                "ModelPending", stage, Localization.Get("journal.people.model_pending"), 21f,
                Subtle, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_modelPending.rectTransform,
                new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.82f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            _modelName = UICanvasUtil.NewHeading(
                "SelectedName", panel, "", 31f, Cream,
                FontStyles.Normal, TextAlignmentOptions.Center);
            _modelName.enableAutoSizing = true;
            _modelName.fontSizeMin = 22f;
            _modelName.fontSizeMax = 31f;
            UICanvasUtil.SetRect(_modelName.rectTransform,
                new Vector2(0.15f, 0f), new Vector2(0.85f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 42f), new Vector2(0f, 55f));

            _modelCounter = UICanvasUtil.NewEyebrow(
                "Counter", panel, "", 15.5f, Faint, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_modelCounter.rectTransform,
                new Vector2(0.3f, 0f), new Vector2(0.7f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 20f), new Vector2(0f, 27f));

            _previousButton = BuildBrowseButton(
                panel, "Previous", "‹", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(52f, 52f), new Vector2(16f, 18f), () => Browse(-1));
            _nextButton = BuildBrowseButton(
                panel, "Next", "›", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(52f, 52f), new Vector2(-16f, 18f), () => Browse(1));
        }

        private Button BuildBrowseButton(
            Transform parent,
            string objectName,
            string glyph,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 position,
            Action action)
        {
            RectTransform root = UICanvasUtil.NewRect(objectName, parent);
            UICanvasUtil.SetRect(root, anchorMin, anchorMax, pivot, size, position);
            Image face = root.gameObject.AddComponent<Image>();
            face.color = PanelRaised;
            face.raycastTarget = true;
            UICanvasUtil.Roundify(face, 18);
            Button button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = face;
            button.onClick.AddListener(() => action());
            TMP_Text label = UICanvasUtil.NewHeading(
                "Glyph", root, glyph, 37f, Gold, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(label.rectTransform);
            label.rectTransform.anchoredPosition = new Vector2(0f, 3f);
            JournalChrome.AddSurfaceFocus(root.gameObject, 18, 1.04f);
            return button;
        }

        private void BuildDetailPanel(Transform parent)
        {
            RectTransform panel = BuildPanel("DossierPanel", parent, 580f, 580f, 1f);
            RectTransform scrollRoot = UICanvasUtil.NewRect("DossierScroll", panel);
            scrollRoot.anchorMin = Vector2.zero;
            scrollRoot.anchorMax = Vector2.one;
            scrollRoot.offsetMin = new Vector2(8f, 8f);
            scrollRoot.offsetMax = new Vector2(-8f, -8f);
            Image blocker = scrollRoot.gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            blocker.raycastTarget = true;
            _detailScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            _detailScroll.horizontal = false;
            _detailScroll.vertical = true;
            _detailScroll.scrollSensitivity = 40f;
            _detailScroll.movementType = ScrollRect.MovementType.Clamped;
            _detailWaypoint = scrollRoot.gameObject.AddComponent<Selectable>();
            _detailWaypoint.transition = Selectable.Transition.None;
            scrollRoot.gameObject.AddComponent<ScrollFocusFollower>();

            RectTransform viewport = UICanvasUtil.NewRect("Viewport", scrollRoot);
            UICanvasUtil.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _detailScroll.viewport = viewport;

            _detailContent = UICanvasUtil.NewRect("Content", viewport);
            _detailContent.anchorMin = new Vector2(0f, 1f);
            _detailContent.anchorMax = new Vector2(1f, 1f);
            _detailContent.pivot = new Vector2(0.5f, 1f);
            _detailContent.sizeDelta = Vector2.zero;
            _detailContent.anchoredPosition = Vector2.zero;
            var contentLayout = _detailContent.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var contentFitter = _detailContent.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _detailScroll.content = _detailContent;
            // The dossier is a controller-selectable scroll region. Give that otherwise invisible
            // navigation waypoint the same gold rail/wash as every other focused journal surface.
            JournalChrome.AddSurfaceFocus(scrollRoot.gameObject, 14, 1f);
        }

        private RectTransform BuildPanel(string panelName, Transform parent, float width, float minWidth, float flexibleWidth)
        {
            RectTransform panel = UICanvasUtil.NewRect(panelName, parent);
            Image face = panel.gameObject.AddComponent<Image>();
            face.color = Panel;
            face.raycastTarget = false;
            UICanvasUtil.Roundify(face, 18);
            JournalChrome.AddStructuralBorder(panel, 18, 0.10f);
            LayoutElement size = panel.gameObject.AddComponent<LayoutElement>();
            if (width > 0f) size.preferredWidth = width;
            size.minWidth = minWidth > 0f ? minWidth : width;
            size.flexibleWidth = flexibleWidth;
            size.flexibleHeight = 1f;
            return panel;
        }

        private void ApplyFilter(PeopleFilter filter, bool moveSelection)
        {
            _filter = filter;
            _visibleProfiles.Clear();
            for (int i = 0; i < _rosterRows.Count; i++)
            {
                RosterRow row = _rosterRows[i];
                bool visible = MatchesFilter(row.Profile, filter);
                row.Root.SetActive(visible);
                if (visible) _visibleProfiles.Add(row.Profile);
            }

            for (int i = 0; i < _filterButtons.Length; i++)
            {
                bool active = i == (int)filter;
                if (_filterFaces[i] != null)
                    _filterFaces[i].color = active
                        ? new Color(Gold.r, Gold.g, Gold.b, 0.13f)
                        : PanelQuiet;
                if (_filterRules[i] != null)
                {
                    Color color = _filterRules[i].color;
                    color.a = active ? 0.95f : 0f;
                    _filterRules[i].color = color;
                }
            }

            if (_rosterCount != null)
                _rosterCount.text = string.Format(Localization.Get("journal.people.roster_count"), _visibleProfiles.Count);

            if (_visibleProfiles.Count > 0 && !_visibleProfiles.Contains(_selected))
                SelectProfile(_visibleProfiles[0], _isOpen);

            WireNavigation();
            Canvas.ForceUpdateCanvases();
            if (_rosterContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(_rosterContent);
            if (moveSelection && _visibleProfiles.Count > 0)
            {
                RosterRow first = FindRow(_visibleProfiles[0]);
                if (first != null && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(first.Root);
            }
        }

        private void SelectProfile(CharacterProfileData profile, bool loadVisuals)
        {
            if (profile == null) return;
            _selected = profile;
            RefreshSelectionChrome();
            RefreshModelLabels();

            if (loadVisuals && _isOpen)
            {
                bool replacingLoadedAssets = _visualizedProfile != null && _visualizedProfile != profile;
                Sprite backdrop = profile.CharacterSheet;
                if (backdrop == null) backdrop = profile.TPosePlate;
                if (backdrop == null) backdrop = profile.HeroPortrait;
                _backdropArt.SetSprite(backdrop, Bg);
                _backdropArt.SetTint(new Color(0.48f, 0.62f, 0.52f, 0.095f));

                _model.SetProfile(profile);
                _modelPending.gameObject.SetActive(!_model.HasModel);
                RebuildDetails(profile);
                _visualizedProfile = profile;
                if (replacingLoadedAssets) ScheduleResourceCleanup();
            }

            ScrollRosterToSelected();
        }

        private void RefreshSelectionChrome()
        {
            for (int i = 0; i < _rosterRows.Count; i++)
            {
                RosterRow row = _rosterRows[i];
                bool active = row.Profile == _selected;
                row.Face.color = active
                    ? new Color(Gold.r, Gold.g, Gold.b, 0.12f)
                    : PanelQuiet;
                row.Name.color = active ? Gold : Cream;
                row.Number.color = active ? Cream : Gold;
                row.Subtitle.color = active ? Body : Subtle;
                Color rail = row.SelectionRail.color;
                rail.a = active ? 1f : 0f;
                row.SelectionRail.color = rail;
            }
        }

        private void RefreshModelLabels()
        {
            if (_selected == null) return;
            int index = _allProfiles.IndexOf(_selected);
            _modelEdition.text = CategoryLabel(_selected.Category).ToUpperInvariant();
            _modelBadge.text = Localization.Get("journal.people.model_badge").ToUpperInvariant();
            _modelName.text = ProfileName(_selected);
            _modelCounter.text = string.Format(
                Localization.Get("journal.people.model_counter"), index + 1, _allProfiles.Count);
        }

        private void RebuildDetails(CharacterProfileData profile)
        {
            ReleaseDetailVisuals();
            Canvas.ForceUpdateCanvases();
            if (profile == null)
            {
                BuildClosedDetailMessage();
                return;
            }

            _detailBodyRoot = UICanvasUtil.NewRect("SelectedDossier", _detailContent);
            _detailBodyRoot.anchorMin = new Vector2(0f, 1f);
            _detailBodyRoot.anchorMax = new Vector2(1f, 1f);
            _detailBodyRoot.pivot = new Vector2(0.5f, 1f);
            _detailBodyRoot.sizeDelta = Vector2.zero;
            var layout = _detailBodyRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 28, 42);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = _detailBodyRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildIdentityDetail(profile, _detailBodyRoot);
            BuildStatsDetail(profile, _detailBodyRoot);
            BuildRelationshipDetail(profile, _detailBodyRoot);
            BuildMissionDetail(profile, _detailBodyRoot);
            BuildQuoteDetail(profile, _detailBodyRoot);
            BuildBiographyDetail(profile, _detailBodyRoot);
            BuildArtDetail(profile, _detailBodyRoot);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_detailBodyRoot);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_detailContent);
            _detailScroll.verticalNormalizedPosition = 1f;
        }

        private void BuildIdentityDetail(CharacterProfileData profile, Transform parent)
        {
            string role = JournalText.Character(profile, "role", profile.Role);
            string home = JournalText.Character(profile, "home", profile.Home);
            TMP_Text eyebrow = UICanvasUtil.NewEyebrow(
                "Identity", parent,
                string.Format(Localization.Get("format.pair"), role, home), 17.5f, Gold);
            eyebrow.textWrappingMode = TextWrappingModes.Normal;
            eyebrow.lineSpacing = 4f;
            Pin(eyebrow.gameObject, 0f,
                PreferredHeight(eyebrow, DetailTextWidth(), 30f), true);

            TMP_Text name = UICanvasUtil.NewHeading(
                "Name", parent, ProfileName(profile), 54f, Cream,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            name.enableAutoSizing = true;
            name.fontSizeMin = 36f;
            name.fontSizeMax = 54f;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            Pin(name.gameObject, 0f, 66f, true);

            TMP_Text tagline = UICanvasUtil.NewBody(
                "Tagline", parent,
                JournalText.Character(profile, "tagline", profile.Tagline),
                20f, Subtle);
            tagline.lineSpacing = 7f;
            Pin(tagline.gameObject, 0f,
                PreferredHeight(tagline, DetailTextWidth(), 58f), true);

            GameObject rule = UICanvasUtil.NewImage("IdentityRule", parent, GoldDim, false);
            Pin(rule, 0f, 1f, true);

            string lead = Localization.Get(profile.DescriptionId, profile.LeadParagraph);
            TMP_Text leadText = UICanvasUtil.NewBody("Lead", parent, lead, 21f, Body);
            leadText.lineSpacing = 8f;
            Pin(leadText.gameObject, 0f,
                PreferredHeight(leadText, DetailTextWidth(), 92f), true);

            if (!string.IsNullOrWhiteSpace(profile.Pullquote))
                BuildPullquoteCard(parent, profile.Pullquote);
        }

        private void BuildPullquoteCard(Transform parent, string quote)
        {
            RectTransform card = UICanvasUtil.NewRect("Pullquote", parent);
            Image face = card.gameObject.AddComponent<Image>();
            face.color = new Color(Gold.r, Gold.g, Gold.b, 0.075f);
            UICanvasUtil.Roundify(face, 12);
            GameObject rail = UICanvasUtil.NewImage("QuoteRail", card, Gold, false);
            UICanvasUtil.SetRect(rail.GetComponent<RectTransform>(),
                new Vector2(0f, 0.14f), new Vector2(0f, 0.86f), new Vector2(0f, 0.5f),
                new Vector2(3f, 0f), new Vector2(18f, 0f));
            TMP_Text text = UICanvasUtil.NewHeading(
                "Text", card, quote, 25f, Cream, FontStyles.Italic, TextAlignmentOptions.Left);
            text.enableAutoSizing = true;
            text.fontSizeMin = 21f;
            text.fontSizeMax = 25f;
            text.lineSpacing = 3f;
            UICanvasUtil.SetRect(text.rectTransform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(-76f, -34f), new Vector2(20f, 0f));
            float height = PreferredHeight(text, DetailTextWidth(76f), 104f) + 48f;
            Pin(card.gameObject, 0f, height, true);
        }

        private void BuildStatsDetail(CharacterProfileData profile, Transform parent)
        {
            TMP_Text heading = BuildSectionHeading(parent, Localization.Get("journal.people.details"));
            heading.margin = new Vector4(0f, 8f, 0f, 0f);

            float availableWidth = Mathf.Max(300f, _detailContent.rect.width - 56f);
            bool twoColumns = availableWidth >= 700f;
            RectTransform gridRoot = UICanvasUtil.NewRect("Stats", parent);
            Pin(gridRoot.gameObject, 0f, twoColumns ? 188f : 388f, true);
            var grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(
                twoColumns ? (availableWidth - 12f) * 0.5f : availableWidth,
                88f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = twoColumns ? 2 : 1;
            grid.childAlignment = TextAnchor.UpperLeft;
            BuildStat(gridRoot, Localization.Get("journal.people.age"), profile.Age);
            BuildStat(gridRoot, Localization.Get("journal.people.home"), profile.Home);
            BuildStat(gridRoot, Localization.Get("journal.people.work"), profile.Work);
            BuildStat(gridRoot, Localization.Get("journal.people.keepsake"), profile.Keepsake);
        }

        private void BuildRelationshipDetail(CharacterProfileData profile, Transform parent)
        {
            string npcId = RelationshipNpcId(profile);
            if (string.IsNullOrEmpty(npcId)) return;

            var memories = VillagerRelationships.MemoriesFor(npcId);
            int relationship = GameScores.GetRelationship(npcId);
            string favorId = FavorId(npcId);
            int favorStage = VillagerRelationships.FavorStage(favorId);
            BuildSectionHeading(parent, Localization.Get("journal.people.relationship_history"));

            RectTransform summary = UICanvasUtil.NewRect("RelationshipSummary", parent);
            Image summaryFace = summary.gameObject.AddComponent<Image>();
            summaryFace.color = new Color(Gold.r, Gold.g, Gold.b, 0.075f);
            UICanvasUtil.Roundify(summaryFace, 10);
            Pin(summary.gameObject, 0f, 98f, true);

            TMP_Text standing = UICanvasUtil.NewEyebrow(
                "Standing", summary, Localization.Get("journal.people.relationship_standing"), 17.5f, Faint);
            UICanvasUtil.SetRect(standing.rectTransform,
                new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                new Vector2(-20f, 23f), new Vector2(16f, -12f));
            TMP_Text standingValue = UICanvasUtil.NewHeading(
                "StandingValue", summary, RelationshipRank(relationship), 23f, Cream,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(standingValue.rectTransform,
                new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 0f),
                new Vector2(-20f, -36f), new Vector2(16f, 8f));

            TMP_Text thread = UICanvasUtil.NewEyebrow(
                "Thread", summary, Localization.Get("journal.people.personal_thread"), 17.5f, Faint);
            UICanvasUtil.SetRect(thread.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-20f, 23f), new Vector2(12f, -12f));
            string progress = string.Format(Localization.Get("journal.people.personal_thread_progress"),
                Mathf.Clamp(favorStage, 0, 2), 2);
            TMP_Text threadValue = UICanvasUtil.NewHeading(
                "ThreadValue", summary, progress, 23f, favorStage >= 2 ? Gold : Cream,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(threadValue.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f),
                new Vector2(-20f, -36f), new Vector2(12f, 8f));

            if (memories.Count == 0)
            {
                BuildQuietNote(parent, Localization.Get("journal.people.memories_none"));
                return;
            }

            int shown = Mathf.Min(3, memories.Count);
            for (int i = 0; i < shown; i++) BuildMemoryCard(parent, memories[i]);

            var bonds = VillagerRelationships.BondsFor(npcId);
            if (bonds.Count > 0)
            {
                var bond = bonds[0];
                string other = string.Equals(bond.FirstNpcId, npcId, StringComparison.Ordinal)
                    ? bond.SecondNpcId
                    : bond.FirstNpcId;
                string copy = string.Format(Localization.Get("journal.people.village_bond"),
                    VillagerDisplayName(other), BondRank(bond.Value));
                BuildQuietNote(parent, copy);
            }
        }

        private void BuildMemoryCard(Transform parent, VillagerRelationships.MemoryRecord memory)
        {
            RectTransform card = UICanvasUtil.NewRect("Memory_" + memory.MemoryId, parent);
            Image face = card.gameObject.AddComponent<Image>();
            face.color = PanelQuiet;
            UICanvasUtil.Roundify(face, 9);
            string day = string.Format(Localization.Get("journal.people.memory_day"), memory.Day);
            TMP_Text dayText = UICanvasUtil.NewEyebrow("Day", card, day, 17.5f, Gold);
            UICanvasUtil.SetRect(dayText.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-32f, 23f), new Vector2(16f, -12f));
            string fallback = HumanizeMemory(memory.MemoryId);
            string copy = Localization.Get("relationship.memory." + memory.MemoryId, fallback);
            TMP_Text body = UICanvasUtil.NewBody("Memory", card, copy, 20f, Body);
            body.lineSpacing = 7f;
            UICanvasUtil.SetRect(body.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f),
                new Vector2(-32f, -50f), new Vector2(16f, 12f));
            float height = PreferredHeight(body, DetailTextWidth(32f), 54f) + 64f;
            Pin(card.gameObject, 0f, height, true);
        }

        private static string RelationshipNpcId(CharacterProfileData profile)
        {
            if (profile == null) return null;
            switch (profile.Id)
            {
                case "old-bram": return "bram";
                case "sister-almy": return "almy";
                case "elder-pell": return "pell";
                case "joren":
                case "marra":
                case "edda": return profile.Id;
                default: return null;
            }
        }

        private static string FavorId(string npcId)
        {
            switch (npcId)
            {
                case "bram": return "favor.bram.after_hours";
                case "almy": return "favor.almy.garden_walks";
                case "joren": return "favor.joren.working_silence";
                case "marra": return "favor.marra.kitchen_table";
                case "edda": return "favor.edda.care_rounds";
                case "pell": return "favor.pell.village_ledger";
                default: return string.Empty;
            }
        }

        private static string RelationshipRank(int value)
        {
            string key = value >= 10 ? "trusted" : value >= 6 ? "close" : value >= 3 ? "familiar" :
                value <= -3 ? "strained" : "acquainted";
            return Localization.Get("journal.people.relationship_rank." + key);
        }

        private static string BondRank(int value)
        {
            string key = value >= 4 ? "steadfast" : value >= 2 ? "strengthening" :
                value <= -2 ? "strained" : "warming";
            return Localization.Get("journal.people.bond_rank." + key);
        }

        private static string VillagerDisplayName(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId)) return "—";
            string localized = Localization.Get("npc." + npcId + ".name");
            return localized == "npc." + npcId + ".name"
                ? char.ToUpperInvariant(npcId[0]) + npcId.Substring(1)
                : localized;
        }

        private static string HumanizeMemory(string memoryId)
        {
            if (string.IsNullOrWhiteSpace(memoryId)) return "A shared moment, not yet described.";
            int split = memoryId.IndexOf('.');
            string value = split >= 0 && split + 1 < memoryId.Length ? memoryId.Substring(split + 1) : memoryId;
            value = value.Replace('_', ' ');
            return char.ToUpperInvariant(value[0]) + value.Substring(1) + ".";
        }

        private void BuildStat(Transform parent, string labelCopy, string value)
        {
            RectTransform cell = UICanvasUtil.NewRect("Stat", parent);
            Image face = cell.gameObject.AddComponent<Image>();
            face.color = PanelQuiet;
            UICanvasUtil.Roundify(face, 9);
            TMP_Text label = UICanvasUtil.NewEyebrow("Label", cell, labelCopy, 17.5f, Faint);
            UICanvasUtil.SetRect(label.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-24f, 23f), new Vector2(13f, -10f));
            TMP_Text copy = UICanvasUtil.NewHeading(
                "Value", cell, ValueOrDash(value), 21f, Gold,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            copy.textWrappingMode = TextWrappingModes.Normal;
            copy.overflowMode = TextOverflowModes.Ellipsis;
            copy.maxVisibleLines = 2;
            copy.enableAutoSizing = true;
            copy.fontSizeMin = 17.5f;
            copy.fontSizeMax = 21f;
            UICanvasUtil.SetRect(copy.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f),
                new Vector2(-24f, -34f), new Vector2(13f, 7f));
        }

        private void BuildMissionDetail(CharacterProfileData profile, Transform parent)
        {
            CharacterProfileData.MissionEntry[] missions = profile.Missions;
            int count = missions != null ? missions.Length : 0;
            BuildSectionHeading(parent, string.Format(
                Localization.Get("journal.people.missions_count"), count));

            if (count == 0)
            {
                string empty = profile.Category == CharacterProfileData.CharacterCategory.Villager
                    ? Localization.Get("journal.people.ambient_note")
                    : Localization.Get("journal.people.missions_none");
                BuildQuietNote(parent, empty);
                return;
            }

            for (int i = 0; i < count; i++) BuildMissionCard(parent, missions[i]);
        }

        private void BuildMissionCard(Transform parent, CharacterProfileData.MissionEntry mission)
        {
            RectTransform card = UICanvasUtil.NewRect("Mission_" + mission.QuestId, parent);
            Image face = card.gameObject.AddComponent<Image>();
            face.color = PanelQuiet;
            UICanvasUtil.Roundify(face, 10);

            string act = mission.Act > 0
                ? string.Format(Localization.Get("journal.people.act"), Roman(mission.Act))
                : Localization.Get("journal.people.story_thread");
            TMP_Text actText = UICanvasUtil.NewEyebrow("Act", card, act, 17.5f, Gold);
            UICanvasUtil.SetRect(actText.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(110f, 24f), new Vector2(18f, -14f));

            TMP_Text title = UICanvasUtil.NewHeading(
                "Title", card, mission.Title, 23f, Cream,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            title.enableAutoSizing = true;
            title.fontSizeMin = 19f;
            title.fontSizeMax = 23f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            UICanvasUtil.SetRect(title.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-148f, 31f), new Vector2(132f, -10f));

            TMP_Text copy = UICanvasUtil.NewBody("Summary", card, mission.Summary, 20f, Body);
            copy.lineSpacing = 8f;
            UICanvasUtil.SetRect(copy.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f),
                new Vector2(-36f, -54f), new Vector2(18f, 15f));
            float summaryHeight = PreferredHeight(copy, DetailTextWidth(36f), 62f);
            Pin(card.gameObject, 0f, 80f + summaryHeight, true);
        }

        private void BuildQuoteDetail(CharacterProfileData profile, Transform parent)
        {
            CharacterProfileData.QuoteEntry[] quotes = profile.Quotes;
            int count = quotes != null ? quotes.Length : 0;
            BuildSectionHeading(parent, string.Format(
                Localization.Get("journal.people.quotes_count"), count));
            if (count == 0)
            {
                BuildQuietNote(parent, Localization.Get("journal.people.quotes_none"));
                return;
            }

            for (int i = 0; i < count; i++) BuildQuoteCard(parent, quotes[i]);
        }

        private void BuildQuoteCard(Transform parent, CharacterProfileData.QuoteEntry quote)
        {
            RectTransform card = UICanvasUtil.NewRect("Quote", parent);
            Image face = card.gameObject.AddComponent<Image>();
            face.color = new Color(0.045f, 0.066f, 0.049f, 0.78f);
            UICanvasUtil.Roundify(face, 10);

            TMP_Text mark = UICanvasUtil.NewHeading(
                "Mark", card, "“", 58f, new Color(Gold.r, Gold.g, Gold.b, 0.42f),
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(mark.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(52f, 52f), new Vector2(14f, -9f));

            TMP_Text text = UICanvasUtil.NewHeading(
                "Text", card, quote.Text, 24f, Cream,
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            text.lineSpacing = 3f;

            TMP_Text context = null;
            float contextHeight = 0f;
            if (!string.IsNullOrWhiteSpace(quote.Context))
            {
                context = UICanvasUtil.NewBody(
                    "Context", card, quote.Context, 17.5f, Subtle);
                context.lineSpacing = 4f;
                contextHeight = PreferredHeight(context, DetailTextWidth(88f), 30f);
            }

            float quoteHeight = PreferredHeight(text, DetailTextWidth(88f), 70f);
            Pin(card.gameObject, 0f, quoteHeight + contextHeight + 56f, true);
            UICanvasUtil.SetRect(text.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f),
                new Vector2(-88f, -(contextHeight + 34f)), new Vector2(64f, contextHeight + 16f));

            if (context != null)
            {
                UICanvasUtil.SetRect(context.rectTransform,
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                    new Vector2(-88f, contextHeight), new Vector2(64f, 14f));
            }
        }

        private void BuildBiographyDetail(CharacterProfileData profile, Transform parent)
        {
            BuildSectionHeading(parent, Localization.Get("journal.people.biography"));
            if (!string.IsNullOrWhiteSpace(profile.BackgroundParagraph))
                BuildTextCard(parent, Localization.Get("journal.people.background"),
                    JournalText.Character(profile, "background", profile.BackgroundParagraph));
            if (!string.IsNullOrWhiteSpace(profile.PerspectiveParagraph))
                BuildTextCard(parent, Localization.Get("journal.people.perspective"),
                    JournalText.Character(profile, "perspective", profile.PerspectiveParagraph));
        }

        private void BuildTextCard(Transform parent, string headingCopy, string copy)
        {
            RectTransform card = UICanvasUtil.NewRect("BiographyCard", parent);
            Image face = card.gameObject.AddComponent<Image>();
            face.color = PanelQuiet;
            UICanvasUtil.Roundify(face, 10);
            TMP_Text heading = UICanvasUtil.NewEyebrow("Heading", card, headingCopy, 17.5f, Gold);
            UICanvasUtil.SetRect(heading.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-36f, 24f), new Vector2(18f, -14f));
            TMP_Text body = UICanvasUtil.NewBody("Body", card, copy, 20f, Body);
            body.lineSpacing = 8f;
            UICanvasUtil.SetRect(body.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f),
                new Vector2(-36f, -58f), new Vector2(18f, 18f));
            float copyHeight = PreferredHeight(body, DetailTextWidth(36f), 82f);
            Pin(card.gameObject, 0f, copyHeight + 76f, true);
        }

        private void BuildArtDetail(CharacterProfileData profile, Transform parent)
        {
            Sprite sheet = profile.CharacterSheet;
            Sprite tPose = profile.TPosePlate;
            if (sheet == null && tPose == null) return;

            BuildSectionHeading(parent, Localization.Get("journal.people.plates"));
            if (sheet != null)
                BuildArtPlate(parent, Localization.Get("journal.people.plate.identity"), sheet);
            if (tPose != null && tPose != sheet)
                BuildArtPlate(parent, Localization.Get("journal.people.plate.tpose"), tPose);
        }

        private void BuildArtPlate(Transform parent, string caption, Sprite sprite)
        {
            RectTransform card = UICanvasUtil.NewRect("CharacterPlate", parent);
            Pin(card.gameObject, 0f, 382f, true);
            Image face = card.gameObject.AddComponent<Image>();
            face.color = new Color(0.018f, 0.031f, 0.023f, 0.96f);
            UICanvasUtil.Roundify(face, 12);
            card.gameObject.AddComponent<RectMask2D>();

            JournalArtPresenter art = JournalArtPresenter.Create("Art", card, false, face.color);
            UICanvasUtil.SetRect(art.Frame,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-28f, -68f), new Vector2(0f, 16f));
            art.SetSprite(sprite, face.color);

            TMP_Text label = UICanvasUtil.NewEyebrow(
                "Caption", card, caption, 17.5f, Subtle, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(label.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-32f, 26f), new Vector2(0f, 17f));
        }

        private TMP_Text BuildSectionHeading(Transform parent, string copy)
        {
            TMP_Text heading = UICanvasUtil.NewEyebrow("SectionHeading", parent, copy, 17.5f, Gold);
            Pin(heading.gameObject, 0f, 36f, true);
            return heading;
        }

        private void BuildQuietNote(Transform parent, string copy)
        {
            RectTransform card = UICanvasUtil.NewRect("ArchiveNote", parent);
            Image face = card.gameObject.AddComponent<Image>();
            face.color = new Color(Cream.r, Cream.g, Cream.b, 0.035f);
            UICanvasUtil.Roundify(face, 9);
            TMP_Text text = UICanvasUtil.NewBody(
                "Text", card, copy, 19f, Subtle);
            text.lineSpacing = 7f;
            UICanvasUtil.SetRect(text.rectTransform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(-34f, -24f), Vector2.zero);
            float height = PreferredHeight(text, DetailTextWidth(34f), 62f) + 36f;
            Pin(card.gameObject, 0f, height, true);
        }

        private void BuildClosedDetailMessage()
        {
            if (_detailContent == null || _detailBodyRoot != null) return;
            _detailBodyRoot = UICanvasUtil.NewRect("ArchiveClosed", _detailContent);
            _detailBodyRoot.anchorMin = new Vector2(0f, 1f);
            _detailBodyRoot.anchorMax = new Vector2(1f, 1f);
            _detailBodyRoot.pivot = new Vector2(0.5f, 1f);
            _detailBodyRoot.sizeDelta = new Vector2(0f, 420f);
            Pin(_detailBodyRoot.gameObject, 0f, 420f, true);
            TMP_Text message = UICanvasUtil.NewHeading(
                "Message", _detailBodyRoot, Localization.Get("journal.people.open_note"),
                28f, Faint, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(message.rectTransform,
                new Vector2(0.12f, 0.2f), new Vector2(0.88f, 0.8f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private void ReleaseDetailVisuals()
        {
            if (_detailBodyRoot == null) return;
            foreach (Image image in _detailBodyRoot.GetComponentsInChildren<Image>(true))
                if (image != null) image.sprite = null;
            Destroy(_detailBodyRoot.gameObject);
            _detailBodyRoot = null;
        }

        private void Browse(int direction)
        {
            if (_visibleProfiles.Count == 0) return;
            int index = _visibleProfiles.IndexOf(_selected);
            if (index < 0) index = 0;
            index = (index + direction + _visibleProfiles.Count) % _visibleProfiles.Count;
            SelectProfile(_visibleProfiles[index], true);
        }

        private void HandleArchiveInput()
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.leftShoulder.wasPressedThisFrame) Browse(-1);
                if (gamepad.rightShoulder.wasPressedThisFrame) Browse(1);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftBracketKey.wasPressedThisFrame)
                    Browse(-1);
                if (keyboard.rightBracketKey.wasPressedThisFrame)
                    Browse(1);
            }
        }

        private void HandleDetailScrollInput()
        {
            if (_detailScroll == null || _detailWaypoint == null || EventSystem.current == null ||
                EventSystem.current.currentSelectedGameObject != _detailWaypoint.gameObject)
                return;

            float direction = 0f;
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                direction = gamepad.leftStick.ReadValue().y;
                if (Mathf.Abs(direction) < 0.12f) direction = gamepad.dpad.ReadValue().y;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.isPressed) direction = 1f;
                if (keyboard.downArrowKey.isPressed) direction = -1f;
                if (keyboard.pageUpKey.wasPressedThisFrame)
                {
                    _detailScroll.verticalNormalizedPosition = Mathf.Clamp01(
                        _detailScroll.verticalNormalizedPosition + 0.68f);
                    return;
                }
                if (keyboard.pageDownKey.wasPressedThisFrame)
                {
                    _detailScroll.verticalNormalizedPosition = Mathf.Clamp01(
                        _detailScroll.verticalNormalizedPosition - 0.68f);
                    return;
                }
            }

            if (Mathf.Abs(direction) < 0.05f) return;
            _detailScroll.verticalNormalizedPosition = Mathf.Clamp01(
                _detailScroll.verticalNormalizedPosition + direction * 0.72f * Time.unscaledDeltaTime);
        }

        private void HandleModelInput()
        {
            if (_model == null || !_model.HasModel) return;
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                float deltaTime = Time.unscaledDeltaTime;
                Vector2 orbit = pad.rightStick.ReadValue();
                if (orbit.sqrMagnitude > 0.0025f)
                {
                    _model.ApplyRotationDelta(
                        orbit.x * GamepadRotateSpeed * deltaTime,
                        -orbit.y * GamepadRotateSpeed * deltaTime);
                }

                float zoom = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                if (Mathf.Abs(zoom) > 0.05f)
                    _model.ApplyZoomDelta(-zoom * GamepadZoomSpeed * deltaTime);
                if (pad.rightStickButton.wasPressedThisFrame) _model.ResetView();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) _model.ResetView();
        }

        private void AnimateBackdrop()
        {
            if (_backdropDrift == null) return;
            float time = Time.unscaledTime;
            float scale = 1.045f + Mathf.Sin(time * 0.075f) * 0.012f;
            _backdropDrift.localScale = new Vector3(scale, scale, 1f);
            _backdropDrift.anchoredPosition = new Vector2(
                Mathf.Sin(time * 0.047f + 1.1f) * 12f,
                Mathf.Cos(time * 0.041f) * 7f);
        }

        private void ScrollRosterToSelected()
        {
            if (_rosterScroll == null || _rosterContent == null || _selected == null) return;
            RosterRow row = FindRow(_selected);
            if (row == null || !row.Root.activeInHierarchy) return;
            Canvas.ForceUpdateCanvases();
            RectTransform rowRect = row.Root.transform as RectTransform;
            RectTransform viewport = _rosterScroll.viewport;
            float contentHeight = _rosterContent.rect.height;
            float viewportHeight = viewport != null ? viewport.rect.height : 0f;
            float scrollable = contentHeight - viewportHeight;
            if (rowRect == null || scrollable <= 0f) return;
            float rowCenterFromTop = -rowRect.anchoredPosition.y + rowRect.rect.height * 0.5f;
            float desiredTop = Mathf.Clamp(rowCenterFromTop - viewportHeight * 0.5f, 0f, scrollable);
            _rosterScroll.verticalNormalizedPosition = 1f - desiredTop / scrollable;
        }

        private void WireNavigation()
        {
            if (_previousButton == null || _nextButton == null) return;
            var visibleRows = new List<RosterRow>();
            for (int i = 0; i < _rosterRows.Count; i++)
                if (_rosterRows[i].Root.activeSelf) visibleRows.Add(_rosterRows[i]);

            Selectable firstRow = visibleRows.Count > 0 ? visibleRows[0].Button : null;
            for (int i = 0; i < _filterButtons.Length; i++)
            {
                Selectable left = i > 0 ? _filterButtons[i - 1] : null;
                Selectable right = i + 1 < _filterButtons.Length ? _filterButtons[i + 1] : _previousButton;
                JournalChrome.SetNavigation(_filterButtons[i], _closeButton, firstRow, left, right);
            }

            for (int i = 0; i < visibleRows.Count; i++)
            {
                Selectable up = i > 0 ? visibleRows[i - 1].Button : _filterButtons[(int)_filter];
                Selectable down = i + 1 < visibleRows.Count ? visibleRows[i + 1].Button : _filterButtons[(int)_filter];
                JournalChrome.SetNavigation(visibleRows[i].Button, up, down, null, _previousButton);
            }

            JournalChrome.SetNavigation(_previousButton, _filterButtons[2], _nextButton,
                visibleRows.Count > 0 ? visibleRows[0].Button : null, _nextButton);
            JournalChrome.SetNavigation(_nextButton, _filterButtons[2], _previousButton,
                _previousButton, _detailWaypoint);
            JournalChrome.SetNavigation(_detailWaypoint, _detailWaypoint, _detailWaypoint, _nextButton, null);
            if (_closeButton != null)
                JournalChrome.SetNavigation(_closeButton, _detailWaypoint, _filterButtons[0], _detailWaypoint, null);
        }

        private void ScheduleResourceCleanup()
        {
            if (_resourceCleanup != null) StopCoroutine(_resourceCleanup);
            _resourceCleanup = StartCoroutine(UnloadUnselectedResources());
        }

        private IEnumerator UnloadUnselectedResources()
        {
            // Debounce fast browsing: after destroyed preview instances and old
            // dossier sprites release their references, unload only assets no
            // longer used by the currently selected portrait.
            yield return new WaitForSecondsRealtime(1.75f);
            yield return null;
            yield return Resources.UnloadUnusedAssets();
            _resourceCleanup = null;
        }

        private RosterRow FindRow(CharacterProfileData profile)
        {
            for (int i = 0; i < _rosterRows.Count; i++)
                if (_rosterRows[i].Profile == profile) return _rosterRows[i];
            return null;
        }

        private int CountCategory(CharacterProfileData.CharacterCategory category)
        {
            int count = 0;
            for (int i = 0; i < _allProfiles.Count; i++)
                if (_allProfiles[i].Category == category) count++;
            return count;
        }

        private static bool MatchesFilter(CharacterProfileData profile, PeopleFilter filter)
        {
            if (filter == PeopleFilter.All) return true;
            if (filter == PeopleFilter.Story)
                return profile.Category == CharacterProfileData.CharacterCategory.Story;
            return profile.Category != CharacterProfileData.CharacterCategory.Story;
        }

        private static Color CategoryWash(CharacterProfileData.CharacterCategory category)
        {
            switch (category)
            {
                case CharacterProfileData.CharacterCategory.Story:
                    return new Color(0.23f, 0.34f, 0.26f, 0.92f);
                case CharacterProfileData.CharacterCategory.Family:
                    return new Color(0.31f, 0.25f, 0.18f, 0.92f);
                default:
                    return new Color(0.18f, 0.27f, 0.24f, 0.92f);
            }
        }

        private static string CategoryLabel(CharacterProfileData.CharacterCategory category)
        {
            switch (category)
            {
                case CharacterProfileData.CharacterCategory.Story:
                    return Localization.Get("journal.people.category.story");
                case CharacterProfileData.CharacterCategory.Family:
                    return Localization.Get("journal.people.category.family");
                default:
                    return Localization.Get("journal.people.category.villager");
            }
        }

        private static string ProfileName(CharacterProfileData profile)
        {
            if (profile == null) return string.Empty;
            return string.IsNullOrWhiteSpace(profile.DisplayNameId)
                ? profile.CharacterName
                : Localization.Get(profile.DisplayNameId, profile.CharacterName);
        }

        private static string ValueOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value;
        }

        private static string Roman(int value)
        {
            switch (value)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                default: return value.ToString();
            }
        }

        private float DetailTextWidth(float innerPadding = 0f)
        {
            float contentWidth = _detailContent != null && _detailContent.rect.width > 1f
                ? _detailContent.rect.width
                : 840f;
            // SelectedDossier has 28 px padding on both sides.
            return Mathf.Max(240f, contentWidth - 56f - Mathf.Max(0f, innerPadding));
        }

        private static float PreferredHeight(TMP_Text text, float width, float minimum)
        {
            if (text == null || string.IsNullOrWhiteSpace(text.text)) return minimum;
            Vector2 preferred = text.GetPreferredValues(
                text.text, Mathf.Max(1f, width), float.PositiveInfinity);
            return Mathf.Max(minimum, Mathf.Ceil(preferred.y + 2f));
        }

        private static void Pin(GameObject go, float width, float height, bool flexibleWidth = false)
        {
            LayoutElement layout = go.GetComponent<LayoutElement>();
            if (layout == null) layout = go.AddComponent<LayoutElement>();
            if (width > 0f) layout.preferredWidth = width;
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleWidth = flexibleWidth ? 1f : 0f;
            layout.flexibleHeight = 0f;
        }

        private void EnsureCanvas()
        {
            if (GetComponent<Canvas>() == null)
            {
                Canvas canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                gameObject.AddComponent<CanvasScaler>().Init1080();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            RectTransform rect = transform as RectTransform;
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
