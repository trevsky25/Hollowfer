using System;
using System.Collections.Generic;
using Hollowfen.Cinematics;
using Hollowfen.Dialogue;
using Hollowfen.Input;
using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Hollowfen.Apothecary
{
    /// <summary>
    /// Controller-safe appointment ledger: accept a case, reveal observations and testimony,
    /// choose one already-prepared fictional remedy, then return after time has passed.
    /// </summary>
    public sealed class ApothecaryCaseScreen : MonoBehaviour
    {
        private static readonly Color Paper = new Color(.925f, .895f, .82f, 1f);
        private static readonly Color Page = new Color(.972f, .95f, .89f, 1f);
        private static readonly Color Ink = new Color(.13f, .105f, .075f, 1f);
        private static readonly Color Muted = new Color(.31f, .27f, .20f, .82f);
        private static readonly Color Forest = new Color(.10f, .22f, .135f, 1f);
        private static readonly Color Gold = HollowfenPalette.Gold;
        private static readonly Color Success = new Color(.25f, .49f, .29f, 1f);
        private static readonly Color Warning = new Color(.59f, .31f, .18f, 1f);

        public static ApothecaryCaseScreen Instance { get; private set; }

        private readonly List<GameObject> _caseRoots = new List<GameObject>();
        private readonly List<Button> _caseButtons = new List<Button>();
        private readonly List<Button> _pageButtons = new List<Button>();
        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _caseList;
        private RectTransform _pageRoot;
        private RectTransform _stageRail;
        private Image _portrait;
        private TMP_Text _patient;
        private TMP_Text _title;
        private TMP_Text _complaint;
        private TMP_Text _feedback;
        private Button _closeButton;
        private ApothecaryCaseDatabase _database;
        private ApothecaryCaseLedgerStation _station;
        private InputActions _input;
        private int _selected;
        private NarrativePresentationSession.Lease _presentationLease;

        public bool IsOpen => _canvas != null && _canvas.enabled;

        public static ApothecaryCaseScreen Ensure()
        {
            if (Instance != null) return Instance;
            return new GameObject("_ApothecaryCaseScreen").AddComponent<ApothecaryCaseScreen>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _input = new InputActions();
            Build();
            _canvas.enabled = false;
        }

        private void OnEnable()
        {
            _input?.UI.Enable();
            if (_input != null) _input.UI.Cancel.performed += OnCancel;
            ApothecaryCases.OnChanged += Refresh;
            ApothecaryRuntime.OnChanged += Refresh;
        }

        private void OnDisable()
        {
            ApothecaryCases.OnChanged -= Refresh;
            ApothecaryRuntime.OnChanged -= Refresh;
            if (_input == null) return;
            _input.UI.Cancel.performed -= OnCancel;
            _input.UI.Disable();
        }

        private void OnDestroy()
        {
            ReleasePresentation();
            if (Instance == this) Instance = null;
            _input?.Dispose();
        }

        public void Open(ApothecaryCaseLedgerStation station)
        {
            if (station == null || IsOpen) return;
            _database = ApothecaryCaseDatabase.Load();
            _station = station;
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, (_database?.Cases.Count ?? 1) - 1));
            _presentationLease = NarrativePresentationSession.Acquire(
                this, NarrativePresentationSession.Modal);
            EnsureEventSystem();
            _canvas.enabled = true;
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            Refresh();
            SelectFirst();
            UISfx.PageTurn();
        }

        public void Close()
        {
            if (!IsOpen) return;
            _canvas.enabled = false;
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _station = null;
            ReleasePresentation();
            EventSystem.current?.SetSelectedGameObject(null);
            UISfx.Back();
        }

        private void OnCancel(InputAction.CallbackContext _) => Close();

        private void Update()
        {
            if (!IsOpen) return;
            UIFocusRecovery.RestoreIfLost(transform, PreferredFocus());
        }

        private GameObject PreferredFocus()
        {
            if (_caseButtons.Count > 0)
            {
                int index = Mathf.Clamp(_selected, 0, _caseButtons.Count - 1);
                if (_caseButtons[index] != null && _caseButtons[index].IsInteractable())
                    return _caseButtons[index].gameObject;
            }
            foreach (Button button in _pageButtons)
                if (button != null && button.IsInteractable()) return button.gameObject;
            return _closeButton != null ? _closeButton.gameObject : null;
        }

        private void ReleasePresentation()
        {
            _presentationLease?.Dispose();
            _presentationLease = null;
        }

        private void Refresh()
        {
            if (!IsOpen) return;
            string priorSelection = EventSystem.current != null &&
                                    EventSystem.current.currentSelectedGameObject != null
                ? EventSystem.current.currentSelectedGameObject.name
                : string.Empty;
            BuildCaseRows();
            RefreshPage();
            RestoreSelection(priorSelection);
        }

        private ApothecaryCaseData Current()
        {
            if (_database == null || _selected < 0 || _selected >= _database.Cases.Count) return null;
            return _database.Cases[_selected];
        }

        private void SelectCase(int index)
        {
            if (_database == null || index < 0 || index >= _database.Cases.Count) return;
            _selected = index;
            _feedback.text = string.Empty;
            Refresh();
            SelectFirst();
            UISfx.PageTurn(.25f);
        }

        private void BuildCaseRows()
        {
            foreach (GameObject root in _caseRoots) if (root != null) Destroy(root);
            _caseRoots.Clear();
            _caseButtons.Clear();
            if (_database == null) return;

            for (int i = 0; i < _database.Cases.Count; i++)
            {
                int captured = i;
                ApothecaryCaseData data = _database.Cases[i];
                RectTransform row = NewFixed("Case_" + (data != null ? data.Id : i.ToString()),
                    _caseList, new Vector2(350f, 80f), new Vector2(0f, 230f - i * 90f));
                Image background = row.gameObject.AddComponent<Image>();
                background.sprite = UICanvasUtil.RoundedRect(14);
                background.type = Image.Type.Sliced;
                background.color = i == _selected
                    ? new Color(.97f, .90f, .68f, 1f)
                    : new Color(.99f, .97f, .91f, .54f);
                ApothecaryCases.CaseRecord state = ApothecaryCases.Get(data);
                bool sealedCase = state.Stage == ApothecaryCaseStage.Unstarted &&
                                  !ApothecaryCases.IsUnlocked(data);
                TMP_Text number = UICanvasUtil.NewEyebrow("Number", row, (i + 1).ToString("00"),
                    18f, Muted);
                SetFixed(number.rectTransform, new Vector2(46f, 22f), new Vector2(-142f, 24f));
                TMP_Text label = UICanvasUtil.NewHeading("Title", row,
                    data != null && !sealedCase
                        ? Localization.Get(data.TextId("short"))
                        : Localization.Get("apothecary.case.sealed"), 21f, Ink,
                    FontStyles.Italic, TextAlignmentOptions.Left);
                SetFixed(label.rectTransform, new Vector2(265f, 35f), new Vector2(30f, 20f));
                TMP_Text status = UICanvasUtil.NewBody("Status", row, CaseStatus(data, state), 18f,
                    state.Stage == ApothecaryCaseStage.Resolved ? Success : Muted,
                    FontStyles.Bold, TextAlignmentOptions.Left);
                SetFixed(status.rectTransform, new Vector2(265f, 25f), new Vector2(30f, -24f));
                Button button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                ConfigureColors(button, background.color);
                button.onClick.AddListener(() => SelectCase(captured));
                _caseRoots.Add(row.gameObject);
                _caseButtons.Add(button);
            }
        }

        private string CaseStatus(ApothecaryCaseData data, ApothecaryCases.CaseRecord state)
        {
            if (data == null) return "—";
            switch (state.Stage)
            {
                case ApothecaryCaseStage.Investigating:
                    return Localization.Get("apothecary.case.status.investigating");
                case ApothecaryCaseStage.AwaitingFollowUp:
                    return Localization.Get("apothecary.case.status.followup");
                case ApothecaryCaseStage.Resolved:
                    return Localization.Get("apothecary.case.status.resolved");
                default:
                    return ApothecaryCases.IsUnlocked(data)
                        ? Localization.Get("apothecary.case.status.new")
                        : Localization.Get("apothecary.case.status.sealed");
            }
        }

        private void RefreshPage()
        {
            ClearPage();
            ApothecaryCaseData data = Current();
            if (data == null)
            {
                _title.text = Localization.Get("apothecary.case.empty");
                _patient.text = string.Empty;
                _complaint.text = Localization.Get("apothecary.case.empty.body");
                _portrait.enabled = false;
                return;
            }

            _portrait.sprite = data.PatientProfile != null ? data.PatientProfile.HeroPortrait : null;
            _portrait.enabled = _portrait.sprite != null;
            _portrait.preserveAspect = true;
            ApothecaryCases.CaseRecord record = ApothecaryCases.Get(data);
            bool sealedCase = record.Stage == ApothecaryCaseStage.Unstarted &&
                              !ApothecaryCases.IsUnlocked(data);
            if (sealedCase)
            {
                _portrait.enabled = false;
                _patient.text = Localization.Get("apothecary.case.sealed").ToUpperInvariant();
                _title.text = Localization.Get("apothecary.case.sealed");
                _complaint.text = Localization.Get(data.TextId("locked"));
                BuildStageRail(record.Stage);
                BuildUnstarted(data);
                WireNavigation();
                return;
            }
            _patient.text = data.PatientProfile != null
                ? data.PatientProfile.CharacterName.ToUpperInvariant()
                : data.PatientNpcId.ToUpperInvariant();
            _title.text = Localization.Get(data.TextId("title"));
            _complaint.text = Localization.Get(data.TextId("complaint"));
            BuildStageRail(ApothecaryCases.Get(data).Stage);

            switch (record.Stage)
            {
                case ApothecaryCaseStage.Investigating:
                    if (ApothecaryCases.IsInvestigationComplete(data)) BuildDecisions(data);
                    else BuildInvestigation(data);
                    break;
                case ApothecaryCaseStage.AwaitingFollowUp:
                    BuildWaiting(data, record);
                    break;
                case ApothecaryCaseStage.Resolved:
                    BuildResolution(data, record);
                    break;
                default:
                    BuildUnstarted(data);
                    break;
            }
            WireNavigation();
        }

        private void ClearPage()
        {
            for (int i = _pageRoot.childCount - 1; i >= 0; i--)
                Destroy(_pageRoot.GetChild(i).gameObject);
            _pageButtons.Clear();
        }

        private void BuildUnstarted(ApothecaryCaseData data)
        {
            bool unlocked = ApothecaryCases.IsUnlocked(data);
            TMP_Text context = UICanvasUtil.NewBody("Context", _pageRoot,
                Localization.Get(data.TextId(unlocked ? "context" : "locked")), 20f, Muted,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetFixed(context.rectTransform, new Vector2(880f, 180f), new Vector2(0f, 85f));
            Button action = MakeButton("Begin", _pageRoot, new Vector2(520f, 64f),
                new Vector2(0f, -115f), Localization.Get(unlocked
                    ? "apothecary.case.begin" : "apothecary.case.sealed"),
                unlocked ? Forest : new Color(.30f, .28f, .24f, .35f),
                unlocked ? HollowfenPalette.Cream : Muted);
            action.interactable = unlocked;
            action.onClick.AddListener(() => BeginCase(data));
            _pageButtons.Add(action);
        }

        private void BuildInvestigation(ApothecaryCaseData data)
        {
            TMP_Text evidenceHead = UICanvasUtil.NewEyebrow("EvidenceHead", _pageRoot,
                Localization.Get("apothecary.case.observe"), 16f, HollowfenPalette.PaperAccentInk);
            SetFixed(evidenceHead.rectTransform, new Vector2(410f, 28f), new Vector2(-238f, 175f));
            TMP_Text interviewHead = UICanvasUtil.NewEyebrow("InterviewHead", _pageRoot,
                Localization.Get("apothecary.case.ask"), 16f, HollowfenPalette.PaperAccentInk);
            SetFixed(interviewHead.rectTransform, new Vector2(410f, 28f), new Vector2(238f, 175f));

            for (int i = 0; i < data.Clues.Length; i++)
            {
                int captured = i;
                bool known = ApothecaryCases.HasObserved(data, i);
                string suffix = "clue." + data.Clues[i].id + "." + (known ? "finding" : "label");
                Button button = MakeTextButton("Clue_" + i, _pageRoot, new Vector2(430f, 104f),
                    new Vector2(-238f, 92f - i * 118f), Localization.Get(data.TextId(suffix)),
                    known ? new Color(.88f, .91f, .80f, 1f) : new Color(.95f, .91f, .81f, 1f),
                    known ? Success : Ink);
                button.onClick.AddListener(() => RevealClue(data, captured));
                _pageButtons.Add(button);
            }
            for (int i = 0; i < data.Interviews.Length; i++)
            {
                int captured = i;
                bool known = ApothecaryCases.HasInterviewed(data, i);
                string suffix = "interview." + data.Interviews[i].id + "." +
                                (known ? "answer" : "question");
                Button button = MakeTextButton("Interview_" + i, _pageRoot,
                    new Vector2(430f, 104f), new Vector2(238f, 92f - i * 118f),
                    Localization.Get(data.TextId(suffix)),
                    known ? new Color(.88f, .91f, .80f, 1f) : new Color(.95f, .91f, .81f, 1f),
                    known ? Success : Ink);
                button.onClick.AddListener(() => RevealInterview(data, captured));
                _pageButtons.Add(button);
            }
        }

        private void BuildDecisions(ApothecaryCaseData data)
        {
            TMP_Text heading = UICanvasUtil.NewHeading("DecisionHeading", _pageRoot,
                Localization.Get("apothecary.case.choose"), 29f, Ink, FontStyles.Italic,
                TextAlignmentOptions.Center);
            SetFixed(heading.rectTransform, new Vector2(850f, 48f), new Vector2(0f, 180f));
            TMP_Text guidance = UICanvasUtil.NewBody("DecisionGuidance", _pageRoot,
                Localization.Get("apothecary.case.choose.body"), 20f, Muted,
                FontStyles.Italic, TextAlignmentOptions.Center);
            SetFixed(guidance.rectTransform, new Vector2(880f, 45f), new Vector2(0f, 135f));

            int count = data.Decisions?.Length ?? 0;
            float spacing = 305f;
            for (int i = 0; i < count; i++)
            {
                ApothecaryCaseDecision decision = data.Decisions[i];
                float x = (i - (count - 1) * .5f) * spacing;
                RectTransform card = NewFixed("Decision_" + decision.id, _pageRoot,
                    new Vector2(280f, 230f), new Vector2(x, -25f));
                UICanvasUtil.MakeRoundedPanel(card, new Color(.94f, .90f, .80f, 1f), 18, .42f);
                TMP_Text product = UICanvasUtil.NewHeading("Product", card,
                    Localization.Get(decision.preparation.ResultNameId), 23f, Ink,
                    FontStyles.Italic, TextAlignmentOptions.Center);
                SetFixed(product.rectTransform, new Vector2(240f, 72f), new Vector2(0f, 66f));
                int stock = ApothecaryRuntime.ProductCount(decision.preparation.ResultId);
                TMP_Text countText = UICanvasUtil.NewBody("Stock", card,
                    string.Format(Localization.Get("apothecary.case.stock"), stock), 18f,
                    stock > 0 ? Success : Warning, FontStyles.Bold, TextAlignmentOptions.Center);
                SetFixed(countText.rectTransform, new Vector2(230f, 30f), new Vector2(0f, 5f));
                Button choose = MakeButton("Choose", card, new Vector2(230f, 55f),
                    new Vector2(0f, -69f), Localization.Get("apothecary.case.use"),
                    stock > 0 ? Forest : new Color(.30f, .28f, .24f, .35f),
                    stock > 0 ? HollowfenPalette.Cream : Muted);
                choose.interactable = stock > 0;
                string captured = decision.id;
                choose.onClick.AddListener(() => Choose(data, captured));
                _pageButtons.Add(choose);
            }
        }

        private void BuildWaiting(ApothecaryCaseData data, ApothecaryCases.CaseRecord record)
        {
            ApothecaryCaseDecision? selected = ApothecaryCases.ChosenDecision(data);
            TMP_Text heading = UICanvasUtil.NewHeading("WaitingHeading", _pageRoot,
                Localization.Get("apothecary.case.waiting"), 32f, Ink, FontStyles.Italic,
                TextAlignmentOptions.Center);
            SetFixed(heading.rectTransform, new Vector2(850f, 50f), new Vector2(0f, 165f));
            string product = selected.HasValue
                ? Localization.Get(selected.Value.preparation.ResultNameId)
                : "—";
            TMP_Text body = UICanvasUtil.NewBody("WaitingBody", _pageRoot,
                string.Format(Localization.Get("apothecary.case.waiting.body"), product,
                    record.FollowUpDay), 21f, Muted, FontStyles.Normal, TextAlignmentOptions.Center);
            SetFixed(body.rectTransform, new Vector2(850f, 130f), new Vector2(0f, 65f));
            bool due = Hollowfen.GameTime.TimeManager.Instance != null &&
                       Hollowfen.GameTime.TimeManager.Instance.Day >= record.FollowUpDay;
            Button review = MakeButton("Review", _pageRoot, new Vector2(520f, 64f),
                new Vector2(0f, -100f), Localization.Get(due
                    ? "apothecary.case.review" : "apothecary.case.not_due"),
                due ? Forest : new Color(.30f, .28f, .24f, .35f),
                due ? HollowfenPalette.Cream : Muted);
            review.interactable = due;
            review.onClick.AddListener(() => Resolve(data));
            _pageButtons.Add(review);
        }

        private void BuildResolution(ApothecaryCaseData data, ApothecaryCases.CaseRecord record)
        {
            ApothecaryCaseDecision? selected = ApothecaryCases.ChosenDecision(data);
            if (!selected.HasValue) return;
            string root = "decision." + selected.Value.id + ".";
            TMP_Text check = UICanvasUtil.NewHeading("Check", _pageRoot,
                "<sprite name=\"ui_check\">", 72f, Success,
                FontStyles.Bold, TextAlignmentOptions.Center);
            SetFixed(check.rectTransform, new Vector2(90f, 85f), new Vector2(0f, 175f));
            TMP_Text heading = UICanvasUtil.NewHeading("OutcomeHeading", _pageRoot,
                Localization.Get(data.TextId(root + "outcome_title")), 31f, Ink,
                FontStyles.Italic, TextAlignmentOptions.Center);
            SetFixed(heading.rectTransform, new Vector2(850f, 54f), new Vector2(0f, 105f));
            TMP_Text body = UICanvasUtil.NewBody("OutcomeBody", _pageRoot,
                Localization.Get(data.TextId(root + "outcome_body")), 20f, Muted,
                FontStyles.Normal, TextAlignmentOptions.Center);
            SetFixed(body.rectTransform, new Vector2(860f, 150f), new Vector2(0f, -5f));
            TMP_Text grade = UICanvasUtil.NewEyebrow("Grade", _pageRoot,
                Localization.Get("apothecary.case.grade." +
                                 selected.Value.grade.ToString().ToLowerInvariant()), 16f,
                selected.Value.grade == ApothecaryCaseGrade.Careful ? Success : Gold,
                TextAlignmentOptions.Center);
            SetFixed(grade.rectTransform, new Vector2(600f, 30f), new Vector2(0f, -125f));
            TMP_Text day = UICanvasUtil.NewBody("ResolvedDay", _pageRoot,
                string.Format(Localization.Get("apothecary.case.resolved_day"), record.ResolvedDay),
                17f, Muted, FontStyles.Italic, TextAlignmentOptions.Center);
            SetFixed(day.rectTransform, new Vector2(500f, 28f), new Vector2(0f, -170f));
        }

        private void BeginCase(ApothecaryCaseData data)
        {
            ApothecaryCaseActionResult result = ApothecaryCases.Begin(data);
            if (result != ApothecaryCaseActionResult.Completed) { ShowFailure(result); return; }
            UISfx.Confirm();
            if (data.IntakeDialogue != null && DialogueScreen.Instance != null)
            {
                Transform anchor = _station != null ? _station.transform : null;
                Close();
                DialogueScreen.Instance.Open(data.IntakeDialogue, anchor);
            }
        }

        private void RevealClue(ApothecaryCaseData data, int index)
        {
            if (ApothecaryCases.Observe(data, index))
            {
                UISfx.PageTurn(.28f);
                SelectNextInvestigationAction(data);
            }
            else UISfx.Error(.18f);
        }

        private void RevealInterview(ApothecaryCaseData data, int index)
        {
            if (ApothecaryCases.Interview(data, index))
            {
                UISfx.Select(.35f);
                SelectNextInvestigationAction(data);
            }
            else UISfx.Error(.18f);
        }

        private void Choose(ApothecaryCaseData data, string decisionId)
        {
            ApothecaryCaseActionResult result = ApothecaryCases.Decide(data, decisionId);
            if (result != ApothecaryCaseActionResult.Completed) { ShowFailure(result); return; }
            _feedback.text = Localization.Get("apothecary.case.treatment_recorded");
            _feedback.color = Success;
            UISfx.Confirm(.7f);
            Refresh();
        }

        private void Resolve(ApothecaryCaseData data)
        {
            ApothecaryCaseActionResult result = ApothecaryCases.Resolve(data);
            if (result != ApothecaryCaseActionResult.Completed) { ShowFailure(result); return; }
            UISfx.Confirm(.8f);
            if (data.FollowUpDialogue != null && DialogueScreen.Instance != null)
            {
                Transform anchor = _station != null ? _station.transform : null;
                Close();
                DialogueScreen.Instance.Open(data.FollowUpDialogue, anchor);
            }
        }

        private void ShowFailure(ApothecaryCaseActionResult result)
        {
            _feedback.text = Localization.Get("apothecary.case.error." + result.ToString().ToLowerInvariant());
            _feedback.color = Warning;
            UISfx.Error();
        }

        private void BuildStageRail(ApothecaryCaseStage stage)
        {
            for (int i = _stageRail.childCount - 1; i >= 0; i--)
                Destroy(_stageRail.GetChild(i).gameObject);
            string[] labels =
            {
                "apothecary.case.stage.listen", "apothecary.case.stage.observe",
                "apothecary.case.stage.decide", "apothecary.case.stage.followup",
            };
            int active = stage == ApothecaryCaseStage.Unstarted ? 0 :
                stage == ApothecaryCaseStage.Investigating ? 1 :
                stage == ApothecaryCaseStage.AwaitingFollowUp ? 2 : 3;
            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform item = NewFixed("Stage_" + i, _stageRail, new Vector2(225f, 42f),
                    new Vector2((i - 1.5f) * 237f, 0f));
                Image bg = item.gameObject.AddComponent<Image>();
                bg.sprite = UICanvasUtil.RoundedRect(12);
                bg.type = Image.Type.Sliced;
                bg.color = i < active ? new Color(.39f, .57f, .39f, .28f) :
                    i == active ? new Color(.85f, .69f, .31f, .34f) :
                    new Color(.27f, .24f, .19f, .09f);
                TMP_Text text = UICanvasUtil.NewEyebrow("Label", item, Localization.Get(labels[i]),
                    18f, i <= active ? Ink : Muted, TextAlignmentOptions.Center);
                UICanvasUtil.Stretch(text.rectTransform);
            }
        }

        private void WireNavigation()
        {
            for (int i = 0; i < _caseButtons.Count; i++)
                _caseButtons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? _caseButtons[i - 1] : _closeButton,
                    selectOnDown = i + 1 < _caseButtons.Count ? _caseButtons[i + 1] : _closeButton,
                    selectOnRight = _pageButtons.Count > 0 ? _pageButtons[0] : _closeButton,
                };
            for (int i = 0; i < _pageButtons.Count; i++)
                _pageButtons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? _pageButtons[i - 1] :
                        (_caseButtons.Count > 0 ? _caseButtons[_selected] : _closeButton),
                    selectOnDown = i + 1 < _pageButtons.Count ? _pageButtons[i + 1] : _closeButton,
                    selectOnLeft = _caseButtons.Count > 0 ? _caseButtons[_selected] : _closeButton,
                };
            _closeButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = _pageButtons.Count > 0 ? _pageButtons[_pageButtons.Count - 1] :
                    (_caseButtons.Count > 0 ? _caseButtons[_caseButtons.Count - 1] : null),
                selectOnDown = _caseButtons.Count > 0 ? _caseButtons[0] :
                    (_pageButtons.Count > 0 ? _pageButtons[0] : null),
            };
        }

        private void SelectFirst()
        {
            GameObject selected = _caseButtons.Count > _selected
                ? _caseButtons[_selected].gameObject
                : _pageButtons.Count > 0 ? _pageButtons[0].gameObject : _closeButton.gameObject;
            EventSystem.current?.SetSelectedGameObject(selected);
        }

        private void RestoreSelection(string objectName)
        {
            if (string.IsNullOrEmpty(objectName) || EventSystem.current == null) return;
            foreach (Button button in _caseButtons)
                if (button != null && button.gameObject.name == objectName)
                {
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                    return;
                }
            foreach (Button button in _pageButtons)
                if (button != null && button.gameObject.name == objectName)
                {
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                    return;
                }
            if (_closeButton != null && _closeButton.gameObject.name == objectName)
                EventSystem.current.SetSelectedGameObject(_closeButton.gameObject);
        }

        private void SelectNextInvestigationAction(ApothecaryCaseData data)
        {
            if (EventSystem.current == null || data == null) return;
            if (ApothecaryCases.IsInvestigationComplete(data))
            {
                if (_pageButtons.Count > 0)
                    EventSystem.current.SetSelectedGameObject(_pageButtons[0].gameObject);
                return;
            }

            for (int i = 0; i < data.Clues.Length; i++)
                if (!ApothecaryCases.HasObserved(data, i) && i < _pageButtons.Count)
                {
                    EventSystem.current.SetSelectedGameObject(_pageButtons[i].gameObject);
                    return;
                }
            int offset = data.Clues.Length;
            for (int i = 0; i < data.Interviews.Length; i++)
                if (!ApothecaryCases.HasInterviewed(data, i) && offset + i < _pageButtons.Count)
                {
                    EventSystem.current.SetSelectedGameObject(_pageButtons[offset + i].gameObject);
                    return;
                }
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 93;
            gameObject.AddComponent<CanvasScaler>().Init1080();
            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();
            GameObject scrim = UICanvasUtil.NewImage("Scrim", transform,
                new Color(.018f, .022f, .016f, .72f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            RectTransform card = NewFixed("AppointmentLedger", transform,
                new Vector2(1580f, 900f), Vector2.zero);
            UICanvasUtil.MakeRoundedPanel(card, Paper, 28, .64f);
            UICanvasUtil.AddShadow(card, 24, 32, .50f, -10f);
            TMP_Text eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", card,
                Localization.Get("apothecary.case.eyebrow"), 18f,
                HollowfenPalette.PaperAccentInk, TextAlignmentOptions.Center);
            SetFixed(eyebrow.rectTransform, new Vector2(900f, 28f), new Vector2(0f, 420f));
            TMP_Text heading = UICanvasUtil.NewHeading("Heading", card,
                Localization.Get("apothecary.case.title"), 42f, Ink, FontStyles.Italic,
                TextAlignmentOptions.Center);
            SetFixed(heading.rectTransform, new Vector2(1000f, 56f), new Vector2(0f, 380f));

            RectTransform left = NewFixed("Cases", card, new Vector2(405f, 740f),
                new Vector2(-562f, -52f));
            UICanvasUtil.MakeRoundedPanel(left, new Color(.85f, .81f, .71f, 1f), 20, .45f);
            TMP_Text cases = UICanvasUtil.NewEyebrow("CasesLabel", left,
                Localization.Get("apothecary.case.appointments"), 18f,
                HollowfenPalette.PaperAccentInk, TextAlignmentOptions.Center);
            SetFixed(cases.rectTransform, new Vector2(350f, 30f), new Vector2(0f, 330f));
            _caseList = NewFixed("CaseList", left, new Vector2(360f, 590f), new Vector2(0f, 0f));
            _closeButton = MakeButton("Close", left, new Vector2(275f, 48f),
                new Vector2(0f, -326f), Localization.Get("apothecary.close"),
                new Color(.17f, .14f, .10f, .10f), Ink);
            _closeButton.onClick.AddListener(Close);

            RectTransform detail = NewFixed("CasePage", card, new Vector2(1085f, 740f),
                new Vector2(220f, -52f));
            UICanvasUtil.MakeRoundedPanel(detail, Page, 20, .42f);
            RectTransform portraitPlate = NewFixed("PortraitPlate", detail, new Vector2(255f, 245f),
                new Vector2(-388f, 211f));
            UICanvasUtil.MakeRoundedPanel(portraitPlate, new Color(.14f, .125f, .10f, 1f), 14, .3f);
            RectTransform portrait = NewFixed("Portrait", portraitPlate, new Vector2(235f, 225f), Vector2.zero);
            _portrait = portrait.gameObject.AddComponent<Image>();
            _portrait.raycastTarget = false;
            _patient = UICanvasUtil.NewEyebrow("Patient", detail, "", 18f,
                HollowfenPalette.PaperAccentInk);
            SetFixed(_patient.rectTransform, new Vector2(680f, 28f), new Vector2(128f, 314f));
            _title = UICanvasUtil.NewHeading("CaseTitle", detail, "", 38f, Ink,
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            SetFixed(_title.rectTransform, new Vector2(680f, 80f), new Vector2(128f, 250f));
            _complaint = UICanvasUtil.NewBody("Complaint", detail, "", 21f, Muted,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetFixed(_complaint.rectTransform, new Vector2(680f, 130f), new Vector2(128f, 145f));
            _stageRail = NewFixed("StageRail", detail, new Vector2(960f, 48f), new Vector2(0f, 55f));
            _pageRoot = NewFixed("PageContent", detail, new Vector2(990f, 410f), new Vector2(0f, -160f));
            _feedback = UICanvasUtil.NewBody("Feedback", detail, "", 17f, Warning,
                FontStyles.Italic, TextAlignmentOptions.Center);
            SetFixed(_feedback.rectTransform, new Vector2(900f, 35f), new Vector2(0f, -316f));
            TMP_Text disclaimer = UICanvasUtil.NewBody("Disclaimer", detail,
                Localization.Get("apothecary.case.disclaimer"), 20f, Muted,
                FontStyles.Italic, TextAlignmentOptions.Center);
            SetFixed(disclaimer.rectTransform, new Vector2(900f, 24f), new Vector2(0f, -346f));
        }

        private Button MakeTextButton(string name, Transform parent, Vector2 size, Vector2 position,
            string text, Color background, Color foreground)
        {
            Button button = MakeButton(name, parent, size, position, text, background, foreground);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.fontSize = 18f;
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.margin = new Vector4(18f, 10f, 18f, 10f);
            return button;
        }

        private Button MakeButton(string name, Transform parent, Vector2 size, Vector2 position,
            string text, Color background, Color foreground)
        {
            RectTransform rt = NewFixed(name, parent, size, position);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = UICanvasUtil.RoundedRect(14);
            image.type = Image.Type.Sliced;
            image.color = background;
            TMP_Text label = UICanvasUtil.NewBody("Label", rt, text, 19f, foreground,
                FontStyles.Bold, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(label.rectTransform);
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ConfigureColors(button, background);
            return button;
        }

        private static void ConfigureColors(Button button, Color normal)
        {
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(normal, Color.white, .13f);
            colors.selectedColor = Color.Lerp(normal, Gold, .20f);
            colors.pressedColor = Color.Lerp(normal, Color.black, .10f);
            colors.disabledColor = new Color(.30f, .28f, .24f, .32f);
            button.colors = colors;
        }

        private static RectTransform NewFixed(string name, Transform parent, Vector2 size,
            Vector2 position)
        {
            RectTransform rt = UICanvasUtil.NewRect(name, parent);
            SetFixed(rt, size, position);
            return rt;
        }

        private static void SetFixed(RectTransform rt, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
