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
        //
        // Batch-31 restyle: an offset-LEFT journal page — the east road and Wren stay
        // visible on the right (the card invites the player INTO the world, it shouldn't
        // hide it). Paper grain + inset gold frame + ledger double-rule + the task as an
        // inset entry + keycap chips for controls. Behavior/flow untouched from batch-30.

        private const float CardW = 760f;
        private const float CardH = 660f;
        private const float MarginX = 60f;
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

            // Left-weighted scrim: dark under the page, nearly clear over the world.
            var scrim = UICanvasUtil.NewImage("Scrim", canvasGo.transform, Color.white, true);
            var scrimImg = scrim.GetComponent<Image>();
            scrimImg.sprite = UICanvasUtil.MakeHorizontalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0f,    new Color(0.03f, 0.027f, 0.024f, 0.62f)),
                new UICanvasUtil.GradientStop(0.52f, new Color(0.03f, 0.027f, 0.024f, 0.26f)),
                new UICanvasUtil.GradientStop(1f,    new Color(0.03f, 0.027f, 0.024f, 0.08f)),
            });
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            // The journal page, held up on the left.
            var card = UICanvasUtil.NewRect("Card", canvasGo.transform);
            card.anchorMin = new Vector2(0f, 0.5f);
            card.anchorMax = new Vector2(0f, 0.5f);
            card.pivot = new Vector2(0f, 0.5f);
            card.sizeDelta = new Vector2(CardW, CardH);
            card.anchoredPosition = new Vector2(110f, 0f);
            UICanvasUtil.AddShadow(card, 24, 34, 0.5f, -12f);
            UICanvasUtil.MakeRoundedPanel(card, Paper, 24, 0.55f);

            // Paper materiality: grain speckle + a faint top-light gradient.
            var grain = UICanvasUtil.NewImage("Grain", card, new Color(Ink.r, Ink.g, Ink.b, 1f), false);
            var grainImg = grain.GetComponent<Image>();
            grainImg.sprite = UICanvasUtil.PaperGrain();
            grainImg.type = Image.Type.Simple;
            UICanvasUtil.Stretch((RectTransform)grain.transform);
            var sheen = UICanvasUtil.NewImage("Sheen", card, Color.white, false);
            var sheenImg = sheen.GetComponent<Image>();
            sheenImg.sprite = UICanvasUtil.MakeVerticalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0f, new Color(0.16f, 0.13f, 0.095f, 0.05f)),   // faint ink pooling at the foot
                new UICanvasUtil.GradientStop(0.4f, new Color(1f, 1f, 1f, 0f)),
                new UICanvasUtil.GradientStop(1f, new Color(1f, 1f, 1f, 0.16f)),             // light from the top
            });
            UICanvasUtil.Stretch((RectTransform)sheen.transform);

            // Inset gold frame — a hairline sitting just inside the page edge.
            var frame = UICanvasUtil.NewImage("InnerFrame", card, new Color(Bronze.r, Bronze.g, Bronze.b, 0.4f), false);
            var frameImg = frame.GetComponent<Image>();
            frameImg.sprite = UICanvasUtil.RoundedOutline(16, 1.4f);
            frameImg.type = Image.Type.Sliced;
            var frameRt = (RectTransform)frame.transform;
            frameRt.anchorMin = Vector2.zero; frameRt.anchorMax = Vector2.one;
            frameRt.offsetMin = new Vector2(14f, 14f); frameRt.offsetMax = new Vector2(-14f, -14f);

            // Header.
            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", card, Localization.Get("guide.eyebrow"), 13f, Bronze);
            Place(eyebrow.rectTransform, MarginX, -46f, InnerW, 20f);

            var title = UICanvasUtil.NewHeading("Title", card, Localization.Get("guide.title"), 38f, Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(title.rectTransform, MarginX, -70f, InnerW, 50f);

            // Ledger double-rule: gold over ink hairline.
            var ruleGold = UICanvasUtil.NewImage("RuleGold", card, new Color(Bronze.r, Bronze.g, Bronze.b, 0.55f), false);
            Place((RectTransform)ruleGold.transform, MarginX, -128f, InnerW, 1.6f);
            var ruleInk = UICanvasUtil.NewImage("RuleInk", card, new Color(Ink.r, Ink.g, Ink.b, 0.22f), false);
            Place((RectTransform)ruleInk.transform, MarginX, -132.5f, InnerW, 0.9f);

            // Wren's journal passage.
            var passage = UICanvasUtil.NewBody("Passage", card, Localization.Get("guide.passage"), 17f, InkSoft, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            passage.lineSpacing = 10f;
            Place(passage.rectTransform, MarginX, -148f, InnerW, 112f);

            // The task, as an inset ledger entry.
            var task = UICanvasUtil.NewRect("TaskPanel", card);
            Place(task, MarginX, -268f, InnerW, 96f);
            var taskFill = task.gameObject.AddComponent<Image>();
            taskFill.sprite = UICanvasUtil.RoundedRect(12);
            taskFill.type = Image.Type.Sliced;
            taskFill.color = new Color(Ink.r, Ink.g, Ink.b, 0.06f);
            taskFill.raycastTarget = false;
            var taskStroke = UICanvasUtil.NewImage("Stroke", task, new Color(Ink.r, Ink.g, Ink.b, 0.14f), false);
            var taskStrokeImg = taskStroke.GetComponent<Image>();
            taskStrokeImg.sprite = UICanvasUtil.RoundedOutline(12, 1.1f);
            taskStrokeImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)taskStroke.transform);

            var taskLabel = UICanvasUtil.NewEyebrow("TaskLabel", task, Localization.Get("guide.task_label"), 11.5f, Bronze);
            Place(taskLabel.rectTransform, 22f, -14f, InnerW - 44f, 16f);
            _questName = UICanvasUtil.NewHeading("QuestName", task, "", 25f, Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(_questName.rectTransform, 22f, -32f, InnerW - 44f, 32f);
            _questObjective = UICanvasUtil.NewBody("QuestObjective", task, "", 15.5f, InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(_questObjective.rectTransform, 22f, -64f, InnerW - 44f, 24f);

            // Orientation hints.
            string[] hintKeys = { "guide.hint.compass", "guide.hint.ribbon", "guide.hint.journal" };
            float hy = -380f;
            foreach (var key in hintKeys)
            {
                var dot = UICanvasUtil.NewBody("Dot", card, "\u00b7", 17f, Bronze, FontStyles.Bold, TextAlignmentOptions.TopLeft);
                Place(dot.rectTransform, MarginX, hy, 14f, 22f);
                var hint = UICanvasUtil.NewBody("Hint", card, Localization.Get(key), 14.5f, InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                Place(hint.rectTransform, MarginX + 18f, hy, InnerW - 18f, 22f);
                hy -= 27f;
            }

            // Controls as keycap chips, three rows of two groups.
            float ky = hy - 12f;
            ky = KeycapRow(card, ky, new[]
            {
                new KeyGroup("guide.ctl.move",     new[] { "guide.key.wasd", "guide.key.lstick" }),
                new KeyGroup("guide.ctl.look",     new[] { "guide.key.mouse", "guide.key.rstick" }),
            });
            ky = KeycapRow(card, ky, new[]
            {
                new KeyGroup("guide.ctl.interact", new[] { "guide.key.e", "guide.key.y" }),
                new KeyGroup("guide.ctl.satchel",  new[] { "guide.key.j", "guide.key.x" }),
            });
            KeycapRow(card, ky, new[]
            {
                new KeyGroup("guide.ctl.map",      new[] { "guide.key.m", "guide.key.select" }),
                new KeyGroup("guide.ctl.journal",  new[] { "guide.key.esc", "guide.key.start" }),
            });

            // "Set out →" — the main menu's action grammar.
            var btnRt = UICanvasUtil.NewRect("SetOutButton", card);
            btnRt.anchorMin = new Vector2(0.5f, 0f); btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.sizeDelta = new Vector2(320f, 52f);
            btnRt.anchoredPosition = new Vector2(0f, 30f);

            var btnFill = btnRt.gameObject.AddComponent<Image>();
            btnFill.sprite = UICanvasUtil.RoundedRect(12);
            btnFill.type = Image.Type.Sliced;
            btnFill.color = new Color(Bronze.r, Bronze.g, Bronze.b, 0.13f);
            btnFill.raycastTarget = true;

            _button = btnRt.gameObject.AddComponent<Button>();
            _button.transition = Selectable.Transition.None;
            _button.targetGraphic = btnFill;
            _button.onClick.AddListener(Dismiss);
            var nav = _button.navigation; nav.mode = Navigation.Mode.None; _button.navigation = nav;

            var btnStroke = UICanvasUtil.NewImage("BtnStroke", btnRt, new Color(Bronze.r, Bronze.g, Bronze.b, 0.5f), false);
            var btnStrokeImg = btnStroke.GetComponent<Image>();
            btnStrokeImg.sprite = UICanvasUtil.RoundedOutline(12, 1.3f);
            btnStrokeImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)btnStroke.transform);

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
            setF("_focusedColor", new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.24f));
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

        private struct KeyGroup
        {
            public string LabelKey;
            public string[] KeyKeys;
            public KeyGroup(string labelKey, string[] keyKeys) { LabelKey = labelKey; KeyKeys = keyKeys; }
        }

        // One row of [label  [chip][chip]] groups; the second group starts at the row midpoint
        // so the columns align down the card. Returns the y for the next row.
        private float KeycapRow(RectTransform card, float y, KeyGroup[] groups)
        {
            for (int g = 0; g < groups.Length; g++)
            {
                float x = MarginX + g * (InnerW * 0.5f);
                var label = UICanvasUtil.NewEyebrow("CtlLabel", card, Localization.Get(groups[g].LabelKey), 10.5f, MossDark);
                label.ForceMeshUpdate();
                float lw = label.preferredWidth + 4f;
                Place(label.rectTransform, x, y - 6f, lw, 16f);
                x += lw + 10f;
                foreach (var keyKey in groups[g].KeyKeys)
                {
                    x += Keycap(card, x, y, Localization.Get(keyKey)) + 5f;
                }
            }
            return y - 34f;
        }

        // A single key chip: rounded fill + hairline + centered label. Returns its width.
        private float Keycap(RectTransform card, float x, float y, string text)
        {
            var label = UICanvasUtil.NewBody("Key", card, text, 12f, Ink, FontStyles.Normal, TextAlignmentOptions.Center);
            label.enableWordWrapping = false;
            label.ForceMeshUpdate();
            float w = Mathf.Max(26f, label.preferredWidth + 16f);

            var chip = UICanvasUtil.NewRect("Chip", card);
            Place(chip, x, y, w, 25f);
            var fill = chip.gameObject.AddComponent<Image>();
            fill.sprite = UICanvasUtil.RoundedRect(6);
            fill.type = Image.Type.Sliced;
            fill.color = new Color(Ink.r, Ink.g, Ink.b, 0.07f);
            fill.raycastTarget = false;
            var stroke = UICanvasUtil.NewImage("Stroke", chip, new Color(Ink.r, Ink.g, Ink.b, 0.3f), false);
            var strokeImg = stroke.GetComponent<Image>();
            strokeImg.sprite = UICanvasUtil.RoundedOutline(6, 1.1f);
            strokeImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)stroke.transform);

            label.rectTransform.SetParent(chip, false);
            UICanvasUtil.Stretch(label.rectTransform);
            return w;
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
