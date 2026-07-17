using System.Collections.Generic;
using Hollowfen.GameTime;
using Hollowfen.Map;
using UnityEngine;
using UnityEngine.Audio;

namespace Hollowfen.Audio
{
    /// <summary>
    /// Procedural, region-aware ambience shared by menu and gameplay. Two day/night sources per
    /// bank blend with the art-directed clock; two banks allow equal-power region crossfades.
    /// Clips are synthesized lazily with fixed seeds and cached for the scene lifetime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbienceManager : MonoBehaviour
    {
        public static AmbienceManager Instance { get; private set; }

        [SerializeField, Tooltip("Route through the mixer Master group; the dedicated Ambience preference trims the sources.")]
        private AudioMixerGroup _output;
        [SerializeField, Min(.1f)] private float _fadeInSeconds = 3.5f;
        [SerializeField, Min(.1f)] private float _regionCrossfadeSeconds = 4f;
        [SerializeField, Range(0f, 1f)] private float _ceiling = .38f;
        [SerializeField, Tooltip("Used until a gameplay RegionTrigger establishes a more specific region.")]
        private string _defaultGameplayRegion = "village";
        [SerializeField, Tooltip("The main-menu painting uses this woodland atmosphere when no game clock exists.")]
        private string _menuRegion = "old_wood";

        public string CurrentRegion => _bankRegions[_activeBank] ?? "";
        public string PendingRegion => _transitioning ? _bankRegions[_toBank] ?? "" : "";
        public bool IsTransitioning => _transitioning;
        public float NightBlend { get; private set; }
        public float CurrentDayVolume { get; private set; }
        public float CurrentNightVolume { get; private set; }
        public int SynthesizedProfileCount => _clipCache.Count;
        public int LiveSourceCount => _started ? 4 : 0;
        public AudioMixerGroup Output => _output;
        public AudioClip ActiveDayClip => _sources[_activeBank, Day] != null
            ? _sources[_activeBank, Day].clip : null;
        public AudioClip ActiveNightClip => _sources[_activeBank, Night] != null
            ? _sources[_activeBank, Night].clip : null;

        private const string PrefAmbience = "audio.ambience";
        private const float DefaultUserVolume = .8f;
        private const int Rate = 48000;
        private const float LoopSeconds = 12f;
        private const int Day = 0;
        private const int Night = 1;

        private readonly AudioSource[,] _sources = new AudioSource[2, 2];
        private readonly string[] _bankRegions = new string[2];
        private readonly float[] _bankWeights = new float[2];
        private readonly Dictionary<string, AudioClip[]> _clipCache =
            new Dictionary<string, AudioClip[]>(4);

        private DayNightLighting _lighting;
        private int _activeBank;
        private int _fromBank;
        private int _toBank;
        private float _transitionProgress;
        private float _startupBlend;
        private float _userVolume = DefaultUserVolume;
        private bool _transitioning;
        private bool _started;

        private readonly struct Profile
        {
            public readonly int Seed;
            public readonly float Wind;
            public readonly float Leaves;
            public readonly float Birds;
            public readonly float Insects;
            public readonly float DryStream;
            public readonly float Drone;
            public readonly float Gain;

            public Profile(int seed, float wind, float leaves, float birds, float insects,
                float dryStream, float drone, float gain)
            {
                Seed = seed;
                Wind = wind;
                Leaves = leaves;
                Birds = birds;
                Insects = insects;
                DryStream = dryStream;
                Drone = drone;
                Gain = gain;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            LocationRegistry.RegionChanged += HandleRegionChanged;
        }

        private void OnDisable()
        {
            LocationRegistry.RegionChanged -= HandleRegionChanged;
        }

        private void Start()
        {
            _userVolume = PlayerPrefs.GetFloat(PrefAmbience, DefaultUserVolume);
            if (TimeManager.Instance != null)
                _lighting = TimeManager.Instance.GetComponent<DayNightLighting>();

            for (int bank = 0; bank < 2; bank++)
                for (int phase = 0; phase < 2; phase++)
                    _sources[bank, phase] = CreateSource(bank, phase);

            _started = true;
            _startupBlend = _fadeInSeconds <= 0f ? 1f : 0f;
            string initial = ResolveInitialRegion();
            ConfigureBank(0, initial, true);
            _activeBank = 0;
            _bankWeights[0] = 1f;
            _bankWeights[1] = 0f;
            ApplyVolumes();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var pair in _clipCache)
            {
                var clips = pair.Value;
                if (clips == null) continue;
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i] != null) Destroy(clips[i]);
            }
            _clipCache.Clear();
        }

        private void Update()
        {
            if (!_started) return;
            float dt = Time.unscaledDeltaTime;
            if (_startupBlend < 1f)
                _startupBlend = Mathf.MoveTowards(_startupBlend, 1f,
                    dt / Mathf.Max(.01f, _fadeInSeconds));

            if (_transitioning)
            {
                _transitionProgress += dt / Mathf.Max(.01f, _regionCrossfadeSeconds);
                float k = Mathf.Clamp01(_transitionProgress);
                _bankWeights[_fromBank] = Mathf.Cos(k * Mathf.PI * .5f);
                _bankWeights[_toBank] = Mathf.Sin(k * Mathf.PI * .5f);
                if (k >= 1f) CompleteTransitionImmediate();
            }

            ApplyVolumes();
        }

        public void SetUserVolume(float value)
        {
            _userVolume = Mathf.Clamp01(value);
            if (_started) ApplyVolumes();
        }

        public void BeginRegionTransition(string regionId)
        {
            regionId = NormalizeRegion(regionId);
            if (!_started || string.IsNullOrEmpty(regionId)) return;
            if (regionId == CurrentRegion && !_transitioning) return;
            if (_transitioning && regionId == PendingRegion) return;
            if (_transitioning) CommitDominantBank();

            _fromBank = _activeBank;
            _toBank = 1 - _activeBank;
            ConfigureBank(_toBank, regionId, true);
            _transitionProgress = 0f;
            _bankWeights[_fromBank] = 1f;
            _bankWeights[_toBank] = 0f;
            _transitioning = true;
            ApplyVolumes();
        }

        /// <summary>Immediate state seam for initialization, focused verification, and editor previews.</summary>
        public void SetRegionImmediate(string regionId)
        {
            regionId = NormalizeRegion(regionId);
            if (!_started || string.IsNullOrEmpty(regionId)) return;
            StopBank(1 - _activeBank);
            ConfigureBank(_activeBank, regionId, true);
            _bankWeights[_activeBank] = 1f;
            _bankWeights[1 - _activeBank] = 0f;
            _transitioning = false;
            ApplyVolumes();
        }

        public void CompleteTransitionImmediate()
        {
            if (!_transitioning) return;
            StopBank(_fromBank);
            _activeBank = _toBank;
            _bankWeights[_activeBank] = 1f;
            _bankWeights[1 - _activeBank] = 0f;
            _transitioning = false;
            _transitionProgress = 1f;
            ApplyVolumes();
        }

        public void RefreshImmediate()
        {
            if (_started) ApplyVolumes();
        }

        private void HandleRegionChanged(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return; // retain the last atmosphere across small trigger gaps
            BeginRegionTransition(regionId);
        }

        private string ResolveInitialRegion()
        {
            if (!string.IsNullOrEmpty(LocationRegistry.CurrentRegion))
                return NormalizeRegion(LocationRegistry.CurrentRegion);
            return TimeManager.Instance == null ? NormalizeRegion(_menuRegion) :
                NormalizeRegion(_defaultGameplayRegion);
        }

        private static string NormalizeRegion(string regionId)
        {
            return RegionCatalog.IsKnown(regionId) ? regionId : "village";
        }

        private AudioSource CreateSource(int bank, int phase)
        {
            var sourceObject = new GameObject("Ambience_" + bank + "_" +
                (phase == Day ? "Day" : "Night"));
            sourceObject.transform.SetParent(transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 20;
            source.outputAudioMixerGroup = _output;
            source.volume = 0f;
            return source;
        }

        private void ConfigureBank(int bank, string regionId, bool play)
        {
            var clips = GetOrCreateClips(regionId);
            _bankRegions[bank] = regionId;
            for (int phase = 0; phase < 2; phase++)
            {
                var source = _sources[bank, phase];
                if (source.clip != clips[phase])
                {
                    source.Stop();
                    source.clip = clips[phase];
                }
                if (play && !source.isPlaying) source.Play();
            }
        }

        private void StopBank(int bank)
        {
            for (int phase = 0; phase < 2; phase++)
            {
                var source = _sources[bank, phase];
                if (source == null) continue;
                source.volume = 0f;
                source.Stop();
            }
            _bankRegions[bank] = null;
        }

        private void CommitDominantBank()
        {
            int keep = _bankWeights[_toBank] >= _bankWeights[_fromBank] ? _toBank : _fromBank;
            int stop = 1 - keep;
            StopBank(stop);
            _activeBank = keep;
            _bankWeights[keep] = 1f;
            _bankWeights[stop] = 0f;
            _transitioning = false;
        }

        private void ApplyVolumes()
        {
            NightBlend = ResolveNightBlend();
            float dayGain = Mathf.Cos(NightBlend * Mathf.PI * .5f);
            float nightGain = Mathf.Sin(NightBlend * Mathf.PI * .5f);
            float baseGain = _ceiling * _userVolume * _startupBlend;
            CurrentDayVolume = 0f;
            CurrentNightVolume = 0f;

            for (int bank = 0; bank < 2; bank++)
            {
                string region = _bankRegions[bank];
                float profileGain = string.IsNullOrEmpty(region) ? 0f : GetProfile(region).Gain;
                float bankGain = _bankWeights[bank] * profileGain * baseGain;
                float dayVolume = bankGain * dayGain;
                float nightVolume = bankGain * nightGain;
                if (_sources[bank, Day] != null) _sources[bank, Day].volume = dayVolume;
                if (_sources[bank, Night] != null) _sources[bank, Night].volume = nightVolume;
                CurrentDayVolume += dayVolume;
                CurrentNightVolume += nightVolume;
            }
        }

        private float ResolveNightBlend()
        {
            if (_lighting != null) return Mathf.Clamp01(_lighting.NightBlend);
            if (TimeManager.Instance == null) return .12f;
            float hour = TimeManager.Instance.Hour;
            if (hour >= 21f || hour < 4.75f) return 1f;
            if (hour >= 18f) return Smooth01((hour - 18f) / 3f);
            if (hour < 7f) return 1f - Smooth01((hour - 4.75f) / 2.25f);
            return 0f;
        }

        private AudioClip[] GetOrCreateClips(string regionId)
        {
            if (_clipCache.TryGetValue(regionId, out var clips)) return clips;
            var profile = GetProfile(regionId);
            clips = new[]
            {
                Synthesize(regionId, profile, false),
                Synthesize(regionId, profile, true),
            };
            _clipCache.Add(regionId, clips);
            return clips;
        }

        private static Profile GetProfile(string regionId)
        {
            switch (regionId)
            {
                case "old_wood": return new Profile(4103, .18f, .050f, .042f, .045f, .010f, .008f, .90f);
                case "wend":     return new Profile(2909, .15f, .025f, .026f, .028f, .060f, .006f, .84f);
                case "manor":    return new Profile(7331, .11f, .016f, .018f, .020f, .008f, .012f, .72f);
                default:         return new Profile(1601, .13f, .035f, .050f, .036f, .012f, .003f, .82f);
            }
        }

        private static AudioClip Synthesize(string regionId, Profile profile, bool night)
        {
            int count = Mathf.RoundToInt(Rate * LoopSeconds);
            int crossfade = Mathf.RoundToInt(Rate * .5f);
            var work = new float[count + crossfade];
            var random = new System.Random(profile.Seed + (night ? 7919 : 0));
            float low = 0f;
            float lower = 0f;
            float previous = 0f;
            float nightWind = night ? 1.12f : 1f;
            float leafScale = night ? .42f : 1f;

            for (int i = 0; i < work.Length; i++)
            {
                float t = i / (float)Rate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += .035f * (white - low);
                lower += .055f * (low - lower);
                float gust = .58f + .27f * Mathf.Sin(2f * Mathf.PI * (2f / LoopSeconds) * t)
                    + .15f * Mathf.Sin(2f * Mathf.PI * (5f / LoopSeconds) * t + 1.1f);
                float wind = lower * gust * profile.Wind * nightWind;

                float high = white - previous;
                previous = white;
                float leafMotion = .45f + .55f * Mathf.Sin(2f * Mathf.PI *
                    (3f / LoopSeconds) * t + .6f);
                float leaves = high * leafMotion * profile.Leaves * leafScale;

                float dryCourse = (low - lower) * profile.DryStream *
                    (.65f + .35f * Mathf.Sin(2f * Mathf.PI * (4f / LoopSeconds) * t + 2f));
                float drone = profile.Drone * Mathf.Sin(2f * Mathf.PI * (night ? 74f : 92f) * t) *
                    (.5f + .5f * Mathf.Sin(2f * Mathf.PI * (1f / LoopSeconds) * t));
                work[i] = wind + leaves + dryCourse + drone;
            }

            if (night)
            {
                AddInsects(work, 1.8f, profile.Insects, profile.Seed + 17);
                AddInsects(work, 6.4f, profile.Insects * .86f, profile.Seed + 31);
                AddInsects(work, 9.2f, profile.Insects * .72f, profile.Seed + 47);
                if (regionId == "old_wood") AddOwl(work, 4.15f, .026f);
            }
            else
            {
                AddBird(work, 2.2f, 2350f + profile.Seed % 500, 3, profile.Birds);
                AddBird(work, 7.1f, 2800f + profile.Seed % 350, 2, profile.Birds * .82f);
                AddBird(work, 9.8f, 2050f + profile.Seed % 420, 4, profile.Birds * .68f);
            }

            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float sample;
                if (i < crossfade)
                {
                    float a = (i + 1) / (float)crossfade;
                    sample = work[i] * Mathf.Sin(a * Mathf.PI * .5f) +
                        work[count + i] * Mathf.Cos(a * Mathf.PI * .5f);
                }
                else sample = work[i];
                data[i] = Mathf.Clamp(sample, -.85f, .85f);
            }

            var clip = AudioClip.Create("Ambience_" + regionId + (night ? "_Night" : "_Day"),
                count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void AddBird(float[] data, float atSeconds, float baseHz, int syllables,
            float amplitude)
        {
            const float syllableLength = .09f;
            const float gap = .05f;
            int start = Mathf.RoundToInt(atSeconds * Rate);
            for (int syllable = 0; syllable < syllables; syllable++)
            {
                int offset = start + Mathf.RoundToInt(syllable * (syllableLength + gap) * Rate);
                int length = Mathf.RoundToInt(syllableLength * Rate);
                double phase = 0d;
                for (int i = 0; i < length; i++)
                {
                    int index = offset + i;
                    if (index < 0 || index >= data.Length) continue;
                    float u = i / (float)length;
                    float frequency = Mathf.Lerp(baseHz * 1.12f, baseHz * .86f, u);
                    phase += 2d * Mathf.PI * frequency / Rate;
                    data[index] += Mathf.Sin((float)phase) * Mathf.Sin(u * Mathf.PI) * amplitude;
                }
            }
        }

        private static void AddInsects(float[] data, float atSeconds, float amplitude, int seed)
        {
            var random = new System.Random(seed);
            int start = Mathf.RoundToInt(atSeconds * Rate);
            int pulses = 7;
            for (int pulse = 0; pulse < pulses; pulse++)
            {
                int length = Mathf.RoundToInt(.026f * Rate);
                int offset = start + Mathf.RoundToInt(pulse * .055f * Rate);
                float frequency = 4300f + (float)random.NextDouble() * 1500f;
                for (int i = 0; i < length; i++)
                {
                    int index = offset + i;
                    if (index < 0 || index >= data.Length) continue;
                    float u = i / (float)length;
                    data[index] += Mathf.Sin(2f * Mathf.PI * frequency * i / Rate) *
                        Mathf.Sin(u * Mathf.PI) * amplitude;
                }
            }
        }

        private static void AddOwl(float[] data, float atSeconds, float amplitude)
        {
            for (int hoot = 0; hoot < 2; hoot++)
            {
                int start = Mathf.RoundToInt((atSeconds + hoot * .72f) * Rate);
                int length = Mathf.RoundToInt(.42f * Rate);
                for (int i = 0; i < length; i++)
                {
                    int index = start + i;
                    if (index < 0 || index >= data.Length) continue;
                    float u = i / (float)length;
                    float frequency = Mathf.Lerp(540f, 460f, u);
                    float envelope = Mathf.Sin(u * Mathf.PI);
                    data[index] += Mathf.Sin(2f * Mathf.PI * frequency * i / Rate) *
                        envelope * amplitude;
                }
            }
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
