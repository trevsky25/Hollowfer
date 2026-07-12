using System;
using System.Collections;
using Hollowfen.Foraging;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Full-black narration overlay with sequential serif-italic captions — ports the web
    // prototype's homecoming intro treatment (playHomecomingIntro). Scene-local singleton,
    // builds programmatically on first Show, runs on unscaled time, suspends the player.
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

        private Canvas _canvas;
        private CanvasGroup _group;
        private TMP_Text _caption;
        private TMP_Text _hint;
        private bool _built;
        private Coroutine _running;
        private AudioSource _voiceSource;

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

        public void Show(string[] captions, Action onDone = null)
        {
            Show(captions, null, onDone);
        }

        // Caption-synced voice-over variant (batch-29): clips[i] plays as captions[i] fades in;
        // the caption holds for at least the clip's length. clips may be null or shorter than
        // captions — missing entries are silent.
        public void Show(string[] captions, AudioClip[] clips, Action onDone = null)
        {
            if (captions == null || captions.Length == 0) { onDone?.Invoke(); return; }
            BuildIfNeeded();
            if (_running != null) StopCoroutine(_running);
            if (_voiceSource != null) _voiceSource.Stop();   // a superseded run's clip must not bleed into this one
            _running = StartCoroutine(Run(captions, clips, onDone));
        }

        // Immediate dismiss — used by automated verification and as a safety hatch.
        public void SkipAll()
        {
            if (_running == null) return;
            StopCoroutine(_running);
            _running = null;
            if (_voiceSource != null) _voiceSource.Stop();
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            PlayerInteractor.Suspended = false;
            PlayerInteractor.SetPlayerInputEnabled(true);
        }

        private void PlayVoice(AudioClip clip)
        {
            if (_voiceSource == null)
            {
                if (clip == null) return;
                _voiceSource = gameObject.AddComponent<AudioSource>();
                _voiceSource.playOnAwake = false;
                _voiceSource.spatialBlend = 0f;
                _voiceSource.outputAudioMixerGroup = _voiceOutput;
            }
            _voiceSource.Stop();
            if (clip == null) return;
            _voiceSource.clip = clip;
            _voiceSource.Play();
        }

        private IEnumerator Run(string[] captions, AudioClip[] clips, Action onDone)
        {
            PlayerInteractor.Suspended = true;
            PlayerInteractor.SetPlayerInputEnabled(false);

            _canvas.gameObject.SetActive(true);
            _group.alpha = 0f;
            _caption.text = "";

            yield return Fade(0f, 1f);

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

                // A voiced caption holds until its read finishes (+ a breath); input can
                // still advance early after the minimum hold — the clip cuts with it.
                float autoAdvance = clip != null ? Mathf.Max(_autoAdvanceSeconds, clip.length + 0.8f) : _autoAdvanceSeconds;
                float shown = Time.unscaledTime;
                while (true)
                {
                    float elapsed = Time.unscaledTime - shown;
                    if (elapsed >= autoAdvance) break;
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
            _canvas.gameObject.SetActive(false);

            PlayerInteractor.Suspended = false;
            PlayerInteractor.SetPlayerInputEnabled(true);
            _running = null;
            onDone?.Invoke();
        }

        private IEnumerator Fade(float from, float to)
        {
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < _fadeSeconds)
            {
                _group.alpha = Mathf.Lerp(from, to, (Time.unscaledTime - t0) / _fadeSeconds);
                yield return null;
            }
            _group.alpha = to;
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
            _canvas.sortingOrder = 70; // above dialogue (60) and map (50)
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;

            var black = UICanvasUtil.NewImage("Black", canvasGo.transform, Color.black, false);
            UICanvasUtil.Stretch((RectTransform)black.transform);

            _caption = UICanvasUtil.NewHeading("Caption", canvasGo.transform, "", 38f,
                new Color(0.93f, 0.90f, 0.82f, 1f), FontStyles.Italic, TextAlignmentOptions.Center);
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
    }
}
