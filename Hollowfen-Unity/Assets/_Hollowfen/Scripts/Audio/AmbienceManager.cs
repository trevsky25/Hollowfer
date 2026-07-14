using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Hollowfen.Audio
{
    // Procedural forest-ambience bed (batch-57). No audio assets — a soft, seamless-looping wood
    // atmosphere is synthesized once at Start (fixed seed → same bed every boot, like UICanvasUtil's
    // PaperGrain). Placed on the menu so the silent hero screen has an atmosphere.
    //
    // Layers: a low wind "shhh" (low-passed noise, slow gusts) + a quiet leaf shimmer (high-passed
    // noise) + a few sparse bird calls, all kept away from the loop seam; the buffer's head crossfades
    // with its tail so it loops without a click.
    //
    // Volume: an internal ceiling (kept well behind music/VO) times the player's Ambience setting
    // (pref "audio.ambience", live via SetUserVolume). Routed through the serialized mixer group so the
    // Master slider still governs it. This is source-level trim, not a dedicated mixer node — chosen
    // over risky .mixer surgery on the shipping asset; a true Ambience node can be added later if DSP
    // ducking is wanted.
    [DisallowMultipleComponent]
    public class AmbienceManager : MonoBehaviour
    {
        public static AmbienceManager Instance { get; private set; }

        [SerializeField, Tooltip("Route through the mixer (Master group) so the Master slider applies.")]
        private AudioMixerGroup _output;
        [SerializeField] private float _fadeInSeconds = 3.5f;
        [SerializeField, Range(0f, 1f), Tooltip("Source-level ceiling under the player's Ambience setting — keeps the bed well behind music and speech.")]
        private float _ceiling = 0.45f;

        private const string PrefAmbience = "audio.ambience";
        private const float DefaultUserVolume = 0.8f;

        private AudioSource _source;
        private float _userVolume = DefaultUserVolume;
        private const int Rate = 44100;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            _userVolume = PlayerPrefs.GetFloat(PrefAmbience, DefaultUserVolume);

            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = Synthesize(14f);
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.priority = 20;                 // below music (16) and speech (0) — first to virtualize
            _source.outputAudioMixerGroup = _output;
            _source.volume = 0f;
            _source.Play();
            StartCoroutine(FadeIn());
        }

        // Live update from the settings Ambience slider.
        public void SetUserVolume(float v)
        {
            _userVolume = Mathf.Clamp01(v);
            if (_source != null && !_fading) _source.volume = _userVolume * _ceiling;
        }

        private bool _fading;

        private IEnumerator FadeIn()
        {
            _fading = true;
            float target = _userVolume * _ceiling;
            float t = 0f;
            while (t < _fadeInSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = _fadeInSeconds <= 0f ? 1f : Mathf.Clamp01(t / _fadeInSeconds);
                // re-read target each frame so a mid-fade slider change is honored
                target = _userVolume * _ceiling;
                _source.volume = target * k;
                yield return null;
            }
            _source.volume = _userVolume * _ceiling;
            _fading = false;
        }

        // ---- synthesis ----

        private AudioClip Synthesize(float seconds)
        {
            int n = (int)(Rate * seconds);
            int xf = (int)(Rate * 0.5f);           // 0.5 s head/tail crossfade for a seamless loop
            var buf = new float[n + xf];
            var rng = new System.Random(20260713);  // fixed seed — same forest every boot

            // One-pole low-pass state for the wind bed, and a high-pass differentiator for shimmer.
            float lp = 0f, lp2 = 0f, prevWhite = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)Rate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Wind: two cascaded one-pole low-passes → soft "shhh"; gusts from periodic LFOs
                // (frequencies are integer cycles over the loop so the modulation itself is seamless).
                lp  += 0.05f * (white - lp);
                lp2 += 0.08f * (lp - lp2);
                float gust = 0.55f
                    + 0.30f * Mathf.Sin(2f * Mathf.PI * (2f / seconds) * t)
                    + 0.15f * Mathf.Sin(2f * Mathf.PI * (5f / seconds) * t + 1.3f);
                float wind = lp2 * gust * 0.16f;

                // Leaf shimmer: high-passed noise (difference), quiet, its own slow swell.
                float hp = white - prevWhite; prevWhite = white;
                float shimmerLfo = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (3f / seconds) * t + 0.7f);
                float shimmer = hp * shimmerLfo * 0.035f;

                buf[i] = wind + shimmer;
            }

            // Sparse bird calls, placed away from the crossfade seam (which spans [0,xf) and [n,n+xf)).
            AddBird(buf, 2.3f, 2600f, 3, rng);
            AddBird(buf, 6.9f, 3100f, 2, rng);
            AddBird(buf, 10.4f, 2200f, 4, rng);

            // Seamless loop: fold the tail (n..n+xf) back over the head (0..xf) with an equal-power
            // crossfade, so sample n-1 flows into sample 0.
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i < xf)
                {
                    float a = (i + 1) / (float)xf;               // 0→1 across the fade
                    float wIn = Mathf.Sin(a * Mathf.PI * 0.5f);   // equal-power
                    float wOut = Mathf.Cos(a * Mathf.PI * 0.5f);
                    data[i] = buf[i] * wIn + buf[n + i] * wOut;
                }
                else data[i] = buf[i];
            }

            // Gentle safety limiter so nothing clips.
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -0.9f, 0.9f);

            var clip = AudioClip.Create("ForestAmbience", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // A short multi-syllable chirp: a sine warble with a quick downward pitch glide per syllable.
        private void AddBird(float[] buf, float atSeconds, float baseHz, int syllables, System.Random rng)
        {
            int start = (int)(atSeconds * Rate);
            float sylLen = 0.09f;
            float gap = 0.05f;
            for (int s = 0; s < syllables; s++)
            {
                int s0 = start + (int)(s * (sylLen + gap) * Rate);
                int sn = (int)(sylLen * Rate);
                float f0 = baseHz * (1f + 0.10f * (float)(rng.NextDouble() - 0.5));
                double phase = 0.0;
                for (int i = 0; i < sn; i++)
                {
                    int idx = s0 + i;
                    if (idx < 0 || idx >= buf.Length) continue;
                    float u = i / (float)sn;
                    float freq = Mathf.Lerp(f0 * 1.15f, f0 * 0.85f, u);   // quick downward glide
                    phase += 2.0 * Mathf.PI * freq / Rate;
                    float env = Mathf.Sin(u * Mathf.PI);                  // soft in/out
                    buf[idx] += Mathf.Sin((float)phase) * env * 0.055f;
                }
            }
        }
    }
}
