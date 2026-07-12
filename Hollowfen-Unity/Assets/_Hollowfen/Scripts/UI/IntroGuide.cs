using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // First-steps journal page (batch-30): shown once per save after the homecoming intro
    // narration. Not a tutorial toast — a parchment journal card in Wren's voice framing the
    // first task, with the LIVE quest pulled from QuestManager, orientation hints, a compact
    // controls line, and a single "Set out →" action. Voiced via the batch-29 VO pipeline.
    // Scene-local singleton on _IntroGuide; builds programmatically on first Show.
    public class IntroGuide : MonoBehaviour
    {
        public static IntroGuide Instance { get; private set; }

        public const string SeenFlag = "intro_guide_seen";

        [SerializeField, Tooltip("Narrator read of the journal passage (optional).")]
        private AudioClip _voiceClip;
        [SerializeField, Tooltip("Mixer group for the voice read (SFX until the Voice group exists).")]
        private UnityEngine.Audio.AudioMixerGroup _voiceOutput;
        [SerializeField] private float _fadeSeconds = 0.4f;
        [SerializeField, Tooltip("Grace period before dismiss input is accepted — the keypress that ended the narration must not also eat this card.")]
        private float _inputGraceSeconds = 0.7f;

        // Paper-surface palette (parchment card, walnut ink — the pause/dialogue paper family).
        private static readonly Color Paper     = new Color(0.902f, 0.867f, 0.788f, 0.985f);
        private static readonly Color Ink       = new Color(0.160f, 0.130f, 0.095f, 1f);
        private static readonly Color InkSoft   = new Color(0.160f, 0.130f, 0.095f, 0.82f);
        private static readonly Color Bronze    = new Color(0.520f, 0.385f, 0.110f, 1f);
        private static readonly Color MossDark  = new Color(0.365f, 0.365f, 0.290f, 1f);

        private bool _built;
        private Canvas _canvas;
        private CanvasGroup _group;
        private Button _button;
        private AudioSource _voiceSource;
        private Coroutine _running;
        private float _openedAt;
        private bool _closing;

        public bool IsShowing => _canvas != null && _canvas.gameObject.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            // Scene unload while showing must not leak the static suspend flags.
            if (IsShowing)
            {
                Foraging.PlayerInteractor.Suspended = false;
                Foraging.PlayerInteractor.SetPlayerInputEnabled(true);
            }
            if (Instance == this) Instance = null;
        }

        // Once per save: no-op when already seen, or when the player is past the arrive beat
        // (an old save that never saw the guide mid-village shouldn't get onboarding).
        public void ShowOnce()
        {
            if (Quests.GameScores.HasFlag(SeenFlag)) return;
            if (Quests.QuestManager.IsCompleted("arrive")) return;
            if (IsShowing) return;
            BuildIfNeeded();
            if (_running != null) StopCoroutine(_running);
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

            Foraging.PlayerInteractor.Suspended = true;
            Foraging.PlayerInteractor.SetPlayerInputEnabled(false);

            RefreshQuestBlock();
            _canvas.gameObject.SetActive(true);
            _group.alpha = 0f;
            _openedAt = Time.unscaledTime;

            if (_voiceClip != null)
            {
                if (_voiceSource == null)
                {
                    _voiceSource = gameObject.AddComponent<AudioSource>();
                    _voiceSource.playOnAwake = false;
                    _voiceSource.spatialBlend = 0f;
                    _voiceSource.priority = 0;   // speech: never virtualized (the vendored world carries ~50 ambient sources)
                    _voiceSource.outputAudioMixerGroup = _voiceOutput;
                }
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
                _group.alpha = Mathf.Clamp01((Time.unscaledTime - t0) / _fadeSeconds);
                yield return null;
            }
            _group.alpha = 1f;

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
                _group.alpha = Mathf.Lerp(from, 0f, (Time.unscaledTime - t0) / 0.25f);
                yield return null;
            }
            _canvas.gameObject.SetActive(false);
            _closing = false;

            Quests.GameScores.SetFlag(SeenFlag);   // persists with the save's score store
            Foraging.PlayerInteractor.Suspended = false;
            Foraging.PlayerInteractor.SetPlayerInputEnabled(true);
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

            // Dim the world; block clicks behind the card.
            var scrim = UICanvasUtil.NewImage("Scrim", canvasGo.transform, new Color(0.03f, 0.027f, 0.024f, 0.45f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            // The journal page.
            var card = UICanvasUtil.NewRect("Card", canvasGo.transform);
            card.sizeDelta = new Vector2(760f, 612f);
            UICanvasUtil.AddShadow(card, 22, 30, 0.45f, -10f);
            UICanvasUtil.MakeRoundedPanel(card, Paper, 22, 0.5f);

            float x = 64f, w = 760f - 128f;

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", card, Localization.Get("guide.eyebrow"), 13f, Bronze);
            Place(eyebrow.rectTransform, x, -44f, w, 20f);

            var title = UICanvasUtil.NewHeading("Title", card, Localization.Get("guide.title"), 34f, Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(title.rectTransform, x, -68f, w, 44f);

            var passage = UICanvasUtil.NewBody("Passage", card, Localization.Get("guide.passage"), 16.5f, InkSoft, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            passage.lineSpacing = 8f;
            Place(passage.rectTransform, x, -124f, w, 122f);

            var rule = UICanvasUtil.NewImage("Rule", card, new Color(Bronze.r, Bronze.g, Bronze.b, 0.45f), false);
            Place((RectTransform)rule.transform, x, -258f, w, 1.5f);

            var taskLabel = UICanvasUtil.NewEyebrow("TaskLabel", card, Localization.Get("guide.task_label"), 12f, Bronze);
            Place(taskLabel.rectTransform, x, -276f, w, 18f);

            _questName = UICanvasUtil.NewHeading("QuestName", card, "", 25f, Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(_questName.rectTransform, x, -298f, w, 32f);

            _questObjective = UICanvasUtil.NewBody("QuestObjective", card, "", 16f, InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(_questObjective.rectTransform, x, -334f, w, 24f);

            string[] hintKeys = { "guide.hint.compass", "guide.hint.ribbon", "guide.hint.journal" };
            float hy = -376f;
            foreach (var key in hintKeys)
            {
                var dot = UICanvasUtil.NewBody("Dot", card, "·", 16f, Bronze, FontStyles.Bold, TextAlignmentOptions.TopLeft);
                Place(dot.rectTransform, x, hy, 14f, 22f);
                var hint = UICanvasUtil.NewBody("Hint", card, Localization.Get(key), 14.5f, InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                Place(hint.rectTransform, x + 18f, hy, w - 18f, 22f);
                hy -= 27f;
            }

            var controls = UICanvasUtil.NewBody("Controls", card, Localization.Get("guide.controls"), 12.5f, MossDark, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            controls.lineSpacing = 6f;
            Place(controls.rectTransform, x, hy - 10f, w, 44f);

            // "Set out →" — the main menu's action grammar.
            var btnRt = UICanvasUtil.NewRect("SetOutButton", card);
            btnRt.anchorMin = new Vector2(0.5f, 0f); btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.sizeDelta = new Vector2(300f, 54f);
            btnRt.anchoredPosition = new Vector2(0f, 30f);

            var btnFill = btnRt.gameObject.AddComponent<Image>();
            btnFill.sprite = UICanvasUtil.RoundedRect(12);
            btnFill.type = Image.Type.Sliced;
            btnFill.color = new Color(Bronze.r, Bronze.g, Bronze.b, 0.12f);
            btnFill.raycastTarget = true;

            _button = btnRt.gameObject.AddComponent<Button>();
            _button.transition = Selectable.Transition.None;
            _button.targetGraphic = btnFill;
            _button.onClick.AddListener(Dismiss);
            var nav = _button.navigation; nav.mode = Navigation.Mode.None; _button.navigation = nav;

            var btnLabel = UICanvasUtil.NewHeading("Label", btnRt, Localization.Get("guide.button"), 25f, Ink, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(btnLabel.rectTransform);

            var glowGo = UICanvasUtil.NewImage("FocusGlow", btnRt, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f), false);
            var glowImg = glowGo.GetComponent<Image>();
            glowImg.sprite = UICanvasUtil.RoundedRect(12);
            glowImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)glowGo.transform);

            var fh = btnRt.gameObject.AddComponent<FocusHighlight>();
            var fhT = typeof(FocusHighlight);
            System.Action<string, object> setF = (n, v) =>
            {
                var f = fhT.GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (f != null) f.SetValue(fh, v);
            };
            setF("_targetGraphic", glowImg);
            setF("_baseColor", new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f));
            setF("_focusedColor", new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.22f));
            setF("_focusedScale", 1.03f);
            setF("_swapColor", true);
            setF("_swapScale", true);

            var hint2 = UICanvasUtil.NewBody("DismissHint", card, Localization.Get("guide.dismiss_hint"), 11.5f, MossDark, FontStyles.Italic, TextAlignmentOptions.Center);
            var h2 = hint2.rectTransform;
            h2.anchorMin = new Vector2(0.5f, 0f); h2.anchorMax = new Vector2(0.5f, 0f);
            h2.pivot = new Vector2(0.5f, 0f);
            h2.sizeDelta = new Vector2(400f, 16f);
            h2.anchoredPosition = new Vector2(0f, 12f);

            _canvas.gameObject.SetActive(false);
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }
    }
}
