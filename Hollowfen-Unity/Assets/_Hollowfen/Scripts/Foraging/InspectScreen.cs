using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hollowfen.Cinematics;
using Hollowfen.Data;
using Hollowfen.Input;
using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.Foraging
{
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(Canvas))]
    public class InspectScreen : MonoBehaviour
    {
        public static InspectScreen Instance { get; private set; }

        private Canvas _canvas;
        private CanvasGroup _group;
        private InputActions _input;
        private bool _inputCallbacksBound;

        private RawImage _previewImage;
        private RectTransform _previewBgRT;
        private TMP_Text _eyebrow;
        private TMP_Text _title;
        private TMP_Text _titleRule;
        private TMP_Text _latin;
        private Image _edibilityDot;
        private TMP_Text _edibilityLabel;
        private RectTransform _edibilityChip;
        private TMP_Text _body;
        private RectTransform _statStrip;
        private TMP_Text _statHabitat;
        private TMP_Text _statSeason;
        private TMP_Text _statLookalikes;
        private TMP_Text _foragerNote;
        private Button _forageBtn;
        private Button _leaveBtn;
        private TMP_Text _forageGlyph;
        private TMP_Text _leaveGlyph;
        private TMP_Text _forageLabel;
        private GameObject _inspectButtonsRow;

        private GameObject _quizPanel;
        private Image _quizJournalPage;
        private RectTransform _quizJournalPageRT;
        private Image _quizPageTurnShadow;
        private TMP_Text _quizReferenceEyebrow;
        private TMP_Text _quizPageCounter;
        private TMP_Text _quizPageHint;
        private Button _quizPreviousPage;
        private Button _quizViewFullPage;
        private Button _quizNextPage;
        private GameObject _quizSilhouetteFrame;
        private RawImage _quizSilhouettePreview;
        private Button _quizInspectSpecimen;
        private TMP_Text _quizProgress;
        private TMP_Text _quizEyebrow;
        private TMP_Text _quizQuestion;
        private TMP_Text _quizFeedback;
        private GameObject _quizFeedbackCard;
        private readonly Button[] _quizAnswers = new Button[3];
        private readonly TMP_Text[] _quizAnswerLabels = new TMP_Text[3];
        private readonly TMP_Text[] _quizAnswerMarkers = new TMP_Text[3];
        private readonly Image[] _quizAnswerOutlines = new Image[3];
        private readonly bool[] _quizAnswerWrong = new bool[3];
        private Button _quizBack;
        private TMP_Text _quizBackLabel;
        private GameObject _quizSuccessRoot;
        private CanvasGroup _quizSuccessGroup;
        private RectTransform _quizSuccessSeal;
        private Image _quizSuccessSealImage;
        private RectTransform _quizSuccessCheck;
        private Image _quizSuccessPulse;
        private RawImage _quizSuccessModelPreview;
        private CanvasGroup _quizSuccessModelGroup;
        private TMP_Text _quizSuccessTitle;
        private TMP_Text _quizSuccessBody;
        private TMP_Text _quizSuccessRarity;
        private TMP_Text _quizSuccessProgress;
        private GameObject _quizInkMarksRoot;
        private CanvasGroup _quizInkMarksGroup;
        private readonly RectTransform[] _quizInkMarks = new RectTransform[3];
        private GameObject _quizDiscoveryAnnotationRoot;
        private CanvasGroup _quizDiscoveryAnnotationGroup;
        private TMP_Text _quizDiscoveryAnnotationName;
        private TMP_Text _quizDiscoveryAnnotationMeta;
        private Coroutine _quizSuccessRoutine;
        private Coroutine _quizPageTurnRoutine;
        private Coroutine _quizHapticRoutine;
        private int _quizStage;
        private bool _quizComplete;
        private string _quizCarryFeedback;
        private MushroomFieldGuideData[] _quizCandidatePages = Array.Empty<MushroomFieldGuideData>();
        private int _quizCandidateIndex;
        private bool _quizBrowsingCandidates;
        private bool _quizStudyIntro;

        private GameObject _quizFullPageOverlay;
        private Image _quizFullPageArt;
        private RectTransform _quizFullPageArtRT;
        private Image _quizFullPageTurnShadow;
        private TMP_Text _quizFullPageCounter;
        private Button _quizFullPagePrevious;
        private Button _quizFullPageClose;
        private Button _quizFullPageNext;
        private GameObject _quizFocusBeforeFullPage;
        private bool _quizPageTurning;

        private GameObject _quizSpecimenOverlay;
        private RectTransform _quizSpecimenPreviewRT;
        private RawImage _quizSpecimenPreview;
        private Button _quizSpecimenClose;
        private GameObject _quizFocusBeforeSpecimen;
        private Gamepad _quizHapticPad;

        [Header("Inspect art")]
        [SerializeField, Tooltip("Optional parchment texture used as the panel background. Falls back to a procedural cream when null.")]
        private Sprite _parchmentSprite;

        [Header("Inspect controls")]
        [SerializeField, Tooltip("Mouse-drag rotation: degrees per pixel.")]
        private float _mouseRotateSpeed = 0.35f;
        [SerializeField, Tooltip("Mouse wheel zoom: ortho-size delta per scroll tick.")]
        private float _mouseZoomSpeed = 0.0008f;
        [SerializeField, Tooltip("Gamepad right-stick rotation: degrees per second at full deflection.")]
        private float _gamepadRotateSpeed = 140f;
        [SerializeField, Tooltip("Gamepad trigger zoom: ortho-size delta per second at full trigger.")]
        private float _gamepadZoomSpeed = 0.18f;
        [SerializeField, Tooltip("Mouse middle-drag pan: world meters per pixel.")]
        private float _mousePanSpeed = 0.0006f;
        [SerializeField, Tooltip("Gamepad left-stick pan: world meters per second at full deflection.")]
        private float _gamepadPanSpeed = 0.20f;

        private MushroomNode _currentNode;
        private NarrativePresentationSession.Lease _presentationLease;
        private bool _built;
        private GameObject _lastSelectedButton;

        public bool IsOpen => _canvas != null && _canvas.enabled;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _canvas = GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 60;
            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.Init1080();
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            EnsureInputActions();
            _canvas.enabled = false;
        }

        private void OnDestroy()
        {
            StopQuizHaptics();
            ReleasePresentation();
            if (Instance == this) Instance = null;
            UnbindInputCallbacks();
            _input?.Dispose();
            _input = null;
        }

        private void OnEnable()
        {
            // Unity preserves runtime-built scene objects through an in-Play assembly reload, but
            // static singletons, generated InputActions, and non-persistent UnityEvent delegates
            // are rebuilt. Recover the still-visible journal instead of leaving buttons that look
            // interactable but have no click callbacks.
            bool recoveringFromAssemblyReload = _input == null;
            if (Instance == null) Instance = this;
            else if (Instance != this) return;
            EnsureInputActions();
            BindInputCallbacks();
            if (recoveringFromAssemblyReload && _built)
                RecoverRuntimeBindingsAfterAssemblyReload();
        }

        private void OnDisable()
        {
            UnbindInputCallbacks();
        }

        private void EnsureInputActions()
        {
            if (_input == null) _input = new InputActions();
        }

        private void BindInputCallbacks()
        {
            if (_input == null || _inputCallbacksBound) return;
            _input.UI.Enable();
            _input.UI.Cancel.performed += OnCancel;
            _input.UI.Submit.performed += OnSubmit;
            // Player/Interact (Triangle / E) doubles as Forage shortcut while the screen is open.
            _input.Player.Enable();
            _input.Player.Interact.performed += OnPlayerInteractShortcut;
            _inputCallbacksBound = true;
        }

        private void UnbindInputCallbacks()
        {
            if (_input == null || !_inputCallbacksBound) return;
            _input.UI.Cancel.performed -= OnCancel;
            _input.UI.Submit.performed -= OnSubmit;
            _input.Player.Interact.performed -= OnPlayerInteractShortcut;
            _input.UI.Disable();
            _input.Player.Disable();
            _inputCallbacksBound = false;
        }

        private void RecoverRuntimeBindingsAfterAssemblyReload()
        {
            BindPersistentButtonCallbacks();
            StopQuizPageTurn();

            if (_quizPanel != null && _quizPanel.activeSelf && !_quizComplete)
            {
                if (_quizStudyIntro) ConfigureStudyIntro();
                else ConfigureQuizStage();
            }

            if (IsOpen && _presentationLease == null)
                _presentationLease = NarrativePresentationSession.Acquire(
                    this, NarrativePresentationSession.Modal);

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                GameObject focus = _quizPanel != null && _quizPanel.activeSelf
                    ? (_quizComplete ? _quizBack?.gameObject
                        : (_quizAnswers[0] != null && _quizAnswers[0].gameObject.activeInHierarchy
                            ? _quizAnswers[0].gameObject
                            : _quizViewFullPage?.gameObject))
                    : (_forageBtn != null && _forageBtn.interactable
                        ? _forageBtn.gameObject
                        : _leaveBtn?.gameObject);
                if (focus != null) EventSystem.current.SetSelectedGameObject(focus);
            }
        }

        private void BindPersistentButtonCallbacks()
        {
            if (_forageBtn != null)
            {
                _forageBtn.onClick.RemoveAllListeners();
                _forageBtn.onClick.AddListener(OnForageClicked);
            }
            if (_leaveBtn != null)
            {
                _leaveBtn.onClick.RemoveAllListeners();
                _leaveBtn.onClick.AddListener(OnLeaveClicked);
            }
            if (_quizPreviousPage != null)
            {
                _quizPreviousPage.onClick.RemoveAllListeners();
                _quizPreviousPage.onClick.AddListener(() => ChangeQuizCandidatePage(-1));
            }
            if (_quizViewFullPage != null)
            {
                _quizViewFullPage.onClick.RemoveAllListeners();
                _quizViewFullPage.onClick.AddListener(OpenQuizFullPage);
            }
            if (_quizNextPage != null)
            {
                _quizNextPage.onClick.RemoveAllListeners();
                _quizNextPage.onClick.AddListener(() => ChangeQuizCandidatePage(1));
            }
            if (_quizInspectSpecimen != null)
            {
                _quizInspectSpecimen.onClick.RemoveAllListeners();
                _quizInspectSpecimen.onClick.AddListener(OpenQuizSpecimen);
            }
            if (_quizBack != null)
            {
                _quizBack.onClick.RemoveAllListeners();
                _quizBack.onClick.AddListener(OnQuizFooterClicked);
            }
            if (_quizSpecimenClose != null)
            {
                _quizSpecimenClose.onClick.RemoveAllListeners();
                _quizSpecimenClose.onClick.AddListener(() => CloseQuizSpecimen());
            }
            if (_quizFullPagePrevious != null)
            {
                _quizFullPagePrevious.onClick.RemoveAllListeners();
                _quizFullPagePrevious.onClick.AddListener(() => ChangeQuizCandidatePage(-1));
            }
            if (_quizFullPageClose != null)
            {
                _quizFullPageClose.onClick.RemoveAllListeners();
                _quizFullPageClose.onClick.AddListener(() => CloseQuizFullPage());
            }
            if (_quizFullPageNext != null)
            {
                _quizFullPageNext.onClick.RemoveAllListeners();
                _quizFullPageNext.onClick.AddListener(() => ChangeQuizCandidatePage(1));
            }
        }

        // Static entry point used by MushroomNode.Interact.
        public static void Open(MushroomNode node)
        {
            if (Instance == null || node == null || node.Data == null) return;
            Instance.ShowInternal(node);
        }

        private void ShowInternal(MushroomNode node)
        {
            if (IsOpen) return;
            BuildIfNeeded();
            EnsureEventSystem();
            _currentNode = node;
            var data = node.Data;
            bool fieldIdentified = MushroomKnowledge.IsFieldIdentified(data);
            ApplyContent(data, fieldIdentified);

            if (MushroomPreviewer.Instance != null)
            {
                MushroomPreviewer.Instance.Show(data, !fieldIdentified);
                // Show() creates the shared preview rig on first use, so bind after it has had a
                // chance to allocate its RenderTexture.
                if (_previewImage != null)
                    _previewImage.texture = MushroomPreviewer.Instance.RenderTexture;
                if (_quizSilhouettePreview != null)
                    _quizSilhouettePreview.texture = MushroomPreviewer.Instance.RenderTexture;
                if (_quizSpecimenPreview != null)
                    _quizSpecimenPreview.texture = MushroomPreviewer.Instance.RenderTexture;
                if (_quizSuccessModelPreview != null)
                    _quizSuccessModelPreview.texture = MushroomPreviewer.Instance.RenderTexture;
            }

            _canvas.enabled = true;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;

            _presentationLease = NarrativePresentationSession.Acquire(
                this, NarrativePresentationSession.Modal);

            if (EventSystem.current != null && _forageBtn != null)
            {
                var initial = _forageBtn.interactable ? _forageBtn.gameObject : _leaveBtn.gameObject;
                EventSystem.current.SetSelectedGameObject(initial);
                _lastSelectedButton = initial;
            }
        }

        // If we entered Scene_Hollowfen directly (no MainMenu boot flow), UIManager's DDOL'd EventSystem
        // is missing. Create one on the fly so buttons + gamepad nav work.
        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                new GameObject("EventSystem", typeof(EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }
            // Don't deselect the focused button when the mouse moves off it.
            var module = EventSystem.current != null
                ? EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>()
                : null;
            if (module != null) module.deselectOnBackgroundClick = false;
        }

        private void Hide()
        {
            StopQuizSuccessAnimation();
            StopQuizPageTurn();
            StopQuizHaptics();
            CloseQuizSpecimen(false);
            CloseQuizFullPage(false);
            if (_quizPanel != null) _quizPanel.SetActive(false);
            if (_inspectButtonsRow != null) _inspectButtonsRow.SetActive(true);
            _quizComplete = false;
            _canvas.enabled = false;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            if (MushroomPreviewer.Instance != null) MushroomPreviewer.Instance.Clear();

            ReleasePresentation();

            _currentNode = null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void ReleasePresentation()
        {
            _presentationLease?.Dispose();
            _presentationLease = null;
        }

        private void OnForageClicked()
        {
            var node = _currentNode;
            if (node == null)
            {
                UISfx.Error();
                return;
            }
            if (!MushroomKnowledge.IsFieldIdentified(node.Data))
            {
                if (MushroomKnowledge.CanCompare(node.Data)) BeginJournalComparison();
                else if (MushroomKnowledge.CanReadPage(node.Data)) BeginJournalStudy();
                else UISfx.Error();
                return;
            }
            if (!MushroomRules.CanHarvest(node.Data))
            {
                UISfx.Error();
                return;
            }
            // The challenge acquires its lease synchronously before Inspect releases its own,
            // so there is no frame in which gameplay input or time briefly resumes.
            if (!node.BeginHarvest())
            {
                UISfx.Error();
                return;
            }
            Hide();
        }

        private void OnLeaveClicked()
        {
            Hide();
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (!IsOpen) return;
            if (_quizSpecimenOverlay != null && _quizSpecimenOverlay.activeSelf) CloseQuizSpecimen();
            else if (_quizFullPageOverlay != null && _quizFullPageOverlay.activeSelf) CloseQuizFullPage();
            else if (_quizPanel != null && _quizPanel.activeSelf) CloseJournalComparison();
            else Hide();
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Hot-swap button glyphs to whatever pad the player is using right now.
            RefreshButtonGlyphs();

            if (_quizPanel != null && _quizPanel.activeSelf)
            {
                RefreshQuizAnswerFocusVisuals();
                if (_quizSpecimenOverlay != null && _quizSpecimenOverlay.activeSelf)
                    UpdateQuizSpecimenControls();
                return;
            }

            // Keep gamepad navigation alive: if the EventSystem deselected (e.g., mouse moved off a button),
            // restore the last button we focused so left/right still toggles Forage <-> Leave.
            var es = EventSystem.current;
            if (es != null && _forageBtn != null && _leaveBtn != null)
            {
                var sel = es.currentSelectedGameObject;
                if (sel == _forageBtn.gameObject || sel == _leaveBtn.gameObject)
                    _lastSelectedButton = sel;
                else if (sel == null)
                    es.SetSelectedGameObject(_lastSelectedButton != null ? _lastSelectedButton : _forageBtn.gameObject);
            }

            var prev = MushroomPreviewer.Instance;
            if (prev == null) return;
            float dt = Time.unscaledDeltaTime;

            // Gamepad: right stick rotate, left stick pan, triggers zoom
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null)
            {
                Vector2 rs = pad.rightStick.ReadValue();
                if (rs.sqrMagnitude > 0.0025f)
                {
                    prev.ApplyRotationDelta(
                        rs.x * _gamepadRotateSpeed * dt,
                       -rs.y * _gamepadRotateSpeed * dt);
                }
                Vector2 ls = pad.leftStick.ReadValue();
                if (ls.sqrMagnitude > 0.0025f)
                {
                    prev.ApplyPanDelta(new Vector2(
                        ls.x * _gamepadPanSpeed * dt,
                        ls.y * _gamepadPanSpeed * dt));
                }
                float zoomGp = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                if (Mathf.Abs(zoomGp) > 0.05f)
                {
                    prev.ApplyZoomDelta(-zoomGp * _gamepadZoomSpeed * dt);
                }
            }

            // Mouse: left-drag rotate (in preview rect), middle-drag pan (in preview rect),
            // scroll wheel to zoom (anywhere over the screen).
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                bool inRect = _previewBgRT != null
                    && RectTransformUtility.RectangleContainsScreenPoint(_previewBgRT, mouse.position.ReadValue(), null);
                if (mouse.leftButton.isPressed && inRect)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    if (d.sqrMagnitude > 0.01f)
                    {
                        prev.ApplyRotationDelta(
                            d.x * _mouseRotateSpeed,
                           -d.y * _mouseRotateSpeed);
                    }
                }
                if (mouse.middleButton.isPressed && inRect)
                {
                    Vector2 d = mouse.delta.ReadValue();
                    if (d.sqrMagnitude > 0.01f)
                    {
                        prev.ApplyPanDelta(new Vector2(
                            d.x * _mousePanSpeed,
                            d.y * _mousePanSpeed));
                    }
                }
                Vector2 scroll = mouse.scroll.ReadValue();
                if (Mathf.Abs(scroll.y) > 0.01f)
                {
                    prev.ApplyZoomDelta(-scroll.y * _mouseZoomSpeed);
                }
            }
        }

        private void OnSubmit(InputAction.CallbackContext _)
        {
            if (!IsOpen) return;
            if (_quizPanel != null && _quizPanel.activeSelf) return;
            // If a button is focused, route through it (so users see the press feedback);
            // otherwise default to Forage.
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel != null && sel == (_leaveBtn != null ? _leaveBtn.gameObject : null)) { OnLeaveClicked(); return; }
            OnForageClicked();
        }

        private void OnPlayerInteractShortcut(InputAction.CallbackContext _)
        {
            if (!IsOpen) return;
            if (_quizPanel != null && _quizPanel.activeSelf) return;
            OnForageClicked();
        }

        private void ApplyContent(MushroomFieldGuideData data, bool identityKnown)
        {
            bool harvestUnlocked = MushroomRules.CanHarvest(data);
            bool fieldIdentified = MushroomKnowledge.IsFieldIdentified(data);
            if (identityKnown)
            {
                string edibility = JournalText.MushroomEdibility(data);
                string latin = JournalText.MushroomLatin(data);
                string habitat = JournalText.MushroomHabitat(data);
                string season = JournalText.MushroomSeason(data);
                string lookalikes = JournalText.MushroomLookalikes(data);
                string notes = JournalText.MushroomNotes(data);
                _eyebrow.text = (string.IsNullOrEmpty(edibility) ? data.Edibility.ToString() : edibility).ToUpperInvariant();
                _title.text = JournalText.MushroomName(data);
                _latin.text = latin;
                _latin.gameObject.SetActive(!string.IsNullOrEmpty(latin));

                var ediColor = HollowfenPalette.Edibility(data.Edibility);
                _edibilityDot.color = ediColor;
                _edibilityLabel.color = HollowfenPalette.InkDeep;
                _edibilityLabel.text = Hollowfen.Localization.Get(
                    "inspect.edibility." + data.Edibility.ToString().ToLowerInvariant(), data.Edibility.ToString()).ToUpperInvariant();
                if (_edibilityChip != null) _edibilityChip.gameObject.SetActive(true);

                _body.text = JournalText.MushroomDescription(data);

                if (_statStrip != null)
                {
                    bool any = !string.IsNullOrEmpty(habitat)
                            || !string.IsNullOrEmpty(season)
                            || !string.IsNullOrEmpty(lookalikes);
                    _statStrip.gameObject.SetActive(any);
                    SetStat(_statHabitat, Hollowfen.Localization.Get("journal.field.habitat"), habitat);
                    SetStat(_statSeason, Hollowfen.Localization.Get("journal.field.season"), season);
                    SetStat(_statLookalikes, Hollowfen.Localization.Get("journal.field.lookalikes"), lookalikes);
                }

                if (_foragerNote != null)
                {
                    bool hasNote = !string.IsNullOrEmpty(notes);
                    _foragerNote.gameObject.SetActive(hasNote);
                    if (hasNote) _foragerNote.text = string.Format(Hollowfen.Localization.Get("format.quote"), notes);
                }
            }
            else
            {
                _eyebrow.text = Hollowfen.Localization.Get("inspect.unknown.eyebrow");
                _title.text = Hollowfen.Localization.Get("inspect.unknown.title");
                _latin.gameObject.SetActive(false);
                if (_edibilityChip != null) _edibilityChip.gameObject.SetActive(false);
                _body.text = Hollowfen.Localization.Get("inspect.unknown.body");
                if (_statStrip != null) _statStrip.gameObject.SetActive(false);
                if (_foragerNote != null) _foragerNote.gameObject.SetActive(false);
            }

            bool canCompare = !fieldIdentified && MushroomKnowledge.CanCompare(data);
            bool canStudy = !fieldIdentified && MushroomKnowledge.CanReadPage(data) &&
                            !MushroomKnowledge.HasStudied(data);
            if (_forageBtn != null) _forageBtn.interactable = fieldIdentified
                ? harvestUnlocked
                : canCompare || canStudy;
            if (_forageLabel != null)
                _forageLabel.text = Hollowfen.Localization.Get(fieldIdentified
                    ? "inspect.btn.forage"
                    : (canStudy ? "inspect.btn.study_page" : "inspect.btn.compare"));
            if (!fieldIdentified && _foragerNote != null)
            {
                _foragerNote.gameObject.SetActive(true);
                _foragerNote.text = Hollowfen.Localization.Get(!MushroomKnowledge.HasJournal
                    ? "inspect.locked.no_journal"
                    : (canStudy ? "inspect.ready.study_page"
                        : (canCompare ? "inspect.ready.compare" : "inspect.locked.knowledge")));
            }
            else if (!harvestUnlocked && _foragerNote != null)
            {
                _foragerNote.gameObject.SetActive(true);
                _foragerNote.text = Hollowfen.Localization.Get("inspect.locked.knowledge");
            }
        }

        private void BeginJournalStudy()
        {
            MushroomFieldGuideData species = _currentNode != null ? _currentNode.Data : null;
            if (species == null || !MushroomKnowledge.CanReadPage(species) || _quizPanel == null)
            {
                UISfx.Error();
                return;
            }

            // Keep first discovery in one uninterrupted field flow. The full journal remains
            // available from the pause menu, but studying a live specimen should not make the
            // player close a book, walk back to the mushroom, and interact a second time.
            MushroomKnowledge.StudyPage(species);
            UISfx.Confirm();
            _quizStage = 0;
            _quizComplete = false;
            _quizCarryFeedback = null;
            PrepareQuizCandidates(species);
            _quizPanel.SetActive(true);
            if (_inspectButtonsRow != null) _inspectButtonsRow.SetActive(false);
            ConfigureStudyIntro();
        }

        private void BeginJournalComparison()
        {
            if (_currentNode == null || !MushroomKnowledge.CanCompare(_currentNode.Data) ||
                _quizPanel == null)
            {
                UISfx.Error();
                return;
            }
            _quizStage = 0;
            _quizComplete = false;
            _quizCarryFeedback = null;
            PrepareQuizCandidates(_currentNode.Data);
            _quizPanel.SetActive(true);
            if (_inspectButtonsRow != null) _inspectButtonsRow.SetActive(false);
            ConfigureQuizStage();
        }

        private void CloseJournalComparison()
        {
            StopQuizSuccessAnimation();
            StopQuizPageTurn();
            StopQuizHaptics();
            CloseQuizSpecimen(false);
            CloseQuizFullPage(false);
            if (_quizPanel != null) _quizPanel.SetActive(false);
            if (_inspectButtonsRow != null) _inspectButtonsRow.SetActive(true);
            _quizComplete = false;
            _quizCarryFeedback = null;
            _quizStudyIntro = false;
            _quizCandidatePages = Array.Empty<MushroomFieldGuideData>();
            if (_currentNode != null && _currentNode.Data != null)
                ApplyContent(_currentNode.Data, MushroomKnowledge.IsFieldIdentified(_currentNode.Data));
            if (EventSystem.current != null && _forageBtn != null)
            {
                EventSystem.current.SetSelectedGameObject(_forageBtn.gameObject);
                _lastSelectedButton = _forageBtn.gameObject;
            }
        }

        private void ConfigureStudyIntro()
        {
            MushroomFieldGuideData species = _currentNode != null ? _currentNode.Data : null;
            if (species == null) return;
            ResetQuizSuccessPresentation();
            _quizStudyIntro = true;
            SetQuizReferenceMode(true);

            _quizProgress.text = ProgressText(-1);
            _quizEyebrow.text = Hollowfen.Localization.Get("inspect.study.eyebrow");
            _quizQuestion.text = Hollowfen.Localization.Get("inspect.study.title");
            _quizFeedback.text = Hollowfen.Localization.Get("inspect.study.body");
            _quizBackLabel.text = Hollowfen.Localization.Get("inspect.quiz.back");
            _quizBack.gameObject.SetActive(true);

            for (int i = 0; i < _quizAnswers.Length; i++)
                _quizAnswers[i].gameObject.SetActive(i == 0);
            ResetQuizAnswerVisuals();
            PositionSingleQuizAnswer();
            _quizAnswerLabels[0].text = Hollowfen.Localization.Get("inspect.study.begin");
            _quizAnswers[0].onClick.RemoveAllListeners();
            _quizAnswers[0].onClick.AddListener(BeginIdentificationAfterStudy);
            WireQuizNavigation();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_quizAnswers[0].gameObject);
        }

        private void BeginIdentificationAfterStudy()
        {
            _quizStage = 0;
            _quizCarryFeedback = Hollowfen.Localization.Get("inspect.study.ready");
            UISfx.Confirm();
            ConfigureQuizStage();
        }

        private void ConfigureQuizStage()
        {
            MushroomFieldGuideData species = _currentNode != null ? _currentNode.Data : null;
            if (species == null) return;
            ResetQuizSuccessPresentation();
            _quizStudyIntro = false;
            _quizProgress.text = ProgressText(_quizStage);
            _quizEyebrow.text = string.Format(
                Hollowfen.Localization.Get("inspect.quiz.eyebrow"), _quizStage + 1);
            _quizFeedback.text = !string.IsNullOrEmpty(_quizCarryFeedback)
                ? _quizCarryFeedback
                : QuizHint(_quizStage);
            _quizCarryFeedback = null;
            _quizBackLabel.text = Hollowfen.Localization.Get("inspect.quiz.back");
            _quizBack.gameObject.SetActive(true);

            if (_quizStage == 0)
            {
                _quizQuestion.text = Hollowfen.Localization.Get("inspect.quiz.name");
                SetQuizReferenceMode(true);
                ResetQuizAnswerVisuals();
                for (int i = 0; i < _quizAnswers.Length; i++)
                    _quizAnswers[i].gameObject.SetActive(i == 0);
                PositionSingleQuizAnswer();
                _quizAnswerLabels[0].text = Hollowfen.Localization.Get("inspect.quiz.choose_page");
                _quizAnswers[0].onClick.RemoveAllListeners();
                _quizAnswers[0].onClick.AddListener(ChooseCurrentJournalPage);
                WireQuizNavigation();
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(_quizAnswers[0].gameObject);
                RefreshQuizAnswerFocusVisuals();
                return;
            }

            SetQuizReferenceMode(false);
            ShowTargetReferencePage(species);
            string correct;
            var wrong = new List<string>();
            if (_quizStage == 1)
            {
                _quizQuestion.text = Hollowfen.Localization.Get("inspect.quiz.feature");
                correct = FirstFeature(species);
                foreach (MushroomFieldGuideData other in QuizDistractors(species))
                    wrong.Add(FirstFeature(other));
            }
            else
            {
                _quizQuestion.text = Hollowfen.Localization.Get("inspect.quiz.safety");
                correct = Hollowfen.Localization.Get("inspect.quiz.safe");
                wrong.Add(Hollowfen.Localization.Get("inspect.quiz.color"));
                wrong.Add(Hollowfen.Localization.Get("inspect.quiz.taste"));
            }

            while (wrong.Count < 2) wrong.Add(Hollowfen.Localization.Get("inspect.quiz.color"));
            int correctIndex = PositiveModulo(StableHash(species.Id) + _quizStage, 3);
            int wrongIndex = 0;
            ResetQuizAnswerVisuals();
            for (int i = 0; i < _quizAnswers.Length; i++)
            {
                _quizAnswers[i].gameObject.SetActive(true);
                PositionQuizAnswer(i);
                bool isCorrect = i == correctIndex;
                _quizAnswerLabels[i].text = isCorrect ? correct : wrong[wrongIndex++];
                _quizAnswers[i].onClick.RemoveAllListeners();
                int answerIndex = i;
                _quizAnswers[i].onClick.AddListener(() => HandleQuizAnswer(answerIndex, isCorrect));
            }
            WireQuizNavigation();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_quizAnswers[0].gameObject);
            RefreshQuizAnswerFocusVisuals();
        }

        private void ChooseCurrentJournalPage()
        {
            MushroomFieldGuideData target = _currentNode != null ? _currentNode.Data : null;
            MushroomFieldGuideData candidate = CurrentCandidatePage;
            HandleQuizAnswer(0, target != null && candidate == target);
        }

        private void HandleQuizAnswer(int answerIndex, bool correct)
        {
            if (!correct)
            {
                UISfx.Error();
                if (_quizStage == 0 && CurrentCandidatePage != null)
                {
                    string observation = ConciseObservation(FirstFeature(CurrentCandidatePage));
                    _quizFeedback.text = string.Format(Hollowfen.Localization.Get(
                        "inspect.quiz.wrong.page_detail"), observation);
                }
                else _quizFeedback.text = WrongFeedback(_quizStage);
                MarkQuizAnswerWrong(answerIndex);
                return;
            }

            UISfx.Confirm();
            if (_quizStage < 2)
            {
                _quizCarryFeedback = Hollowfen.Localization.Get(_quizStage == 0
                    ? "inspect.quiz.correct.name"
                    : "inspect.quiz.correct.feature");
                _quizStage++;
                ConfigureQuizStage();
                return;
            }

            MushroomFieldGuideData species = _currentNode != null ? _currentNode.Data : null;
            if (species == null || _currentNode == null ||
                !MushroomKnowledge.RecordIdentification(species, _currentNode.transform.position))
            {
                UISfx.Error();
                return;
            }
            _quizComplete = true;
            SetQuizReferenceMode(false);
            ShowTargetReferencePage(species);
            if (_quizPageHint != null)
                _quizPageHint.text = Hollowfen.Localization.Get("inspect.browser.verified");
            _quizProgress.text = ProgressText(3);
            _quizEyebrow.gameObject.SetActive(false);
            _quizQuestion.gameObject.SetActive(false);
            if (_quizFeedbackCard != null) _quizFeedbackCard.SetActive(false);
            foreach (Button answer in _quizAnswers) answer.gameObject.SetActive(false);
            ConfigureDiscoveryReveal(species);
            _quizSuccessRoot.SetActive(true);
            _quizBackLabel.text = Hollowfen.Localization.Get("inspect.quiz.prepare_cut");
            _quizBack.interactable = false;
            ApplyContent(species, true);
            WireQuizNavigation();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _quizSuccessRoutine = StartCoroutine(AnimateQuizSuccess());
        }

        private void OnQuizFooterClicked()
        {
            bool continueToCut = _quizComplete;
            CloseJournalComparison();
            if (continueToCut) OnForageClicked();
        }

        private MushroomFieldGuideData CurrentCandidatePage =>
            _quizCandidatePages != null && _quizCandidatePages.Length > 0
                ? _quizCandidatePages[Mathf.Clamp(_quizCandidateIndex, 0, _quizCandidatePages.Length - 1)]
                : null;

        private void PrepareQuizCandidates(MushroomFieldGuideData target)
        {
            if (target == null)
            {
                _quizCandidatePages = Array.Empty<MushroomFieldGuideData>();
                _quizCandidateIndex = 0;
                return;
            }

            var pages = QuizDistractors(target)
                .Concat(new[] { target })
                .Where(entry => entry != null && entry.JournalPage != null)
                .Distinct()
                .OrderBy(entry => StableHash(target.Id + ".browser." + entry.Id))
                .ToArray();
            if (pages.Length == 0) pages = new[] { target };
            _quizCandidatePages = pages;

            // A plausible non-target page opens first whenever one is available. The correct
            // spread remains somewhere in the book instead of arriving pre-pinned as the answer.
            int firstDistractor = Array.FindIndex(pages, entry => entry != target);
            _quizCandidateIndex = firstDistractor >= 0 ? firstDistractor : 0;
            ApplyCurrentCandidatePage();
        }

        private void SetQuizReferenceMode(bool browsing)
        {
            _quizBrowsingCandidates = browsing && _quizCandidatePages.Length > 1;
            if (_quizPreviousPage != null) _quizPreviousPage.gameObject.SetActive(_quizBrowsingCandidates);
            if (_quizNextPage != null) _quizNextPage.gameObject.SetActive(_quizBrowsingCandidates);
            if (_quizViewFullPage != null)
                _quizViewFullPage.gameObject.SetActive(CurrentCandidatePage != null &&
                    CurrentCandidatePage.JournalPage != null);
            if (_quizSilhouetteFrame != null)
                _quizSilhouetteFrame.SetActive(browsing);
            if (_quizPageHint != null)
                _quizPageHint.text = Hollowfen.Localization.Get(browsing
                    ? "inspect.browser.hint"
                    : "inspect.browser.matched");
            ApplyCurrentCandidatePage();
        }

        private void ShowTargetReferencePage(MushroomFieldGuideData target)
        {
            if (target == null) return;
            int index = Array.IndexOf(_quizCandidatePages, target);
            if (index < 0)
            {
                _quizCandidatePages = new[] { target };
                index = 0;
            }
            _quizCandidateIndex = index;
            ApplyCurrentCandidatePage();
        }

        private void ChangeQuizCandidatePage(int delta)
        {
            if (!_quizBrowsingCandidates || _quizCandidatePages.Length < 2 || _quizPageTurning) return;
            _quizPageTurnRoutine = StartCoroutine(TurnQuizPage(delta < 0 ? -1 : 1));
        }

        private IEnumerator TurnQuizPage(int direction)
        {
            _quizPageTurning = true;
            SetPageTurnControlsInteractable(false);
            bool pageChanged = false;
            try
            {
                UISfx.PageTurn();
                PulseQuizHaptics(0.035f, 0.055f, 0.09f);

                const float foldSeconds = 0.17f;
                float elapsed = 0f;
                while (elapsed < foldSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / foldSeconds);
                    float smooth = t * t * (3f - 2f * t);
                    ApplyPageFold(Mathf.Lerp(1f, 0.045f, smooth),
                        Mathf.Sin(t * Mathf.PI) * 0.72f, direction);
                    yield return null;
                }

                _quizCandidateIndex = PositiveModulo(_quizCandidateIndex + direction,
                    _quizCandidatePages.Length);
                ApplyCurrentCandidatePage();
                pageChanged = true;

                elapsed = 0f;
                const float unfoldSeconds = 0.19f;
                while (elapsed < unfoldSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / unfoldSeconds);
                    float smooth = t * t * (3f - 2f * t);
                    ApplyPageFold(Mathf.Lerp(0.045f, 1f, smooth),
                        Mathf.Sin((1f - t) * Mathf.PI) * 0.72f, direction);
                    yield return null;
                }
            }
            finally
            {
                // An editor assembly reload, screen close, or unexpected presentation exception
                // must never strand the journal in its disabled/folded transition state.
                ApplyPageFold(1f, 0f, direction);
                _quizPageTurning = false;
                _quizPageTurnRoutine = null;
                SetPageTurnControlsInteractable(true);
            }

            if (pageChanged && !_quizStudyIntro && _quizStage == 0)
            {
                ResetQuizAnswerVisuals();
                _quizFeedback.text = QuizHint(0);
            }
        }

        private void ApplyPageFold(float horizontalScale, float shadowAlpha, int direction)
        {
            Vector3 scale = new Vector3(horizontalScale, 1f, 1f);
            if (_quizJournalPageRT != null) _quizJournalPageRT.localScale = scale;
            if (_quizFullPageArtRT != null) _quizFullPageArtRT.localScale = scale;
            SetPageTurnShadow(_quizPageTurnShadow, shadowAlpha, direction);
            SetPageTurnShadow(_quizFullPageTurnShadow, shadowAlpha, direction);
        }

        private static void SetPageTurnShadow(Image shadow, float alpha, int direction)
        {
            if (shadow == null) return;
            Color color = shadow.color;
            color.a = alpha;
            shadow.color = color;
            RectTransform rt = (RectTransform)shadow.transform;
            rt.localScale = new Vector3(direction < 0 ? -1f : 1f, 1f, 1f);
        }

        private void SetPageTurnControlsInteractable(bool interactable)
        {
            if (_quizPreviousPage != null) _quizPreviousPage.interactable = interactable;
            if (_quizNextPage != null) _quizNextPage.interactable = interactable;
            if (_quizFullPagePrevious != null) _quizFullPagePrevious.interactable = interactable;
            if (_quizFullPageNext != null) _quizFullPageNext.interactable = interactable;
        }

        private void StopQuizPageTurn()
        {
            if (_quizPageTurnRoutine != null)
            {
                StopCoroutine(_quizPageTurnRoutine);
                _quizPageTurnRoutine = null;
            }
            _quizPageTurning = false;
            ApplyPageFold(1f, 0f, 1);
            SetPageTurnControlsInteractable(true);
        }

        private void ApplyCurrentCandidatePage()
        {
            MushroomFieldGuideData page = CurrentCandidatePage;
            Sprite art = page != null ? page.JournalPage : null;
            if (_quizJournalPage != null)
            {
                _quizJournalPage.sprite = art;
                _quizJournalPage.enabled = art != null;
            }
            string counter = string.Format(Hollowfen.Localization.Get("inspect.browser.page"),
                _quizCandidatePages.Length > 0 ? _quizCandidateIndex + 1 : 0,
                _quizCandidatePages.Length);
            if (_quizPageCounter != null) _quizPageCounter.text = counter;
            if (_quizFullPageCounter != null) _quizFullPageCounter.text = counter;
            if (_quizFullPageArt != null)
            {
                _quizFullPageArt.sprite = art;
                _quizFullPageArt.enabled = art != null;
            }
        }

        private void OpenQuizFullPage()
        {
            if (_quizPageTurning || _quizFullPageOverlay == null || CurrentCandidatePage == null ||
                CurrentCandidatePage.JournalPage == null)
            {
                UISfx.Error();
                return;
            }

            _quizFocusBeforeFullPage = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            ApplyCurrentCandidatePage();
            _quizFullPagePrevious.gameObject.SetActive(_quizBrowsingCandidates);
            _quizFullPageNext.gameObject.SetActive(_quizBrowsingCandidates);
            WireFullPageNavigation();
            _quizFullPageOverlay.SetActive(true);
            _quizFullPageOverlay.transform.SetAsLastSibling();
            UISfx.Confirm();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_quizFullPageClose.gameObject);
        }

        private void CloseQuizFullPage(bool restoreFocus = true)
        {
            if (_quizFullPageOverlay == null || !_quizFullPageOverlay.activeSelf) return;
            _quizFullPageOverlay.SetActive(false);
            if (restoreFocus && EventSystem.current != null)
            {
                GameObject target = _quizFocusBeforeFullPage != null &&
                                    _quizFocusBeforeFullPage.activeInHierarchy
                    ? _quizFocusBeforeFullPage
                    : (_quizViewFullPage != null ? _quizViewFullPage.gameObject : null);
                EventSystem.current.SetSelectedGameObject(target);
            }
            _quizFocusBeforeFullPage = null;
        }

        private void WireFullPageNavigation()
        {
            if (_quizFullPageClose == null) return;
            bool paging = _quizBrowsingCandidates && _quizFullPagePrevious != null &&
                          _quizFullPageNext != null;

            Navigation closeNav = _quizFullPageClose.navigation;
            closeNav.mode = Navigation.Mode.Explicit;
            closeNav.selectOnLeft = paging ? _quizFullPagePrevious : _quizFullPageClose;
            closeNav.selectOnRight = paging ? _quizFullPageNext : _quizFullPageClose;
            closeNav.selectOnUp = closeNav.selectOnDown = _quizFullPageClose;
            _quizFullPageClose.navigation = closeNav;
            if (!paging) return;

            Navigation previousNav = _quizFullPagePrevious.navigation;
            previousNav.mode = Navigation.Mode.Explicit;
            previousNav.selectOnLeft = _quizFullPageNext;
            previousNav.selectOnRight = _quizFullPageClose;
            previousNav.selectOnUp = previousNav.selectOnDown = _quizFullPagePrevious;
            _quizFullPagePrevious.navigation = previousNav;

            Navigation nextNav = _quizFullPageNext.navigation;
            nextNav.mode = Navigation.Mode.Explicit;
            nextNav.selectOnLeft = _quizFullPageClose;
            nextNav.selectOnRight = _quizFullPagePrevious;
            nextNav.selectOnUp = nextNav.selectOnDown = _quizFullPageNext;
            _quizFullPageNext.navigation = nextNav;
        }

        private void PositionSingleQuizAnswer()
        {
            if (_quizAnswers[0] == null) return;
            RectTransform answerRT = (RectTransform)_quizAnswers[0].transform;
            answerRT.sizeDelta = new Vector2(650f, 82f);
            answerRT.anchoredPosition = new Vector2(0f, -130f);
        }

        private void PositionQuizAnswer(int index)
        {
            if (index < 0 || index >= _quizAnswers.Length || _quizAnswers[index] == null) return;
            RectTransform answerRT = (RectTransform)_quizAnswers[index].transform;
            answerRT.sizeDelta = new Vector2(650f, 94f);
            answerRT.anchoredPosition = new Vector2(0f, 86f - index * 116f);
        }

        private static string ProgressText(int current)
        {
            string gold = ColorUtility.ToHtmlStringRGBA(HollowfenPalette.Gold);
            string faint = ColorUtility.ToHtmlStringRGBA(new Color(0.961f, 0.925f, 0.855f, 0.48f));
            string[] labels = { "01  STUDY", "02  MATCH", "03  FEATURE", "04  SAFETY" };
            for (int i = 0; i < labels.Length; i++)
            {
                bool active = current < 0 ? i == 0 : current >= 3 ? true : i == current + 1;
                labels[i] = $"<color=#{(active ? gold : faint)}>{labels[i]}</color>";
            }
            return string.Join("   /   ", labels);
        }

        private static string QuizHint(int stage) => Hollowfen.Localization.Get(stage == 0
            ? "inspect.quiz.hint.name"
            : stage == 1 ? "inspect.quiz.hint.feature" : "inspect.quiz.hint.safety");

        private static string WrongFeedback(int stage) => Hollowfen.Localization.Get(stage == 0
            ? "inspect.quiz.wrong.name"
            : stage == 1 ? "inspect.quiz.wrong.feature" : "inspect.quiz.wrong.safety");

        private void ResetQuizAnswerVisuals()
        {
            for (int i = 0; i < _quizAnswers.Length; i++)
            {
                if (_quizAnswers[i] == null) continue;
                _quizAnswerWrong[i] = false;
                ColorBlock colors = _quizAnswers[i].colors;
                colors.normalColor = new Color(0.16f, 0.12f, 0.075f, 1f);
                colors.highlightedColor = Color.Lerp(HollowfenPalette.Gold, Color.white, 0.08f);
                colors.selectedColor = Color.Lerp(HollowfenPalette.Gold, Color.white, 0.12f);
                colors.pressedColor = Color.Lerp(HollowfenPalette.Gold, Color.black, 0.16f);
                colors.disabledColor = new Color(0.10f, 0.08f, 0.06f, 0.55f);
                colors.fadeDuration = 0.06f;
                _quizAnswers[i].colors = colors;
                if (_quizAnswerLabels[i] != null) _quizAnswerLabels[i].color = HollowfenPalette.InkDeep;
                _quizAnswers[i].transform.localScale = Vector3.one;
            }
            RefreshQuizAnswerFocusVisuals();
        }

        private void MarkQuizAnswerWrong(int index)
        {
            if (index < 0 || index >= _quizAnswers.Length || _quizAnswers[index] == null) return;
            _quizAnswerWrong[index] = true;
            Color wrong = new Color(0.46f, 0.16f, 0.12f, 1f);
            ColorBlock colors = _quizAnswers[index].colors;
            colors.normalColor = wrong;
            colors.highlightedColor = Color.Lerp(wrong, Color.white, 0.14f);
            colors.selectedColor = Color.Lerp(wrong, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(wrong, Color.black, 0.18f);
            _quizAnswers[index].colors = colors;
            _quizAnswerLabels[index].color = HollowfenPalette.Cream;
            RefreshQuizAnswerFocusVisuals();
        }

        private void RefreshQuizAnswerFocusVisuals()
        {
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            for (int i = 0; i < _quizAnswers.Length; i++)
            {
                Button answer = _quizAnswers[i];
                if (answer == null || !answer.gameObject.activeSelf) continue;
                bool focused = selected == answer.gameObject;
                bool wrong = _quizAnswerWrong[i];

                if (_quizAnswerLabels[i] != null)
                    _quizAnswerLabels[i].color = wrong || !focused
                        ? HollowfenPalette.Cream
                        : HollowfenPalette.InkDeep;
                if (_quizAnswerMarkers[i] != null)
                {
                    // ASCII punctuation is intentional: the journal serif does not ship the
                    // geometric-dingbat block, which made the old ◆/◇ focus mark render blank.
                    _quizAnswerMarkers[i].text = wrong ? "×" : (focused ? ">" : "·");
                    _quizAnswerMarkers[i].color = wrong || !focused
                        ? HollowfenPalette.Gold
                        : HollowfenPalette.InkDeep;
                }
                if (_quizAnswerOutlines[i] != null)
                {
                    Color outline = wrong
                        ? new Color(0.85f, 0.31f, 0.23f, focused ? 1f : 0.72f)
                        : new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                            HollowfenPalette.Gold.b, focused ? 1f : 0.34f);
                    _quizAnswerOutlines[i].color = outline;
                }
                answer.transform.localScale = focused ? Vector3.one * 1.012f : Vector3.one;
            }
        }

        private void ResetQuizSuccessPresentation()
        {
            StopQuizSuccessAnimation();
            StopQuizHaptics();
            if (_quizSuccessRoot != null) _quizSuccessRoot.SetActive(false);
            if (_quizDiscoveryAnnotationRoot != null) _quizDiscoveryAnnotationRoot.SetActive(false);
            if (_quizInkMarksRoot != null) _quizInkMarksRoot.SetActive(false);
            if (_quizEyebrow != null) _quizEyebrow.gameObject.SetActive(true);
            if (_quizQuestion != null) _quizQuestion.gameObject.SetActive(true);
            if (_quizFeedbackCard != null) _quizFeedbackCard.SetActive(true);
            if (_quizPageCounter != null) _quizPageCounter.gameObject.SetActive(true);
            if (_quizPageHint != null) _quizPageHint.gameObject.SetActive(true);
            if (_quizReferenceEyebrow != null)
                _quizReferenceEyebrow.text = Hollowfen.Localization.Get(
                    "inspect.study.reference_eyebrow");
            if (_quizViewFullPage != null)
                ((RectTransform)_quizViewFullPage.transform).anchoredPosition = new Vector2(0f, -146f);
            if (_quizBack != null) _quizBack.interactable = true;
        }

        private void StopQuizSuccessAnimation()
        {
            if (_quizSuccessRoutine != null)
            {
                StopCoroutine(_quizSuccessRoutine);
                _quizSuccessRoutine = null;
            }
        }

        private IEnumerator AnimateQuizSuccess()
        {
            if (_quizSuccessGroup == null || _quizSuccessSeal == null || _quizSuccessCheck == null)
                yield break;

            _quizSuccessGroup.alpha = 0f;
            _quizSuccessSeal.localScale = Vector3.zero;
            _quizSuccessCheck.localScale = Vector3.zero;
            _quizSuccessCheck.localRotation = Quaternion.Euler(0f, 0f, -14f);
            if (_quizSuccessModelGroup != null) _quizSuccessModelGroup.alpha = 1f;
            if (_quizSuccessPulse != null)
            {
                _quizSuccessPulse.transform.localScale = Vector3.one * 0.64f;
                _quizSuccessPulse.color = new Color(HollowfenPalette.Gold.r,
                    HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f);
            }

            // Let the unknown silhouette arrive as the subject before Father's markings and
            // Wren's own hand turn the correct reference into a personal record.
            const float openingSeconds = 0.34f;
            float elapsed = 0f;
            while (elapsed < openingSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / openingSeconds);
                _quizSuccessGroup.alpha = t * t * (3f - 2f * t);
                yield return null;
            }
            _quizSuccessGroup.alpha = 1f;

            if (_quizInkMarksGroup != null) _quizInkMarksGroup.alpha = 1f;
            for (int mark = 0; mark < _quizInkMarks.Length; mark++)
            {
                if (_quizInkMarks[mark] == null) continue;
                elapsed = 0f;
                const float markSeconds = 0.22f;
                while (elapsed < markSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / markSeconds);
                    float back = 1f + 2.0f * Mathf.Pow(t - 1f, 3f) +
                                 1.1f * Mathf.Pow(t - 1f, 2f);
                    _quizInkMarks[mark].localScale = Vector3.one *
                        Mathf.LerpUnclamped(0.15f, 1f, back);
                    yield return null;
                }
                _quizInkMarks[mark].localScale = Vector3.one;
            }

            if (_quizDiscoveryAnnotationGroup != null)
                _quizDiscoveryAnnotationGroup.alpha = 1f;
            UISfx.Pencil();
            int nameCharacters = _quizDiscoveryAnnotationName != null
                ? _quizDiscoveryAnnotationName.text.Length
                : 0;
            int metaCharacters = _quizDiscoveryAnnotationMeta != null
                ? _quizDiscoveryAnnotationMeta.text.Length
                : 0;
            elapsed = 0f;
            const float writingSeconds = 0.84f;
            while (elapsed < writingSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / writingSeconds);
                if (_quizDiscoveryAnnotationName != null)
                    _quizDiscoveryAnnotationName.maxVisibleCharacters =
                        Mathf.CeilToInt(nameCharacters * Mathf.Clamp01(t * 1.45f));
                if (_quizDiscoveryAnnotationMeta != null)
                    _quizDiscoveryAnnotationMeta.maxVisibleCharacters =
                        Mathf.CeilToInt(metaCharacters * Mathf.Clamp01((t - 0.42f) / 0.58f));
                yield return null;
            }
            if (_quizDiscoveryAnnotationName != null)
                _quizDiscoveryAnnotationName.maxVisibleCharacters = int.MaxValue;
            if (_quizDiscoveryAnnotationMeta != null)
                _quizDiscoveryAnnotationMeta.maxVisibleCharacters = int.MaxValue;

            // The specimen itself is the identity payoff: briefly dip the shared render, replace
            // the flat silhouette materials with the real authored model, then bring colour up.
            elapsed = 0f;
            const float colorDipSeconds = 0.20f;
            while (elapsed < colorDipSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / colorDipSeconds);
                if (_quizSuccessModelGroup != null)
                    _quizSuccessModelGroup.alpha = Mathf.Lerp(1f, 0.08f, t);
                yield return null;
            }
            MushroomFieldGuideData species = _currentNode != null ? _currentNode.Data : null;
            if (species != null && MushroomPreviewer.Instance != null)
            {
                MushroomPreviewer.Instance.Show(species, false);
                RenderTexture texture = MushroomPreviewer.Instance.RenderTexture;
                if (_quizSuccessModelPreview != null) _quizSuccessModelPreview.texture = texture;
                if (_quizSilhouettePreview != null) _quizSilhouettePreview.texture = texture;
                if (_quizSpecimenPreview != null) _quizSpecimenPreview.texture = texture;
            }
            elapsed = 0f;
            const float colorRiseSeconds = 0.46f;
            while (elapsed < colorRiseSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / colorRiseSeconds);
                float smooth = t * t * (3f - 2f * t);
                if (_quizSuccessModelGroup != null)
                    _quizSuccessModelGroup.alpha = Mathf.Lerp(0.08f, 1f, smooth);
                yield return null;
            }
            if (_quizSuccessModelGroup != null) _quizSuccessModelGroup.alpha = 1f;

            UISfx.InkStamp();
            PulseQuizHaptics(0.16f, 0.26f, 0.13f);
            Hollowfen.Audio.GameplaySfx.ItemAcquired();
            elapsed = 0f;
            const float stampSeconds = 0.54f;
            while (elapsed < stampSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / stampSeconds);
                float smooth = t * t * (3f - 2f * t);
                float back = 1f + 2.4f * Mathf.Pow(t - 1f, 3f) +
                             1.4f * Mathf.Pow(t - 1f, 2f);
                _quizSuccessSeal.localScale = Vector3.one *
                    Mathf.LerpUnclamped(0.22f, 1f, back);
                _quizSuccessCheck.localScale = Vector3.one *
                    Mathf.LerpUnclamped(0.10f, 1f, back);
                _quizSuccessCheck.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Lerp(-14f, 0f, smooth));
                if (_quizSuccessPulse != null)
                {
                    _quizSuccessPulse.transform.localScale = Vector3.one *
                        Mathf.Lerp(0.64f, 1.48f, smooth);
                    _quizSuccessPulse.color = new Color(HollowfenPalette.Gold.r,
                        HollowfenPalette.Gold.g, HollowfenPalette.Gold.b,
                        (1f - smooth) * 0.7f);
                }
                yield return null;
            }

            _quizSuccessGroup.alpha = 1f;
            _quizSuccessSeal.localScale = Vector3.one;
            _quizSuccessCheck.localScale = Vector3.one;
            _quizSuccessCheck.localRotation = Quaternion.identity;
            if (_quizSuccessPulse != null)
                _quizSuccessPulse.color = new Color(HollowfenPalette.Gold.r,
                    HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f);
            _quizBack.interactable = true;
            WireQuizNavigation();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(
                _quizBack.gameObject);
            _quizSuccessRoutine = null;
        }

        private void PulseQuizHaptics(float low, float high, float seconds)
        {
            StopQuizHaptics();
            Gamepad pad = Gamepad.current;
            if (pad == null) return;
            _quizHapticPad = pad;
            _quizHapticRoutine = StartCoroutine(QuizHapticPulse(low, high, seconds));
        }

        private IEnumerator QuizHapticPulse(float low, float high, float seconds)
        {
            if (_quizHapticPad == null) yield break;
            _quizHapticPad.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_quizHapticPad != null) _quizHapticPad.SetMotorSpeeds(0f, 0f);
            _quizHapticPad = null;
            _quizHapticRoutine = null;
        }

        private void StopQuizHaptics()
        {
            if (_quizHapticRoutine != null)
            {
                StopCoroutine(_quizHapticRoutine);
                _quizHapticRoutine = null;
            }
            if (_quizHapticPad != null) _quizHapticPad.SetMotorSpeeds(0f, 0f);
            _quizHapticPad = null;
        }

        private static IEnumerable<MushroomFieldGuideData> QuizDistractors(MushroomFieldGuideData target)
        {
            MushroomFieldGuideDatabase database = Resources.Load<MushroomFieldGuideDatabase>(
                "MushroomFieldGuideDatabase");
            if (database == null || database.Entries == null) return Array.Empty<MushroomFieldGuideData>();
            var candidates = database.Entries.Where(entry => entry != null && entry != target &&
                entry.Tier == target.Tier && MushroomKnowledge.CanReadPage(entry)).ToList();
            if (candidates.Count < 2)
                candidates.AddRange(database.Entries.Where(entry => entry != null && entry != target &&
                    MushroomKnowledge.CanReadPage(entry) && !candidates.Contains(entry)));
            return candidates.OrderBy(entry => StableHash(target.Id + "." + entry.Id)).Take(2).ToArray();
        }

        private static string FirstFeature(MushroomFieldGuideData species)
        {
            if (species == null) return Hollowfen.Localization.Get("journal.field.unknown");
            string feature = JournalText.MushroomFeature(species, 0);
            return string.IsNullOrWhiteSpace(feature)
                ? JournalText.MushroomDescription(species)
                : feature;
        }

        private static string ConciseObservation(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Hollowfen.Localization.Get("journal.field.unknown");
            string trimmed = value.Trim();
            int sentence = trimmed.IndexOfAny(new[] { '.', ';' });
            if (sentence >= 28) trimmed = trimmed.Substring(0, sentence + 1);
            const int maxLength = 108;
            if (trimmed.Length <= maxLength) return trimmed;
            int breakAt = trimmed.LastIndexOf(' ', maxLength);
            if (breakAt < 56) breakAt = maxLength;
            return trimmed.Substring(0, breakAt).TrimEnd() + "...";
        }

        private void ConfigureDiscoveryReveal(MushroomFieldGuideData species)
        {
            string speciesName = JournalText.MushroomName(species);
            MushroomFieldNote note = MushroomFieldNotes.ForDisplay(species);
            string place = MushroomFieldNotes.PlaceName(note);
            _quizSuccessTitle.text = string.Format(
                Hollowfen.Localization.Get("inspect.quiz.success.title"), speciesName);
            _quizSuccessBody.text = Hollowfen.Localization.Get("inspect.quiz.success.body");

            string rarityKey = "inspect.discovery.rarity.common";
            Color sealColor = new Color(0.25f, 0.36f, 0.24f, 1f);
            if (species.Edibility == Edibility.Deadly)
            {
                rarityKey = "inspect.discovery.rarity.deadly";
                sealColor = new Color(0.43f, 0.13f, 0.10f, 1f);
            }
            else if (species.Tier == ForageTier.FinalLesson)
            {
                rarityKey = "inspect.discovery.rarity.final";
                sealColor = new Color(0.25f, 0.20f, 0.35f, 1f);
            }
            else if (species.Tier == ForageTier.Deepwood)
            {
                rarityKey = "inspect.discovery.rarity.deepwood";
                sealColor = new Color(0.16f, 0.31f, 0.25f, 1f);
            }
            if (_quizSuccessRarity != null)
                _quizSuccessRarity.text = Hollowfen.Localization.Get(rarityKey);
            if (_quizSuccessSealImage != null) _quizSuccessSealImage.color = sealColor;

            MushroomFieldGuideDatabase database = Resources.Load<MushroomFieldGuideDatabase>(
                "MushroomFieldGuideDatabase");
            int total = database != null && database.Entries != null
                ? database.Entries.Count(entry => entry != null)
                : 21;
            int recorded = MushroomDiscovery.All.Count();
            if (_quizSuccessProgress != null)
                _quizSuccessProgress.text = string.Format(Hollowfen.Localization.Get(
                    "inspect.discovery.progress"), recorded, total);

            if (_quizDiscoveryAnnotationName != null)
            {
                _quizDiscoveryAnnotationName.text = string.Format(Hollowfen.Localization.Get(
                    "inspect.discovery.annotation.name"), speciesName);
                _quizDiscoveryAnnotationName.maxVisibleCharacters = 0;
            }
            if (_quizDiscoveryAnnotationMeta != null)
            {
                _quizDiscoveryAnnotationMeta.text = note.HasRecordedContext
                    ? string.Format(Hollowfen.Localization.Get("inspect.discovery.annotation.context"),
                        note.Day, place)
                    : Hollowfen.Localization.Get("inspect.discovery.annotation.legacy");
                _quizDiscoveryAnnotationMeta.maxVisibleCharacters = 0;
            }
            if (_quizDiscoveryAnnotationRoot != null)
                _quizDiscoveryAnnotationRoot.SetActive(true);
            if (_quizDiscoveryAnnotationGroup != null) _quizDiscoveryAnnotationGroup.alpha = 0f;
            if (_quizInkMarksRoot != null) _quizInkMarksRoot.SetActive(true);
            if (_quizInkMarksGroup != null) _quizInkMarksGroup.alpha = 0f;
            for (int i = 0; i < _quizInkMarks.Length; i++)
                if (_quizInkMarks[i] != null) _quizInkMarks[i].localScale = Vector3.zero;

            if (_quizPageCounter != null) _quizPageCounter.gameObject.SetActive(false);
            if (_quizPageHint != null) _quizPageHint.gameObject.SetActive(false);
            if (_quizViewFullPage != null)
            {
                _quizViewFullPage.gameObject.SetActive(true);
                ((RectTransform)_quizViewFullPage.transform).anchoredPosition = new Vector2(0f, -329f);
            }
            if (_quizReferenceEyebrow != null)
                _quizReferenceEyebrow.text = Hollowfen.Localization.Get(
                    "inspect.discovery.annotation.eyebrow");
            if (_quizSuccessModelPreview != null && MushroomPreviewer.Instance != null)
                _quizSuccessModelPreview.texture = MushroomPreviewer.Instance.RenderTexture;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 16777619;
                return (int)(hash & 0x7fffffff);
            }
        }

        private static int PositiveModulo(int value, int divisor) => (value % divisor + divisor) % divisor;

        private void RefreshButtonGlyphs()
        {
            if (_forageGlyph == null || _leaveGlyph == null) return;
            string forage, leave;
            ResolveGlyphs(out forage, out leave);
            if (_forageGlyph.text != forage) _forageGlyph.text = forage;
            if (_leaveGlyph.text  != leave)  _leaveGlyph.text  = leave;
        }

        // Brand glyphs via the shared resolver (batch-48): PlayStation pads get the real shape
        // icons from the ControllerGlyphs TMP sprite sheet; Xbox/Switch get their letters.
        // Falls back to keyboard prompts only when no controller is connected.
        private static void ResolveGlyphs(out string forage, out string leave)
        {
            // Forage shortcut = Player/Interact (buttonNorth). Leave = UI/Cancel (buttonEast).
            if (!Hollowfen.UI.ControllerGlyphs.IsGamepadActive)
            {
                forage = "E";
                leave = "Esc";
                return;
            }
            forage = Hollowfen.UI.ControllerGlyphs.For(Hollowfen.UI.ControllerGlyphs.Face.North);
            leave = Hollowfen.UI.ControllerGlyphs.For(Hollowfen.UI.ControllerGlyphs.Face.East);
        }

        private static void SetStat(TMP_Text t, string label, string value)
        {
            if (t == null) return;
            if (string.IsNullOrEmpty(value)) { t.gameObject.SetActive(false); return; }
            t.gameObject.SetActive(true);
            string goldHex = ColorUtility.ToHtmlStringRGB(HollowfenPalette.PaperAccentInk);
            string inkHex = ColorUtility.ToHtmlStringRGB(InkSoftDark);
            t.richText = true;
            t.text = $"<size=18><color=#{goldHex}><b>{label}</b></color></size>\n<color=#{inkHex}>{value}</color>";
        }

        private static readonly Color InkSoftDark = new Color(0.20f, 0.16f, 0.12f, 1f);
        private static readonly Color BodyInk     = new Color(0.18f, 0.14f, 0.10f, 1f);

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            // Full-screen dark scrim — gameplay reads but the panel pops
            var scrim = UICanvasUtil.NewImage("Scrim", transform, new Color(0f, 0f, 0f, 0.78f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            // Panel: 1500×940 parchment journal page (taller so buttons sit cleanly below preview)
            const float panelW = 1500f;
            const float panelH = 940f;
            const float pad = 56f;

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.raycastTarget = true;
            // Rounded journal page (batch-47 square sweep) — the paper family established by
            // pause/settings/ConfirmModal. The old square parchment texture read as a hard box.
            panelImg.color = HollowfenPalette.Parchment;
            UICanvasUtil.Roundify(panelImg, 24);
            var panelRT = (RectTransform)panel.transform;
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(panelW, panelH);
            panelRT.anchoredPosition = Vector2.zero;

            // Double-rule frame inside the panel (outer + inner gold lines, both faint)
            BuildPanelFrame(panelRT, panelW, panelH);

            // Eyebrow strip at the very top: "FIELD JOURNAL — SPECIMEN"
            var topEyebrow = UICanvasUtil.NewEyebrow("TopEyebrow", panel.transform,
                Hollowfen.Localization.Get("inspect.eyebrow"), 18f, HollowfenPalette.PaperAccentInk, TMPro.TextAlignmentOptions.Center);
            var teRT = topEyebrow.rectTransform;
            teRT.anchorMin = new Vector2(0f, 1f);
            teRT.anchorMax = new Vector2(1f, 1f);
            teRT.pivot = new Vector2(0.5f, 1f);
            teRT.sizeDelta = new Vector2(0f, 18f);
            teRT.anchoredPosition = new Vector2(0f, -22f);

            // === LEFT: 3D PREVIEW ===
            // Frame around the preview area — thin gold inset. Sits in the upper portion of the panel
            // so the centered button row below has clear vertical room.
            const float previewSize = 700f;
            const float previewYOffset = 60f;
            var previewFrame = UICanvasUtil.NewImage("PreviewFrame", panel.transform, HollowfenPalette.GoldFaint, false);
            UICanvasUtil.Roundify(previewFrame.GetComponent<Image>(), 16);
            var pfRT = (RectTransform)previewFrame.transform;
            pfRT.anchorMin = new Vector2(0f, 0.5f);
            pfRT.anchorMax = new Vector2(0f, 0.5f);
            pfRT.pivot = new Vector2(0f, 0.5f);
            pfRT.sizeDelta = new Vector2(previewSize + 4f, previewSize + 4f);
            pfRT.anchoredPosition = new Vector2(pad - 2f, previewYOffset - 2f);

            var previewBg = UICanvasUtil.NewImage("PreviewBg", panel.transform, HollowfenPalette.Parchment, false);
            UICanvasUtil.Roundify(previewBg.GetComponent<Image>(), 14);
            _previewBgRT = (RectTransform)previewBg.transform;
            _previewBgRT.anchorMin = new Vector2(0f, 0.5f);
            _previewBgRT.anchorMax = new Vector2(0f, 0.5f);
            _previewBgRT.pivot = new Vector2(0f, 0.5f);
            _previewBgRT.sizeDelta = new Vector2(previewSize, previewSize);
            _previewBgRT.anchoredPosition = new Vector2(pad, previewYOffset);

            var previewGO = new GameObject("Preview", typeof(RectTransform));
            previewGO.transform.SetParent(previewBg.transform, false);
            _previewImage = previewGO.AddComponent<RawImage>();
            _previewImage.raycastTarget = false;
            UICanvasUtil.Stretch((RectTransform)previewGO.transform);
            if (MushroomPreviewer.Instance != null)
                _previewImage.texture = MushroomPreviewer.Instance.RenderTexture;

            // Hint moved to TOP of preview frame so it never collides with the centered button row below.
            var hintScrim = UICanvasUtil.NewImage("HintScrim", previewBg.transform, new Color(0f, 0f, 0f, 0.28f), false);
            var hsRT = (RectTransform)hintScrim.transform;
            hsRT.anchorMin = new Vector2(0f, 1f);
            hsRT.anchorMax = new Vector2(1f, 1f);
            hsRT.pivot = new Vector2(0.5f, 1f);
            hsRT.sizeDelta = new Vector2(0f, 34f);
            hsRT.anchoredPosition = Vector2.zero;
            var hint = UICanvasUtil.NewBody("Hint", hintScrim.transform,
                Hollowfen.Localization.Get("inspect.preview.hint"),
                18f, HollowfenPalette.Cream,
                TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(hint.rectTransform);

            // === RIGHT: TEXT COLUMN ===
            var col = UICanvasUtil.NewRect("TextCol", panel.transform);
            const float colLeft = pad + previewSize + 56f;
            col.anchorMin = new Vector2(0f, 0f);
            col.anchorMax = new Vector2(1f, 1f);
            col.pivot = new Vector2(0f, 1f);
            col.offsetMin = new Vector2(colLeft, 130f);
            col.offsetMax = new Vector2(-pad, -78f);

            float y = 0f;

            // Eyebrow (edibility category, gold uppercase, letter-spaced)
            _eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", col, "", 18f, HollowfenPalette.PaperAccentInk);
            var eRT = _eyebrow.rectTransform;
            eRT.anchorMin = new Vector2(0f, 1f); eRT.anchorMax = new Vector2(1f, 1f);
            eRT.pivot = new Vector2(0f, 1f);
            eRT.sizeDelta = new Vector2(0f, 18f);
            eRT.anchoredPosition = new Vector2(0f, y);
            y -= 30f;

            // Title (IM Fell serif, dark on parchment)
            _title = UICanvasUtil.NewHeading("Title", col, "", 68f, HollowfenPalette.InkDeep,
                TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var tRT = _title.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0f, 1f);
            tRT.sizeDelta = new Vector2(0f, 80f);
            tRT.anchoredPosition = new Vector2(0f, y);
            y -= 88f;

            // Gold underline rule
            var underline = UICanvasUtil.NewImage("TitleRule", col, HollowfenPalette.Gold, false);
            var urRT = (RectTransform)underline.transform;
            urRT.anchorMin = new Vector2(0f, 1f); urRT.anchorMax = new Vector2(0f, 1f);
            urRT.pivot = new Vector2(0f, 1f);
            urRT.sizeDelta = new Vector2(120f, 2f);
            urRT.anchoredPosition = new Vector2(0f, y);
            y -= 18f;

            // Latin (italic moss)
            _latin = UICanvasUtil.NewBody("Latin", col, "", 22f, HollowfenPalette.PaperMutedInk,
                TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.TopLeft);
            var lRT = _latin.rectTransform;
            lRT.anchorMin = new Vector2(0f, 1f); lRT.anchorMax = new Vector2(1f, 1f);
            lRT.pivot = new Vector2(0f, 1f);
            lRT.sizeDelta = new Vector2(0f, 30f);
            lRT.anchoredPosition = new Vector2(0f, y);
            y -= 36f;

            // Edibility chip — pill: dot inside, label inside, parchment with gold border feel
            _edibilityChip = UICanvasUtil.NewRect("EdibilityChip", col);
            _edibilityChip.anchorMin = new Vector2(0f, 1f); _edibilityChip.anchorMax = new Vector2(0f, 1f);
            _edibilityChip.pivot = new Vector2(0f, 1f);
            _edibilityChip.sizeDelta = new Vector2(220f, 30f);
            _edibilityChip.anchoredPosition = new Vector2(0f, y);

            var chipBg = UICanvasUtil.NewImage("ChipBg", _edibilityChip, new Color(0f, 0f, 0f, 0.06f), false);
            UICanvasUtil.Roundify(chipBg.GetComponent<Image>(), 15);   // full pill at 30px height
            UICanvasUtil.Stretch((RectTransform)chipBg.transform);

            var dotGO = UICanvasUtil.NewImage("Dot", _edibilityChip, Color.white, false);
            dotGO.GetComponent<Image>().sprite = UICanvasUtil.Circle(); // a dot, not a tiny square
            var dotRT = (RectTransform)dotGO.transform;
            dotRT.anchorMin = new Vector2(0f, 0.5f); dotRT.anchorMax = new Vector2(0f, 0.5f);
            dotRT.pivot = new Vector2(0f, 0.5f);
            dotRT.sizeDelta = new Vector2(12f, 12f);
            dotRT.anchoredPosition = new Vector2(12f, 0f);
            _edibilityDot = dotGO.GetComponent<Image>();

            _edibilityLabel = UICanvasUtil.NewEyebrow("EdibilityLabel", _edibilityChip, "", 18f, HollowfenPalette.InkDeep);
            var elRT = _edibilityLabel.rectTransform;
            elRT.anchorMin = new Vector2(0f, 0.5f); elRT.anchorMax = new Vector2(1f, 0.5f);
            elRT.pivot = new Vector2(0f, 0.5f);
            elRT.sizeDelta = new Vector2(-32f, 24f);
            elRT.anchoredPosition = new Vector2(32f, 0f);
            y -= 44f;

            // Body description (dark on parchment)
            _body = UICanvasUtil.NewBody("Body", col, "", 21f, BodyInk,
                TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var bRT = _body.rectTransform;
            bRT.anchorMin = new Vector2(0f, 1f); bRT.anchorMax = new Vector2(1f, 1f);
            bRT.pivot = new Vector2(0f, 1f);
            bRT.sizeDelta = new Vector2(0f, 200f);
            bRT.anchoredPosition = new Vector2(0f, y);
            _body.lineSpacing = 8f;
            y -= 220f;

            // Stat strip — three columns: HABITAT / SEASON / LOOK-ALIKES.
            // Height sized for the worst-case stat value (LOOK-ALIKES tends to run long with safety copy).
            const float statStripH = 170f;
            _statStrip = UICanvasUtil.NewRect("StatStrip", col);
            _statStrip.anchorMin = new Vector2(0f, 1f); _statStrip.anchorMax = new Vector2(1f, 1f);
            _statStrip.pivot = new Vector2(0f, 1f);
            _statStrip.sizeDelta = new Vector2(0f, statStripH);
            _statStrip.anchoredPosition = new Vector2(0f, y);

            // Top rule on stat strip
            var statRule = UICanvasUtil.NewImage("StatRule", _statStrip, HollowfenPalette.GoldFaint, false);
            var srRT = (RectTransform)statRule.transform;
            srRT.anchorMin = new Vector2(0f, 1f); srRT.anchorMax = new Vector2(1f, 1f);
            srRT.pivot = new Vector2(0.5f, 1f);
            srRT.sizeDelta = new Vector2(0f, 1f);
            srRT.anchoredPosition = Vector2.zero;

            _statHabitat   = MakeStatColumn(_statStrip, 0f / 3f);
            _statSeason    = MakeStatColumn(_statStrip, 1f / 3f);
            _statLookalikes= MakeStatColumn(_statStrip, 2f / 3f);

            y -= statStripH + 12f;

            // Forager's note — italic gold pull-quote
            _foragerNote = UICanvasUtil.NewBody("ForagerNote", col, "", 20f, HollowfenPalette.PaperAccentInk,
                TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.TopLeft);
            var fnRT = _foragerNote.rectTransform;
            fnRT.anchorMin = new Vector2(0f, 1f); fnRT.anchorMax = new Vector2(1f, 1f);
            fnRT.pivot = new Vector2(0f, 1f);
            fnRT.sizeDelta = new Vector2(0f, 80f);
            fnRT.anchoredPosition = new Vector2(0f, y);
            _foragerNote.lineSpacing = 6f;

            // === BUTTONS ===
            var btnRow = UICanvasUtil.NewRect("Buttons", panel.transform);
            _inspectButtonsRow = btnRow.gameObject;
            btnRow.anchorMin = new Vector2(0.5f, 0f); btnRow.anchorMax = new Vector2(0.5f, 0f);
            btnRow.pivot = new Vector2(0.5f, 0f);
            btnRow.sizeDelta = new Vector2(720f, 78f);
            btnRow.anchoredPosition = new Vector2(0f, 36f);

            _forageBtn = MakeJournalButton("ForageBtn", btnRow,
                Hollowfen.Localization.Get("inspect.btn.forage"),
                HollowfenPalette.Gold, HollowfenPalette.InkDeep, true,
                new Vector2(-176f, 0f), out _forageGlyph);
            _forageBtn.onClick.AddListener(OnForageClicked);
            _forageLabel = _forageBtn.transform.Find("Label")?.GetComponent<TMP_Text>();

            _leaveBtn = MakeJournalButton("LeaveBtn", btnRow,
                Hollowfen.Localization.Get("inspect.btn.leave"),
                Color.white, HollowfenPalette.InkDeep, false,
                new Vector2(176f, 0f), out _leaveGlyph);
            _leaveBtn.onClick.AddListener(OnLeaveClicked);

            RefreshButtonGlyphs();

            var fNav = _forageBtn.navigation; fNav.mode = Navigation.Mode.Explicit;
            fNav.selectOnRight = _leaveBtn; _forageBtn.navigation = fNav;
            var lNav = _leaveBtn.navigation; lNav.mode = Navigation.Mode.Explicit;
            lNav.selectOnLeft = _forageBtn; _leaveBtn.navigation = lNav;

            BuildJournalComparison(panel.transform);
        }

        private void BuildJournalComparison(Transform panel)
        {
            _quizPanel = UICanvasUtil.NewImage("JournalComparison", panel,
                new Color(0.075f, 0.062f, 0.046f, 1f), true);
            var quizRT = (RectTransform)_quizPanel.transform;
            quizRT.anchorMin = new Vector2(0.5f, 0.5f);
            quizRT.anchorMax = new Vector2(0.5f, 0.5f);
            quizRT.pivot = new Vector2(0.5f, 0.5f);
            quizRT.sizeDelta = new Vector2(1420f, 820f);
            UICanvasUtil.Roundify(_quizPanel.GetComponent<Image>(), 22);
            JournalChrome.AddStructuralBorder(quizRT, 22, 0.16f);

            // Left leaf: Father's illustrated reference stays visible throughout the field test.
            // Players compare the drawing, written marks, and live 3D specimen without bouncing
            // between two unrelated full-screen menus.
            var referenceLeaf = UICanvasUtil.NewImage("ReferenceLeaf", quizRT,
                HollowfenPalette.Parchment, false);
            var referenceRT = (RectTransform)referenceLeaf.transform;
            referenceRT.anchorMin = referenceRT.anchorMax = new Vector2(0.5f, 0.5f);
            referenceRT.pivot = new Vector2(0.5f, 0.5f);
            referenceRT.sizeDelta = new Vector2(610f, 720f);
            referenceRT.anchoredPosition = new Vector2(-365f, 0f);
            UICanvasUtil.Roundify(referenceLeaf.GetComponent<Image>(), 16);
            JournalChrome.AddStructuralBorder(referenceRT, 16, 0.12f);

            _quizReferenceEyebrow = UICanvasUtil.NewEyebrow("ReferenceEyebrow", referenceRT,
                Hollowfen.Localization.Get("inspect.study.reference_eyebrow"), 18f,
                HollowfenPalette.PaperAccentInk, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_quizReferenceEyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-44f, 22f), new Vector2(0f, -20f));

            var pageFrame = UICanvasUtil.NewImage("PageFrame", referenceRT,
                new Color(0.10f, 0.075f, 0.045f, 0.16f), false);
            var pageFrameRT = (RectTransform)pageFrame.transform;
            UICanvasUtil.SetRect(pageFrameRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(558f, 348f), new Vector2(0f, -52f));
            UICanvasUtil.Roundify(pageFrame.GetComponent<Image>(), 10);
            var pageArt = UICanvasUtil.NewRect("IllustratedPage", pageFrameRT);
            _quizJournalPageRT = pageArt;
            UICanvasUtil.Stretch(pageArt);
            pageArt.offsetMin = new Vector2(8f, 8f);
            pageArt.offsetMax = new Vector2(-8f, -8f);
            _quizJournalPage = pageArt.gameObject.AddComponent<Image>();
            _quizJournalPage.preserveAspect = true;
            _quizJournalPage.raycastTarget = false;
            var pageShadow = UICanvasUtil.NewImage("TurnShadow", pageArt,
                new Color(0f, 0f, 0f, 0f), false);
            _quizPageTurnShadow = pageShadow.GetComponent<Image>();
            _quizPageTurnShadow.sprite = UICanvasUtil.MakeHorizontalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0f, new Color(0f, 0f, 0f, 0f)),
                new UICanvasUtil.GradientStop(0.72f, new Color(0f, 0f, 0f, 0.18f)),
                new UICanvasUtil.GradientStop(1f, new Color(0f, 0f, 0f, 0.78f)),
            }, 128);
            UICanvasUtil.Stretch((RectTransform)pageShadow.transform);

            _quizInkMarksRoot = UICanvasUtil.NewRect("WrenInkMarks", pageFrameRT).gameObject;
            var inkRootRT = (RectTransform)_quizInkMarksRoot.transform;
            UICanvasUtil.Stretch(inkRootRT);
            inkRootRT.offsetMin = new Vector2(8f, 8f);
            inkRootRT.offsetMax = new Vector2(-8f, -8f);
            _quizInkMarksGroup = _quizInkMarksRoot.AddComponent<CanvasGroup>();
            Color ink = new Color(0.45f, 0.19f, 0.09f, 0.88f);
            Vector2[] inkPositions =
            {
                new Vector2(-165f, 62f), new Vector2(-95f, -66f), new Vector2(-225f, -92f),
            };
            Vector2[] inkSizes =
            {
                new Vector2(128f, 86f), new Vector2(108f, 76f), new Vector2(82f, 64f),
            };
            for (int i = 0; i < _quizInkMarks.Length; i++)
            {
                var mark = UICanvasUtil.NewImage("Observation" + (i + 1), inkRootRT, ink, false);
                _quizInkMarks[i] = (RectTransform)mark.transform;
                UICanvasUtil.RoundifyOutline(mark.GetComponent<Image>(), 34, 3f);
                UICanvasUtil.SetRect(_quizInkMarks[i],
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), inkSizes[i], inkPositions[i]);
                _quizInkMarks[i].localRotation = Quaternion.Euler(0f, 0f, i == 1 ? -8f : 6f);
            }
            _quizInkMarksRoot.SetActive(false);

            _quizPageCounter = UICanvasUtil.NewEyebrow("PageCounter", referenceRT, "", 18f,
                HollowfenPalette.PaperAccentInk, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_quizPageCounter.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-48f, 24f), new Vector2(0f, -76f));

            TMP_Text previousGlyph;
            _quizPreviousPage = MakeJournalButton("PreviousPage", referenceRT,
                Hollowfen.Localization.Get("inspect.browser.previous"), Color.white,
                HollowfenPalette.InkDeep, false, new Vector2(-205f, -146f), out previousGlyph);
            ConfigureReferenceButton(_quizPreviousPage, 132f, 58f);
            _quizPreviousPage.onClick.AddListener(() => ChangeQuizCandidatePage(-1));

            TMP_Text fullPageGlyph;
            _quizViewFullPage = MakeJournalButton("ViewFullPage", referenceRT,
                Hollowfen.Localization.Get("inspect.browser.enlarge"), HollowfenPalette.Gold,
                HollowfenPalette.InkDeep, true, new Vector2(0f, -146f), out fullPageGlyph);
            ConfigureReferenceButton(_quizViewFullPage, 252f, 58f);
            _quizViewFullPage.onClick.AddListener(OpenQuizFullPage);

            TMP_Text nextGlyph;
            _quizNextPage = MakeJournalButton("NextPage", referenceRT,
                Hollowfen.Localization.Get("inspect.browser.next"), Color.white,
                HollowfenPalette.InkDeep, false, new Vector2(205f, -146f), out nextGlyph);
            ConfigureReferenceButton(_quizNextPage, 132f, 58f);
            _quizNextPage.onClick.AddListener(() => ChangeQuizCandidatePage(1));

            _quizPageHint = UICanvasUtil.NewBody("PageHint", referenceRT, "", 18f,
                BodyInk, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_quizPageHint.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-80f, 92f), new Vector2(0f, -238f));
            _quizPageHint.textWrappingMode = TextWrappingModes.Normal;
            _quizPageHint.enableAutoSizing = false;

            _quizDiscoveryAnnotationRoot = UICanvasUtil.NewImage("WrenFieldNote", referenceRT,
                new Color(0.88f, 0.82f, 0.69f, 1f), false);
            var annotationRT = (RectTransform)_quizDiscoveryAnnotationRoot.transform;
            annotationRT.anchorMin = annotationRT.anchorMax = new Vector2(0.5f, 0.5f);
            annotationRT.pivot = new Vector2(0.5f, 0.5f);
            annotationRT.sizeDelta = new Vector2(520f, 164f);
            annotationRT.anchoredPosition = new Vector2(0f, -218f);
            annotationRT.localRotation = Quaternion.Euler(0f, 0f, -1.4f);
            UICanvasUtil.Roundify(_quizDiscoveryAnnotationRoot.GetComponent<Image>(), 10);
            JournalChrome.AddStructuralBorder(annotationRT, 10, 0.18f);
            _quizDiscoveryAnnotationGroup = _quizDiscoveryAnnotationRoot.AddComponent<CanvasGroup>();
            _quizDiscoveryAnnotationName = UICanvasUtil.NewBody("Name", annotationRT, "", 31f,
                new Color(0.20f, 0.12f, 0.08f, 1f), FontStyles.Normal,
                TextAlignmentOptions.Center);
            _quizDiscoveryAnnotationName.font = UICanvasUtil.CursiveFont;
            _quizDiscoveryAnnotationName.characterSpacing = 1.2f;
            UICanvasUtil.SetRect(_quizDiscoveryAnnotationName.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-36f, 64f), new Vector2(0f, -22f));
            _quizDiscoveryAnnotationName.enableAutoSizing = true;
            _quizDiscoveryAnnotationName.fontSizeMin = 21f;
            _quizDiscoveryAnnotationName.fontSizeMax = 31f;
            _quizDiscoveryAnnotationMeta = UICanvasUtil.NewBody("Context", annotationRT, "", 16f,
                new Color(0.27f, 0.19f, 0.13f, 0.86f), FontStyles.Italic,
                TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_quizDiscoveryAnnotationMeta.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-48f, 52f), new Vector2(0f, 24f));
            _quizDiscoveryAnnotationRoot.SetActive(false);

            // Right leaf: one decision at a time, with a persistent progress trail and a single
            // clear escape/continue action at the bottom.
            var questionRT = UICanvasUtil.NewRect("QuestionLeaf", quizRT);
            questionRT.anchorMin = questionRT.anchorMax = new Vector2(0.5f, 0.5f);
            questionRT.pivot = new Vector2(0.5f, 0.5f);
            questionRT.sizeDelta = new Vector2(690f, 720f);
            questionRT.anchoredPosition = new Vector2(365f, 0f);

            _quizProgress = UICanvasUtil.NewBody("Progress", questionRT, "", 20f,
                HollowfenPalette.Cream, FontStyles.Bold, TextAlignmentOptions.Center);
            _quizProgress.richText = true;
            _quizProgress.textWrappingMode = TextWrappingModes.NoWrap;
            UICanvasUtil.SetRect(_quizProgress.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-16f, 24f), new Vector2(0f, 0f));

            _quizEyebrow = UICanvasUtil.NewEyebrow("Eyebrow", questionRT, "", 18f,
                HollowfenPalette.Gold, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(_quizEyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(-28f, 24f), new Vector2(14f, -48f));
            _quizQuestion = UICanvasUtil.NewHeading("Question", questionRT, "", 40f,
                HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(_quizQuestion.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(-28f, 118f), new Vector2(14f, -84f));
            _quizQuestion.enableAutoSizing = true;
            _quizQuestion.fontSizeMin = 27f;
            _quizQuestion.fontSizeMax = 40f;

            _quizSilhouetteFrame = UICanvasUtil.NewImage("LiveSilhouette", questionRT,
                HollowfenPalette.Parchment, false);
            var silhouetteRT = (RectTransform)_quizSilhouetteFrame.transform;
            silhouetteRT.anchorMin = silhouetteRT.anchorMax = new Vector2(0.5f, 0.5f);
            silhouetteRT.pivot = new Vector2(0.5f, 0.5f);
            silhouetteRT.sizeDelta = new Vector2(318f, 210f);
            silhouetteRT.anchoredPosition = new Vector2(0f, 24f);
            UICanvasUtil.Roundify(_quizSilhouetteFrame.GetComponent<Image>(), 14);
            var silhouetteImage = _quizSilhouetteFrame.GetComponent<Image>();
            silhouetteImage.raycastTarget = true;
            _quizInspectSpecimen = _quizSilhouetteFrame.AddComponent<Button>();
            _quizInspectSpecimen.transition = Selectable.Transition.None;
            _quizInspectSpecimen.targetGraphic = silhouetteImage;
            _quizInspectSpecimen.onClick.AddListener(OpenQuizSpecimen);
            var silhouetteOutline = UICanvasUtil.NewImage("Outline", silhouetteRT,
                HollowfenPalette.GoldFaint, false);
            UICanvasUtil.RoundifyOutline(silhouetteOutline.GetComponent<Image>(), 14, 2f);
            UICanvasUtil.Stretch((RectTransform)silhouetteOutline.transform);
            var silhouetteImageRT = UICanvasUtil.NewRect("SpecimenSilhouette", silhouetteRT);
            UICanvasUtil.Stretch(silhouetteImageRT);
            silhouetteImageRT.offsetMin = new Vector2(8f, 8f);
            silhouetteImageRT.offsetMax = new Vector2(-8f, -8f);
            _quizSilhouettePreview = silhouetteImageRT.gameObject.AddComponent<RawImage>();
            _quizSilhouettePreview.raycastTarget = false;
            if (MushroomPreviewer.Instance != null)
                _quizSilhouettePreview.texture = MushroomPreviewer.Instance.RenderTexture;
            var silhouetteLabel = UICanvasUtil.NewImage("Label", silhouetteRT,
                new Color(0.075f, 0.062f, 0.046f, 0.82f), false);
            var silhouetteLabelRT = (RectTransform)silhouetteLabel.transform;
            UICanvasUtil.SetRect(silhouetteLabelRT,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 34f), Vector2.zero);
            var silhouetteLabelText = UICanvasUtil.NewEyebrow("Text", silhouetteLabelRT,
                Hollowfen.Localization.Get("inspect.browser.silhouette"), 18f,
                HollowfenPalette.Cream, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(silhouetteLabelText.rectTransform);
            var silhouetteFocus = _quizSilhouetteFrame.AddComponent<FocusHighlight>();
            silhouetteFocus.Configure(silhouetteOutline.GetComponent<Image>(), silhouetteRT,
                HollowfenPalette.Gold, 1.025f);

            for (int i = 0; i < _quizAnswers.Length; i++)
            {
                TMP_Text unusedGlyph;
                Button answer = MakeJournalButton("Answer" + (i + 1), questionRT, "",
                    HollowfenPalette.Gold, HollowfenPalette.InkDeep, true,
                    new Vector2(0f, 86f - i * 116f), out unusedGlyph);
                RectTransform answerRT = (RectTransform)answer.transform;
                answerRT.sizeDelta = new Vector2(650f, 94f);
                answerRT.anchorMin = answerRT.anchorMax = new Vector2(0.5f, 0.5f);
                answer.transform.Find("Glyph").gameObject.SetActive(false);
                _quizAnswers[i] = answer;
                _quizAnswerLabels[i] = answer.transform.Find("Label").GetComponent<TMP_Text>();
                _quizAnswerLabels[i].fontSize = 21f;
                _quizAnswerLabels[i].enableAutoSizing = true;
                _quizAnswerLabels[i].fontSizeMin = 15f;
                _quizAnswerLabels[i].fontSizeMax = 21f;
                RectTransform labelRT = _quizAnswerLabels[i].rectTransform;
                labelRT.offsetMin = new Vector2(68f, 10f);
                labelRT.offsetMax = new Vector2(-28f, -10f);

                var focusOutline = UICanvasUtil.NewImage("FocusOutline", answerRT,
                    HollowfenPalette.GoldFaint, false);
                _quizAnswerOutlines[i] = focusOutline.GetComponent<Image>();
                UICanvasUtil.RoundifyOutline(_quizAnswerOutlines[i], 14, 2.5f);
                UICanvasUtil.Stretch((RectTransform)focusOutline.transform);
                focusOutline.transform.SetSiblingIndex(0);

                _quizAnswerMarkers[i] = UICanvasUtil.NewHeading("FocusMarker", answerRT, "·", 30f,
                    HollowfenPalette.Gold, FontStyles.Normal, TextAlignmentOptions.Center);
                UICanvasUtil.SetRect(_quizAnswerMarkers[i].rectTransform,
                    new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                    new Vector2(54f, -8f), new Vector2(14f, 0f));
            }

            var feedbackCard = UICanvasUtil.NewImage("FeedbackCard", questionRT,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                    HollowfenPalette.Gold.b, 0.08f), false);
            _quizFeedbackCard = feedbackCard;
            var feedbackRT = (RectTransform)feedbackCard.transform;
            UICanvasUtil.SetRect(feedbackRT, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(650f, 78f), new Vector2(0f, 92f));
            UICanvasUtil.Roundify(feedbackCard.GetComponent<Image>(), 12);
            var feedbackOutline = UICanvasUtil.NewImage("Outline", feedbackRT,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                    HollowfenPalette.Gold.b, 0.28f), false);
            UICanvasUtil.RoundifyOutline(feedbackOutline.GetComponent<Image>(), 12, 1.25f);
            UICanvasUtil.Stretch((RectTransform)feedbackOutline.transform);

            _quizFeedback = UICanvasUtil.NewBody("Feedback", feedbackRT, "", 20f,
                HollowfenPalette.Gold, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(_quizFeedback.rectTransform);
            _quizFeedback.rectTransform.offsetMin = new Vector2(24f, 10f);
            _quizFeedback.rectTransform.offsetMax = new Vector2(-24f, -10f);
            _quizFeedback.textWrappingMode = TextWrappingModes.Normal;
            _quizFeedback.overflowMode = TextOverflowModes.Ellipsis;
            _quizFeedback.enableAutoSizing = false;

            BuildQuizSuccessPresentation(questionRT);

            TMP_Text backGlyph;
            _quizBack = MakeJournalButton("Back", questionRT,
                Hollowfen.Localization.Get("inspect.quiz.back"), Color.white,
                HollowfenPalette.Cream, false, new Vector2(0f, -322f), out backGlyph);
            ((RectTransform)_quizBack.transform).sizeDelta = new Vector2(420f, 64f);
            _quizBack.transform.Find("Glyph").gameObject.SetActive(false);
            _quizBackLabel = _quizBack.transform.Find("Label").GetComponent<TMP_Text>();
            _quizBackLabel.rectTransform.offsetMin = new Vector2(16f, 0f);
            _quizBackLabel.rectTransform.offsetMax = new Vector2(-16f, 0f);
            _quizBack.onClick.AddListener(OnQuizFooterClicked);
            BuildQuizSpecimenOverlay(quizRT);
            BuildQuizFullPageOverlay(quizRT);
            _quizPanel.SetActive(false);
        }

        private static void ConfigureReferenceButton(Button button, float width, float height)
        {
            if (button == null) return;
            RectTransform rt = (RectTransform)button.transform;
            rt.sizeDelta = new Vector2(width, height);
            Transform glyph = button.transform.Find("Glyph");
            if (glyph != null) glyph.gameObject.SetActive(false);
            TMP_Text label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label == null) return;
            label.rectTransform.offsetMin = new Vector2(10f, 0f);
            label.rectTransform.offsetMax = new Vector2(-10f, 0f);
            label.enableAutoSizing = false;
            label.fontSize = 18f;
        }

        private void BuildQuizSpecimenOverlay(RectTransform quizRT)
        {
            _quizSpecimenOverlay = UICanvasUtil.NewImage("SpecimenLens", quizRT,
                new Color(0.045f, 0.037f, 0.030f, 1f), true);
            var overlayRT = (RectTransform)_quizSpecimenOverlay.transform;
            UICanvasUtil.Stretch(overlayRT);
            UICanvasUtil.Roundify(_quizSpecimenOverlay.GetComponent<Image>(), 22);
            JournalChrome.AddStructuralBorder(overlayRT, 22, 0.24f);

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", overlayRT,
                Hollowfen.Localization.Get("inspect.lens.eyebrow"), 14f,
                HollowfenPalette.Gold, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(eyebrow.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-60f, 26f), new Vector2(0f, -20f));

            var frame = UICanvasUtil.NewImage("SpecimenFrame", overlayRT,
                HollowfenPalette.Parchment, false);
            _quizSpecimenPreviewRT = (RectTransform)frame.transform;
            _quizSpecimenPreviewRT.anchorMin = _quizSpecimenPreviewRT.anchorMax =
                new Vector2(0.5f, 0.5f);
            _quizSpecimenPreviewRT.pivot = new Vector2(0.5f, 0.5f);
            _quizSpecimenPreviewRT.sizeDelta = new Vector2(760f, 630f);
            _quizSpecimenPreviewRT.anchoredPosition = new Vector2(0f, 16f);
            UICanvasUtil.Roundify(frame.GetComponent<Image>(), 16);
            JournalChrome.AddStructuralBorder(_quizSpecimenPreviewRT, 16, 0.15f);

            var previewRT = UICanvasUtil.NewRect("LivePreview", _quizSpecimenPreviewRT);
            UICanvasUtil.Stretch(previewRT);
            previewRT.offsetMin = new Vector2(12f, 12f);
            previewRT.offsetMax = new Vector2(-12f, -12f);
            _quizSpecimenPreview = previewRT.gameObject.AddComponent<RawImage>();
            _quizSpecimenPreview.raycastTarget = false;
            if (MushroomPreviewer.Instance != null)
                _quizSpecimenPreview.texture = MushroomPreviewer.Instance.RenderTexture;

            var hintBar = UICanvasUtil.NewImage("Controls", _quizSpecimenPreviewRT,
                new Color(0.075f, 0.062f, 0.046f, 0.82f), false);
            var hintRT = (RectTransform)hintBar.transform;
            UICanvasUtil.SetRect(hintRT, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 48f), Vector2.zero);
            var hint = UICanvasUtil.NewBody("Hint", hintRT,
                Hollowfen.Localization.Get("inspect.lens.controls"), 15f,
                HollowfenPalette.Cream, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(hint.rectTransform);

            TMP_Text closeGlyph;
            _quizSpecimenClose = MakeJournalButton("Close", overlayRT,
                Hollowfen.Localization.Get("inspect.lens.close"), HollowfenPalette.Gold,
                HollowfenPalette.InkDeep, true, new Vector2(0f, -358f), out closeGlyph);
            ConfigureReferenceButton(_quizSpecimenClose, 300f, 60f);
            _quizSpecimenClose.onClick.AddListener(() => CloseQuizSpecimen());
            Navigation nav = _quizSpecimenClose.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = nav.selectOnDown = nav.selectOnLeft = nav.selectOnRight =
                _quizSpecimenClose;
            _quizSpecimenClose.navigation = nav;
            _quizSpecimenOverlay.SetActive(false);
        }

        private void OpenQuizSpecimen()
        {
            if (_quizSpecimenOverlay == null || !_quizBrowsingCandidates)
            {
                UISfx.Error();
                return;
            }
            _quizFocusBeforeSpecimen = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            if (_quizSpecimenPreview != null && MushroomPreviewer.Instance != null)
                _quizSpecimenPreview.texture = MushroomPreviewer.Instance.RenderTexture;
            _quizSpecimenOverlay.SetActive(true);
            _quizSpecimenOverlay.transform.SetAsLastSibling();
            UISfx.Select();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_quizSpecimenClose.gameObject);
        }

        private void CloseQuizSpecimen(bool restoreFocus = true)
        {
            if (_quizSpecimenOverlay == null || !_quizSpecimenOverlay.activeSelf) return;
            _quizSpecimenOverlay.SetActive(false);
            if (restoreFocus && EventSystem.current != null)
            {
                GameObject target = _quizFocusBeforeSpecimen != null &&
                                    _quizFocusBeforeSpecimen.activeInHierarchy
                    ? _quizFocusBeforeSpecimen
                    : (_quizInspectSpecimen != null ? _quizInspectSpecimen.gameObject : null);
                EventSystem.current.SetSelectedGameObject(target);
            }
            _quizFocusBeforeSpecimen = null;
        }

        private void UpdateQuizSpecimenControls()
        {
            MushroomPreviewer previewer = MushroomPreviewer.Instance;
            if (previewer == null) return;
            float dt = Time.unscaledDeltaTime;
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 orbit = pad.rightStick.ReadValue();
                if (orbit.sqrMagnitude > 0.0025f)
                    previewer.ApplyRotationDelta(orbit.x * _gamepadRotateSpeed * dt,
                        -orbit.y * _gamepadRotateSpeed * dt);
                float zoom = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                if (Mathf.Abs(zoom) > 0.05f)
                    previewer.ApplyZoomDelta(-zoom * _gamepadZoomSpeed * dt);
                if (pad.rightStickButton.wasPressedThisFrame) previewer.ResetView();
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || _quizSpecimenPreviewRT == null) return;
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(
                _quizSpecimenPreviewRT, mouse.position.ReadValue(), null);
            if (inside && mouse.leftButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0.01f)
                    previewer.ApplyRotationDelta(delta.x * _mouseRotateSpeed,
                        -delta.y * _mouseRotateSpeed);
            }
            if (inside && mouse.middleButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0.01f)
                    previewer.ApplyPanDelta(delta * _mousePanSpeed);
            }
            if (inside)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    previewer.ApplyZoomDelta(-scroll * _mouseZoomSpeed);
            }
        }

        private void BuildQuizFullPageOverlay(RectTransform quizRT)
        {
            _quizFullPageOverlay = UICanvasUtil.NewImage("FullPageGallery", quizRT,
                new Color(0.045f, 0.037f, 0.030f, 1f), true);
            var overlayRT = (RectTransform)_quizFullPageOverlay.transform;
            UICanvasUtil.Stretch(overlayRT);
            UICanvasUtil.Roundify(_quizFullPageOverlay.GetComponent<Image>(), 22);
            JournalChrome.AddStructuralBorder(overlayRT, 22, 0.24f);

            _quizFullPageCounter = UICanvasUtil.NewEyebrow("PageCounter", overlayRT, "", 13f,
                HollowfenPalette.Gold, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_quizFullPageCounter.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-60f, 24f), new Vector2(0f, -18f));

            var artFrame = UICanvasUtil.NewImage("IllustrationFrame", overlayRT,
                new Color(HollowfenPalette.Parchment.r, HollowfenPalette.Parchment.g,
                    HollowfenPalette.Parchment.b, 0.10f), false);
            var artFrameRT = (RectTransform)artFrame.transform;
            artFrameRT.anchorMin = artFrameRT.anchorMax = new Vector2(0.5f, 0.5f);
            artFrameRT.pivot = new Vector2(0.5f, 0.5f);
            artFrameRT.sizeDelta = new Vector2(1250f, 704f);
            artFrameRT.anchoredPosition = new Vector2(0f, 2f);
            UICanvasUtil.Roundify(artFrame.GetComponent<Image>(), 12);
            var artRT = UICanvasUtil.NewRect("JournalSpread", artFrameRT);
            _quizFullPageArtRT = artRT;
            UICanvasUtil.Stretch(artRT);
            artRT.offsetMin = new Vector2(8f, 8f);
            artRT.offsetMax = new Vector2(-8f, -8f);
            _quizFullPageArt = artRT.gameObject.AddComponent<Image>();
            _quizFullPageArt.preserveAspect = true;
            _quizFullPageArt.raycastTarget = false;
            var turnShadow = UICanvasUtil.NewImage("TurnShadow", artRT,
                new Color(0f, 0f, 0f, 0f), false);
            _quizFullPageTurnShadow = turnShadow.GetComponent<Image>();
            _quizFullPageTurnShadow.sprite = UICanvasUtil.MakeHorizontalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0f, new Color(0f, 0f, 0f, 0f)),
                new UICanvasUtil.GradientStop(0.72f, new Color(0f, 0f, 0f, 0.18f)),
                new UICanvasUtil.GradientStop(1f, new Color(0f, 0f, 0f, 0.78f)),
            }, 128);
            UICanvasUtil.Stretch((RectTransform)turnShadow.transform);

            TMP_Text previousGlyph;
            _quizFullPagePrevious = MakeJournalButton("Previous", overlayRT,
                Hollowfen.Localization.Get("inspect.browser.previous_page"), Color.white,
                HollowfenPalette.Cream, false, new Vector2(-420f, -362f), out previousGlyph);
            ConfigureReferenceButton(_quizFullPagePrevious, 230f, 58f);
            _quizFullPagePrevious.onClick.AddListener(() => ChangeQuizCandidatePage(-1));

            TMP_Text closeGlyph;
            _quizFullPageClose = MakeJournalButton("Close", overlayRT,
                Hollowfen.Localization.Get("inspect.browser.close"), HollowfenPalette.Gold,
                HollowfenPalette.InkDeep, true, new Vector2(0f, -362f), out closeGlyph);
            ConfigureReferenceButton(_quizFullPageClose, 260f, 58f);
            _quizFullPageClose.onClick.AddListener(() => CloseQuizFullPage());

            TMP_Text nextGlyph;
            _quizFullPageNext = MakeJournalButton("Next", overlayRT,
                Hollowfen.Localization.Get("inspect.browser.next_page"), Color.white,
                HollowfenPalette.Cream, false, new Vector2(420f, -362f), out nextGlyph);
            ConfigureReferenceButton(_quizFullPageNext, 230f, 58f);
            _quizFullPageNext.onClick.AddListener(() => ChangeQuizCandidatePage(1));

            _quizFullPageOverlay.SetActive(false);
        }

        private void BuildQuizSuccessPresentation(RectTransform questionRT)
        {
            _quizSuccessRoot = UICanvasUtil.NewRect("VerificationSuccess", questionRT).gameObject;
            var successRT = (RectTransform)_quizSuccessRoot.transform;
            successRT.anchorMin = Vector2.zero;
            successRT.anchorMax = Vector2.one;
            successRT.offsetMin = Vector2.zero;
            successRT.offsetMax = Vector2.zero;
            _quizSuccessGroup = _quizSuccessRoot.AddComponent<CanvasGroup>();
            _quizSuccessGroup.interactable = false;
            _quizSuccessGroup.blocksRaycasts = false;

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", successRT,
                Hollowfen.Localization.Get("inspect.quiz.success.eyebrow"), 14f,
                HollowfenPalette.Gold, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-40f, 24f), new Vector2(0f, 230f));

            _quizSuccessRarity = UICanvasUtil.NewEyebrow("Rarity", successRT, "", 12.5f,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                    HollowfenPalette.Gold.b, 0.78f), TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_quizSuccessRarity.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-72f, 22f), new Vector2(0f, 194f));

            var modelFrame = UICanvasUtil.NewImage("RevealedSpecimen", successRT,
                HollowfenPalette.Parchment, false);
            var modelFrameRT = (RectTransform)modelFrame.transform;
            UICanvasUtil.SetRect(modelFrameRT,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(340f, 220f), new Vector2(0f, 65f));
            UICanvasUtil.Roundify(modelFrame.GetComponent<Image>(), 14);
            JournalChrome.AddStructuralBorder(modelFrameRT, 14, 0.16f);
            _quizSuccessModelGroup = modelFrame.AddComponent<CanvasGroup>();
            var modelRT = UICanvasUtil.NewRect("Preview", modelFrameRT);
            UICanvasUtil.Stretch(modelRT);
            modelRT.offsetMin = new Vector2(8f, 8f);
            modelRT.offsetMax = new Vector2(-8f, -8f);
            _quizSuccessModelPreview = modelRT.gameObject.AddComponent<RawImage>();
            _quizSuccessModelPreview.raycastTarget = false;
            if (MushroomPreviewer.Instance != null)
                _quizSuccessModelPreview.texture = MushroomPreviewer.Instance.RenderTexture;

            var pulse = UICanvasUtil.NewImage("Pulse", successRT,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                    HollowfenPalette.Gold.b, 0.7f), false);
            _quizSuccessPulse = pulse.GetComponent<Image>();
            UICanvasUtil.RoundifyOutline(_quizSuccessPulse, 90, 3f);
            UICanvasUtil.SetRect((RectTransform)pulse.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(124f, 124f), new Vector2(130f, 145f));

            var seal = UICanvasUtil.NewImage("Seal", successRT,
                new Color(0.25f, 0.36f, 0.24f, 1f), false);
            _quizSuccessSeal = (RectTransform)seal.transform;
            _quizSuccessSealImage = seal.GetComponent<Image>();
            UICanvasUtil.Roundify(_quizSuccessSealImage, 50);
            UICanvasUtil.SetRect(_quizSuccessSeal,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(100f, 100f), new Vector2(130f, 145f));
            var sealOutline = UICanvasUtil.NewImage("GoldRing", _quizSuccessSeal,
                HollowfenPalette.Gold, false);
            UICanvasUtil.RoundifyOutline(sealOutline.GetComponent<Image>(), 48, 3f);
            UICanvasUtil.Stretch((RectTransform)sealOutline.transform);

            // Build the check from rounded UI strokes. The journal font intentionally has a
            // narrow historical glyph set and silently drops Unicode ✓/◆ characters.
            _quizSuccessCheck = UICanvasUtil.NewRect("Checkmark", _quizSuccessSeal);
            UICanvasUtil.SetRect(_quizSuccessCheck,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(76f, 76f), new Vector2(0f, 1f));
            var shortStroke = UICanvasUtil.NewImage("ShortStroke", _quizSuccessCheck,
                HollowfenPalette.Cream, false);
            var shortRT = (RectTransform)shortStroke.transform;
            UICanvasUtil.SetRect(shortRT,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(12f, 32f), new Vector2(-14f, -5f));
            shortRT.localRotation = Quaternion.Euler(0f, 0f, -135f);
            UICanvasUtil.Roundify(shortStroke.GetComponent<Image>(), 8);
            var longStroke = UICanvasUtil.NewImage("LongStroke", _quizSuccessCheck,
                HollowfenPalette.Cream, false);
            var longRT = (RectTransform)longStroke.transform;
            UICanvasUtil.SetRect(longRT,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(12f, 52f), new Vector2(10f, 0f));
            longRT.localRotation = Quaternion.Euler(0f, 0f, -45f);
            UICanvasUtil.Roundify(longStroke.GetComponent<Image>(), 8);

            _quizSuccessTitle = UICanvasUtil.NewHeading("Title", successRT, "", 40f,
                HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_quizSuccessTitle.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-60f, 58f), new Vector2(0f, -82f));
            _quizSuccessTitle.enableAutoSizing = true;
            _quizSuccessTitle.fontSizeMin = 28f;
            _quizSuccessTitle.fontSizeMax = 40f;

            _quizSuccessBody = UICanvasUtil.NewBody("Body", successRT, "", 18f,
                new Color(HollowfenPalette.Parchment.r, HollowfenPalette.Parchment.g,
                    HollowfenPalette.Parchment.b, 0.82f), FontStyles.Normal,
                TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_quizSuccessBody.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-96f, 54f), new Vector2(0f, -146f));
            _quizSuccessBody.textWrappingMode = TextWrappingModes.Normal;
            _quizSuccessBody.overflowMode = TextOverflowModes.Ellipsis;
            _quizSuccessBody.enableAutoSizing = true;
            _quizSuccessBody.fontSizeMin = 14f;
            _quizSuccessBody.fontSizeMax = 18f;

            _quizSuccessProgress = UICanvasUtil.NewEyebrow("CollectionProgress", successRT, "", 13f,
                HollowfenPalette.Gold, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_quizSuccessProgress.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-100f, 26f), new Vector2(0f, -204f));

            _quizSuccessRoot.SetActive(false);
        }

        private void WireQuizNavigation()
        {
            var activeAnswers = _quizAnswers.Where(answer => answer != null && answer.gameObject.activeSelf).ToArray();
            var referenceControls = new[] { _quizPreviousPage, _quizViewFullPage, _quizNextPage }
                .Where(button => button != null && button.gameObject.activeSelf).ToArray();
            Selectable referenceEntry = _quizViewFullPage != null && _quizViewFullPage.gameObject.activeSelf
                ? _quizViewFullPage
                : referenceControls.FirstOrDefault();
            Selectable specimenEntry = _quizInspectSpecimen != null &&
                                       _quizInspectSpecimen.gameObject.activeSelf
                ? _quizInspectSpecimen
                : null;
            for (int i = 0; i < activeAnswers.Length; i++)
            {
                Navigation nav = activeAnswers[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? activeAnswers[i - 1] : specimenEntry ?? _quizBack;
                nav.selectOnDown = i < activeAnswers.Length - 1 ? activeAnswers[i + 1] : _quizBack;
                nav.selectOnLeft = referenceEntry;
                activeAnswers[i].navigation = nav;
            }

            for (int i = 0; i < referenceControls.Length; i++)
            {
                Navigation nav = referenceControls[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnLeft = i > 0
                    ? referenceControls[i - 1]
                    : (activeAnswers.Length > 0 ? activeAnswers[0] : _quizBack);
                nav.selectOnRight = i < referenceControls.Length - 1
                    ? referenceControls[i + 1]
                    : (activeAnswers.Length > 0 ? activeAnswers[0] : _quizBack);
                nav.selectOnUp = _quizBack;
                nav.selectOnDown = activeAnswers.Length > 0 ? activeAnswers[0] : _quizBack;
                referenceControls[i].navigation = nav;
            }
            if (specimenEntry != null)
            {
                Navigation nav = specimenEntry.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = _quizBack;
                nav.selectOnDown = activeAnswers.Length > 0 ? activeAnswers[0] : _quizBack;
                nav.selectOnLeft = referenceEntry;
                nav.selectOnRight = activeAnswers.Length > 0 ? activeAnswers[0] : referenceEntry;
                specimenEntry.navigation = nav;
            }
            Navigation backNav = _quizBack.navigation;
            backNav.mode = Navigation.Mode.Explicit;
            backNav.selectOnUp = activeAnswers.Length > 0 ? activeAnswers[activeAnswers.Length - 1] : null;
            backNav.selectOnDown = specimenEntry ??
                                   (activeAnswers.Length > 0 ? activeAnswers[0] : null);
            backNav.selectOnLeft = referenceEntry;
            _quizBack.navigation = backNav;
        }

        // Inner-frame double-rule: outer thin gold + inner thinner gold, both at panel-edge inset.
        // Rounded outlines (batch-47) — the old 4-strip rectangles read as hard square boxes.
        private static void BuildPanelFrame(RectTransform panelRT, float w, float h)
        {
            MakeInsetOutline(panelRT, "FrameOuter", 14f, HollowfenPalette.GoldFaint, 1.5f, 16);
            MakeInsetOutline(panelRT, "FrameInner", 22f, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.22f), 1f, 12);
        }

        private static void MakeInsetOutline(RectTransform parent, string name, float inset, Color color, float thickness, int radius)
        {
            var go = UICanvasUtil.NewImage(name, parent, color, false);
            UICanvasUtil.RoundifyOutline(go.GetComponent<Image>(), radius, thickness);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static TMP_Text MakeStatColumn(RectTransform parent, float anchorX)
        {
            var t = UICanvasUtil.NewBody("Stat", parent, "", 20f, BodyInk,
                TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.TopLeft);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(anchorX, 0f); rt.anchorMax = new Vector2(anchorX + 1f / 3f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(8f, 4f);
            rt.offsetMax = new Vector2(-8f, -10f);
            t.lineSpacing = 4f;
            return t;
        }

        // Solid CTA button (Forage) vs outlined parchment button (Leave). Includes a small glyph pill on the left
        // so users see exactly which controller button maps to it without leaving the screen.
        // Returns the inner glyph TMP_Text via `out` so the caller can hot-swap PS/Xbox/keyboard glyphs.
        private static Button MakeJournalButton(string name, RectTransform parent, string label,
                                                Color bg, Color fg, bool isSolid, Vector2 anchored,
                                                out TMP_Text glyphTextOut)
        {
            var bgGO = new GameObject(name, typeof(RectTransform));
            bgGO.transform.SetParent(parent, false);
            var bgRT = (RectTransform)bgGO.transform;
            bgRT.anchorMin = new Vector2(0.5f, 0.5f); bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.pivot = new Vector2(0.5f, 0.5f);
            bgRT.sizeDelta = new Vector2(316f, 64f);
            bgRT.anchoredPosition = anchored;

            var img = bgGO.AddComponent<Image>();
            img.color = isSolid ? bg : new Color(bg.r, bg.g, bg.b, 0.15f);
            img.raycastTarget = true;
            UICanvasUtil.Roundify(img, 14);

            // Outline ring for the non-solid (Leave) button — rounded hairline (batch-47).
            if (!isSolid)
            {
                var outline = UICanvasUtil.NewImage("Outline", bgRT, HollowfenPalette.GoldFaint, false);
                UICanvasUtil.RoundifyOutline(outline.GetComponent<Image>(), 14, 1.5f);
                outline.transform.SetSiblingIndex(0);
                UICanvasUtil.Stretch((RectTransform)outline.transform);
            }

            var btn = bgGO.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            Color baseCol = img.color;
            cb.normalColor = baseCol;
            cb.highlightedColor = isSolid ? Color.Lerp(bg, Color.white, 0.18f) : new Color(bg.r, bg.g, bg.b, 0.30f);
            cb.selectedColor   = isSolid ? Color.Lerp(bg, Color.white, 0.22f) : new Color(bg.r, bg.g, bg.b, 0.35f);
            cb.pressedColor    = isSolid ? Color.Lerp(bg, Color.black, 0.18f) : new Color(bg.r, bg.g, bg.b, 0.10f);
            cb.disabledColor   = Color.Lerp(baseCol, Color.black, 0.5f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            // Glyph pill on the left
            var glyphGO = UICanvasUtil.NewImage("Glyph", bgRT,
                new Color(0f, 0f, 0f, isSolid ? 0.18f : 0.10f), false);
            UICanvasUtil.Roundify(glyphGO.GetComponent<Image>(), 10);
            var gRT = (RectTransform)glyphGO.transform;
            gRT.anchorMin = new Vector2(0f, 0.5f); gRT.anchorMax = new Vector2(0f, 0.5f);
            gRT.pivot = new Vector2(0f, 0.5f);
            gRT.sizeDelta = new Vector2(36f, 36f);
            gRT.anchoredPosition = new Vector2(14f, 0f);

            var glyphTxt = UICanvasUtil.NewBody("GlyphTxt", glyphGO.transform, "?", 22f, fg,
                TMPro.FontStyles.Bold, TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(glyphTxt.rectTransform);
            glyphTxt.raycastTarget = false;
            glyphTextOut = glyphTxt;

            // Label
            var lblTxt = UICanvasUtil.NewEyebrow("Label", bgRT, label, 18f, fg, TMPro.TextAlignmentOptions.Center);
            var lblRT = lblTxt.rectTransform;
            lblRT.anchorMin = new Vector2(0f, 0f); lblRT.anchorMax = new Vector2(1f, 1f);
            lblRT.pivot = new Vector2(0.5f, 0.5f);
            lblRT.offsetMin = new Vector2(60f, 0f);
            lblRT.offsetMax = new Vector2(-16f, 0f);
            lblTxt.raycastTarget = false;

            return btn;
        }
    }
}
