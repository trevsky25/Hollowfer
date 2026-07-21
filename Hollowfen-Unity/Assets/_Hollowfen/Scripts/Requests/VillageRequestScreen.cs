using Hollowfen.Audio;
using Hollowfen.Apothecary;
using Hollowfen.Cinematics;
using Hollowfen.Dialogue;
using Hollowfen.Foraging;
using Hollowfen.Input;
using Hollowfen.Items;
using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Hollowfen.Requests
{
    /// <summary>A controller-first illustrated order card shared by all request-giving NPCs.</summary>
    public sealed class VillageRequestScreen : MonoBehaviour
    {
        public static VillageRequestScreen Instance { get; private set; }

        private readonly TMP_Text[] _requirementNames = new TMP_Text[4];
        private readonly TMP_Text[] _requirementCounts = new TMP_Text[4];
        private readonly Image[] _requirementDots = new Image[4];
        private Canvas _canvas;
        private CanvasGroup _group;
        private Image _hero;
        private AspectRatioFitter _heroAspect;
        private TMP_Text _eyebrow;
        private TMP_Text _title;
        private TMP_Text _description;
        private TMP_Text _requesterLine;
        private TMP_Text _reward;
        private Button _deliverButton;
        private Button _talkButton;
        private Button _leaveButton;
        private TMP_Text _deliverLabel;
        private TMP_Text _talkLabel;
        private TMP_Text _leaveLabel;
        private InputActions _input;
        private VillageRequestData _request;
        private DialogueData _fallbackDialogue;
        private Transform _anchor;
        private string _npcName;
        private NarrativePresentationSession.Lease _presentationLease;
        private bool _completed;

        public bool IsOpen => _canvas != null && _canvas.enabled;

        public static VillageRequestScreen Ensure()
        {
            if (Instance != null) return Instance;
            return new GameObject("_VillageRequestScreen").AddComponent<VillageRequestScreen>();
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
            InventoryRuntime.OnChanged += HandleInventoryChanged;
            ApothecaryRuntime.OnChanged += HandleApothecaryChanged;
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.UI.Cancel.performed -= OnCancel;
                _input.UI.Disable();
            }
            InventoryRuntime.OnChanged -= HandleInventoryChanged;
            ApothecaryRuntime.OnChanged -= HandleApothecaryChanged;
        }

        private void OnDestroy()
        {
            ReleasePresentation();
            if (Instance == this) Instance = null;
            _input?.Dispose();
        }

        public void Open(VillageRequestData request, string npcName, Transform anchor,
            DialogueData fallbackDialogue)
        {
            if (request == null || IsOpen) return;
            _request = request;
            _npcName = npcName;
            _anchor = anchor;
            _fallbackDialogue = fallbackDialogue;
            _completed = false;
            VillageRequests.Track(request);
            VillageRequestTrackerHUD.Ensure();

            _presentationLease = NarrativePresentationSession.Acquire(
                this, NarrativePresentationSession.Modal);

            EnsureEventSystem();
            Populate();
            _canvas.enabled = true;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            SelectDefault();
        }

        public void Close()
        {
            if (!IsOpen) return;
            _canvas.enabled = false;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _request = null;
            _fallbackDialogue = null;
            _anchor = null;
            ReleasePresentation();
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void ReleasePresentation()
        {
            _presentationLease?.Dispose();
            _presentationLease = null;
        }

        private void Populate()
        {
            if (_request == null) return;
            _eyebrow.text = string.Format(Localization.Get("format.pair"),
                Localization.Get(KindLabelId(_request.Kind)), _npcName).ToUpperInvariant();
            _title.text = Localization.Get(_request.TitleId);
            _description.text = Localization.Get(_request.DescriptionId);
            _requesterLine.text = string.Format(Localization.Get("format.quote"),
                Localization.Get(_request.RequesterLineId));
            _hero.sprite = ResolveHero(_request);
            _hero.enabled = _hero.sprite != null;
            if (_hero.sprite != null)
            {
                var rect = _hero.sprite.rect;
                _heroAspect.aspectRatio = rect.height > 0f ? rect.width / rect.height : 1f;
            }

            for (int i = 0; i < _requirementNames.Length; i++)
            {
                bool raw = i < _request.RequirementCount;
                int preparationIndex = i - _request.RequirementCount;
                bool prepared = preparationIndex >= 0 &&
                    preparationIndex < _request.PreparationRequirementCount;
                bool active = raw && _request.RequiredSpecies[i] != null || prepared &&
                    _request.RequiredPreparations[preparationIndex] != null;
                _requirementNames[i].transform.parent.gameObject.SetActive(active);
                if (!active) continue;
                int need;
                int have;
                string displayName;
                if (raw)
                {
                    var species = _request.RequiredSpecies[i];
                    need = _request.RequiredCountAt(i);
                    have = InventoryRuntime.GetCount(species);
                    displayName = JournalText.MushroomName(species);
                }
                else
                {
                    var recipe = _request.RequiredPreparations[preparationIndex];
                    need = _request.RequiredPreparationCountAt(preparationIndex);
                    have = ApothecaryRuntime.ProductCount(recipe.ResultId);
                    displayName = Localization.Get(recipe.ResultNameId);
                }
                bool ready = have >= need;
                _requirementNames[i].text = displayName;
                _requirementCounts[i].text = Mathf.Min(have, need) + " / " + need;
                _requirementCounts[i].color = ready ? HollowfenPalette.PaperSuccessInk : new Color(0.48f, 0.28f, 0.18f, 1f);
                _requirementDots[i].color = ready ? HollowfenPalette.Moss : new Color(0.55f, 0.47f, 0.34f, 0.45f);
            }

            int rewardCopper = VillageRequests.RewardFor(_request);
            _reward.text = rewardCopper > 0
                ? string.Format(Localization.Get("request.reward.copper"), CoinPurse.Format(rewardCopper))
                : Localization.Get("request.reward.story");
            _deliverButton.interactable = VillageRequests.CanDeliver(_request);
            _deliverLabel.text = _deliverButton.interactable
                ? Localization.Get("request.deliver")
                : Localization.Get("request.missing");
            _talkButton.gameObject.SetActive(_fallbackDialogue != null);
            _talkLabel.text = Localization.Get("request.talk_instead");
            _leaveLabel.text = Localization.Get("request.leave");
            WireNavigation();
        }

        private void ShowCompleted(VillageRequests.CompletionResult result)
        {
            _completed = true;
            _eyebrow.text = Localization.Get("request.completed.eyebrow");
            _title.text = Localization.Get("request.completed.title");
            _description.text = result.Copper > 0
                ? string.Format(Localization.Get("request.completed.body"), CoinPurse.Format(result.Copper))
                : Localization.Get("request.completed.story_body");
            _requesterLine.text = result.FirstCompletion
                ? Localization.Get("request.completed.first")
                : Localization.Get("request.completed.repeat");
            _reward.text = "";
            for (int i = 0; i < _requirementNames.Length; i++)
                _requirementNames[i].transform.parent.gameObject.SetActive(false);
            _deliverButton.gameObject.SetActive(false);
            _talkButton.gameObject.SetActive(false);
            _leaveLabel.text = Localization.Get("request.continue");
            _leaveButton.navigation = new Navigation { mode = Navigation.Mode.None };
            EventSystem.current?.SetSelectedGameObject(_leaveButton.gameObject);
        }

        private void Deliver()
        {
            if (_request == null || _completed) return;
            var request = _request;
            var anchor = _anchor;
            var result = VillageRequests.Complete(request);
            if (!result.Success)
            {
                UISfx.Error();
                Populate();
                SelectDefault();
                return;
            }
            GameplaySfx.DeliveryComplete();
            if (request.CompletionDialogue != null && DialogueScreen.Instance != null)
            {
                Close();
                DialogueScreen.Instance.Open(request.CompletionDialogue, anchor);
                return;
            }
            ShowCompleted(result);
        }

        private void TalkInstead()
        {
            var dialog = _fallbackDialogue;
            var anchor = _anchor;
            Close();
            if (dialog != null && DialogueScreen.Instance != null)
                DialogueScreen.Instance.Open(dialog, anchor);
        }

        private void SelectDefault()
        {
            if (EventSystem.current == null) return;
            if (_deliverButton.interactable) EventSystem.current.SetSelectedGameObject(_deliverButton.gameObject);
            else if (_talkButton.gameObject.activeSelf) EventSystem.current.SetSelectedGameObject(_talkButton.gameObject);
            else EventSystem.current.SetSelectedGameObject(_leaveButton.gameObject);
        }

        private void HandleInventoryChanged(string id, int count)
        {
            if (IsOpen && !_completed) Populate();
        }

        private void HandleApothecaryChanged()
        {
            if (IsOpen && !_completed) Populate();
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            if (IsOpen) Close();
        }

        private void Update()
        {
            if (!IsOpen) return;
            GameObject preferred = _completed
                ? _leaveButton.gameObject
                : _deliverButton.interactable
                    ? _deliverButton.gameObject
                    : _talkButton.gameObject.activeInHierarchy
                        ? _talkButton.gameObject
                        : _leaveButton.gameObject;
            UIFocusRecovery.RestoreIfLost(transform, preferred);
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 88;
            gameObject.AddComponent<CanvasScaler>().Init1080();
            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();

            var scrim = UICanvasUtil.NewImage("Scrim", transform, new Color(0.018f, 0.016f, 0.012f, 0.84f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);
            var card = UICanvasUtil.NewRect("RequestCard", transform);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(1340f, 790f);
            UICanvasUtil.MakeRoundedPanel(card, new Color(0.905f, 0.87f, 0.79f, 1f), 26, 0.72f);
            UICanvasUtil.AddShadow(card, 25, 34, 0.58f, -12f);

            var photoFrame = UICanvasUtil.NewRect("IllustrationFrame", card);
            UICanvasUtil.SetRect(photoFrame, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f), new Vector2(500f, -80f), new Vector2(44f, 0f));
            var frameBg = photoFrame.gameObject.AddComponent<Image>();
            frameBg.sprite = UICanvasUtil.RoundedRect(18);
            frameBg.type = Image.Type.Sliced;
            frameBg.color = new Color(0.10f, 0.08f, 0.055f, 0.96f);
            var photoMask = photoFrame.gameObject.AddComponent<RectMask2D>();
            photoMask.padding = new Vector4(8f, 8f, 8f, 8f);
            var heroGo = new GameObject("StoryImage", typeof(RectTransform));
            heroGo.transform.SetParent(photoFrame, false);
            _hero = heroGo.AddComponent<Image>();
            _hero.preserveAspect = true;
            _hero.color = Color.white;
            _heroAspect = heroGo.AddComponent<AspectRatioFitter>();
            _heroAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            UICanvasUtil.Stretch((RectTransform)heroGo.transform);

            var content = UICanvasUtil.NewRect("Content", card);
            UICanvasUtil.SetRect(content, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(-620f, -80f), new Vector2(280f, 0f));
            _eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", content, "", 14f,
                HollowfenPalette.PaperAccentInk, TextAlignmentOptions.TopLeft);
            PlaceTop(_eyebrow.rectTransform, 0f, 0f, 700f, 26f);
            _title = UICanvasUtil.NewHeading("Title", content, "", 44f,
                HollowfenPalette.InkDeep, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            _title.textWrappingMode = TextWrappingModes.Normal;
            PlaceTop(_title.rectTransform, 0f, -34f, 700f, 72f);
            _description = UICanvasUtil.NewBody("Description", content, "", 20f,
                new Color(0.20f, 0.17f, 0.12f, 0.88f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _description.textWrappingMode = TextWrappingModes.Normal;
            PlaceTop(_description.rectTransform, 0f, -116f, 700f, 90f);
            _requesterLine = UICanvasUtil.NewBody("RequesterLine", content, "", 18f,
                HollowfenPalette.PaperAccentInk,
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            _requesterLine.textWrappingMode = TextWrappingModes.Normal;
            PlaceTop(_requesterLine.rectTransform, 0f, -210f, 700f, 66f);

            var reqHeader = UICanvasUtil.NewEyebrow("RequirementsHeader", content,
                Localization.Get("request.requirements"), 14.5f, HollowfenPalette.PaperAccentInk, TextAlignmentOptions.TopLeft);
            PlaceTop(reqHeader.rectTransform, 0f, -292f, 700f, 22f);
            for (int i = 0; i < _requirementNames.Length; i++) BuildRequirementRow(content, i);

            _reward = UICanvasUtil.NewBody("Reward", content, "", 18f,
                HollowfenPalette.InkDeep, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            PlaceTop(_reward.rectTransform, 0f, -522f, 700f, 34f);

            _deliverButton = BuildButton(content, "Deliver", out _deliverLabel, 0f, true);
            _talkButton = BuildButton(content, "TalkInstead", out _talkLabel, 234f, false);
            _leaveButton = BuildButton(content, "Leave", out _leaveLabel, 468f, false);
            _deliverButton.onClick.AddListener(Deliver);
            _talkButton.onClick.AddListener(TalkInstead);
            _leaveButton.onClick.AddListener(Close);
        }

        private void BuildRequirementRow(RectTransform parent, int index)
        {
            var row = UICanvasUtil.NewRect("Requirement_" + index, parent);
            PlaceTop(row, 0f, -320f - index * 48f, 700f, 42f);
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = UICanvasUtil.RoundedRect(9);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.18f, 0.15f, 0.10f, 0.055f);
            var dotGo = UICanvasUtil.NewImage("State", row, Color.white, false);
            _requirementDots[index] = dotGo.GetComponent<Image>();
            _requirementDots[index].sprite = UICanvasUtil.RoundedRect(8);
            UICanvasUtil.SetRect((RectTransform)dotGo.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(12f, 12f), new Vector2(16f, 0f));
            _requirementNames[index] = UICanvasUtil.NewBody("Species", row, "", 18f,
                HollowfenPalette.InkDeep, FontStyles.Normal, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(_requirementNames[index].rectTransform, Vector2.zero, Vector2.one,
                new Vector2(0f, 0.5f), new Vector2(-100f, 0f), new Vector2(42f, 0f));
            _requirementCounts[index] = UICanvasUtil.NewHeading("Count", row, "", 18f,
                HollowfenPalette.InkDeep, FontStyles.Normal, TextAlignmentOptions.Right);
            UICanvasUtil.SetRect(_requirementCounts[index].rectTransform, new Vector2(1f, 0f), Vector2.one,
                new Vector2(1f, 0.5f), new Vector2(92f, 0f), new Vector2(-16f, 0f));
        }

        private Button BuildButton(RectTransform parent, string name, out TMP_Text label, float x, bool accent)
        {
            var rt = UICanvasUtil.NewRect(name, parent);
            PlaceTop(rt, x, -584f, 214f, 54f);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = UICanvasUtil.RoundedRect(13);
            image.type = Image.Type.Sliced;
            image.color = accent ? HollowfenPalette.Gold : new Color(0.18f, 0.15f, 0.10f, 0.09f);
            label = UICanvasUtil.NewHeading("Label", rt, "", 17f,
                accent ? HollowfenPalette.InkDeep : HollowfenPalette.InkDeep,
                FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(label.rectTransform);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var focus = rt.gameObject.AddComponent<FocusHighlight>();
            focus.Configure(image, rt, accent ? new Color(0.94f, 0.83f, 0.48f, 1f) :
                new Color(0.58f, 0.44f, 0.16f, 0.25f), 1.025f, true, true);
            return button;
        }

        private void WireNavigation()
        {
            var visible = _talkButton.gameObject.activeSelf;
            _deliverButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = visible ? _talkButton : _leaveButton,
                selectOnLeft = _leaveButton,
            };
            _talkButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = _leaveButton,
                selectOnLeft = _deliverButton.interactable ? _deliverButton : _leaveButton,
            };
            _leaveButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = _deliverButton.interactable ? _deliverButton : (visible ? _talkButton : _leaveButton),
                selectOnLeft = visible ? _talkButton : (_deliverButton.interactable ? _deliverButton : _leaveButton),
            };
        }

        private static void PlaceTop(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private static string KindLabelId(VillageRequestKind kind)
        {
            switch (kind)
            {
                case VillageRequestKind.Medicine: return "request.kind.medicine";
                case VillageRequestKind.Market: return "request.kind.market";
                case VillageRequestKind.Gathering: return "request.kind.gathering";
                default: return "request.kind.kitchen";
            }
        }

        private static Sprite ResolveHero(VillageRequestData request)
        {
            if (request == null) return null;
            if (request.HeroImage != null) return request.HeroImage;
            if (request.RequirementCount > 0 && request.RequiredSpecies[0] != null)
                return request.RequiredSpecies[0].Photo;
            if (request.PreparationRequirementCount <= 0 || request.RequiredPreparations[0] == null)
                return null;
            var species = request.RequiredPreparations[0].HeroSpecies;
            return species != null ? species.Photo != null ? species.Photo : species.JournalPage : null;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
