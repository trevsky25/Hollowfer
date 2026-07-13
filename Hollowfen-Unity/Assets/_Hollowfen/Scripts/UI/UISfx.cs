using UnityEngine;
using UnityEngine.Audio;

namespace Hollowfen.UI
{
    // Procedural UI sound effects (batch-44). No external audio assets — the click is synthesized
    // once at first use, matching the procedural tier of the UI primitives (RoundedRect, PaperGrain).
    // UIManager passes its serialized SFX mixer group at Awake so the click respects the SFX slider.
    //
    // The click itself: a soft fingernail "tick" — a fast-decaying 1.7 kHz tap layered over a quiet
    // 240 Hz thump, ~60 ms total. Subtle by design; it marks page turns, it doesn't announce them.
    public static class UISfx
    {
        private static AudioSource _source;
        private static AudioClip _click;
        private static AudioMixerGroup _output;

        public static void SetOutput(AudioMixerGroup group) => _output = group;

        // Page/tab transition tick. Safe to call from any screen code; no-ops without an audio device.
        public static void Click(float volume = 0.55f)
        {
            EnsureSource();
            if (_source == null) return;
            _source.PlayOneShot(_click, volume);
        }

        private static void EnsureSource()
        {
            if (_source != null) return;
            var go = new GameObject("_UISfx");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;      // UI-space
            _source.priority = 16;          // above ambience, below speech
            _source.ignoreListenerPause = true;
            _source.outputAudioMixerGroup = _output;
            _click = SynthesizeClick();
        }

        private static AudioClip SynthesizeClick()
        {
            const int rate = 44100;
            const float seconds = 0.06f;
            int n = (int)(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                // Tap: 1.7 kHz sine, very fast decay — the "click" transient.
                float tap = Mathf.Sin(2f * Mathf.PI * 1700f * t) * Mathf.Exp(-t * 160f) * 0.9f;
                // Thump: 240 Hz body under it, slightly slower decay — keeps it warm, not glassy.
                float thump = Mathf.Sin(2f * Mathf.PI * 240f * t) * Mathf.Exp(-t * 70f) * 0.35f;
                // 2 ms attack ramp so the transient doesn't pop the DAC.
                float attack = Mathf.Clamp01(t / 0.002f);
                data[i] = (tap + thump) * attack * 0.5f;
            }
            var clip = AudioClip.Create("UIClick", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
