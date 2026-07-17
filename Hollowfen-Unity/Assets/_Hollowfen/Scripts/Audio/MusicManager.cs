using System;
using Hollowfen.GameTime;
using Hollowfen.Map;
using UnityEngine;
using UnityEngine.Audio;

namespace Hollowfen.Audio
{
    [Serializable]
    public struct RegionMusicState
    {
        public string regionId;
        [Tooltip("Optional regional track. Null inherits the main bed without restarting it.")]
        public AudioClip track;
        [Range(0f, 1.25f)] public float volumeScale;
        [Range(500f, 22000f)] public float dayLowPassHz;
        [Range(500f, 22000f)] public float nightLowPassHz;
    }

    /// <summary>
    /// Adaptive music bed with equal-power two-source track crossfades and smoothly evaluated
    /// region/day-night volume and low-pass states. Null regional tracks inherit the main bed,
    /// allowing the current score to react now and accept future compositions without a rewrite.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicManager : MonoBehaviour
    {
        [SerializeField, Tooltip("Default looping bed. Null = component sleeps safely.")]
        private AudioClip _track;
        [SerializeField] private AudioMixerGroup _output;
        [SerializeField, Min(.1f)] private float _fadeInSeconds = 5f;
        [SerializeField, Min(.1f)] private float _stateCrossfadeSeconds = 5f;
        [SerializeField, Range(0f, 1f)] private float _targetVolume = .55f;
        [SerializeField, Range(0f, 1f)] private float _nightVolumeScale = .82f;
        [SerializeField] private RegionMusicState[] _regionStates;

        public string CurrentRegion => _states[_activeBank].RegionId ?? "";
        public string PendingRegion => _transitioning ? _states[_toBank].RegionId ?? "" : "";
        public bool IsTransitioning => _transitioning;
        public float CurrentTargetVolume { get; private set; }
        public float CurrentLowPassHz { get; private set; } = 22000f;
        public float NightBlend { get; private set; }
        public int SourceCount => _started ? 2 : 0;
        public AudioMixerGroup Output => _output;

        private readonly AudioSource[] _sources = new AudioSource[2];
        private readonly AudioLowPassFilter[] _filters = new AudioLowPassFilter[2];
        private readonly ResolvedState[] _states = new ResolvedState[2];
        private readonly float[] _bankWeights = new float[2];

        private DayNightLighting _lighting;
        private int _activeBank;
        private int _fromBank;
        private int _toBank;
        private float _transitionProgress;
        private float _startupBlend;
        private bool _transitioning;
        private bool _started;

        private readonly struct ResolvedState
        {
            public readonly string RegionId;
            public readonly AudioClip Track;
            public readonly float VolumeScale;
            public readonly float DayLowPassHz;
            public readonly float NightLowPassHz;

            public ResolvedState(string regionId, AudioClip track, float volumeScale,
                float dayLowPassHz, float nightLowPassHz)
            {
                RegionId = regionId;
                Track = track;
                VolumeScale = volumeScale;
                DayLowPassHz = dayLowPassHz;
                NightLowPassHz = nightLowPassHz;
            }
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
            if (_track == null) return;
            if (TimeManager.Instance != null)
                _lighting = TimeManager.Instance.GetComponent<DayNightLighting>();

            for (int i = 0; i < 2; i++) CreateBank(i);
            _started = true;
            _startupBlend = _fadeInSeconds <= 0f ? 1f : 0f;
            _activeBank = 0;
            ConfigureBank(0, ResolveState(string.IsNullOrEmpty(LocationRegistry.CurrentRegion)
                ? "village" : LocationRegistry.CurrentRegion), true);
            _bankWeights[0] = 1f;
            ApplyMix(false);
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
                _transitionProgress += dt / Mathf.Max(.01f, _stateCrossfadeSeconds);
                float k = Mathf.Clamp01(_transitionProgress);
                _bankWeights[_fromBank] = Mathf.Cos(k * Mathf.PI * .5f);
                _bankWeights[_toBank] = Mathf.Sin(k * Mathf.PI * .5f);
                if (k >= 1f) CompleteTransitionImmediate();
            }
            ApplyMix(false);
        }

        public void BeginRegionTransition(string regionId)
        {
            if (!_started || string.IsNullOrEmpty(regionId)) return;
            var next = ResolveState(regionId);
            if (!_transitioning && next.RegionId == CurrentRegion) return;
            if (_transitioning && next.RegionId == PendingRegion) return;

            // A region can inherit the currently playing composition. Keep phase continuity and
            // let volume/filter targets glide instead of restarting the same clip.
            if (!_transitioning && next.Track == _states[_activeBank].Track)
            {
                _states[_activeBank] = next;
                ApplyMix(false);
                return;
            }

            if (_transitioning) CommitDominantBank();
            _fromBank = _activeBank;
            _toBank = 1 - _activeBank;
            ConfigureBank(_toBank, next, true);
            _transitionProgress = 0f;
            _bankWeights[_fromBank] = 1f;
            _bankWeights[_toBank] = 0f;
            _transitioning = true;
            ApplyMix(false);
        }

        public void SetRegionImmediate(string regionId)
        {
            if (!_started || string.IsNullOrEmpty(regionId)) return;
            var state = ResolveState(regionId);
            StopBank(1 - _activeBank);
            ConfigureBank(_activeBank, state, true);
            _bankWeights[_activeBank] = 1f;
            _bankWeights[1 - _activeBank] = 0f;
            _transitioning = false;
            ApplyMix(true);
        }

        public void CompleteTransitionImmediate()
        {
            if (!_transitioning) return;
            StopBank(_fromBank);
            _activeBank = _toBank;
            _bankWeights[_activeBank] = 1f;
            _bankWeights[1 - _activeBank] = 0f;
            _transitioning = false;
            ApplyMix(true);
        }

        public void RefreshImmediate()
        {
            if (_started) ApplyMix(true);
        }

        private void HandleRegionChanged(string regionId)
        {
            if (!string.IsNullOrEmpty(regionId)) BeginRegionTransition(regionId);
        }

        private void CreateBank(int index)
        {
            var sourceObject = new GameObject("MusicBank_" + index);
            sourceObject.transform.SetParent(transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 16;
            source.outputAudioMixerGroup = _output;
            source.volume = 0f;
            _sources[index] = source;

            var filter = sourceObject.AddComponent<AudioLowPassFilter>();
            filter.lowpassResonanceQ = 1f;
            filter.cutoffFrequency = 22000f;
            _filters[index] = filter;
        }

        private void ConfigureBank(int bank, ResolvedState state, bool play)
        {
            var source = _sources[bank];
            if (source.clip != state.Track)
            {
                source.Stop();
                source.clip = state.Track;
            }
            _states[bank] = state;
            if (play && state.Track != null && !source.isPlaying) source.Play();
        }

        private void StopBank(int bank)
        {
            if (_sources[bank] != null)
            {
                _sources[bank].volume = 0f;
                _sources[bank].Stop();
            }
            _states[bank] = default;
            _bankWeights[bank] = 0f;
        }

        private void CommitDominantBank()
        {
            int keep = _bankWeights[_toBank] >= _bankWeights[_fromBank] ? _toBank : _fromBank;
            StopBank(1 - keep);
            _activeBank = keep;
            _bankWeights[keep] = 1f;
            _transitioning = false;
        }

        private void ApplyMix(bool immediate)
        {
            NightBlend = ResolveNightBlend();
            float nightScale = Mathf.Lerp(1f, _nightVolumeScale, NightBlend);
            CurrentTargetVolume = 0f;
            CurrentLowPassHz = 22000f;

            for (int bank = 0; bank < 2; bank++)
            {
                var state = _states[bank];
                if (_sources[bank] == null || state.Track == null)
                {
                    if (_sources[bank] != null) _sources[bank].volume = 0f;
                    continue;
                }

                float target = _targetVolume * Mathf.Max(0f, state.VolumeScale) *
                    nightScale * _startupBlend;
                float desiredVolume = target * _bankWeights[bank];
                float cutoff = Mathf.Clamp(Mathf.Lerp(state.DayLowPassHz, state.NightLowPassHz,
                    NightBlend), 500f, 22000f);
                if (immediate)
                {
                    _sources[bank].volume = desiredVolume;
                    _filters[bank].cutoffFrequency = cutoff;
                }
                else
                {
                    float duration = Mathf.Max(.1f, _stateCrossfadeSeconds);
                    _sources[bank].volume = Mathf.MoveTowards(_sources[bank].volume, desiredVolume,
                        Time.unscaledDeltaTime * Mathf.Max(.05f, _targetVolume) / duration);
                    _filters[bank].cutoffFrequency = Mathf.MoveTowards(
                        _filters[bank].cutoffFrequency, cutoff,
                        Time.unscaledDeltaTime * 21500f / duration);
                }

                if (bank == _activeBank || (_transitioning && bank == _toBank &&
                    _bankWeights[bank] >= _bankWeights[_activeBank]))
                {
                    CurrentTargetVolume = target;
                    CurrentLowPassHz = _filters[bank].cutoffFrequency;
                }
            }
        }

        private ResolvedState ResolveState(string regionId)
        {
            regionId = RegionCatalog.IsKnown(regionId) ? regionId : "village";
            if (_regionStates != null)
            {
                for (int i = 0; i < _regionStates.Length; i++)
                {
                    var state = _regionStates[i];
                    if (state.regionId != regionId) continue;
                    return new ResolvedState(regionId, state.track != null ? state.track : _track,
                        state.volumeScale <= 0f ? 1f : state.volumeScale,
                        state.dayLowPassHz <= 0f ? 22000f : state.dayLowPassHz,
                        state.nightLowPassHz <= 0f ? 13500f : state.nightLowPassHz);
                }
            }
            return new ResolvedState(regionId, _track, 1f, 22000f, 13500f);
        }

        private float ResolveNightBlend()
        {
            if (_lighting != null) return Mathf.Clamp01(_lighting.NightBlend);
            if (TimeManager.Instance == null) return 0f;
            float hour = TimeManager.Instance.Hour;
            if (hour >= 21f || hour < 4.75f) return 1f;
            if (hour >= 18f) return Smooth01((hour - 18f) / 3f);
            if (hour < 7f) return 1f - Smooth01((hour - 4.75f) / 2.25f);
            return 0f;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
