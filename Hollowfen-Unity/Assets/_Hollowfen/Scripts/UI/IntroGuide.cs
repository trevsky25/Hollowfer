using System.Collections;
using Hollowfen.Audio;
using Hollowfen.Cinematics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // First playable story/objective card: shown once per save after the homecoming intro.
    // A cinematic ink-glass handoff keeps the world visible, foregrounds the LIVE quest,
    // teaches only the controls needed for the next action, and hands play back through one
    // clear "Set out →" action. Voiced via the homecoming VO pipeline.
    // Scene-local singleton on _IntroGuide; builds programmatically on first Show.
    public class IntroGuide : MonoBehaviour
    {
        public static IntroGuide Instance { get; private set; }

        public const string SeenFlag = "intro_guide_seen";

        [SerializeField, Tooltip("Narrator read of the journal passage (optional).")]
        private AudioClip _voiceClip;
        [SerializeField, Tooltip("Mixer reference used to resolve the Master route for independently adjustable voice-over.")]
        private UnityEngine.Audio.AudioMixerGroup _voiceOutput;
        [SerializeField] private float _fadeSeconds = 0.4f;
        [SerializeField, Tooltip("Grace period before dismiss input is accepted — the keypress that ended the narration must not also eat this card.")]
        private float _inputGraceSeconds = 0.7f;

        private bool _built;
        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _card;
        private Vector2 _cardRestingPosition;
        private Button _button;
        private AudioSource _voiceSource;
        private Coroutine _running;
        private float _openedAt;
        private bool _closing;
        private NarrativePresentationSession.Lease _presentationLease;
        private TMP_Text _moveKeys;
        private TMP_Text _interactKeys;
        private TMP_Text _dismissHint;
        private TMP_Text _journalHint;

        public bool IsShowing => _canvas != null && _canvas.gameObject.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            VoiceAudio.Unregister(_voiceSource);
            ReleasePresentation();
            if (Instance == this) Instance = null;
        }

        // Once per save: no-op when already seen, or when the player is past the arrive beat
        // (an old save that never saw the guide mid-village shouldn't get onboarding).
        public void ShowOnce()
        {
            if (Quests.GameScores.HasFlag(SeenFlag)) return;
            if (Quests.QuestManager.IsCompleted("arrive")) return;
            if (IsShowing || _running != null || _presentationLease != null) return;
            BuildIfNeeded();
            _presentationLease = NarrativePresentationSession.Acquire(
                this,
                NarrativePresentationSession.InteractiveNoPause
                    .With(NarrativePresentationSession.Claim.HideGameplayHud));
            _running = StartCoroutine(Run());
        }

        public void Dismiss()
        {
            if (!IsShowing || _closing) return;
            // The same grace that guards the raw poll must guard EventSystem Submit too,
            // or a narration-masher's buffered press burns the once-per-save card unseen
            // (batch-30 fable catch — the button is live at alpha 0).
            if (Time.unscaledTime - _openedAt < _inputGraceSeconds) return;
            _closing = true;
            if (_running != null) { StopCoroutine(_running); _running = null; }
            StartCoroutine(FadeOutAndClose());
        }

        private IEnumerator Run()
        {
            // A held breath after the narration fade before the page turns up.
            yield return new WaitForSecondsRealtime(0.35f);

            RefreshQuestBlock();
            RefreshDeviceHints();
            _canvas.gameObject.SetActive(true);
            _group.alpha = 0f;
            _card.anchoredPosition = _cardRestingPosition + new Vector2(-42f, 0f);
            _card.localScale = Vector3.one * 0.985f;
            _openedAt = Time.unscaledTime;

            if (_voiceClip != null)
            {
                if (_voiceSource == null)
                {
                    _voiceSource = gameObject.AddComponent<AudioSource>();
                    _voiceSource.playOnAwake = false;
                    _voiceSource.spatialBlend = 0f;
                    _voiceSource.priority = 0;   // speech: never virtualized (the vendored world carries ~50 ambient sources)
                }
                VoiceAudio.Configure(_voiceSource, _voiceOutput);
                _voiceSource.clip = _voiceClip;
                _voiceSource.Play();
            }

            var es = EventSystem.current;
            if (es != null)
            {
                es.SetSelectedGameObject(null);
                es.SetSelectedGameObject(_button.gameObject);
            }

            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < _fadeSeconds)
            {
                float u = Mathf.Clamp01((Time.unscaledTime - t0) / _fadeSeconds);
                float ease = 1f - Mathf.Pow(1f - u, 3f);
                _group.alpha = ease;
                _card.anchoredPosition = Vector2.Lerp(_cardRestingPosition + new Vector2(-42f, 0f), _cardRestingPosition, ease);
                _card.localScale = Vector3.one * Mathf.Lerp(0.985f, 1f, ease);
                yield return null;
            }
            _group.alpha = 1f;
            _card.anchoredPosition = _cardRestingPosition;
            _card.localScale = Vector3.one;

            // The first Play() can whiff when the clip finished importing this session
            // (observed in batch-30 verification: source primed, time 0, not playing).
            // One post-fade retry is cheap and covers it.
            if (_voiceSource != null && _voiceSource.clip != null && !_voiceSource.isPlaying && _voiceSource.time <= 0f)
                _voiceSource.Play();

            _running = null;
        }

        private void Update()
        {
            if (!IsShowing) return;
            RefreshDeviceHints();
            if (Time.unscaledTime - _openedAt < _inputGraceSeconds) return;
            // Backstop poll — the button's EventSystem Submit is primary; this catches
            // clicks-elsewhere/space when focus was stolen. Same device set as narration.
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) { Dismiss(); return; }
            var pad = Gamepad.current;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame) { Dismiss(); return; }
        }

        private IEnumerator FadeOutAndClose()
        {
            if (_voiceSource != null) _voiceSource.Stop();
            float t0 = Time.unscaledTime;
            float from = _group.alpha;
            while (Time.unscaledTime - t0 < 0.25f)
            {
                float u = Mathf.Clamp01((Time.unscaledTime - t0) / 0.25f);
                float ease = u * u;
                _group.alpha = Mathf.Lerp(from, 0f, ease);
                _card.anchoredPosition = Vector2.Lerp(_cardRestingPosition, _cardRestingPosition + new Vector2(-24f, 0f), ease);
                _card.localScale = Vector3.one * Mathf.Lerp(1f, 0.99f, ease);
                yield return null;
            }
            _canvas.gameObject.SetActive(false);
            _card.anchoredPosition = _cardRestingPosition;
            _card.localScale = Vector3.one;
            _closing = false;

            Quests.GameScores.SetFlag(SeenFlag);   // persists with the save's score store
            ReleasePresentation();
        }

        // Live quest block — always current, never a hardcoded copy of the objective.
        private TMP_Text _questName, _questObjective;
        private void RefreshQuestBlock()
        {
            var q = Quests.QuestManager.ActiveQuest;
            if (_questName != null)
                _questName.text = q != null ? Localization.Get(q.DisplayNameId) : Localization.Get("quest.arrive.name");
            if (_questObjective != null)
                _questObjective.text = q != null ? Localization.Get(q.ObjectiveTextId) : Localization.Get("quest.arrive.objective");
        }

        // ------------------------------------------------------------------- build
        // The first playable beat is a story handoff, not a settings sheet. Keep the
        // world visible, clear the normal HUD, and present one strong objective with
        // only the two controls needed to act on it.

        private const float CardW = 900f;
        private const float CardH = 520f;
        private const float MarginX = 58f;
        private const float InnerW = CardW - MarginX * 2f;

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            var canvasGo = new GameObject("IntroGuideCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 65;   // above dialogue/HUD (60), below narration (70)
            canvasGo.AddComponent<CanvasScaler>().Init1080();
            canvasGo.AddComponent<GraphicRaycaster>();
            _group = canvasGo.AddComponent<CanvasGroup>();

            // A cinematic left-weighted wash quiets the world without replacing it.
            var scrim = UICanvasUtil.NewImage("Scrim", canvasGo.transform, Color.white, true);
            var scrimImg = scrim.GetComponent<Image>();
            scrimImg.sprite = UICanvasUtil.MakeHorizontalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0f,    new Color(0.015f, 0.022f, 0.016f, 0.92f)),
                new UICanvasUtil.GradientStop(0.48f, new Color(0.015f, 0.022f, 0.016f, 0.64f)),
                new UICanvasUtil.GradientStop(0.78f, new Color(0.015f, 0.022f, 0.016f, 0.24f)),
                new UICanvasUtil.GradientStop(1f,    new Color(0.015f, 0.022f, 0.016f, 0.10f)),
            });
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            // Quiet ink-glass surface: one rounded silhouette, one accent rail, no frame stack.
            _card = UICanvasUtil.NewRect("StoryObjectiveCard", canvasGo.transform);
            _card.anchorMin = new Vector2(0f, 0.5f);
            _card.anchorMax = new Vector2(0f, 0.5f);
            _card.pivot = new Vector2(0f, 0.5f);
            _card.sizeDelta = new Vector2(CardW, CardH);
            _cardRestingPosition = new Vector2(96f, 0f);
            _card.anchoredPosition = _cardRestingPosition;
            // Do not add a generated sibling shadow or a full-rect depth wash here. Both reveal
            // rectangular bounds around the rounded card against the live world. The left scrim,
            // opaque ink surface, and gold rail already provide the intended depth and hierarchy.
            var cardFill = _card.gameObject.AddComponent<Image>();
            cardFill.sprite = UICanvasUtil.RoundedRect(26);
            cardFill.type = Image.Type.Sliced;
            cardFill.color = new Color(HollowfenPalette.SurfaceBase.r, HollowfenPalette.SurfaceBase.g,
                HollowfenPalette.SurfaceBase.b, 0.965f);
            cardFill.raycastTarget = false;

            var rail = UICanvasUtil.NewImage("ChapterRail", _card, HollowfenPalette.FocusRail, false);
            var railImg = rail.GetComponent<Image>();
            railImg.sprite = UICanvasUtil.RoundedRect(2);
            railImg.type = Image.Type.Sliced;
            UICanvasUtil.SetRect((RectTransform)rail.transform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f), new Vector2(4f, -52f), new Vector2(0f, 0f));

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", _card, Localization.Get("guide.eyebrow"), 18f, HollowfenPalette.Gold);
            Place(eyebrow.rectTransform, MarginX, -42f, 480f, 20f);
            var status = UICanvasUtil.NewEyebrow("Status", _card, Localization.Get("guide.status"), 18f,
                new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.52f),
                TextAlignmentOptions.TopRight);
            Place(status.rectTransform, CardW - MarginX - 250f, -44f, 250f, 18f);

            var title = UICanvasUtil.NewHeading("Title", _card, Localization.Get("guide.title"), 48f,
                HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(title.rectTransform, MarginX, -72f, InnerW, 60f);

            var passage = UICanvasUtil.NewBody("Passage", _card, Localization.Get("guide.passage"), 20.5f,
                new Color(HollowfenPalette.Parchment.r, HollowfenPalette.Parchment.g, HollowfenPalette.Parchment.b, 0.86f),
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            passage.lineSpacing = 8f;
            Place(passage.rectTransform, MarginX, -142f, InnerW - 28f, 92f);

            var divider = UICanvasUtil.NewImage("Divider", _card, HollowfenPalette.DividerLine, false);
            Place((RectTransform)divider.transform, MarginX, -252f, InnerW, 1f);

            var taskLabel = UICanvasUtil.NewEyebrow("TaskLabel", _card, Localization.Get("guide.task_label"), 18f,
                HollowfenPalette.Gold);
            Place(taskLabel.rectTransform, MarginX, -276f, 260f, 18f);

            var step = UICanvasUtil.NewImage("Step", _card, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                HollowfenPalette.Gold.b, 0.14f), false);
            var stepImg = step.GetComponent<Image>();
            stepImg.sprite = UICanvasUtil.Circle();
            UICanvasUtil.SetRect((RectTransform)step.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(48f, 48f), new Vector2(MarginX, -304f));
            var stepNumber = UICanvasUtil.NewEyebrow("Number", step.transform, "01", 18f, HollowfenPalette.Gold,
                TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(stepNumber.rectTransform);

            _questName = UICanvasUtil.NewHeading("QuestName", _card, "", 27f, HollowfenPalette.Cream,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(_questName.rectTransform, MarginX + 66f, -300f, InnerW - 66f, 34f);
            _questObjective = UICanvasUtil.NewBody("QuestObjective", _card, "", 18f,
                new Color(HollowfenPalette.Parchment.r, HollowfenPalette.Parchment.g, HollowfenPalette.Parchment.b, 0.76f),
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(_questObjective.rectTransform, MarginX + 66f, -337f, InnerW - 66f, 30f);

            _moveKeys = BuildControlCue(_card, MarginX, -382f, "", Localization.Get("guide.ctl.move"));
            _interactKeys = BuildControlCue(_card, MarginX + 260f, -382f, "",
                Localization.Get("guide.ctl.interact"));

            // A single, unmistakable action hands control back to the player.
            var btnRt = UICanvasUtil.NewRect("SetOutButton", _card);
            btnRt.anchorMin = new Vector2(0f, 0f); btnRt.anchorMax = new Vector2(0f, 0f);
            btnRt.pivot = new Vector2(0f, 0f);
            btnRt.sizeDelta = new Vector2(300f, 58f);
            btnRt.anchoredPosition = new Vector2(MarginX, 34f);

            var btnFill = btnRt.gameObject.AddComponent<Image>();
            btnFill.sprite = UICanvasUtil.RoundedRect(14);
            btnFill.type = Image.Type.Sliced;
            btnFill.color = HollowfenPalette.Gold;
            btnFill.raycastTarget = true;

            _button = btnRt.gameObject.AddComponent<Button>();
            _button.transition = Selectable.Transition.None;
            _button.targetGraphic = btnFill;
            _button.onClick.AddListener(Dismiss);
            var nav = _button.navigation; nav.mode = Navigation.Mode.None; _button.navigation = nav;

            var btnLabel = UICanvasUtil.NewHeading("Label", btnRt, Localization.Get("guide.button"), 24f,
                HollowfenPalette.InkDeep, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(btnLabel.rectTransform);

            var glowGo = UICanvasUtil.NewImage("FocusGlow", btnRt, new Color(1f, 1f, 1f, 0.16f), false);
            var glowImg = glowGo.GetComponent<Image>();
            glowImg.sprite = UICanvasUtil.RoundedRect(14);
            glowImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)glowGo.transform);
            glowGo.transform.SetSiblingIndex(0); // wash under the dark label, never over it

            var fh = btnRt.gameObject.AddComponent<FocusHighlight>();
            fh.Configure(glowImg, btnRt, new Color(1f, 1f, 1f, 0.28f), 1.025f, true, true);

            _dismissHint = UICanvasUtil.NewEyebrow("DismissHint", _card, "", 18f,
                new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.52f));
            PlaceBottom(_dismissHint.rectTransform, MarginX + 322f, 49f, 160f, 18f);
            _journalHint = UICanvasUtil.NewBody("JournalHint", _card, "", 18f,
                new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.55f),
                FontStyles.Italic, TextAlignmentOptions.Right);
            PlaceBottom(_journalHint.rectTransform, CardW - MarginX - 280f, 48f, 280f, 22f);

            RefreshDeviceHints();

            _canvas.gameObject.SetActive(false);
        }

        private void RefreshDeviceHints()
        {
            if (!_built) return;
            bool gamepad = ControllerGlyphs.IsGamepadActive;
            string move = Localization.Get(gamepad ? "guide.key.lstick" : "guide.key.wasd");
            string interact = gamepad
                ? ControllerGlyphs.For(ControllerGlyphs.Face.North)
                : Localization.Get("guide.key.e");
            string dismiss = gamepad
                ? string.Format(Localization.Get("guide.dismiss_hint.gamepad"),
                    ControllerGlyphs.For(ControllerGlyphs.Face.South))
                : Localization.Get("guide.dismiss_hint.keyboard");
            string journal = Localization.Get(gamepad ? "guide.footer.gamepad" : "guide.footer.keyboard");

            if (_moveKeys != null && _moveKeys.text != move) _moveKeys.text = move;
            if (_interactKeys != null && _interactKeys.text != interact) _interactKeys.text = interact;
            if (_dismissHint != null && _dismissHint.text != dismiss) _dismissHint.text = dismiss;
            if (_journalHint != null && _journalHint.text != journal) _journalHint.text = journal;
        }

        private static TMP_Text BuildControlCue(RectTransform parent, float x, float y, string keys, string label)
        {
            var cue = UICanvasUtil.NewRect("ControlCue", parent);
            Place(cue, x, y, 238f, 42f);
            var bg = cue.gameObject.AddComponent<Image>();
            bg.sprite = UICanvasUtil.RoundedRect(11);
            bg.type = Image.Type.Sliced;
            bg.color = HollowfenPalette.SurfaceQuiet;
            bg.raycastTarget = false;

            var keyText = UICanvasUtil.NewBody("Keys", cue, keys, 18f, HollowfenPalette.Gold,
                FontStyles.Normal, TextAlignmentOptions.Left);
            keyText.textWrappingMode = TextWrappingModes.NoWrap;
            Place(keyText.rectTransform, 14f, -9f, 112f, 24f);
            var action = UICanvasUtil.NewEyebrow("Action", cue, label, 18f,
                new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.76f),
                TextAlignmentOptions.Right);
            Place(action.rectTransform, 128f, -9f, 96f, 24f);
            return keyText;
        }

        private void ReleasePresentation()
        {
            _presentationLease?.Dispose();
            _presentationLease = null;
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private static void PlaceBottom(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }
    }
}
