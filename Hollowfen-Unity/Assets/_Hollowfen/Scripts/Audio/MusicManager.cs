using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Hollowfen.Audio
{
    // Minimal looping music bed (batch-29): one track, fade-in on scene load, routed through
    // the mixer's Music group so the existing settings slider governs it. Deliberately dumb —
    // region-aware music states and crossfades belong to the Audio-pass backlog item.
    [DisallowMultipleComponent]
    public class MusicManager : MonoBehaviour
    {
        [SerializeField, Tooltip("The looping bed. Null = component sleeps (safe to ship on the host with no track).")]
        private AudioClip _track;
        [SerializeField, Tooltip("Route through the mixer's Music group so the Music volume slider applies.")]
        private AudioMixerGroup _output;
        [SerializeField] private float _fadeInSeconds = 5f;
        [SerializeField, Range(0f, 1f), Tooltip("Source-level ceiling UNDER the mixer volume — keeps the bed behind dialogue/VO.")]
        private float _targetVolume = 0.55f;

        private AudioSource _source;

        private void Start()
        {
            if (_track == null) return;
            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = _track;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.priority = 16;   // the bed yields only to speech (priority 0), never to ambience
            _source.outputAudioMixerGroup = _output;
            _source.volume = 0f;
            _source.Play();
            StartCoroutine(FadeIn());
        }

        private IEnumerator FadeIn()
        {
            // Unscaled: the intro narration + dialogue freezes shouldn't stall the fade.
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < _fadeInSeconds)
            {
                _source.volume = Mathf.Lerp(0f, _targetVolume, (Time.unscaledTime - t0) / _fadeInSeconds);
                yield return null;
            }
            _source.volume = _targetVolume;
        }
    }
}
