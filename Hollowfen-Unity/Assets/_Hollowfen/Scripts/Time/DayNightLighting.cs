using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hollowfen.GameTime
{
    /// <summary>
    /// Owns the visual side of the game clock. The clock supplies an hour; this component
    /// blends a small set of art-directed lighting states across the sun/moon, sky, ambient
    /// light, fog, reflections, and post exposure. Runtime copies keep third-party skybox and
    /// volume assets untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayNightLighting : MonoBehaviour
    {
        public static DayNightLighting Instance { get; private set; }

        [SerializeField] private Light _keyLight;
        [SerializeField, Tooltip("Higher than authored scene volumes so time-of-day exposure wins.")]
        private float _volumePriority = 100f;

        public float NightBlend { get; private set; }
        public float CurrentExposure { get; private set; }
        public float CurrentSkyExposure { get; private set; }
        public float CurrentFogDensity { get; private set; }

        public readonly struct WeatherModifier
        {
            public WeatherModifier(float keyMultiplier, float ambientMultiplier,
                float fogMultiplier, float fogAddition, float skyExposureOffset,
                float postExposureOffset, float saturationOffset, float temperatureOffset,
                Color lightTint, float tintStrength, float lightning)
            {
                KeyMultiplier = keyMultiplier;
                AmbientMultiplier = ambientMultiplier;
                FogMultiplier = fogMultiplier;
                FogAddition = fogAddition;
                SkyExposureOffset = skyExposureOffset;
                PostExposureOffset = postExposureOffset;
                SaturationOffset = saturationOffset;
                TemperatureOffset = temperatureOffset;
                LightTint = lightTint;
                TintStrength = tintStrength;
                Lightning = lightning;
            }

            public float KeyMultiplier { get; }
            public float AmbientMultiplier { get; }
            public float FogMultiplier { get; }
            public float FogAddition { get; }
            public float SkyExposureOffset { get; }
            public float PostExposureOffset { get; }
            public float SaturationOffset { get; }
            public float TemperatureOffset { get; }
            public Color LightTint { get; }
            public float TintStrength { get; }
            public float Lightning { get; }

            public static WeatherModifier Neutral => new WeatherModifier(1f, 1f, 1f, 0f,
                0f, 0f, 0f, 0f, Color.white, 0f, 0f);
        }

        private float _keyYaw;
        private Material _originalSkybox;
        private Material _runtimeSkybox;
        private AmbientMode _originalAmbientMode;
        private Color _originalAmbientSky;
        private Color _originalAmbientEquator;
        private Color _originalAmbientGround;
        private float _originalAmbientIntensity;
        private Color _originalFogColor;
        private float _originalFogDensity;
        private float _originalReflectionIntensity;
        private Volume _runtimeVolume;
        private VolumeProfile _runtimeProfile;
        private ColorAdjustments _colorAdjustments;
        private WhiteBalance _whiteBalance;
        private Vignette _vignette;
        private bool _initialized;
        private WeatherModifier _weather = WeatherModifier.Neutral;

        private readonly struct LightingState
        {
            public readonly float Hour;
            public readonly float KeyPitch;
            public readonly float KeyYawOffset;
            public readonly float KeyIntensity;
            public readonly Color KeyColor;
            public readonly Color AmbientSky;
            public readonly Color AmbientEquator;
            public readonly Color AmbientGround;
            public readonly float AmbientIntensity;
            public readonly Color FogColor;
            public readonly float FogDensity;
            public readonly float ReflectionIntensity;
            public readonly float SkyExposure;
            public readonly Color SkyTint;
            public readonly Color GroundTint;
            public readonly float AtmosphereThickness;
            public readonly float PostExposure;
            public readonly float Contrast;
            public readonly float Saturation;
            public readonly float Temperature;
            public readonly Color ColorFilter;
            public readonly float Vignette;
            public readonly float Night;

            public LightingState(float hour, float keyPitch, float keyYawOffset,
                float keyIntensity, Color keyColor, Color ambientSky, Color ambientEquator,
                Color ambientGround, float ambientIntensity, Color fogColor, float fogDensity,
                float reflectionIntensity, float skyExposure, Color skyTint, Color groundTint,
                float atmosphereThickness, float postExposure, float contrast, float saturation,
                float temperature, Color colorFilter, float vignette, float night)
            {
                Hour = hour;
                KeyPitch = keyPitch;
                KeyYawOffset = keyYawOffset;
                KeyIntensity = keyIntensity;
                KeyColor = keyColor;
                AmbientSky = ambientSky;
                AmbientEquator = ambientEquator;
                AmbientGround = ambientGround;
                AmbientIntensity = ambientIntensity;
                FogColor = fogColor;
                FogDensity = fogDensity;
                ReflectionIntensity = reflectionIntensity;
                SkyExposure = skyExposure;
                SkyTint = skyTint;
                GroundTint = groundTint;
                AtmosphereThickness = atmosphereThickness;
                PostExposure = postExposure;
                Contrast = contrast;
                Saturation = saturation;
                Temperature = temperature;
                ColorFilter = colorFilter;
                Vignette = vignette;
                Night = night;
            }
        }

        // The first/last entries deliberately match so interpolation across midnight is stable.
        // Values are tuned for a readable, cool moonlit night with warm practical-light contrast.
        private static readonly LightingState[] States =
        {
            S(0f,   28f, 180f, .20f, C(.43f,.56f,.88f), C(.095f,.135f,.22f), C(.07f,.095f,.15f), C(.04f,.05f,.08f), .70f, C(.022f,.038f,.07f), .0145f, .35f, .25f, C(.32f,.40f,.58f), C(.018f,.028f,.05f), .55f, -.22f, 12f, -10f, -8f, C(.72f,.82f,1f), .28f, 1f),
            S(5f,   24f, 180f, .18f, C(.42f,.54f,.86f), C(.09f,.128f,.21f), C(.066f,.09f,.145f), C(.038f,.048f,.075f), .68f, C(.02f,.036f,.066f), .015f, .32f, .23f, C(.30f,.38f,.56f), C(.016f,.026f,.046f), .52f, -.25f, 13f, -11f, -9f, C(.70f,.81f,1f), .29f, 1f),
            S(6f,    8f,   0f, .28f, C(1f,.55f,.30f), C(.18f,.16f,.20f), C(.11f,.10f,.14f), C(.06f,.055f,.065f), .62f, C(.12f,.13f,.18f), .0125f, .35f, .35f, C(.55f,.38f,.38f), C(.09f,.08f,.10f), .78f, -.10f, 13f, -6f, 4f, C(1f,.87f,.78f), .31f, .55f),
            S(7.5f, 24f,   0f, .76f, C(1f,.72f,.48f), C(.40f,.47f,.60f), C(.30f,.34f,.42f), C(.16f,.15f,.14f), .86f, C(.44f,.55f,.68f), .0085f, .72f, .78f, C(.48f,.52f,.65f), C(.20f,.24f,.30f), 1.02f, .40f, 8f, 0f, 1f, C(1f,.97f,.93f), .27f, 0f),
            S(12f,  60f,   0f, 1.20f,C(1f,.93f,.84f), C(.59f,.66f,.77f), C(.43f,.48f,.55f), C(.29f,.27f,.23f), 1.00f, C(.68f,.79f,.88f), .0068f, 1.00f, 1.00f, C(.50f,.50f,.50f), C(.37f,.43f,.51f), 1.00f, .72f, 8f, 1f, 0f, C(1f,.98f,.95f), .25f, 0f),
            S(16.5f,42f,   0f, 1.02f,C(1f,.82f,.61f), C(.51f,.56f,.65f), C(.38f,.40f,.44f), C(.24f,.22f,.18f), .94f, C(.61f,.68f,.73f), .0075f, .88f, .91f, C(.51f,.47f,.45f), C(.31f,.32f,.34f), 1.00f, .58f, 9f, 1f, 2f, C(1f,.96f,.91f), .26f, 0f),
            S(18.25f,10f,  0f, .54f, C(1f,.50f,.25f), C(.34f,.28f,.32f), C(.23f,.21f,.28f), C(.13f,.12f,.15f), .72f, C(.30f,.28f,.34f), .0105f, .56f, .54f, C(.55f,.36f,.36f), C(.14f,.13f,.17f), .88f, .16f, 13f, -3f, 5f, C(1f,.84f,.76f), .30f, .22f),
            S(19.5f,18f, 180f, .17f, C(.48f,.58f,.86f), C(.13f,.13f,.21f), C(.085f,.09f,.15f), C(.045f,.048f,.07f), .58f, C(.075f,.085f,.14f), .013f, .32f, .27f, C(.38f,.39f,.56f), C(.04f,.05f,.085f), .64f, -.34f, 15f, -10f, -5f, C(.78f,.84f,1f), .34f, .78f),
            S(20.5f,28f, 180f, .20f, C(.43f,.56f,.88f), C(.095f,.135f,.22f), C(.07f,.095f,.15f), C(.04f,.05f,.08f), .70f, C(.022f,.038f,.07f), .0145f, .35f, .25f, C(.32f,.40f,.58f), C(.018f,.028f,.05f), .55f, -.22f, 12f, -10f, -8f, C(.72f,.82f,1f), .28f, 1f),
            S(24f,  28f, 180f, .20f, C(.43f,.56f,.88f), C(.095f,.135f,.22f), C(.07f,.095f,.15f), C(.04f,.05f,.08f), .70f, C(.022f,.038f,.07f), .0145f, .35f, .25f, C(.32f,.40f,.58f), C(.018f,.028f,.05f), .55f, -.22f, 12f, -10f, -8f, C(.72f,.82f,1f), .28f, 1f),
        };

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        private static LightingState S(float hour, float pitch, float yaw, float intensity,
            Color key, Color sky, Color equator, Color ground, float ambient, Color fog,
            float fogDensity, float reflection, float skyExposure, Color skyTint,
            Color groundTint, float atmosphere, float postExposure, float contrast,
            float saturation, float temperature, Color filter, float vignette, float night) =>
            new LightingState(hour, pitch, yaw, intensity, key, sky, equator, ground, ambient,
                fog, fogDensity, reflection, skyExposure, skyTint, groundTint, atmosphere,
                postExposure, contrast, saturation, temperature, filter, vignette, night);

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Initialize(_keyLight != null ? _keyLight : RenderSettings.sun);
        }

        public void Initialize(Light keyLight)
        {
            if (_initialized)
            {
                if (keyLight != null) _keyLight = keyLight;
                return;
            }

            _keyLight = keyLight != null ? keyLight : RenderSettings.sun;
            if (_keyLight != null) _keyYaw = _keyLight.transform.eulerAngles.y;

            _originalSkybox = RenderSettings.skybox;
            _originalAmbientMode = RenderSettings.ambientMode;
            _originalAmbientSky = RenderSettings.ambientSkyColor;
            _originalAmbientEquator = RenderSettings.ambientEquatorColor;
            _originalAmbientGround = RenderSettings.ambientGroundColor;
            _originalAmbientIntensity = RenderSettings.ambientIntensity;
            _originalFogColor = RenderSettings.fogColor;
            _originalFogDensity = RenderSettings.fogDensity;
            _originalReflectionIntensity = RenderSettings.reflectionIntensity;

            if (_originalSkybox != null)
            {
                _runtimeSkybox = new Material(_originalSkybox)
                {
                    name = _originalSkybox.name + " (Day Night Runtime)",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                RenderSettings.skybox = _runtimeSkybox;
            }

            _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _runtimeProfile.name = "Day Night Runtime Volume";
            _runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
            _colorAdjustments = _runtimeProfile.Add<ColorAdjustments>(true);
            _whiteBalance = _runtimeProfile.Add<WhiteBalance>(true);
            _vignette = _runtimeProfile.Add<Vignette>(true);

            _runtimeVolume = gameObject.AddComponent<Volume>();
            _runtimeVolume.isGlobal = true;
            _runtimeVolume.priority = _volumePriority;
            _runtimeVolume.weight = 1f;
            _runtimeVolume.sharedProfile = _runtimeProfile;

            Override(_colorAdjustments.postExposure);
            Override(_colorAdjustments.contrast);
            Override(_colorAdjustments.colorFilter);
            Override(_colorAdjustments.saturation);
            Override(_whiteBalance.temperature);
            Override(_vignette.intensity);

            _initialized = true;
        }

        private static void Override<T>(VolumeParameter<T> parameter) => parameter.overrideState = true;

        public void SetWeatherModifier(WeatherModifier modifier) => _weather = modifier;

        public void Apply(float hour)
        {
            if (!_initialized) Initialize(_keyLight != null ? _keyLight : RenderSettings.sun);
            hour = Mathf.Repeat(hour, 24f);

            int upper = 1;
            while (upper < States.Length && hour > States[upper].Hour) upper++;
            int lower = Mathf.Max(0, upper - 1);
            upper = Mathf.Min(States.Length - 1, upper);
            LightingState a = States[lower];
            LightingState b = States[upper];
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.Hour, b.Hour, hour));

            if (_keyLight != null)
            {
                Quaternion from = Quaternion.Euler(a.KeyPitch, _keyYaw + a.KeyYawOffset, 0f);
                Quaternion to = Quaternion.Euler(b.KeyPitch, _keyYaw + b.KeyYawOffset, 0f);
                _keyLight.transform.rotation = Quaternion.Slerp(from, to, t);
                _keyLight.intensity = L(a.KeyIntensity, b.KeyIntensity, t) *
                    Mathf.Max(0f, _weather.KeyMultiplier) + _weather.Lightning * 1.1f;
                _keyLight.color = Color.Lerp(Color.Lerp(a.KeyColor, b.KeyColor, t),
                    _weather.LightTint, Mathf.Clamp01(_weather.TintStrength)) +
                    Color.white * (_weather.Lightning * .22f);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            float tint = Mathf.Clamp01(_weather.TintStrength * .72f);
            RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(a.AmbientSky,
                b.AmbientSky, t), _weather.LightTint, tint);
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(a.AmbientEquator,
                b.AmbientEquator, t), _weather.LightTint, tint * .82f);
            RenderSettings.ambientGroundColor = Color.Lerp(Color.Lerp(a.AmbientGround,
                b.AmbientGround, t), _weather.LightTint, tint * .52f);
            RenderSettings.ambientIntensity = L(a.AmbientIntensity, b.AmbientIntensity, t) *
                Mathf.Max(.1f, _weather.AmbientMultiplier) + _weather.Lightning * .12f;
            RenderSettings.fogColor = Color.Lerp(Color.Lerp(a.FogColor, b.FogColor, t),
                _weather.LightTint, tint * .9f);
            RenderSettings.fogDensity = CurrentFogDensity =
                L(a.FogDensity, b.FogDensity, t) * Mathf.Max(.1f, _weather.FogMultiplier) +
                Mathf.Max(0f, _weather.FogAddition);
            RenderSettings.reflectionIntensity = L(a.ReflectionIntensity, b.ReflectionIntensity, t);

            CurrentSkyExposure = L(a.SkyExposure, b.SkyExposure, t) +
                _weather.SkyExposureOffset + _weather.Lightning * .28f;
            SetSkyboxFloat("_Exposure", CurrentSkyExposure);
            SetSkyboxColor("_SkyTint", Color.Lerp(Color.Lerp(a.SkyTint, b.SkyTint, t),
                _weather.LightTint, tint));
            SetSkyboxColor("_GroundColor", Color.Lerp(a.GroundTint, b.GroundTint, t));
            SetSkyboxFloat("_AtmosphereThickness", L(a.AtmosphereThickness, b.AtmosphereThickness, t));

            CurrentExposure = L(a.PostExposure, b.PostExposure, t) +
                _weather.PostExposureOffset + _weather.Lightning * .32f;
            _colorAdjustments.postExposure.value = CurrentExposure;
            _colorAdjustments.contrast.value = L(a.Contrast, b.Contrast, t);
            _colorAdjustments.saturation.value = L(a.Saturation, b.Saturation, t) +
                _weather.SaturationOffset;
            _colorAdjustments.colorFilter.value = Color.Lerp(a.ColorFilter, b.ColorFilter, t);
            _whiteBalance.temperature.value = L(a.Temperature, b.Temperature, t) +
                _weather.TemperatureOffset;
            _vignette.intensity.value = L(a.Vignette, b.Vignette, t);
            NightBlend = L(a.Night, b.Night, t);
        }

        private static float L(float a, float b, float t) => Mathf.LerpUnclamped(a, b, t);

        private void SetSkyboxFloat(string property, float value)
        {
            if (_runtimeSkybox != null && _runtimeSkybox.HasProperty(property))
                _runtimeSkybox.SetFloat(property, value);
        }

        private void SetSkyboxColor(string property, Color value)
        {
            if (_runtimeSkybox != null && _runtimeSkybox.HasProperty(property))
                _runtimeSkybox.SetColor(property, value);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (!_initialized) return;

            if (RenderSettings.skybox == _runtimeSkybox) RenderSettings.skybox = _originalSkybox;
            RenderSettings.ambientMode = _originalAmbientMode;
            RenderSettings.ambientSkyColor = _originalAmbientSky;
            RenderSettings.ambientEquatorColor = _originalAmbientEquator;
            RenderSettings.ambientGroundColor = _originalAmbientGround;
            RenderSettings.ambientIntensity = _originalAmbientIntensity;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.fogDensity = _originalFogDensity;
            RenderSettings.reflectionIntensity = _originalReflectionIntensity;

            if (_runtimeSkybox != null) Destroy(_runtimeSkybox);
            if (_runtimeProfile != null) Destroy(_runtimeProfile);
        }
    }
}
