using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Hollowfen.UI
{
    // Procedural UI sound set (batch-44 seed, expanded batch-56). No external audio assets — every
    // cue is synthesized once at first use, matching the procedural tier of the UI primitives
    // (RoundedRect, PaperGrain). UIManager passes its serialized SFX mixer group at Awake so every
    // cue respects the SFX slider.
    //
    // The set — each cue is a short, subtle, warm gesture (it marks the action, doesn't announce it):
    //   Move    — arrowing focus between controls: a soft, high, very short tick.
    //   Select  — opening/advancing a screen: a fingernail tap with a warm thump under it.
    //   Back    — closing/retreating: a gentle downward glide.
    //   Confirm — a decisive affirmative (modal confirm): two soft rising notes.
    //   Error   — an invalid action: a muted low double-tone, never harsh.
    // Click() is kept as a Select alias for older call sites.
    public static class UISfx
    {
        private static AudioSource _source;
        private static AudioMixerGroup _output;

        private static AudioClip _move, _select, _back, _confirm, _error;

        public static void SetOutput(AudioMixerGroup group) => _output = group;

        public static void Move(float volume = 0.30f)    { Play(ref _move,    BuildMove,    volume); }
        public static void Select(float volume = 0.55f)  { Play(ref _select,  BuildSelect,  volume); }
        public static void Back(float volume = 0.50f)    { Play(ref _back,    BuildBack,    volume); }
        public static void Confirm(float volume = 0.55f) { Play(ref _confirm, BuildConfirm, volume); }
        public static void Error(float volume = 0.55f)   { Play(ref _error,   BuildError,   volume); }

        // Back-compat: the batch-44 page-turn tick.
        public static void Click(float volume = 0.55f) => Select(volume);

        private static void Play(ref AudioClip clip, Func<AudioClip> build, float volume)
        {
            EnsureSource();
            if (_source == null) return;
            if (clip == null) clip = build();
            _source.PlayOneShot(clip, volume);
        }

        private static void EnsureSource()
        {
            if (_source != null) return;
            var go = new GameObject("_UISfx");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;      // UI-space
            _source.priority = 16;          // above ambience, below speech
            _source.ignoreListenerPause = true;
            _source.outputAudioMixerGroup = _output;
        }

        private const int Rate = 44100;

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Short attack ramp so a transient doesn't pop the DAC.
        private static float Attack(float t, float ms = 0.002f) => Mathf.Clamp01(t / ms);

        private static AudioClip BuildSelect()
        {
            float seconds = 0.06f; int n = (int)(Rate * seconds); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float tap = Mathf.Sin(2f * Mathf.PI * 1700f * t) * Mathf.Exp(-t * 160f) * 0.9f;
                float thump = Mathf.Sin(2f * Mathf.PI * 240f * t) * Mathf.Exp(-t * 70f) * 0.35f;
                d[i] = (tap + thump) * Attack(t) * 0.5f;
            }
            return Make("UISelect", d);
        }

        private static AudioClip BuildMove()
        {
            // Lighter, higher, shorter than Select — a barely-there focus blip.
            float seconds = 0.035f; int n = (int)(Rate * seconds); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float tick = Mathf.Sin(2f * Mathf.PI * 2300f * t) * Mathf.Exp(-t * 240f) * 0.8f;
                d[i] = tick * Attack(t, 0.0015f) * 0.5f;
            }
            return Make("UIMove", d);
        }

        private static AudioClip BuildBack()
        {
            // Downward glide 760 -> 420 Hz with a soft thump — a "close/retreat" gesture.
            float seconds = 0.075f; int n = (int)(Rate * seconds); var d = new float[n];
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float u = t / seconds;
                float freq = Mathf.Lerp(760f, 420f, u);
                phase += 2.0 * Mathf.PI * freq / Rate;
                float glide = Mathf.Sin((float)phase) * Mathf.Exp(-t * 55f) * 0.7f;
                float thump = Mathf.Sin(2f * Mathf.PI * 200f * t) * Mathf.Exp(-t * 60f) * 0.3f;
                d[i] = (glide + thump) * Attack(t) * 0.5f;
            }
            return Make("UIBack", d);
        }

        private static AudioClip BuildConfirm()
        {
            // Two soft rising notes C5 -> G5, gentle sustain — an affirmative "done".
            float seconds = 0.16f; int n = (int)(Rate * seconds); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float e1 = Mathf.Exp(-t * 22f) * Mathf.Clamp01(1f - t / 0.09f);
                float g = Mathf.Clamp01((t - 0.045f) / 0.02f);
                float e2 = g * Mathf.Exp(-(t - 0.045f) * 14f);
                float n1 = Mathf.Sin(2f * Mathf.PI * 523.25f * t) * e1 * 0.7f;
                float n2 = Mathf.Sin(2f * Mathf.PI * 783.99f * t) * e2 * 0.7f;
                d[i] = (n1 + n2) * Attack(t) * 0.45f;
            }
            return Make("UIConfirm", d);
        }

        private static AudioClip BuildError()
        {
            // Muted low double-tone (160 + 168 Hz beating), soft envelope — a non-harsh "no".
            float seconds = 0.13f; int n = (int)(Rate * seconds); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 0.008f) * Mathf.Exp(-t * 26f);
                float a = Mathf.Sin(2f * Mathf.PI * 160f * t);
                float b = Mathf.Sin(2f * Mathf.PI * 168f * t);
                d[i] = (a + b) * 0.5f * env * 0.5f;
            }
            return Make("UIError", d);
        }
    }
}
