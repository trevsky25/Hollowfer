using UnityEngine;

namespace Hollowfen.Settings
{
    public static class GameSettings
    {
        private const string PrefLookSensitivity = "controls.lookSensitivity";
        public const float DefaultLookSensitivity = 1f;
        public const float MinLookSensitivity = 0.75f;
        public const float MaxLookSensitivity = 1.25f;

        // UI exposes a 1-10 slider; slider 5 maps EXACTLY to 1.0x (the tested baseline).
        // Slider 1 -> 0.75x; slider 10 -> 1.25x. Range kept tight to avoid threshold-gating
        // glitch at low end and camera-overshoot at high end.
        public const float MinSlider = 1f;
        public const float MaxSlider = 10f;
        public const float DefaultSlider = 5f;

        public static float SliderToMultiplier(float slider)
        {
            slider = Mathf.Clamp(slider, MinSlider, MaxSlider);
            if (slider <= 5f) return Mathf.Lerp(MinLookSensitivity, DefaultLookSensitivity, (slider - 1f) / 4f);
            return Mathf.Lerp(DefaultLookSensitivity, MaxLookSensitivity, (slider - 5f) / 5f);
        }

        public static float MultiplierToSlider(float mult)
        {
            mult = Mathf.Clamp(mult, MinLookSensitivity, MaxLookSensitivity);
            if (mult <= DefaultLookSensitivity)
                return 1f + (mult - MinLookSensitivity) / (DefaultLookSensitivity - MinLookSensitivity) * 4f;
            return 5f + (mult - DefaultLookSensitivity) / (MaxLookSensitivity - DefaultLookSensitivity) * 5f;
        }

        private static float _lookSensitivity = -1f;

        public static float LookSensitivity
        {
            get
            {
                if (_lookSensitivity < 0f)
                    _lookSensitivity = PlayerPrefs.GetFloat(PrefLookSensitivity, DefaultLookSensitivity);
                return _lookSensitivity;
            }
            set
            {
                _lookSensitivity = Mathf.Clamp(value, MinLookSensitivity, MaxLookSensitivity);
                PlayerPrefs.SetFloat(PrefLookSensitivity, _lookSensitivity);
            }
        }
    }
}
