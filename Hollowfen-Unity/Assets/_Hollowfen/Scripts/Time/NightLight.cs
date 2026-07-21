using UnityEngine;

namespace Hollowfen.GameTime
{
    /// <summary>
    /// Fades an authored practical light in after dusk and adds a restrained candle flicker.
    /// The visible fixture (window, hearth, lantern, or candle) remains ordinary scene art.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NightLight : MonoBehaviour
    {
        [SerializeField] private Light _light;
        [SerializeField, Min(0f)] private float _nightIntensity = 2.2f;
        [SerializeField, Min(.05f)] private float _fadeSpeed = 1.8f;
        [SerializeField, Range(0f, .25f)] private float _flickerAmount = .07f;
        [SerializeField, Min(.1f)] private float _flickerSpeed = 2.4f;

        public Light Light => _light;
        public float NightIntensity => _nightIntensity;
        public float TargetBlend => DayNightLighting.Instance != null
            ? DayNightLighting.Instance.NightBlend
            : 0f;

        private float _blend;
        private float _flickerSeed;

        private void Awake()
        {
            if (_light == null) _light = GetComponentInChildren<Light>(true);
            _flickerSeed = Mathf.Abs(transform.position.x * .173f + transform.position.z * .319f);
            _blend = TargetBlend;
            ApplyIntensity();
        }

        private void Update()
        {
            _blend = Mathf.MoveTowards(_blend, TargetBlend, _fadeSpeed * Time.deltaTime);
            ApplyIntensity();
        }

        public void RefreshImmediate()
        {
            _blend = TargetBlend;
            ApplyIntensity();
        }

        private void ApplyIntensity()
        {
            if (_light == null) return;
            float noise = Mathf.PerlinNoise(_flickerSeed, Time.unscaledTime * _flickerSpeed);
            float flicker = 1f + (noise - .5f) * 2f * _flickerAmount;
            _light.intensity = _nightIntensity * _blend * flicker;
            _light.enabled = _blend > .01f;
        }
    }
}
