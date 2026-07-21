using System.Collections;
using Hollowfen.UI;
using UnityEngine;

namespace Hollowfen.Apothecary
{
    /// <summary>
    /// Coordinates Hollowfen's bounded point lights with the purchased candle, chandelier, and
    /// lantern flame meshes. State is save-backed by ApothecaryRuntime; the visual transition is
    /// deliberately local and non-blocking.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApothecaryLightingController : MonoBehaviour
    {
        [SerializeField] private Light[] _lights;
        [SerializeField] private Renderer[] _flameRenderers;
        [SerializeField] private Animator[] _candleAnimators;
        [SerializeField, Min(.05f)] private float _fadeSeconds = .55f;

        private float[] _authoredIntensities;
        private Coroutine _transition;

        public bool LightsOn => ApothecaryRuntime.InteriorLightsOn;
        public bool IsTransitioning => _transition != null;
        public int LightCount => _lights?.Length ?? 0;
        public int FlameCount => _flameRenderers?.Length ?? 0;
        public int AnimatorCount => _candleAnimators?.Length ?? 0;

        public void Configure(Light[] lights, Renderer[] flameRenderers,
            Animator[] candleAnimators, float fadeSeconds)
        {
            _lights = lights;
            _flameRenderers = flameRenderers;
            _candleAnimators = candleAnimators;
            _fadeSeconds = Mathf.Max(.05f, fadeSeconds);
        }

        private void Awake()
        {
            CaptureAuthoredIntensities();
            ApplyImmediate(LightsOn);
        }

        private void OnEnable() => ApothecaryRuntime.OnChanged += HandleRuntimeChanged;

        private void OnDisable()
        {
            ApothecaryRuntime.OnChanged -= HandleRuntimeChanged;
            if (_transition != null) StopCoroutine(_transition);
            _transition = null;
        }

        public void Toggle()
        {
            if (IsTransitioning) return;
            if (ApothecaryRuntime.TrySetInteriorLights(!LightsOn))
                UISfx.Confirm(.42f);
            else
                UISfx.Error(.42f);
        }

        private void HandleRuntimeChanged()
        {
            if (!isActiveAndEnabled)
            {
                ApplyImmediate(LightsOn);
                return;
            }
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(TransitionTo(LightsOn));
        }

        private IEnumerator TransitionTo(bool on)
        {
            CaptureAuthoredIntensities();
            float[] starts = new float[LightCount];
            for (int i = 0; i < LightCount; i++)
            {
                Light light = _lights[i];
                if (light == null) continue;
                starts[i] = light.enabled ? light.intensity : 0f;
                if (on)
                {
                    light.enabled = true;
                    light.intensity = starts[i];
                }
            }

            if (on) SetFlamesVisible(true);
            PlayVendorAnimations(on);
            float elapsed = 0f;
            while (elapsed < _fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(elapsed / _fadeSeconds));
                for (int i = 0; i < LightCount; i++)
                {
                    Light light = _lights[i];
                    if (light == null) continue;
                    float target = on ? _authoredIntensities[i] : 0f;
                    light.intensity = Mathf.Lerp(starts[i], target, t);
                }
                yield return null;
            }

            for (int i = 0; i < LightCount; i++)
            {
                Light light = _lights[i];
                if (light == null) continue;
                light.intensity = on ? _authoredIntensities[i] : 0f;
                light.enabled = on;
            }
            if (!on) SetFlamesVisible(false);
            _transition = null;
        }

        public void ApplyImmediate(bool on)
        {
            CaptureAuthoredIntensities();
            for (int i = 0; i < LightCount; i++)
            {
                Light light = _lights[i];
                if (light == null) continue;
                light.intensity = on ? _authoredIntensities[i] : 0f;
                light.enabled = on;
            }
            SetFlamesVisible(on);
            PlayVendorAnimations(on);
        }

        private void CaptureAuthoredIntensities()
        {
            if (_authoredIntensities != null && _authoredIntensities.Length == LightCount) return;
            _authoredIntensities = new float[LightCount];
            for (int i = 0; i < LightCount; i++)
            {
                Light light = _lights[i];
                _authoredIntensities[i] = light != null && light.intensity > .001f
                    ? light.intensity : 1f;
            }
        }

        private void SetFlamesVisible(bool visible)
        {
            if (_flameRenderers == null) return;
            foreach (Renderer flame in _flameRenderers)
                if (flame != null) flame.enabled = visible;
        }

        private void PlayVendorAnimations(bool on)
        {
            if (_candleAnimators == null) return;
            foreach (Animator animator in _candleAnimators)
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;
                string controller = animator.runtimeAnimatorController.name;
                string state;
                if (controller == "Candle_Stand_2") state = on ? "Play1" : "Play0";
                else if (controller == "Candle_Stand" || controller == "Candlestick" ||
                         controller == "Lantern") state = on ? "Play0" : "Play1";
                else if (controller == "Chandelier") state = "Chandelier_Light_Idle";
                else continue;
                animator.Play(state, 0, 0f);
            }
        }
    }
}
