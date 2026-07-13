using System;
using System.Collections;
using Hollowfen.Foraging;
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
    //   ShowCinematic(...) — captions in the lower third over a hero image with a slow
    //                        Ken Burns journey + letterbox bars + a bottom scrim (batch-36
    //                        cinematic opening: the homecoming passage painted over homecoming.png).
    public class NarrationOverlay : MonoBehaviour
    {
        public static NarrationOverlay Instance { get; private set; }

        [SerializeField] private float _fadeSeconds = 0.8f;
        [SerializeField, Tooltip("Minimum time a caption holds before input can advance it.")]
        private float _minHoldSeconds = 1.4f;
        [SerializeField, Tooltip("Captions auto-advance after this long without input.")]
        private float _autoAdvanceSeconds = 6.0f;

        [SerializeField, Tooltip("Mixer group for caption voice-over (SFX for the batch-29 test). Null = unrouted.")]
        private UnityEngine.Audio.AudioMixerGroup _voiceOutput;

        [Header("Cinematic mode")]
        [SerializeField, Tooltip("Letterbox bar height at 1080 reference (each bar).")]
        private float _letterboxHeight = 116f;
        [SerializeField, Tooltip("Ken Burns journey length; the hero slowly pushes out + drifts across the painting.")]
        private float _kenBurnsSeconds = 42f;

        private Canvas _canvas;
        private CanvasGroup _group;
        private Image _black;
        private Image _hero;
        private Image _scrim;
        private RectTransform _letterTop;
        private RectTransform _letterBot;
        private TMP_Text _caption;
        private TMP_Text _hint;
        private bool _built;
        private Coroutine _running;
        private Coroutine _kenBurns;
        private AudioSource _voiceSource;
        private Action _pendingOnDone;

        private static Sprite _scrimSprite;

        public bool IsShowing => _running != null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(string[] captions, Action onDone = null) => ShowInternal(captions, null, null, onDone);

        // Caption-synced voice-over variant (batch-29): clips[i] plays as captions[i] fades in.
        public void Show(string[] captions, AudioClip[] clips, Action onDone = null) => ShowInternal(captions, clips, null, onDone);

        // Cinematic variant (batch-36): the same caption/VO flow, painted over a hero image with a
        // slow Ken Burns journey, letterbox bars, a bottom scrim, and lower-third captions.
        public void ShowCinematic(string[] captions, AudioClip[] clips, Sprite hero, Action onDone = null)
            => ShowInternal(captions, clips, hero, onDone);

        private void ShowInternal(string[] captions, AudioClip[] clips, Sprite hero, Action onDone)
        {
            if (captions == null || captions.Length == 0) { onDone?.Invoke(); return; }
            BuildIfNeeded();
            if (_running != null) StopCoroutine(_running);
            if (_kenBurns != null) { StopCoroutine(_kenBurns); _kenBurns = null; }
            if (_voiceSource != null) _voiceSource.Stop();
            _pendingOnDone = onDone;
            _running = StartCoroutine(Run(captions, clips, hero, onDone));
        }

        // Immediate dismiss — used by automated verification and as a safety hatch.
        public void SkipAll()
        {
            if (_running == null) return;
            StopCoroutine(_running);
            if (_kenBurns != null) { StopCoroutine(_kenBurns); _kenBurns = null; }
            _running = null;
            if (_voiceSource != null) _voiceSource.Stop();
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            PlayerInteractor.Suspended = false;
            PlayerInteractor.SetPlayerInputEnabled(true);
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
                _voiceSource.outputAudioMixerGroup = _voiceOutput;
            }
            _voiceSource.Stop();
            if (clip == null) return;
            _voiceSource.clip = clip;
            _voiceSource.Play();
        }

        private IEnumerator Run(string[] captions, AudioClip[] clips, Sprite hero, Action onDone)
        {
            bool cinematic = hero != null;

            PlayerInteractor.Suspended = true;
            PlayerInteractor.SetPlayerInputEnabled(false);

            ConfigureMode(cinematic, hero);

            _canvas.gameObject.SetActive(true);
            _group.alpha = 0f;
            _caption.text = "";

            yield return Fade(0f, 1f);

            if (cinematic) _kenBurns = StartCoroutine(KenBurnsJourney());

            for (int i = 0; i < captions.Length; i++)
            {
                var line = captions[i];
                var clip = clips != null && i < clips.Length ? clips[i] : null;

                _caption.text = line;
                _caption.alpha = 0f;
                PlayVoice(clip);
                float t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < 0.6f)
                {
                    _caption.alpha = Mathf.Clamp01((Time.unscaledTime - t0) / 0.6f);
                    yield return null;
                }
                _caption.alpha = 1f;

                // Voiced: hold for the read (+ a breath). Silent: hold proportional to length
                // so the longer restored passages get time to read.
                float hold = clip != null
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
                while (Time.unscaledTime - f0 < 0.35f)
                {
                    _caption.alpha = 1f - Mathf.Clamp01((Time.unscaledTime - f0) / 0.35f);
                    yield return null;
                }
            }

            yield return Fade(1f, 0f);
            if (_kenBurns != null) { StopCoroutine(_kenBurns); _kenBurns = null; }
            _canvas.gameObject.SetActive(false);

            PlayerInteractor.Suspended = false;
            PlayerInteractor.SetPlayerInputEnabled(true);
            _running = null;
            _pendingOnDone = null;
            onDone?.Invoke();
        }

        private static float SilentHold(string line)
        {
            int words = string.IsNullOrEmpty(line) ? 1 : line.Split(' ').Length;
            return Mathf.Clamp(words * 0.36f + 1.8f, 3.2f, 9.5f);
        }

        // Position/enable the layers for the chosen mode.
        private void ConfigureMode(bool cinematic, Sprite hero)
        {
            _hero.gameObject.SetActive(cinematic);
            _scrim.gameObject.SetActive(cinematic);
            _letterTop.gameObject.SetActive(cinematic);
            _letterBot.gameObject.SetActive(cinematic);
            // Full black stays visible under everything (also fills letterbox aspect gaps).
            var cRT = _caption.rectTransform;
            if (cinematic)
            {
                _hero.sprite = hero;
                _hero.color = Color.white;
                // caption → lower third, above the bottom letterbox bar
                cRT.anchorMin = new Vector2(0.5f, 0f); cRT.anchorMax = new Vector2(0.5f, 0f);
                cRT.pivot = new Vector2(0.5f, 0f);
                cRT.sizeDelta = new Vector2(1440f, 300f);
                cRT.anchoredPosition = new Vector2(0f, _letterboxHeight + 40f);
                _caption.fontSize = 40f;
                _caption.alignment = TextAlignmentOptions.Bottom;
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
        }

        // Slow motivated camera: push out + drift across the painting (Wren → the village),
        // revealing the decline as the narration describes it.
        private IEnumerator KenBurnsJourney()
        {
            var rt = _hero.rectTransform;
            Vector3 scaleA = Vector3.one * 1.20f, scaleB = Vector3.one * 1.06f;
            Vector2 posA = new Vector2(150f, 46f), posB = new Vector2(-140f, -18f);
            rt.localScale = scaleA; rt.anchoredPosition = posA;
            float t = 0f;
            while (t < _kenBurnsSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / _kenBurnsSeconds);
                float ease = u * u * (3f - 2f * u); // smoothstep
                rt.localScale = Vector3.Lerp(scaleA, scaleB, ease);
                rt.anchoredPosition = Vector2.Lerp(posA, posB, ease);
                yield return null;
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            // letterbox bars grow with the fade in cinematic mode
            bool cinematic = _hero.gameObject.activeSelf;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < _fadeSeconds)
            {
                float u = (Time.unscaledTime - t0) / _fadeSeconds;
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

        private static bool AdvancePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) return true;
            var pad = Gamepad.current;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            return false;
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
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;

            _black = UICanvasUtil.NewImage("Black", canvasGo.transform, Color.black, false).GetComponent<Image>();
            UICanvasUtil.Stretch(_black.rectTransform);

            // Hero image (cinematic) — centered, sized to the 1080 reference, Ken-Burned in code.
            _hero = UICanvasUtil.NewImage("Hero", canvasGo.transform, Color.white, false).GetComponent<Image>();
            var heroRT = _hero.rectTransform;
            heroRT.anchorMin = heroRT.anchorMax = new Vector2(0.5f, 0.5f);
            heroRT.pivot = new Vector2(0.5f, 0.5f);
            heroRT.sizeDelta = new Vector2(1920f, 1080f);
            _hero.preserveAspect = false;
            _hero.gameObject.SetActive(false);

            // Bottom scrim so lower-third captions read over the image.
            EnsureScrimSprite();
            _scrim = UICanvasUtil.NewImage("Scrim", canvasGo.transform, Color.white, false).GetComponent<Image>();
            _scrim.sprite = _scrimSprite;
            _scrim.type = Image.Type.Simple;
            var sRT = _scrim.rectTransform;
            sRT.anchorMin = new Vector2(0f, 0f); sRT.anchorMax = new Vector2(1f, 0f);
            sRT.pivot = new Vector2(0.5f, 0f);
            sRT.sizeDelta = new Vector2(0f, 560f);
            sRT.anchoredPosition = Vector2.zero;
            _scrim.color = new Color(0f, 0f, 0f, 0.9f);
            _scrim.gameObject.SetActive(false);

            // Letterbox bars.
            _letterTop = MakeBar("LetterboxTop", canvasGo.transform, 1f);
            _letterBot = MakeBar("LetterboxBot", canvasGo.transform, 0f);

            _caption = UICanvasUtil.NewHeading("Caption", canvasGo.transform, "", 38f,
                new Color(0.95f, 0.92f, 0.84f, 1f), FontStyles.Italic, TextAlignmentOptions.Center);
            _caption.textWrappingMode = TextWrappingModes.Normal;
            _caption.lineSpacing = 12f;
            var cRT = _caption.rectTransform;
            cRT.anchorMin = new Vector2(0.5f, 0.5f); cRT.anchorMax = new Vector2(0.5f, 0.5f);
            cRT.pivot = new Vector2(0.5f, 0.5f);
            cRT.sizeDelta = new Vector2(1180f, 460f);
            cRT.anchoredPosition = Vector2.zero;

            _hint = UICanvasUtil.NewBody("Hint", canvasGo.transform, "Press Space to continue", 14f,
                new Color(0.93f, 0.90f, 0.82f, 0.35f), FontStyles.Italic, TextAlignmentOptions.Center);
            var hRT = _hint.rectTransform;
            hRT.anchorMin = new Vector2(0.5f, 0f); hRT.anchorMax = new Vector2(0.5f, 0f);
            hRT.pivot = new Vector2(0.5f, 0f);
            hRT.sizeDelta = new Vector2(600f, 24f);
            hRT.anchoredPosition = new Vector2(0f, 48f);

            _canvas.gameObject.SetActive(false);
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

        private static void EnsureScrimSprite()
        {
            if (_scrimSprite != null) return;
            int h = 128;
            var tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[4 * h];
            for (int y = 0; y < h; y++)
            {
                float a = 1f - (y / (float)(h - 1)); // bottom (y=0) opaque → top transparent
                a = Mathf.Pow(a, 1.4f);
                byte b = (byte)(a * 255f);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, b);
            }
            tex.SetPixels32(px); tex.Apply();
            _scrimSprite = Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
