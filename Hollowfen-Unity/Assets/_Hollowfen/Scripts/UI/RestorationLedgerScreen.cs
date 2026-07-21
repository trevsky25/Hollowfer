using System;
using System.Collections.Generic;
using Hollowfen.Cinematics;
using Hollowfen.Items;
using Hollowfen.Restoration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    /// <summary>Controller-first restoration record shared by worksite signs and the village board.</summary>
    public sealed class RestorationLedgerScreen : UIScreen
    {
        public const string RuntimeScreenId = "restoration-ledger";
        private const int ProjectSlotCount = 7;
        private const int MilestoneRowCount = 5;
        private const int ContributionSlotCount = 2;

        private static readonly Color Ink = new Color(0.15f, 0.125f, 0.09f, 1f);
        private static readonly Color MutedInk = new Color(0.28f, 0.245f, 0.18f, 0.78f);
        private static readonly Color Forest = new Color(0.12f, 0.235f, 0.145f, 1f);
        private static readonly Color ForestSoft = new Color(0.18f, 0.32f, 0.20f, 1f);

        private sealed class ProjectSlot
        {
            public GameObject Root;
            public Button Button;
            public Image Surface;
            public TMP_Text Number;
            public TMP_Text Title;
            public TMP_Text Stage;
            public RestorationProjectData Project;
        }

        private sealed class MilestoneRow
        {
            public GameObject Root;
            public Image Mark;
            public TMP_Text State;
            public TMP_Text Label;
            public TMP_Text Detail;
        }

        private sealed class ContributionSlot
        {
            public GameObject Root;
            public Button Button;
            public Image Surface;
            public TMP_Text Label;
            public RestorationContribution Contribution;
        }

        private static RestorationLedgerScreen _instance;
        public static RestorationLedgerScreen Instance => _instance;

        private readonly List<ProjectSlot> _projectSlots = new List<ProjectSlot>();
        private readonly List<MilestoneRow> _milestones = new List<MilestoneRow>();
        private readonly List<Image> _stageMarks = new List<Image>();
        private readonly List<TMP_Text> _stageLabels = new List<TMP_Text>();
        private readonly List<ContributionSlot> _contributionSlots = new List<ContributionSlot>();

        private CanvasGroup _group;
        private Button _closeButton;
        private TMP_Text _title;
        private TMP_Text _summary;
        private TMP_Text _benefit;
        private TMP_Text _stageEyebrow;
        private TMP_Text _stageTitle;
        private TMP_Text _stageBody;
        private TMP_Text _location;
        private TMP_Text _dayRecord;
        private TMP_Text _contributionStatus;
        private RectTransform _contentPanel;
        private RestorationProjectData _selected;
        private NarrativePresentationSession.Lease _lease;
        private bool _built;

        public bool IsOpen => UIManager.Instance != null && UIManager.Instance.TopScreen == this;

        public override GameObject DefaultSelected
        {
            get
            {
                foreach (var slot in _projectSlots)
                    if (slot.Root.activeInHierarchy && slot.Button.interactable &&
                        slot.Project == _selected) return slot.Root;
                foreach (var slot in _projectSlots)
                    if (slot.Root.activeInHierarchy && slot.Button.interactable) return slot.Root;
                return _closeButton != null ? _closeButton.gameObject : base.DefaultSelected;
            }
        }

        public static RestorationLedgerScreen Ensure()
        {
            if (_instance != null) return _instance;
            if (UIManager.Instance == null) return null;

            _instance = FindAnyObjectByType<RestorationLedgerScreen>(FindObjectsInactive.Include);
            if (_instance != null)
            {
                _instance.PrepareRegistration();
                UIManager.Instance.RegisterScreen(_instance);
                return _instance;
            }

            var host = new GameObject("_RestorationLedgerScreen", typeof(RectTransform));
            host.SetActive(false);
            host.transform.SetParent(UIManager.Instance.transform, false);
            _instance = host.AddComponent<RestorationLedgerScreen>();
            _instance.PrepareRegistration();
            UIManager.Instance.RegisterScreen(_instance);
            return _instance;
        }

        public static void OpenProject(RestorationProjectData project)
        {
            if (project == null || UIManager.Instance == null) return;
            var screen = Ensure();
            if (screen == null) return;
            screen._selected = project;
            UIManager.Instance.OpenScreen(RuntimeScreenId);
        }

        private void PrepareRegistration() => ConfigureRuntimeScreen(RuntimeScreenId, null, null, false);

        protected override void Awake()
        {
            _instance = this;
            base.Awake();
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;
            try
            {
                BuildLayout();
                _built = true;
                ConfigureRuntimeScreen(RuntimeScreenId, DefaultSelected, _group, false);
            }
            catch (Exception exception)
            {
                Debug.LogError("[RestorationLedgerScreen] Build failed: " + exception);
            }
        }

        public override void OnOpen()
        {
            base.OnOpen();
            if (_lease == null)
                _lease = NarrativePresentationSession.AcquireIfGameplay(this,
                    NarrativePresentationSession.Modal.With(
                        NarrativePresentationSession.Claim.HideGameplayHud));
            RestorationProjects.OnStageChanged += HandleStageChanged;
            Refresh();
            if (EventSystem.current != null && DefaultSelected != null)
                EventSystem.current.SetSelectedGameObject(DefaultSelected);
        }

        public override void OnClose()
        {
            RestorationProjects.OnStageChanged -= HandleStageChanged;
            _lease?.Dispose();
            _lease = null;
            base.OnClose();
        }

        private void OnDestroy()
        {
            RestorationProjects.OnStageChanged -= HandleStageChanged;
            _lease?.Dispose();
            if (_instance == this) _instance = null;
        }

        private void HandleStageChanged(string _, RestorationStage __) => Refresh();

        private void Refresh()
        {
            if (!_built) return;
            var available = new List<RestorationProjectData>();
            foreach (var project in RestorationProjects.AllProjects)
                if (project != null && RestorationProjects.GetStage(project) > RestorationStage.Unavailable)
                    available.Add(project);

            if (_selected == null || !available.Contains(_selected))
                _selected = available.Count > 0 ? available[0] : null;

            for (int i = 0; i < _projectSlots.Count; i++)
            {
                var slot = _projectSlots[i];
                bool visible = i < available.Count;
                slot.Root.SetActive(visible);
                if (!visible) continue;
                slot.Project = available[i];
                var stage = RestorationProjects.GetStage(slot.Project);
                slot.Number.text = (i + 1).ToString("00");
                slot.Title.text = Localization.Get(slot.Project.TitleId);
                slot.Stage.text = Localization.Get(slot.Project.StageShortId(stage));
                bool selected = slot.Project == _selected;
                slot.Surface.color = selected ? Forest : new Color(1f, 1f, 1f, 0.035f);
                slot.Number.color = selected ? HollowfenPalette.Gold :
                    new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                        HollowfenPalette.Gold.b, 0.72f);
                slot.Title.color = selected ? HollowfenPalette.Cream :
                    new Color(HollowfenPalette.Parchment.r, HollowfenPalette.Parchment.g,
                        HollowfenPalette.Parchment.b, 0.78f);
                slot.Stage.color = selected ? new Color(0.78f, 0.72f, 0.50f, 1f) :
                    new Color(HollowfenPalette.Moss.r, HollowfenPalette.Moss.g,
                        HollowfenPalette.Moss.b, 0.82f);
            }

            RefreshProject();
        }

        private void RefreshProject()
        {
            bool hasProject = _selected != null;
            _contentPanel.gameObject.SetActive(hasProject);
            if (!hasProject) return;

            var stage = RestorationProjects.GetStage(_selected);
            _title.text = Localization.Get(_selected.TitleId);
            _summary.text = Localization.Get(_selected.SummaryId);
            bool hasBenefit = !string.IsNullOrWhiteSpace(_selected.BenefitId);
            _benefit.gameObject.SetActive(hasBenefit);
            if (hasBenefit)
                _benefit.text = string.Format(Localization.Get("restoration.benefit.format"),
                    Localization.Get(_selected.BenefitId));
            _stageEyebrow.text = string.Format(Localization.Get("restoration.stage.eyebrow"),
                ((int)stage).ToString("00"), ((int)RestorationStage.Occupied).ToString("00"));
            _stageTitle.text = Localization.Get(_selected.StageTitleId(stage));
            _stageBody.text = Localization.Get(_selected.StageBodyId(stage));
            _location.text = string.Format(Localization.Get("restoration.location.format"),
                Localization.Get(_selected.LocationId));

            int started = RestorationProjects.StartedDay(_selected);
            int changed = RestorationProjects.ChangedDay(_selected);
            _dayRecord.text = started > 0
                ? string.Format(Localization.Get("restoration.day_record"), started, changed)
                : Localization.Get("restoration.day_record.pending");

            for (int i = 0; i < _stageMarks.Count; i++)
            {
                var rowStage = (RestorationStage)(i + 1);
                bool reached = stage >= rowStage;
                bool current = stage == rowStage;
                _stageLabels[i].text = Localization.Get(_selected.StageShortId(rowStage));
                _stageMarks[i].color = reached ? ForestSoft : new Color(Ink.r, Ink.g, Ink.b, 0.12f);
                _stageLabels[i].color = current ? Ink : reached ? ForestSoft : MutedInk;
                _stageLabels[i].fontStyle = current ? FontStyles.Bold : FontStyles.Normal;
            }

            var contributions = _selected.Contributions ?? Array.Empty<RestorationContribution>();
            bool hasContributions = contributions.Length > 0;
            _stageBody.rectTransform.sizeDelta = new Vector2(hasContributions ? 520f : 872f, 43f);
            _stageBody.rectTransform.anchoredPosition = new Vector2(24f, -111f);
            _contributionStatus.gameObject.SetActive(hasContributions);
            if (hasContributions && string.IsNullOrWhiteSpace(_contributionStatus.text))
                _contributionStatus.text = Localization.Get("restoration.contribution.prompt");
            for (int i = 0; i < _contributionSlots.Count; i++)
            {
                var slot = _contributionSlots[i];
                bool visible = i < contributions.Length;
                slot.Root.SetActive(visible);
                if (!visible) continue;
                slot.Contribution = contributions[i];
                bool funded = RestorationProjects.IsContributionFunded(slot.Contribution);
                bool available = stage >= slot.Contribution.AvailableFromStage;
                slot.Label.text = funded
                    ? string.Format(Localization.Get("restoration.contribution.funded"),
                        Localization.Get(slot.Contribution.LabelId))
                    : Localization.Get(slot.Contribution.LabelId) + "  ·  " +
                      CoinPurse.Format(slot.Contribution.CostCopper);
                slot.Button.interactable = available && !funded;
                slot.Surface.color = funded ? ForestSoft : available ? Forest :
                    new Color(Ink.r, Ink.g, Ink.b, .10f);
                slot.Label.color = available || funded ? HollowfenPalette.Cream : MutedInk;
            }

            var milestones = _selected.Milestones ?? Array.Empty<RestorationMilestone>();
            for (int i = 0; i < _milestones.Count; i++)
            {
                var row = _milestones[i];
                bool visible = i < milestones.Length;
                row.Root.SetActive(visible);
                if (!visible) continue;
                bool complete = RestorationProjects.IsMilestoneComplete(milestones[i]);
                row.Mark.color = complete ? ForestSoft : new Color(Ink.r, Ink.g, Ink.b, 0.09f);
                row.State.text = Localization.Get(complete
                    ? "restoration.milestone.complete"
                    : "restoration.milestone.pending");
                row.State.color = complete ? ForestSoft : HollowfenPalette.PaperAccentInk;
                row.Label.text = Localization.Get(milestones[i].LabelId);
                row.Detail.text = Localization.Get(milestones[i].DetailId);
            }
        }

        private void SelectProject(int index)
        {
            if (index < 0 || index >= _projectSlots.Count) return;
            var project = _projectSlots[index].Project;
            if (project == null) return;
            _selected = project;
            if (_contributionStatus != null) _contributionStatus.text = "";
            Refresh();
        }

        private void ReviewContribution(int index)
        {
            if (_selected == null || index < 0 || index >= _contributionSlots.Count) return;
            var slot = _contributionSlots[index];
            if (!slot.Root.activeInHierarchy || !slot.Button.interactable) return;
            var contribution = slot.Contribution;
            string message = Localization.Get(contribution.DetailId) + "\n\n" +
                             string.Format(Localization.Get("restoration.contribution.confirm.body"),
                                 CoinPurse.Format(contribution.CostCopper));
            ConfirmModal.Show(Localization.Get(contribution.LabelId), message, () =>
            {
                var result = RestorationProjects.Contribute(_selected, contribution);
                switch (result)
                {
                    case RestorationProjects.ContributionResult.Funded:
                        _contributionStatus.text = Localization.Get("restoration.contribution.success");
                        break;
                    case RestorationProjects.ContributionResult.InsufficientCoins:
                        _contributionStatus.text = Localization.Get("restoration.contribution.insufficient");
                        break;
                    case RestorationProjects.ContributionResult.SaveUnavailable:
                        _contributionStatus.text = Localization.Get("restoration.contribution.save_failed");
                        break;
                    default:
                        _contributionStatus.text = Localization.Get("restoration.contribution.unavailable");
                        break;
                }
                Refresh();
            });
        }

        private void BuildLayout()
        {
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.Init1080();
            if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            var scrim = UICanvasUtil.NewImage("Scrim", transform, new Color(0.018f, 0.025f, 0.016f, 0.88f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            var ledger = UICanvasUtil.NewRect("RestorationLedger", transform);
            ledger.sizeDelta = new Vector2(1536f, 886f);
            UICanvasUtil.AddShadow(ledger, 30, 42, 0.56f, -12f);
            UICanvasUtil.MakeRoundedPanel(ledger, new Color(0.91f, 0.875f, 0.76f, 1f), 28, 0.48f);

            var inner = UICanvasUtil.NewImage("InnerRule", ledger,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.22f), false);
            var innerImage = inner.GetComponent<Image>();
            innerImage.sprite = UICanvasUtil.RoundedOutline(22, 1.4f);
            innerImage.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)inner.transform);
            ((RectTransform)inner.transform).offsetMin = new Vector2(15f, 15f);
            ((RectTransform)inner.transform).offsetMax = new Vector2(-15f, -15f);

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", ledger,
                Localization.Get("restoration.eyebrow"), 18f, HollowfenPalette.PaperAccentInk,
                TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(650f, 22f), new Vector2(58f, -40f));
            var heading = UICanvasUtil.NewHeading("Heading", ledger, Localization.Get("restoration.heading"),
                54f, Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(720f, 66f), new Vector2(56f, -64f));

            _closeButton = BuildButton("Close", ledger, Localization.Get("restoration.close"),
                new Vector2(1322f, -54f), new Vector2(150f, 48f), false, OnBack);

            BuildProjectRail(ledger);
            BuildProjectContent(ledger);

            var hint = UICanvasUtil.NewBody("Hint", ledger, Localization.Get("restoration.hint"),
                20f, MutedInk, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(-100f, 30f), new Vector2(0f, 19f));
        }

        private void BuildProjectRail(RectTransform ledger)
        {
            var rail = UICanvasUtil.NewRect("ProjectRail", ledger);
            UICanvasUtil.SetRect(rail, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(418f, 686f), new Vector2(54f, -150f));
            UICanvasUtil.MakeRoundedPanel(rail, new Color(0.08f, 0.13f, 0.085f, 0.965f), 22, 0.20f);

            var title = UICanvasUtil.NewEyebrow("RailTitle", rail, Localization.Get("restoration.projects"),
                18f, HollowfenPalette.Gold, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-50f, 20f), new Vector2(0f, -32f));

            for (int i = 0; i < ProjectSlotCount; i++)
            {
                int index = i;
                var rt = UICanvasUtil.NewRect("Project_" + i, rail);
                UICanvasUtil.SetRect(rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-36f, 70f), new Vector2(0f, -88f - i * 85f));
                var surface = UICanvasUtil.MakeRoundedPanel(rt, new Color(1f, 1f, 1f, 0.05f), 16, 0.14f);
                surface.raycastTarget = true;
                var button = rt.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = surface;
                button.onClick.AddListener(() => SelectProject(index));

                // Focus and the currently opened project are separate states. A gold outline
                // follows controller/pointer focus while the forest fill remains the durable
                // content selection, so browsing the rail never makes two projects look active.
                var focusRing = UICanvasUtil.NewImage("FocusRing", rt, HollowfenPalette.Gold, false);
                var focusImage = focusRing.GetComponent<Image>();
                focusImage.sprite = UICanvasUtil.RoundedOutline(16, 1.5f);
                focusImage.type = Image.Type.Sliced;
                UICanvasUtil.Stretch(focusImage.rectTransform);
                focusImage.rectTransform.offsetMin = new Vector2(-2f, -2f);
                focusImage.rectTransform.offsetMax = new Vector2(2f, 2f);
                rt.gameObject.AddComponent<FocusHighlight>().Configure(surface, rt,
                    surface.color, 1.012f, false, true, false, focusImage);

                var number = UICanvasUtil.NewEyebrow("Number", rt, "", 11f, HollowfenPalette.Gold,
                    TextAlignmentOptions.Center);
                UICanvasUtil.SetRect(number.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(13f, 0f));
                var projectTitle = UICanvasUtil.NewHeading("Title", rt, "", 20f, HollowfenPalette.Cream,
                    FontStyles.Normal, TextAlignmentOptions.Left);
                projectTitle.textWrappingMode = TextWrappingModes.NoWrap;
                projectTitle.overflowMode = TextOverflowModes.Ellipsis;
                UICanvasUtil.SetRect(projectTitle.rectTransform, new Vector2(0f, 0.5f), Vector2.one,
                    new Vector2(0f, 0.5f), new Vector2(-84f, 29f), new Vector2(70f, 7f));
                var stage = UICanvasUtil.NewBody("Stage", rt, "", 11f, HollowfenPalette.Moss,
                    FontStyles.Bold, TextAlignmentOptions.Left);
                stage.characterSpacing = 8f;
                UICanvasUtil.SetRect(stage.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f),
                    new Vector2(0f, 0.5f), new Vector2(-84f, 21f), new Vector2(70f, -7f));

                _projectSlots.Add(new ProjectSlot
                {
                    Root = rt.gameObject, Button = button, Surface = surface,
                    Number = number, Title = projectTitle, Stage = stage,
                });
            }
        }

        private void BuildProjectContent(RectTransform ledger)
        {
            _contentPanel = UICanvasUtil.NewRect("ProjectContent", ledger);
            UICanvasUtil.SetRect(_contentPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(974f, 686f), new Vector2(502f, -150f));

            _stageEyebrow = UICanvasUtil.NewEyebrow("StageEyebrow", _contentPanel, "", 12f,
                HollowfenPalette.PaperAccentInk, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(_stageEyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(550f, 20f), new Vector2(8f, -2f));
            _title = UICanvasUtil.NewHeading("ProjectTitle", _contentPanel, "", 46f, Ink,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(700f, 58f), new Vector2(6f, -30f));
            _summary = UICanvasUtil.NewBody("Summary", _contentPanel, "", 17f, MutedInk,
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(_summary.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(900f, 54f), new Vector2(8f, -92f));
            _benefit = UICanvasUtil.NewBody("Benefit", _contentPanel, "", 14f, ForestSoft,
                FontStyles.Bold, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(_benefit.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(900f, 22f), new Vector2(8f, -146f));

            BuildTimeline(_contentPanel);
            BuildStageCard(_contentPanel);
            BuildMilestoneCard(_contentPanel);
        }

        private void BuildTimeline(RectTransform parent)
        {
            var line = UICanvasUtil.NewImage("TimelineLine", parent,
                new Color(Ink.r, Ink.g, Ink.b, 0.12f), false);
            UICanvasUtil.SetRect((RectTransform)line.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f), new Vector2(772f, 2f), new Vector2(92f, -178f));

            string[] labelIds =
            {
                "restoration.stage.surveyed.short", "restoration.stage.suppliescommitted.short",
                "restoration.stage.workunderway.short", "restoration.stage.restored.short",
                "restoration.stage.occupied.short",
            };
            for (int i = 0; i < labelIds.Length; i++)
            {
                float x = 92f + i * 193f;
                var mark = UICanvasUtil.NewImage("StageMark_" + i, parent, Color.white, false).GetComponent<Image>();
                mark.sprite = UICanvasUtil.RoundedRect(12);
                mark.type = Image.Type.Sliced;
                UICanvasUtil.SetRect(mark.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0.5f, 0.5f), new Vector2(20f, 20f), new Vector2(x, -178f));
                var label = UICanvasUtil.NewBody("StageLabel_" + i, parent, Localization.Get(labelIds[i]),
                    12f, MutedInk, FontStyles.Normal, TextAlignmentOptions.Top);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                UICanvasUtil.SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(150f, 24f), new Vector2(x, -194f));
                _stageMarks.Add(mark);
                _stageLabels.Add(label);
            }
        }

        private void BuildStageCard(RectTransform parent)
        {
            var card = UICanvasUtil.NewRect("CurrentStage", parent);
            UICanvasUtil.SetRect(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(920f, 166f), new Vector2(6f, -242f));
            UICanvasUtil.MakeRoundedPanel(card, new Color(1f, 1f, 1f, 0.20f), 20, 0.18f);

            var label = UICanvasUtil.NewEyebrow("Label", card, Localization.Get("restoration.current_stage"),
                12f, HollowfenPalette.PaperAccentInk, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-48f, 20f), new Vector2(0f, -25f));
            _stageTitle = UICanvasUtil.NewHeading("Title", card, "", 28f, Ink,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _stageTitle.textWrappingMode = TextWrappingModes.Normal;
            _stageTitle.overflowMode = TextOverflowModes.Ellipsis;
            UICanvasUtil.SetRect(_stageTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(520f, 60f), new Vector2(24f, -45f));
            _stageBody = UICanvasUtil.NewBody("Body", card, "", 14.5f, MutedInk,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _stageBody.textWrappingMode = TextWrappingModes.Normal;
            UICanvasUtil.SetRect(_stageBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(872f, 43f), new Vector2(24f, -111f));

            _contributionStatus = UICanvasUtil.NewBody("ContributionStatus", card, "", 11f,
                HollowfenPalette.PaperAccentInk, FontStyles.Italic, TextAlignmentOptions.Right);
            UICanvasUtil.SetRect(_contributionStatus.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(325f, 18f), new Vector2(570f, -25f));

            for (int i = 0; i < ContributionSlotCount; i++)
            {
                int index = i;
                var rt = UICanvasUtil.NewRect("Contribution_" + i, card);
                UICanvasUtil.SetRect(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(325f, 43f), new Vector2(570f, -49f - i * 50f));
                var surface = UICanvasUtil.MakeRoundedPanel(rt, Forest, 13, .14f);
                surface.raycastTarget = true;
                var button = rt.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = surface;
                button.onClick.AddListener(() => ReviewContribution(index));
                var text = UICanvasUtil.NewEyebrow("Label", rt, "", 11f,
                    HollowfenPalette.Cream, TextAlignmentOptions.Center);
                UICanvasUtil.Stretch(text.rectTransform);
                rt.gameObject.AddComponent<FocusHighlight>().Configure(surface, rt, ForestSoft, 1.012f);
                _contributionSlots.Add(new ContributionSlot
                {
                    Root = rt.gameObject,
                    Button = button,
                    Surface = surface,
                    Label = text,
                });
            }
        }

        private void BuildMilestoneCard(RectTransform parent)
        {
            var card = UICanvasUtil.NewRect("Milestones", parent);
            UICanvasUtil.SetRect(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(920f, 230f), new Vector2(6f, -420f));
            UICanvasUtil.MakeRoundedPanel(card, new Color(0.08f, 0.13f, 0.085f, 0.965f), 20, 0.18f);

            var label = UICanvasUtil.NewEyebrow("Label", card, Localization.Get("restoration.milestones"),
                12f, HollowfenPalette.Gold, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-48f, 20f), new Vector2(0f, -24f));

            for (int i = 0; i < MilestoneRowCount; i++)
            {
                var row = UICanvasUtil.NewRect("Milestone_" + i, card);
                UICanvasUtil.SetRect(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-48f, 32f), new Vector2(0f, -50f - i * 34f));
                var mark = UICanvasUtil.NewImage("Mark", row, Color.white, false).GetComponent<Image>();
                mark.sprite = UICanvasUtil.RoundedRect(10);
                mark.type = Image.Type.Sliced;
                UICanvasUtil.SetRect(mark.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(0.5f, 0.5f), new Vector2(17f, 17f), new Vector2(4f, 0f));
                var state = UICanvasUtil.NewEyebrow("State", row, "", 9f, HollowfenPalette.Moss,
                    TextAlignmentOptions.Left);
                UICanvasUtil.SetRect(state.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(0f, 0.5f), new Vector2(104f, 20f), new Vector2(24f, 0f));
                var milestoneLabel = UICanvasUtil.NewBody("Title", row, "", 14f, HollowfenPalette.Cream,
                    FontStyles.Bold, TextAlignmentOptions.Left);
                UICanvasUtil.SetRect(milestoneLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(0f, 0.5f), new Vector2(262f, 28f), new Vector2(132f, 0f));
                var detail = UICanvasUtil.NewBody("Detail", row, "", 13f, HollowfenPalette.Moss,
                    FontStyles.Italic, TextAlignmentOptions.Left);
                detail.textWrappingMode = TextWrappingModes.Normal;
                UICanvasUtil.SetRect(detail.rectTransform, new Vector2(0f, 0f), Vector2.one,
                    new Vector2(0f, 0.5f), new Vector2(-420f, 30f), new Vector2(398f, 0f));
                _milestones.Add(new MilestoneRow
                {
                    Root = row.gameObject, Mark = mark, State = state,
                    Label = milestoneLabel, Detail = detail,
                });
            }

            _location = UICanvasUtil.NewBody("Location", parent, "", 14f, Ink,
                FontStyles.Bold, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(_location.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(460f, 22f), new Vector2(14f, 3f));
            _dayRecord = UICanvasUtil.NewBody("DayRecord", parent, "", 13f, MutedInk,
                FontStyles.Italic, TextAlignmentOptions.Right);
            UICanvasUtil.SetRect(_dayRecord.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(440f, 22f), new Vector2(-14f, 3f));
        }

        private static Button BuildButton(string name, RectTransform parent, string label,
            Vector2 anchored, Vector2 size, bool accent, UnityEngine.Events.UnityAction action)
        {
            var rt = UICanvasUtil.NewRect(name + "Button", parent);
            UICanvasUtil.SetRect(rt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), size, anchored);
            var surface = rt.gameObject.AddComponent<Image>();
            surface.sprite = UICanvasUtil.RoundedRect(14);
            surface.type = Image.Type.Sliced;
            surface.color = accent ? Forest : new Color(Ink.r, Ink.g, Ink.b, 0.07f);
            surface.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = surface;
            button.onClick.AddListener(action);
            var text = UICanvasUtil.NewEyebrow("Label", rt, label, 18f,
                accent ? HollowfenPalette.Cream : Ink, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(text.rectTransform);
            rt.gameObject.AddComponent<FocusHighlight>().Configure(surface, rt,
                accent ? ForestSoft : new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                    HollowfenPalette.Gold.b, 0.24f), 1.015f);
            return button;
        }
    }
}
