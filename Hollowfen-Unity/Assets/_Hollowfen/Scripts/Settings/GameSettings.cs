using UnityEngine;

namespace Hollowfen.Settings
{
    public static class GameSettings
    {
        private const string PrefLookSensitivity = "controls.lookSensitivity";
        private const string PrefInterfaceScale = "accessibility.interfaceScale";
        private const string PrefReducedMotion = "accessibility.reducedMotion";
        private const string PrefCaptionBacking = "accessibility.captionBacking";
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
        private static int _interfaceScaleIndex = -1;
        private static int _reducedMotion = -1;
        private static int _captionBacking = -1;

        public const int InterfaceScaleOptionCount = 3;

        public static int InterfaceScaleIndex
        {
            get
            {
                if (_interfaceScaleIndex < 0)
                    _interfaceScaleIndex = Mathf.Clamp(
                        PlayerPrefs.GetInt(PrefInterfaceScale, 0), 0,
                        InterfaceScaleOptionCount - 1);
                return _interfaceScaleIndex;
            }
            set
            {
                _interfaceScaleIndex = Mathf.Clamp(value, 0, InterfaceScaleOptionCount - 1);
                PlayerPrefs.SetInt(PrefInterfaceScale, _interfaceScaleIndex);
                AccessibilityPresentationPolicy.RequestRefresh();
            }
        }

        public static float InterfaceScale
        {
            get
            {
                switch (InterfaceScaleIndex)
                {
                    case 1: return 1.08f;
                    case 2: return 1.15f;
                    default: return 1f;
                }
            }
        }

        public static bool ReducedMotion
        {
            get
            {
                if (_reducedMotion < 0)
                    _reducedMotion = PlayerPrefs.GetInt(PrefReducedMotion, 0) == 1 ? 1 : 0;
                return _reducedMotion == 1;
            }
            set
            {
                _reducedMotion = value ? 1 : 0;
                PlayerPrefs.SetInt(PrefReducedMotion, _reducedMotion);
            }
        }

        public static bool CaptionBacking
        {
            get
            {
                if (_captionBacking < 0)
                    _captionBacking = PlayerPrefs.GetInt(PrefCaptionBacking, 0) == 1 ? 1 : 0;
                return _captionBacking == 1;
            }
            set
            {
                _captionBacking = value ? 1 : 0;
                PlayerPrefs.SetInt(PrefCaptionBacking, _captionBacking);
            }
        }

        public static float LookSensitivity
        {
            get
            {
                if (_lookSensitivity < 0f)
                    _lookSensitivity = Mathf.Clamp(
                        PlayerPrefs.GetFloat(PrefLookSensitivity, DefaultLookSensitivity),
                        MinLookSensitivity, MaxLookSensitivity);
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
