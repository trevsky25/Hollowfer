using System;
using System.Collections;
using Hollowfen.Audio;
using Hollowfen.Cinematics;
using Hollowfen.Data;
using Hollowfen.Foraging;
using Hollowfen.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Narration overlay with sequential serif-italic captions — ports the web prototype's
    // homecoming intro treatment (playHomecomingIntro). Scene-local singleton, builds
    // programmatically on first Show, runs on unscaled time, suspends the player.
    // Two modes:
    //   Show(...)          — captions on full black (act-break journal narration).
    //   ShowCinematic(...) — captions in the lower third over a hero image with a per-image
    //                        motivated Ken Burns + letterbox + a vignette scrim. The multi-image
    //                        variant (batch-41) plays an ordered sequence of paintings that
    //                        CROSSFADE-DISSOLVE into one another (two hero layers, A/B), each with
    //                        its own camera move, so the homecoming passage reads as a short film.
    public class NarrationOverlay : MonoBehaviour
    {
        public static NarrationOverlay Instance { get; private set; }

        [SerializeField] private float _fadeSeconds = 0.8f;
        [SerializeField, Tooltip("Minimum time a caption holds before input can advance it.")]
        private float _minHoldSeconds = 1.4f;
        [SerializeField, Tooltip("Captions auto-advance after this long without input.")]
        private float _autoAdvanceSeconds = 6.0f;

        [SerializeField, Tooltip("Mixer reference used to resolve the Master route for independently adjustable voice-over. Null = unrouted.")]
        private UnityEngine.Audio.AudioMixerGroup _voiceOutput;

        [Header("Cinematic mode")]
        [SerializeField, Tooltip("Letterbox bar height at 1080 reference (each bar).")]
        private float _letterboxHeight = 116f;
        [SerializeField, Tooltip("Per-image Ken Burns journey length; each painting eases across this long.")]
        private float _kenBurnsSeconds = 15f;
        [SerializeField, Tooltip("Crossfade dissolve length between paintings.")]
        private float _crossfadeSeconds = 1.4f;

        // Per-image motivated camera moves (start → end state), index-matched to the intro image list.
        // Image 0's A-state MUST match LoadingScreen's held A-state (scale 1.20, pos 150,46) so the
        // welcome card dissolves into the narration with zero flash (seamless handoff).
        private struct KbMove
        {
            public float ScaleA, ScaleB; public Vector2 PosA, PosB;
            public KbMove(float sa, Vector2 pa, float sb, Vector2 pb) { ScaleA = sa; PosA = pa; ScaleB = sb; PosB = pb; }
        }
        private static readonly KbMove[] DefaultMoves =
        {
            new KbMove(1.20f, new Vector2(150f, 46f),  1.10f, new Vector2(-70f, -12f)), // 0 ridge:   push out, Wren → whole valley
            new KbMove(1.16f, new Vector2(150f, 24f),  1.07f, new Vector2(-140f, 6f)),  // 1 river:   drift across the flood → dead mill
            new KbMove(1.20f, new Vector2(20f, -34f),  1.07f, new Vector2(-8f, 40f)),   // 2 cottages: creep down the lane → far lantern
            new KbMove(1.18f, new Vector2(-130f, 12f), 1.05f, new Vector2(150f, -4f)),  // 3 square:  push in → Bram's lantern-lit door
        };

        // Book spreads already have a cinematic 16:9 composition and readable marginalia. Give
        // them only a breath of movement so their edges and identification notes remain visible.
        private static readonly KbMove[] FullFrameMoves =
        {
            new KbMove(1.035f, new Vector2(-10f, 4f), 1.008f, new Vector2(8f, -3f)),
            new KbMove(1.030f, new Vector2(8f, -3f), 1.006f, new Vector2(-7f, 3f)),
            new KbMove(1.032f, new Vector2(-6f, -2f), 1.007f, new Vector2(7f, 2f)),
            new KbMove(1.030f, new Vector2(7f, 3f), 1.005f, new Vector2(-6f, -2f)),
        };

        private Canvas _canvas;
        private CanvasGroup _group;
        private Image _black;
        private Image _heroA;
        private Image _heroB;
        private Image _scrim;
        private RectTransform _letterTop;
        private RectTransform _letterBot;
        private Image _captionBacking;
        private TMP_Text _caption;
        private TMP_Text _hint;
        private RectTransform _momentTitleRoot;
        private TMP_Text _momentEyebrow;
        private TMP_Text _momentTitle;
        private TMP_Text _momentSubtitle;
        private TMP_Text _pageText;
        private bool _built;
        private Coroutine _running;
        private Coroutine _kbCoA;
        private Coroutine _kbCoB;
        private AudioSource _voiceSource;
        private Action _pendingOnDone;
        private NarrativePresentationSession.Lease _inputLease;
        private Hollowfen.Input.InputActions _input;
        private int _shownFrame;

        private Image _activeHero;
        private bool _cinematic;
        private static Sprite _scrimSprite;
        private Sprite[] _pendingHeroes;
        private int[] _pendingBeatImage;
        // batch-62: cinematic normally snaps opaque (it cross-fades from the boot loading screen).
        // When there's no loading screen behind it (the mid-game journal reveal, dissolving in over a
        // held prop-focus close-up), fade the whole overlay in instead of hard-cutting.
        private bool _pendingFadeIn;
        private StoryMomentData _pendingMoment;

        public bool IsShowing => _running != null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _input = new Hollowfen.Input.InputActions();
        }

        private void OnDestroy()
        {
            VoiceAudio.Unregister(_voiceSource);
            if (_input != null)
            {
                _input.Dialogue.Disable();
                _input.Dispose();
                _input = null;
            }
            ReleaseInputLease();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_running == null) return;
            RefreshInputHint();
            // Do not let the button that opened a presentation dismiss it in the same frame.
            if (Time.frameCount <= _shownFrame) return;
            if (SkipPressed()) SkipAll();
        }

        public void Show(string[] captions, Action onDone = null) => ShowInternal(captions, null, null, null, onDone);

        // Caption-synced voice-over variant (batch-29): clips[i] plays as captions[i] fades in.
        public void Show(string[] captions, AudioClip[] clips, Action onDone = null) => ShowInternal(captions, clips, null, null, onDone);

        // Single-image cinematic (batch-36, back-compat).
        public void ShowCinematic(string[] captions, AudioClip[] clips, Sprite hero, Action onDone = null)
            => ShowInternal(captions, clips, hero != null ? new[] { hero } : null, null, onDone);

        // Two-image cinematic with a hard switch beat (batch-40, back-compat). Rebuilt on top of the
        // multi-image path: image swaps now dissolve instead of hard-cutting.
        public void ShowCinematic(string[] captions, AudioClip[] clips, Sprite hero, Sprite hero2, int switchBeat, Action onDone = null)
        {
            Sprite[] heroes = hero2 != null ? new[] { hero, hero2 } : (hero != null ? new[] { hero } : null);
            int[] map = null;
            if (heroes != null && heroes.Length == 2 && captions != null)
            {
                map = new int[captions.Length];
                for (int i = 0; i < map.Length; i++) map[i] = i >= switchBeat ? 1 : 0;
            }
            ShowInternal(captions, clips, heroes, map, onDone);
        }

        // Multi-image cinematic (batch-41): heroes[] in order; beatImage[i] = which image caption i
        // is painted over. When the image index changes between captions it crossfade-dissolves, and
        // each image runs its own motivated Ken Burns from DefaultMoves.
        // fadeIn (batch-62): dissolve the whole cinematic in (0→1) instead of snapping opaque — for a
        // mid-game reveal with no loading screen behind it (the journal, over a held prop-focus frame).
        public void ShowCinematic(string[] captions, AudioClip[] clips, Sprite[] heroes, int[] beatImage, Action onDone = null, bool fadeIn = false)
            => ShowInternal(captions, clips, heroes, beatImage, onDone, fadeIn);

        // Data-authored quest presentation. The same renderer now handles the journal discovery,
        // NPC scene interstitials, and future act breaks without scene components owning media arrays.
        public void ShowStoryMoment(StoryMomentData moment, Action onDone = null)
        {
            if (moment == null) { onDone?.Invoke(); return; }
            ShowInternal(moment.ResolveCaptions(), moment.VoiceClips, moment.ResolveImages(),
                moment.BeatImages, onDone, moment.FadeIn, moment);
        }

        private void ShowInternal(string[] captions, AudioClip[] clips, Sprite[] heroes, int[] beatImage,
            Action onDone, bool fadeIn = false, StoryMomentData moment = null)
        {
            if (captions == null || captions.Length == 0) { onDone?.Invoke(); return; }
            BuildIfNeeded();
            // Resolve the last-used device before the canvas becomes visible. Update keeps this
            // current afterward, but waiting for that first Update can flash the keyboard hint on
            // a controller-started new game.
            RefreshInputHint();
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
                ReleaseInputLease();
            }
            StopKb(true); StopKb(false);
            if (_voiceSource != null) _voiceSource.Stop();
            _pendingHeroes = (heroes != null && heroes.Length > 0) ? heroes : null;
            _pendingBeatImage = beatImage;
            _pendingFadeIn = fadeIn;
            _pendingMoment = moment;
            _pendingOnDone = onDone;
            _shownFrame = Time.frameCount;
            _input?.Dialogue.Enable();
            _running = StartCoroutine(Run(captions, clips, onDone));
        }

        // Immediate dismiss — used by automated verification and as a safety hatch.
        public void SkipAll()
        {
            if (_running == null) return;
            StopCoroutine(_running);
            StopKb(true); StopKb(false);
            _running = null;
            if (_voiceSource != null) _voiceSource.Stop();
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            _input?.Dialogue.Disable();
            ConfigureStoryMoment(null);
            _pendingMoment = null;
            ReleaseInputLease();
            var done = _pendingOnDone; _pendingOnDone = null;
            done?.Invoke();
        }

        private void PlayVoice(AudioClip clip)
        {
            if (_voiceSource == null)
            {
                if (clip == null) return;
                _voiceSource = gameObject.AddComponent<AudioSource>();
                _voiceSource.playOnAwake = false;
                _voiceSource.spatialBlend = 0f;
                _voiceSource.priority = 0;
            }
            VoiceAudio.Configure(_voiceSource, _voiceOutput);
            _voiceSource.Stop();
            if (clip == null) return;
            _voiceSource.clip = clip;
            _voiceSource.Play();
        }

        private int BeatImage(int i)
        {
            if (_pendingHeroes == null) return 0;
            int idx = (_pendingBeatImage != null && i < _pendingBeatImage.Length) ? _pendingBeatImage[i] : 0;
            return Mathf.Clamp(idx, 0, _pendingHeroes.Length - 1);
        }

        private static KbMove MoveFor(int imageIndex, bool preserveFullFrame)
        {
            if (imageIndex < 0) imageIndex = 0;
            KbMove[] moves = preserveFullFrame ? FullFrameMoves : DefaultMoves;
            return moves[imageIndex % moves.Length];
        }

        private IEnumerator Run(string[] captions, AudioClip[] clips, Action onDone)
        {
            _cinematic = _pendingHeroes != null;
            var activeMoment = _pendingMoment;

            _inputLease = NarrativePresentationSession.Acquire(
                this, NarrativePresentationSession.InputOnly);

            ConfigureMode(_cinematic);
            ConfigureStoryMoment(activeMoment);

            _canvas.gameObject.SetActive(true);
            _caption.text = "";

            int curImg = -1;
            if (_cinematic)
            {
                // Appear opaque immediately at image 0's Ken-Burns A-state + full letterbox, so a
                // cinematic-welcome loading screen (same image, same letterbox) cross-fades out to
                // reveal us with zero flash (seamless opening).
                curImg = BeatImage(0);
                _activeHero = _heroA;
                _heroA.sprite = _pendingHeroes[curImg];
                _heroA.color = Color.white;
                _heroA.gameObject.SetActive(true);
                _heroB.gameObject.SetActive(false);
                StartKb(true, curImg);
                ConfigurePageText(activeMoment, 0, curImg, _heroA.rectTransform, 1f);
                if (_pendingFadeIn)
                {
                    // Dissolve the whole cinematic in over whatever's behind (a held prop-focus close-up):
                    // real journal → painted journal, no hard cut. Fade grows the letterbox with the alpha.
                    _group.alpha = 0f;
                    _letterTop.sizeDelta = new Vector2(0f, 0f);
                    _letterBot.sizeDelta = new Vector2(0f, 0f);
                    yield return Fade(0f, 1f);
                }
                else
                {
                    // Appear opaque immediately at image 0's Ken-Burns A-state + full letterbox, so a
                    // cinematic-welcome loading screen (same image, same letterbox) cross-fades out to
                    // reveal us with zero flash (seamless opening).
                    _group.alpha = 1f;
                    _letterTop.sizeDelta = new Vector2(0f, _letterboxHeight);
                    _letterBot.sizeDelta = new Vector2(0f, _letterboxHeight);
                }
            }
            else
            {
                _group.alpha = 0f;
                yield return Fade(0f, 1f);
            }

            for (int i = 0; i < captions.Length; i++)
            {
                var line = captions[i];
                var clip = clips != null && i < clips.Length ? clips[i] : null;

                // Let the painting change lead the caption slightly — the dissolve begins as the new
                // line fades in, a real film cut rather than a slideshow flip.
                if (_cinematic)
                {
                    int want = BeatImage(i);
                    if (want != curImg) { curImg = want; StartCoroutine(CrossfadeTo(want, i)); }
                    else ConfigurePageText(activeMoment, i, want, _activeHero.rectTransform, 1f);
                }

                _caption.text = line;
                _caption.alpha = 0f;
                PlayVoice(clip);
                float t0 = Time.unscaledTime;
                float captionIn = GameSettings.ReducedMotion ? .10f : .6f;
                while (Time.unscaledTime - t0 < captionIn)
                {
                    _caption.alpha = Mathf.Clamp01((Time.unscaledTime - t0) / captionIn);
                    yield return null;
                }
                _caption.alpha = 1f;

                // Voiced: hold for the read (+ a breath). Silent: hold proportional to length
                // so the longer restored passages get time to read.
                float hold = activeMoment != null && activeMoment.HoldSeconds > 0f
                    ? Mathf.Max(_minHoldSeconds, activeMoment.HoldSeconds)
                    : clip != null
                        ? Mathf.Max(_autoAdvanceSeconds, clip.length + 0.8f)
                        : SilentHold(line);
                float shown = Time.unscaledTime;
                while (true)
                {
                    float elapsed = Time.unscaledTime - shown;
                    if (elapsed >= hold) break;
                    if (elapsed >= _minHoldSeconds && AdvancePressed()) break;
                    yield return null;
                }
                if (_voiceSource != null) _voiceSource.Stop();

                float f0 = Time.unscaledTime;
                float captionOut = GameSettings.ReducedMotion ? .10f : .35f;
                while (Time.unscaledTime - f0 < captionOut)
                {
                    _caption.alpha = 1f - Mathf.Clamp01((Time.unscaledTime - f0) / captionOut);
                    yield return null;
                }
            }

            yield return Fade(1f, 0f);
            StopKb(true); StopKb(false);
            _canvas.gameObject.SetActive(false);

            ConfigureStoryMoment(null);
            _pendingMoment = null;
            ReleaseInputLease();
            _input?.Dialogue.Disable();
            _running = null;
            _pendingOnDone = null;
            onDone?.Invoke();
        }

        private void ReleaseInputLease()
        {
            _inputLease?.Dispose();
            _inputLease = null;
        }

        private void ConfigureStoryMoment(StoryMomentData moment)
        {
            if (_momentTitleRoot == null) return;
            if (moment == null && _pageText != null) _pageText.gameObject.SetActive(false);

            // Dialogue interstitials can use the canonical StoryCard painting as a clean,
            // image-only beat. The hidden caption still drives VO/read-time pacing, but no
            // title, subtitle, caption, or input hint is drawn over the artwork.
            bool showCopy = moment == null || moment.ShowCaptions;
            _caption.gameObject.SetActive(showCopy);
            _hint.gameObject.SetActive(showCopy);
            if (_captionBacking != null)
                _captionBacking.gameObject.SetActive(showCopy && _cinematic &&
                    GameSettings.CaptionBacking);

            bool show = moment != null && moment.ShowStoryCardTitle && moment.StoryCard != null;
            _momentTitleRoot.gameObject.SetActive(show);
            if (!show) return;

            var card = moment.StoryCard;
            string act = JournalText.StoryAct(card);
            string scene = JournalText.StoryScene(card);
            string brow = act;
            if (!string.IsNullOrEmpty(scene))
                brow = string.IsNullOrEmpty(brow)
                    ? scene
                    : string.Format(Localization.Get("format.pair"), brow, scene);
            _momentEyebrow.text = brow.ToUpperInvariant();
            _momentTitle.text = JournalText.StoryTitle(card);
            _momentSubtitle.text = JournalText.StorySubtitle(card);
        }

        // Dissolve the active painting into heroes[imageIndex]. The incoming layer is forced to the
        // upper of the two hero slots (still under the scrim/letterbox/caption) and faded 0→1 over the
        // opaque outgoing layer, so the reveal is correct regardless of which layer (A/B) is incoming.
        private IEnumerator CrossfadeTo(int imageIndex, int beatIndex)
        {
            Image incoming = _activeHero == _heroA ? _heroB : _heroA;
            Image outgoing = _activeHero;
            bool incomingIsA = incoming == _heroA;

            // Deterministic z-order among the two hero layers: outgoing under, incoming over.
            outgoing.transform.SetSiblingIndex(1);
            incoming.transform.SetSiblingIndex(2);

            incoming.sprite = _pendingHeroes[imageIndex];
            incoming.color = new Color(1f, 1f, 1f, 0f);
            incoming.gameObject.SetActive(true);
            StartKb(incomingIsA, imageIndex);
            _activeHero = incoming;
            ConfigurePageText(_pendingMoment, beatIndex, imageIndex, incoming.rectTransform, 0f);

            float dur = GameSettings.ReducedMotion ? .12f : Mathf.Max(0.2f, _crossfadeSeconds);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float e = u * u * (3f - 2f * u); // smoothstep dissolve
                incoming.color = new Color(1f, 1f, 1f, e);
                SetPageTextAlpha(e);
                yield return null;
            }
            incoming.color = Color.white;
            SetPageTextAlpha(1f);
            outgoing.gameObject.SetActive(false);
            StopKb(outgoing == _heroA);
        }

        private void ConfigurePageText(StoryMomentData moment, int beatIndex, int imageIndex,
            RectTransform hero, float alpha)
        {
            if (_pageText == null) return;
            bool show = moment != null && moment.HasPageText &&
                        imageIndex == moment.PageTextImageIndex &&
                        beatIndex >= moment.PageTextStartBeat;
            _pageText.gameObject.SetActive(show);
            if (!show || hero == null) return;

            _pageText.transform.SetParent(hero, false);
            // Reveal authored paragraphs on the same beats as their matching caption/VO instead
            // of showing the entire letter before the narration reaches its signature.
            _pageText.text = moment.PageTextForBeat(beatIndex);
            _pageText.font = moment.UseCursivePageText ? UICanvasUtil.CursiveFont : UICanvasUtil.BodyFont;
            _pageText.fontStyle = moment.UseCursivePageText ? FontStyles.Normal : FontStyles.Italic;
            _pageText.characterSpacing = moment.UseCursivePageText ? 1.5f : 0f;
            _pageText.fontSize = moment.PageTextFontSize;
            _pageText.color = moment.PageTextColor;
            _pageText.alpha = Mathf.Clamp01(alpha) * moment.PageTextColor.a;
            _pageText.rectTransform.localEulerAngles = new Vector3(0f, 0f, moment.PageTextRotation);
            Rect rect = moment.PageTextRect;
            var rt = _pageText.rectTransform;
            rt.anchorMin = new Vector2(rect.xMin, rect.yMin);
            rt.anchorMax = new Vector2(rect.xMax, rect.yMax);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private void SetPageTextAlpha(float alpha)
        {
            if (_pageText == null || !_pageText.gameObject.activeSelf || _pendingMoment == null) return;
            _pageText.alpha = Mathf.Clamp01(alpha) * _pendingMoment.PageTextColor.a;
        }

        private void StartKb(bool layerA, int imageIndex)
        {
            StopKb(layerA);
            var rt = (layerA ? _heroA : _heroB).rectTransform;
            bool preserve = _pendingMoment != null && _pendingMoment.PreserveFullFrameImages;
            var co = StartCoroutine(KenBurns(rt, MoveFor(imageIndex, preserve)));
            if (layerA) _kbCoA = co; else _kbCoB = co;
        }

        private void StopKb(bool layerA)
        {
            if (layerA) { if (_kbCoA != null) { StopCoroutine(_kbCoA); _kbCoA = null; } }
            else { if (_kbCoB != null) { StopCoroutine(_kbCoB); _kbCoB = null; } }
        }

        private static float SilentHold(string line)
        {
            int words = string.IsNullOrEmpty(line) ? 1 : line.Split(' ').Length;
            return Mathf.Clamp(words * 0.36f + 1.8f, 3.2f, 9.5f);
        }

        // Position/enable the non-hero layers for the chosen mode. Hero layers are driven by
        // Run/CrossfadeTo; here we only clear them when returning to black mode.
        private void ConfigureMode(bool cinematic)
        {
            if (!cinematic)
            {
                _heroA.gameObject.SetActive(false);
                _heroB.gameObject.SetActive(false);
            }
            _scrim.gameObject.SetActive(cinematic);
            _letterTop.gameObject.SetActive(cinematic);
            _letterBot.gameObject.SetActive(cinematic);
            // Full black stays visible under everything (also fills letterbox aspect gaps).
            var cRT = _caption.rectTransform;
            if (cinematic)
            {
                // caption → lower third, above the bottom letterbox bar
                cRT.anchorMin = new Vector2(0.5f, 0f); cRT.anchorMax = new Vector2(0.5f, 0f);
                cRT.pivot = new Vector2(0.5f, 0f);
                cRT.sizeDelta = new Vector2(1440f, 300f);
                cRT.anchoredPosition = new Vector2(0f, _letterboxHeight + 40f);
                _caption.fontSize = 40f;
                _caption.alignment = TextAlignmentOptions.Bottom;
                var backingRT = _captionBacking.rectTransform;
                backingRT.anchorMin = new Vector2(.5f, 0f);
                backingRT.anchorMax = new Vector2(.5f, 0f);
                backingRT.pivot = new Vector2(.5f, 0f);
                backingRT.sizeDelta = new Vector2(1500f, 214f);
                backingRT.anchoredPosition = new Vector2(0f, _letterboxHeight + 34f);
                var hRT = _hint.rectTransform;
                hRT.anchoredPosition = new Vector2(0f, _letterboxHeight * 0.5f);
            }
            else
            {
                cRT.anchorMin = new Vector2(0.5f, 0.5f); cRT.anchorMax = new Vector2(0.5f, 0.5f);
                cRT.pivot = new Vector2(0.5f, 0.5f);
                cRT.sizeDelta = new Vector2(1180f, 460f);
                cRT.anchoredPosition = Vector2.zero;
                _caption.fontSize = 38f;
                _caption.alignment = TextAlignmentOptions.Center;
                _hint.rectTransform.anchoredPosition = new Vector2(0f, 48f);
            }
            _captionBacking.gameObject.SetActive(cinematic && GameSettings.CaptionBacking &&
                _caption.gameObject.activeSelf);
        }

        // Slow motivated camera per painting: push/drift eased across _kenBurnsSeconds, restarted on
        // each image so every painting gets its own move (ridge push-out, river drift, lane creep, door push-in).
        private IEnumerator KenBurns(RectTransform rt, KbMove m)
        {
            if (GameSettings.ReducedMotion)
            {
                rt.localScale = Vector3.one * m.ScaleB;
                rt.anchoredPosition = m.PosB;
                yield break;
            }
            rt.localScale = Vector3.one * m.ScaleA;
            rt.anchoredPosition = m.PosA;
            float dur = Mathf.Max(1f, _kenBurnsSeconds);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float ease = u * u * (3f - 2f * u); // smoothstep
                rt.localScale = Vector3.one * Mathf.Lerp(m.ScaleA, m.ScaleB, ease);
                rt.anchoredPosition = Vector2.Lerp(m.PosA, m.PosB, ease);
                yield return null;
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            // letterbox bars grow with the fade in cinematic mode
            bool cinematic = _cinematic;
            float t0 = Time.unscaledTime;
            float duration = GameSettings.ReducedMotion ? .12f : _fadeSeconds;
            while (Time.unscaledTime - t0 < duration)
            {
                float u = duration <= 0f ? 1f : (Time.unscaledTime - t0) / duration;
                _group.alpha = Mathf.Lerp(from, to, u);
                if (cinematic)
                {
                    float h = _letterboxHeight * (from < to ? u : 1f - u);
                    _letterTop.sizeDelta = new Vector2(0f, h);
                    _letterBot.sizeDelta = new Vector2(0f, h);
                }
                yield return null;
            }
            _group.alpha = to;
            if (cinematic)
            {
                float h = from < to ? _letterboxHeight : 0f;
                _letterTop.sizeDelta = new Vector2(0f, h);
                _letterBot.sizeDelta = new Vector2(0f, h);
            }
        }

        private bool AdvancePressed()
        {
            if (_input != null && _input.Dialogue.Advance.WasPressedThisFrame()) return true;
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) return true;
            var pad = Gamepad.current;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            return false;
        }

        private bool SkipPressed()
        {
            if (_input != null && _input.Dialogue.Skip.WasPressedThisFrame()) return true;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            return pad != null && pad.buttonEast.wasPressedThisFrame;
        }

        private void RefreshInputHint()
        {
            if (_hint == null) return;
            string text = ControllerGlyphs.IsGamepadActive
                ? string.Format(Localization.Get("narration.hint.gamepad"),
                    ControllerGlyphs.For(ControllerGlyphs.Face.South),
                    ControllerGlyphs.For(ControllerGlyphs.Face.East))
                : Localization.Get("narration.hint.keyboard");
            if (_hint.text != text) _hint.text = text;
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            var canvasGo = new GameObject("NarrationCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 70;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.Init1080();

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;

            // sibling 0 — full black base.
            _black = UICanvasUtil.NewImage("Black", canvasGo.transform, Color.black, false).GetComponent<Image>();
            UICanvasUtil.Stretch(_black.rectTransform);

            // siblings 1 & 2 — the two hero layers (A/B) that crossfade. Centered, sized to the 1080
            // reference, Ken-Burned in code. Kept as the two lowest non-black siblings so the scrim,
            // letterbox and caption always render above them.
            _heroA = BuildHeroLayer("HeroA", canvasGo.transform);
            _heroB = BuildHeroLayer("HeroB", canvasGo.transform);

            // Optional localized writing that belongs to a painted page. It is reparented to the
            // active hero layer so it follows that image's Ken Burns scale and drift.
            _pageText = UICanvasUtil.NewBody("LivePageText", _heroA.transform, "", 30f,
                new Color(0.16f, 0.1f, 0.05f, 0.86f), FontStyles.Italic,
                TextAlignmentOptions.TopLeft);
            _pageText.textWrappingMode = TextWrappingModes.Normal;
            _pageText.lineSpacing = 7f;
            _pageText.raycastTarget = false;
            _pageText.gameObject.SetActive(false);

            // Full-screen vignette gradient (dark bottom for captions + dark top for the letterbox
            // blend, clear middle). Full-screen → no mid-screen rect edge / cut-off band.
            EnsureScrimSprite();
            _scrim = UICanvasUtil.NewImage("Scrim", canvasGo.transform, Color.white, false).GetComponent<Image>();
            _scrim.sprite = _scrimSprite;
            _scrim.type = Image.Type.Simple;
            UICanvasUtil.Stretch(_scrim.rectTransform);
            _scrim.color = Color.black;
            _scrim.gameObject.SetActive(false);

            // Letterbox bars.
            _letterTop = MakeBar("LetterboxTop", canvasGo.transform, 1f);
            _letterBot = MakeBar("LetterboxBot", canvasGo.transform, 0f);

            // Scene-title plate: restrained gold hierarchy over the illustration. It is independent
            // from unlocking; a dialogue may preview this card while the journal reward stays locked.
            _momentTitleRoot = UICanvasUtil.NewRect("StoryMomentTitle", canvasGo.transform);
            _momentTitleRoot.anchorMin = _momentTitleRoot.anchorMax = new Vector2(0f, 1f);
            _momentTitleRoot.pivot = new Vector2(0f, 1f);
            _momentTitleRoot.sizeDelta = new Vector2(900f, 210f);
            _momentTitleRoot.anchoredPosition = new Vector2(112f, -150f);

            var titleRail = UICanvasUtil.NewImage("TitleRail", _momentTitleRoot,
                HollowfenPalette.Gold, false).GetComponent<Image>();
            titleRail.sprite = UICanvasUtil.RoundedRect(2);
            titleRail.type = Image.Type.Sliced;
            var railRT = titleRail.rectTransform;
            railRT.anchorMin = new Vector2(0f, 0f); railRT.anchorMax = new Vector2(0f, 1f);
            railRT.pivot = new Vector2(0f, 0.5f);
            railRT.sizeDelta = new Vector2(4f, 0f);
            railRT.anchoredPosition = Vector2.zero;

            _momentEyebrow = UICanvasUtil.NewEyebrow("Eyebrow", _momentTitleRoot, "", 16f,
                HollowfenPalette.GoldGlow, TextAlignmentOptions.TopLeft);
            var meRT = _momentEyebrow.rectTransform;
            meRT.anchorMin = new Vector2(0f, 1f); meRT.anchorMax = new Vector2(1f, 1f);
            meRT.pivot = new Vector2(0f, 1f); meRT.sizeDelta = new Vector2(-30f, 28f);
            meRT.anchoredPosition = new Vector2(26f, -4f);

            _momentTitle = UICanvasUtil.NewHeading("Title", _momentTitleRoot, "", 64f,
                HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _momentTitle.textWrappingMode = TextWrappingModes.Normal;
            var mtRT = _momentTitle.rectTransform;
            mtRT.anchorMin = new Vector2(0f, 1f); mtRT.anchorMax = new Vector2(1f, 1f);
            mtRT.pivot = new Vector2(0f, 1f); mtRT.sizeDelta = new Vector2(-30f, 90f);
            mtRT.anchoredPosition = new Vector2(26f, -34f);

            _momentSubtitle = UICanvasUtil.NewBody("Subtitle", _momentTitleRoot, "", 24f,
                new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.82f),
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            _momentSubtitle.textWrappingMode = TextWrappingModes.Normal;
            var msRT = _momentSubtitle.rectTransform;
            msRT.anchorMin = new Vector2(0f, 1f); msRT.anchorMax = new Vector2(1f, 1f);
            msRT.pivot = new Vector2(0f, 1f); msRT.sizeDelta = new Vector2(-30f, 70f);
            msRT.anchoredPosition = new Vector2(28f, -122f);
            _momentTitleRoot.gameObject.SetActive(false);

            _captionBacking = UICanvasUtil.NewImage("CaptionBacking", canvasGo.transform,
                new Color(.025f, .022f, .018f, .88f), false).GetComponent<Image>();
            _captionBacking.sprite = UICanvasUtil.RoundedRect(22);
            _captionBacking.type = Image.Type.Sliced;
            _captionBacking.gameObject.SetActive(false);

            _caption = UICanvasUtil.NewHeading("Caption", canvasGo.transform, "", 38f,
                new Color(0.95f, 0.92f, 0.84f, 1f), FontStyles.Italic, TextAlignmentOptions.Center);
            _caption.textWrappingMode = TextWrappingModes.Normal;
            _caption.lineSpacing = 12f;
            var cRT = _caption.rectTransform;
            cRT.anchorMin = new Vector2(0.5f, 0.5f); cRT.anchorMax = new Vector2(0.5f, 0.5f);
            cRT.pivot = new Vector2(0.5f, 0.5f);
            cRT.sizeDelta = new Vector2(1180f, 460f);
            cRT.anchoredPosition = Vector2.zero;

            _hint = UICanvasUtil.NewBody("Hint", canvasGo.transform,
                Localization.Get("narration.hint.keyboard"), 22f,
                new Color(0.93f, 0.90f, 0.82f, 0.60f), FontStyles.Italic, TextAlignmentOptions.Center);
            var hRT = _hint.rectTransform;
            hRT.anchorMin = new Vector2(0.5f, 0f); hRT.anchorMax = new Vector2(0.5f, 0f);
            hRT.pivot = new Vector2(0.5f, 0f);
            hRT.sizeDelta = new Vector2(680f, 28f);
            hRT.anchoredPosition = new Vector2(0f, 48f);

            _canvas.gameObject.SetActive(false);
        }

        private static Image BuildHeroLayer(string name, Transform parent)
        {
            var img = UICanvasUtil.NewImage(name, parent, Color.white, false).GetComponent<Image>();
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1920f, 1080f);
            img.preserveAspect = false;
            img.gameObject.SetActive(false);
            return img;
        }

        private static RectTransform MakeBar(string name, Transform parent, float anchorY)
        {
            var img = UICanvasUtil.NewImage(name, parent, Color.black, false).GetComponent<Image>();
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, anchorY); rt.anchorMax = new Vector2(1f, anchorY);
            rt.pivot = new Vector2(0.5f, anchorY);
            rt.sizeDelta = new Vector2(0f, 0f);
            rt.anchoredPosition = Vector2.zero;
            img.gameObject.SetActive(false);
            return rt;
        }

        // ONE full-screen vignette gradient — dark at the bottom (caption legibility) + a touch dark
        // at the very top (letterbox blend), clear through the middle. Full-screen so there is no
        // mid-screen rect edge that reads as a "cut-off" band (batch-40 streak fix).
        private static void EnsureScrimSprite()
        {
            if (_scrimSprite != null) return;
            int h = 256;
            var tex = new Texture2D(4, h, TextureFormat.RGBA32, false); tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[4 * h];
            for (int y = 0; y < h; y++)
            {
                float fy = y / (float)(h - 1); // 0 bottom, 1 top
                float tb = Mathf.Clamp01((0.42f - fy) / 0.42f); tb = tb * tb * (3f - 2f * tb);   // bottom fade
                float tt = Mathf.Clamp01((fy - 0.84f) / 0.16f); tt = tt * tt * (3f - 2f * tt);   // top fade
                float a = Mathf.Max(tb * 0.88f, tt * 0.70f);
                byte b = (byte)(a * 255f);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, b);
            }
            tex.SetPixels32(px); tex.Apply();
            _scrimSprite = Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
