using System.Collections;
using Hollowfen.Audio;
using UnityEngine;

namespace Hollowfen.Weather
{
    /// <summary>Procedural rain, wind, shelter filtering, and distance-delayed thunder.</summary>
    [DefaultExecutionOrder(-65)]
    [DisallowMultipleComponent]
    public sealed class WeatherAudio : MonoBehaviour
    {
        private const int SampleRate = 48000;
        private const int LoopSeconds = 6;

        private AudioSource _rain;
        private AudioSource _wind;
        private AudioSource _thunder;
        private AudioLowPassFilter _rainFilter;
        private AudioClip _rainClip;
        private AudioClip _windClip;
        private AudioClip _thunderClip;

        private void Start()
        {
            _rainClip = BuildNoiseLoop("Weather_Rain", 113, true);
            _windClip = BuildNoiseLoop("Weather_Wind", 227, false);
            _thunderClip = BuildThunder();
            _rain = CreateSource("WeatherRain", _rainClip, true, 25);
            _wind = CreateSource("WeatherWind", _windClip, true, 26);
            _thunder = CreateSource("WeatherThunder", _thunderClip, false, 12);
            _rainFilter = _rain.gameObject.AddComponent<AudioLowPassFilter>();
            _rainFilter.cutoffFrequency = 12000f;
            WeatherSystem.Lightning += HandleLightning;
        }

        private void Update()
        {
            var weather = WeatherSystem.Instance;
            if (weather == null || _rain == null) return;
            RouteToAmbienceMixer();
            float user = AmbienceManager.Instance != null
                ? AmbienceManager.Instance.UserVolume
                : .8f;
            WeatherState state = weather.CurrentState;
            float outdoor = weather.OutdoorExposure;
            _rain.volume = state.Precipitation * Mathf.Lerp(.16f, .58f, outdoor) * user;
            _wind.volume = state.Wind * Mathf.Lerp(.10f, .32f, outdoor) * user;
            _rainFilter.cutoffFrequency = Mathf.Lerp(1750f, 14500f, outdoor);
            _rainFilter.lowpassResonanceQ = Mathf.Lerp(1.35f, 1f, outdoor);
        }

        private void OnDestroy()
        {
            WeatherSystem.Lightning -= HandleLightning;
            if (_rainClip != null) Destroy(_rainClip);
            if (_windClip != null) Destroy(_windClip);
            if (_thunderClip != null) Destroy(_thunderClip);
        }

        private void HandleLightning(float distance)
        {
            if (isActiveAndEnabled) StartCoroutine(PlayThunder(distance));
        }

        private IEnumerator PlayThunder(float distance)
        {
            yield return new WaitForSeconds(Mathf.Lerp(.35f, 1.9f, distance));
            if (_thunder == null || WeatherSystem.Instance == null) yield break;
            float user = AmbienceManager.Instance != null
                ? AmbienceManager.Instance.UserVolume
                : .8f;
            _thunder.volume = Mathf.Lerp(.55f, .24f, distance) * user;
            _thunder.pitch = Mathf.Lerp(1.02f, .82f, distance);
            _thunder.Play();
        }

        private AudioSource CreateSource(string objectName, AudioClip clip, bool loop, int priority)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = priority;
            if (AmbienceManager.Instance != null)
                source.outputAudioMixerGroup = AmbienceManager.Instance.Output;
            if (loop) source.Play();
            return source;
        }

        private void RouteToAmbienceMixer()
        {
            if (AmbienceManager.Instance == null || AmbienceManager.Instance.Output == null) return;
            if (_rain != null && _rain.outputAudioMixerGroup == null)
                _rain.outputAudioMixerGroup = AmbienceManager.Instance.Output;
            if (_wind != null && _wind.outputAudioMixerGroup == null)
                _wind.outputAudioMixerGroup = AmbienceManager.Instance.Output;
            if (_thunder != null && _thunder.outputAudioMixerGroup == null)
                _thunder.outputAudioMixerGroup = AmbienceManager.Instance.Output;
        }

        private static AudioClip BuildNoiseLoop(string name, int seed, bool rain)
        {
            int count = SampleRate * LoopSeconds;
            int overlap = SampleRate / 2;
            var work = new float[count + overlap];
            var random = new System.Random(seed);
            float low = 0f;
            float slow = 0f;
            for (int i = 0; i < work.Length; i++)
            {
                float white = (float)random.NextDouble() * 2f - 1f;
                low += (white - low) * (rain ? .18f : .008f);
                slow += (white - slow) * .0012f;
                float t = i / (float)SampleRate;
                float gust = .62f + .38f * Mathf.Sin(t * (rain ? .83f : .31f) * Mathf.PI * 2f + seed);
                work[i] = rain
                    ? (white * .035f + low * .11f) * gust
                    : (slow * .42f + low * .035f) * gust;
            }

            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (i < overlap)
                {
                    float t = i / (float)overlap;
                    data[i] = work[i] * Mathf.Sin(t * Mathf.PI * .5f) +
                              work[count + i] * Mathf.Cos(t * Mathf.PI * .5f);
                }
                else data[i] = work[i];
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildThunder()
        {
            int count = SampleRate * 4;
            var data = new float[count];
            var random = new System.Random(991);
            float low = 0f;
            float lower = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)random.NextDouble() * 2f - 1f;
                low += (white - low) * .018f;
                lower += (low - lower) * .006f;
                float attack = Mathf.Clamp01(t * 15f);
                float decay = Mathf.Exp(-t * .86f);
                float roll = Mathf.Sin(2f * Mathf.PI * (38f + 7f * Mathf.Sin(t * 1.3f)) * t) * .08f;
                data[i] = Mathf.Clamp((lower * 2.8f + low * .65f + roll) * attack * decay,
                    -.82f, .82f);
            }
            var clip = AudioClip.Create("Weather_Thunder", count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
