using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Hollowfen.Audio
{
    /// <summary>
    /// Runtime voice bus shim. Speech is routed to the configured mixer's Master group and
    /// trimmed at the source, which keeps it under Master while making it independent of SFX.
    /// This avoids muting all spoken dialogue when a player deliberately turns effects off.
    /// </summary>
    public static class VoiceAudio
    {
        private const string PrefVoice = "audio.voice";
        private const float DefaultVolume = 0.8f;

        private static readonly HashSet<AudioSource> Sources = new HashSet<AudioSource>();
        private static readonly Dictionary<AudioMixer, AudioMixerGroup> MasterGroups =
            new Dictionary<AudioMixer, AudioMixerGroup>();
        private static float _userVolume = -1f;

        public static float UserVolume
        {
            get
            {
                if (_userVolume < 0f)
                    _userVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefVoice, DefaultVolume));
                return _userVolume;
            }
        }

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            Sources.Clear();
            MasterGroups.Clear();
            _userVolume = -1f;
        }

        public static void Configure(AudioSource source, AudioMixerGroup configuredOutput)
        {
            if (source == null) return;
            source.outputAudioMixerGroup = ResolveMaster(configuredOutput);
            source.volume = UserVolume;
            Sources.Add(source);
        }

        public static void Unregister(AudioSource source)
        {
            if (source != null) Sources.Remove(source);
        }

        public static void SetUserVolume(float volume)
        {
            _userVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefVoice, _userVolume);

            Sources.RemoveWhere(source => source == null);
            foreach (var source in Sources)
                source.volume = _userVolume;
        }

        private static AudioMixerGroup ResolveMaster(AudioMixerGroup configuredOutput)
        {
            if (configuredOutput == null) return null;
            if (configuredOutput.name == "Master") return configuredOutput;

            var mixer = configuredOutput.audioMixer;
            if (mixer == null) return configuredOutput;
            if (MasterGroups.TryGetValue(mixer, out var cached) && cached != null) return cached;

            var candidates = mixer.FindMatchingGroups("Master");
            if (candidates != null)
            {
                foreach (var candidate in candidates)
                {
                    if (candidate != null && candidate.name == "Master")
                    {
                        MasterGroups[mixer] = candidate;
                        return candidate;
                    }
                }
            }

            // Preserve the authored route if this is ever used with a differently named mixer.
            return configuredOutput;
        }
    }
}
