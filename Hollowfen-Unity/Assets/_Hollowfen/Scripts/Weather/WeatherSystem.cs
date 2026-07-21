using System;
using Hollowfen.GameTime;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.Weather
{
    public enum WeatherKind
    {
        Clear = 0,
        Overcast = 1,
        MorningMist = 2,
        Drizzle = 3,
        Rain = 4,
        Storm = 5,
    }

    /// <summary>Art and gameplay values for one deterministic four-hour weather period.</summary>
    public readonly struct WeatherState
    {
        public WeatherState(WeatherKind kind, float precipitation, float mist, float wind,
            float wetness, float keyMultiplier, float ambientMultiplier, float fogMultiplier,
            float fogAddition, float skyExposureOffset, float postExposureOffset,
            float saturationOffset, float temperatureOffset, Color lightTint)
        {
            Kind = kind;
            Precipitation = precipitation;
            Mist = mist;
            Wind = wind;
            Wetness = wetness;
            KeyMultiplier = keyMultiplier;
            AmbientMultiplier = ambientMultiplier;
            FogMultiplier = fogMultiplier;
            FogAddition = fogAddition;
            SkyExposureOffset = skyExposureOffset;
            PostExposureOffset = postExposureOffset;
            SaturationOffset = saturationOffset;
            TemperatureOffset = temperatureOffset;
            LightTint = lightTint;
        }

        public WeatherKind Kind { get; }
        public float Precipitation { get; }
        public float Mist { get; }
        public float Wind { get; }
        public float Wetness { get; }
        public float KeyMultiplier { get; }
        public float AmbientMultiplier { get; }
        public float FogMultiplier { get; }
        public float FogAddition { get; }
        public float SkyExposureOffset { get; }
        public float PostExposureOffset { get; }
        public float SaturationOffset { get; }
        public float TemperatureOffset { get; }
        public Color LightTint { get; }

        public static WeatherState Lerp(WeatherState a, WeatherState b, float t)
        {
            t = Mathf.Clamp01(t);
            return new WeatherState(t < .5f ? a.Kind : b.Kind,
                Mathf.Lerp(a.Precipitation, b.Precipitation, t),
                Mathf.Lerp(a.Mist, b.Mist, t), Mathf.Lerp(a.Wind, b.Wind, t),
                Mathf.Lerp(a.Wetness, b.Wetness, t),
                Mathf.Lerp(a.KeyMultiplier, b.KeyMultiplier, t),
                Mathf.Lerp(a.AmbientMultiplier, b.AmbientMultiplier, t),
                Mathf.Lerp(a.FogMultiplier, b.FogMultiplier, t),
                Mathf.Lerp(a.FogAddition, b.FogAddition, t),
                Mathf.Lerp(a.SkyExposureOffset, b.SkyExposureOffset, t),
                Mathf.Lerp(a.PostExposureOffset, b.PostExposureOffset, t),
                Mathf.Lerp(a.SaturationOffset, b.SaturationOffset, t),
                Mathf.Lerp(a.TemperatureOffset, b.TemperatureOffset, t),
                Color.Lerp(a.LightTint, b.LightTint, t));
        }
    }

    /// <summary>
    /// Hollowfen's deterministic weather clock. Weather is derived from saved day/hour rather
    /// than serialized separately, so old saves and rest/wait always resolve the same sky.
    /// </summary>
    [DefaultExecutionOrder(-75)]
    [DisallowMultipleComponent]
    public sealed class WeatherSystem : MonoBehaviour
    {
        public const float PeriodHours = 4f;
        private const float TransitionHours = .55f;
        private const float MinimumLightningSeconds = 7f;
        private const float MaximumLightningSeconds = 16f;

        public static WeatherSystem Instance { get; private set; }
        public static event Action WeatherChanged;
        public static event Action<float> Lightning;

        public WeatherKind CurrentKind { get; private set; } = WeatherKind.Clear;
        public WeatherKind NextKind { get; private set; } = WeatherKind.Clear;
        public WeatherState CurrentState { get; private set; } = Profile(WeatherKind.Clear);
        public float TransitionBlend { get; private set; }
        public float LightningFlash { get; private set; }
        public float OutdoorExposure => _presentation != null ? _presentation.OutdoorExposure : 1f;
        public bool IsWet => CurrentState.Precipitation >= .08f;
        public bool IsStorm => CurrentKind == WeatherKind.Storm;

        private TimeManager _clock;
        private WeatherPresentation _presentation;
        private int _periodKey = int.MinValue;
        private float _lightningTimer;
        private System.Random _lightningRandom;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            Instance = null;
            WeatherChanged = null;
            Lightning = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var clock = FindAnyObjectByType<TimeManager>();
            if (clock == null || clock.gameObject.scene.name != "Scene_Hollowfen") return;
            if (clock.GetComponent<WeatherSystem>() == null)
                clock.gameObject.AddComponent<WeatherSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            _clock = GetComponent<TimeManager>() ?? TimeManager.Instance;
            _presentation = GetComponent<WeatherPresentation>();
            if (_presentation == null) _presentation = gameObject.AddComponent<WeatherPresentation>();
            if (GetComponent<WeatherAudio>() == null) gameObject.AddComponent<WeatherAudio>();
        }

        private void Start() => Evaluate(true);

        private void Update()
        {
            if (_clock == null) _clock = TimeManager.Instance;
            if (_clock == null) return;
            Evaluate(false);
            UpdateLightning();
            ApplyLighting();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RefreshImmediate()
        {
            Evaluate(true);
            ApplyLighting();
        }

        private void Evaluate(bool force)
        {
            if (_clock == null) return;
            int day = Mathf.Max(1, _clock.Day);
            float hour = Mathf.Repeat(_clock.Hour, 24f);
            int period = Mathf.Clamp(Mathf.FloorToInt(hour / PeriodHours), 0, 5);
            int key = day * 10 + period;
            WeatherKind current = Resolve(day, period);
            int nextDay = period == 5 ? day + 1 : day;
            int nextPeriod = (period + 1) % 6;
            WeatherKind next = Resolve(nextDay, nextPeriod);
            float within = hour - period * PeriodHours;
            float transitionStart = PeriodHours - TransitionHours;
            float blend = within > transitionStart
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(transitionStart, PeriodHours, within))
                : 0f;

            bool changed = force || key != _periodKey || current != CurrentKind || next != NextKind;
            _periodKey = key;
            CurrentKind = current;
            NextKind = next;
            TransitionBlend = blend;
            CurrentState = WeatherState.Lerp(Profile(current), Profile(next), blend);
            if (!changed) return;

            _lightningRandom = new System.Random(StableHash("lightning." + key));
            ResetLightningTimer();
            WeatherChanged?.Invoke();
        }

        private void ApplyLighting()
        {
            var lighting = DayNightLighting.Instance;
            if (lighting == null) return;
            var state = CurrentState;
            lighting.SetWeatherModifier(new DayNightLighting.WeatherModifier(
                state.KeyMultiplier, state.AmbientMultiplier, state.FogMultiplier,
                state.FogAddition, state.SkyExposureOffset, state.PostExposureOffset,
                state.SaturationOffset, state.TemperatureOffset, state.LightTint,
                Mathf.Clamp01(Mathf.Max(state.Mist, state.Precipitation * .65f)),
                LightningFlash));
        }

        private void UpdateLightning()
        {
            LightningFlash = Mathf.MoveTowards(LightningFlash, 0f, Time.deltaTime * 4.8f);
            if (CurrentState.Kind != WeatherKind.Storm || CurrentState.Precipitation < .55f ||
                Time.timeScale <= 0f) return;
            _lightningTimer -= Time.deltaTime;
            if (_lightningTimer > 0f) return;
            LightningFlash = 1f;
            float distance = _lightningRandom != null
                ? Mathf.Lerp(.25f, 1f, (float)_lightningRandom.NextDouble())
                : .6f;
            Lightning?.Invoke(distance);
            ResetLightningTimer();
        }

        private void ResetLightningTimer()
        {
            float t = _lightningRandom != null ? (float)_lightningRandom.NextDouble() : .5f;
            _lightningTimer = Mathf.Lerp(MinimumLightningSeconds, MaximumLightningSeconds, t);
        }

        public string ForecastLabel(float hour)
        {
            string current = Localization.Get(NameId(CurrentKind));
            if (CurrentKind == NextKind)
                return string.Format(Localization.Get("weather.forecast.holding"), current);
            int nextPeriod = (Mathf.FloorToInt(Mathf.Repeat(hour, 24f) / PeriodHours) + 1) % 6;
            return string.Format(Localization.Get("weather.forecast.change"), current,
                Localization.Get(NameId(NextKind)), PeriodName(nextPeriod));
        }

        public static string NameId(WeatherKind kind) => "weather." + kind.ToString().ToLowerInvariant();

        public static string PeriodName(int period)
        {
            switch ((period % 6 + 6) % 6)
            {
                case 0: return Localization.Get("weather.period.night");
                case 1: return Localization.Get("weather.period.dawn");
                case 2: return Localization.Get("weather.period.morning");
                case 3: return Localization.Get("weather.period.afternoon");
                case 4: return Localization.Get("weather.period.evening");
                default: return Localization.Get("weather.period.late");
            }
        }

        public static WeatherKind Resolve(int day, int period)
        {
            day = Mathf.Max(1, day);
            period = (period % 6 + 6) % 6;
            int roll = PositiveModulo(StableHash("hollowfen.weather.v1." + day + "." + period), 100);
            int mistWeight = period == 1 ? 19 : period == 2 ? 8 : 2;
            WeatherKind kind;
            if (roll < 26) kind = WeatherKind.Clear;
            else if (roll < 49) kind = WeatherKind.Overcast;
            else if (roll < 49 + mistWeight) kind = WeatherKind.MorningMist;
            else if (roll < 72 + mistWeight / 2) kind = WeatherKind.Drizzle;
            else if (roll < 92) kind = WeatherKind.Rain;
            else kind = WeatherKind.Storm;
            if (day == 1 && kind == WeatherKind.Storm) kind = WeatherKind.Rain;
            if (kind == WeatherKind.MorningMist && period > 2) kind = WeatherKind.Overcast;
            return kind;
        }

        public static WeatherState Profile(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Overcast:
                    return S(kind, 0f, .18f, .28f, .08f, .70f, .86f, 1.25f, .0015f,
                        -.18f, -.10f, -8f, -3f, C(.66f, .73f, .78f));
                case WeatherKind.MorningMist:
                    return S(kind, 0f, .90f, .08f, .24f, .58f, .82f, 2.1f, .007f,
                        -.24f, -.14f, -10f, -5f, C(.68f, .75f, .76f));
                case WeatherKind.Drizzle:
                    return S(kind, .38f, .42f, .38f, .62f, .57f, .78f, 1.62f, .0045f,
                        -.30f, -.18f, -13f, -5f, C(.59f, .68f, .73f));
                case WeatherKind.Rain:
                    return S(kind, .72f, .55f, .58f, .92f, .43f, .70f, 1.95f, .006f,
                        -.38f, -.24f, -18f, -7f, C(.50f, .61f, .68f));
                case WeatherKind.Storm:
                    return S(kind, 1f, .66f, 1f, 1f, .28f, .58f, 2.28f, .0085f,
                        -.52f, -.34f, -24f, -10f, C(.41f, .52f, .61f));
                default:
                    return S(WeatherKind.Clear, 0f, 0f, .16f, 0f, 1f, 1f, 1f, 0f,
                        0f, 0f, 0f, 0f, Color.white);
            }
        }

        public static float GrowthMultiplierAt(int day, float hour)
        {
            WeatherState state = Profile(Resolve(day,
                Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(hour, 24f) / PeriodHours), 0, 5)));
            return Mathf.Lerp(.94f, 1.22f, Mathf.Clamp01(state.Wetness));
        }

        public static bool IsWetAtCurrentTime()
        {
            if (Instance != null) return Instance.CurrentState.Precipitation >= .08f;
            var clock = TimeManager.Instance;
            int day = clock != null ? clock.Day :
                Mathf.Max(1, SaveManager.GetSlotMeta(SaveManager.ActiveSlot)?.GameDay ?? 1);
            float hour = clock != null ? clock.Hour :
                SaveManager.GetSlotMeta(SaveManager.ActiveSlot)?.GameHour ?? 12f;
            return Profile(Resolve(day, Mathf.FloorToInt(Mathf.Repeat(hour, 24f) /
                PeriodHours))).Precipitation >= .08f;
        }

        public static float AdjustedGrowthHours(int plantedDay, float plantedHour,
            int currentDay, float currentHour)
        {
            double start = (Mathf.Max(1, plantedDay) - 1) * 24d + Mathf.Repeat(plantedHour, 24f);
            double end = (Mathf.Max(1, currentDay) - 1) * 24d + Mathf.Repeat(currentHour, 24f);
            if (end <= start) return 0f;
            double cursor = start;
            float adjusted = 0f;
            int guard = 0;
            while (cursor < end && guard++ < 1440)
            {
                double step = Math.Min(.5d, end - cursor);
                int day = (int)(cursor / 24d) + 1;
                float hour = (float)(cursor % 24d);
                adjusted += (float)step * GrowthMultiplierAt(day, hour);
                cursor += step;
            }
            if (cursor < end) adjusted += (float)(end - cursor);
            return adjusted;
        }

        public static int AdjustedWildRespawnDays(int baseDays, int harvestedDay)
        {
            int result = Mathf.Max(1, baseDays);
            for (int day = Mathf.Max(1, harvestedDay); day < harvestedDay + result; day++)
            {
                float moisture = 0f;
                for (int period = 0; period < 6; period++)
                    moisture += Profile(Resolve(day, period)).Wetness;
                if (moisture / 6f < .43f) continue;
                return Mathf.Max(1, result - 1);
            }
            return result;
        }

        private static WeatherState S(WeatherKind kind, float precipitation, float mist,
            float wind, float wetness, float key, float ambient, float fogMultiplier,
            float fogAddition, float skyOffset, float postOffset, float saturation,
            float temperature, Color tint) => new WeatherState(kind, precipitation, mist,
            wind, wetness, key, ambient, fogMultiplier, fogAddition, skyOffset, postOffset,
            saturation, temperature, tint);

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 16777619;
                return (int)(hash & 0x7fffffff);
            }
        }

        private static int PositiveModulo(int value, int divisor) =>
            (value % divisor + divisor) % divisor;
    }
}
