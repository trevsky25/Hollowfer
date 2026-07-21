#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Hollowfen.GameTime;
using Hollowfen.Save;
using Hollowfen.UI;
using Hollowfen.Weather;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.EditorTools
{
    public static class WeatherSystemVerifier
    {
        [MenuItem("Hollowfen/Verify/Dynamic Weather")]
        private static void RunMenu() => Debug.Log(RunAll());

        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            var clock = TimeManager.Instance;
            var weather = WeatherSystem.Instance;
            var lighting = DayNightLighting.Instance;
            Require(clock != null && weather != null && lighting != null,
                "clock, weather, or day/night lighting is missing");
            int originalDay = clock.Day;
            float originalHour = clock.Hour;
            string originalOverride = SaveManager.EditorSaveDirectoryOverride;
            int originalSlot = SaveManager.ActiveSlot;
            string testDirectory = Path.Combine(Path.GetTempPath(),
                "hollowfen-weather-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            SaveManager.EditorSaveDirectoryOverride = testDirectory;
            SaveManager.WritePlaceholderToSlot(originalSlot);

            try
            {
                VerifyScheduleDomain();
                var rain = Find(WeatherKind.Rain);
                var clear = Find(WeatherKind.Clear, rain.period);
                Require(rain.day > 0 && clear.day > 0, "could not find deterministic rain/clear fixtures");

                clock.SetTime(clear.day, clear.period * WeatherSystem.PeriodHours + 1f);
                weather.RefreshImmediate();
                lighting.Apply(clock.Hour);
                float clearFog = lighting.CurrentFogDensity;
                Require(weather.CurrentKind == WeatherKind.Clear,
                    "clear fixture resolved to the wrong live weather");

                clock.SetTime(rain.day, rain.period * WeatherSystem.PeriodHours + 1f);
                weather.RefreshImmediate();
                lighting.Apply(clock.Hour);
                Require(weather.CurrentKind == WeatherKind.Rain &&
                        lighting.CurrentFogDensity > clearFog,
                    "rain did not darken/fog the live day-night owner");

                var presentation = weather.GetComponent<WeatherPresentation>();
                Require(presentation != null && presentation.HasCameraRig &&
                        presentation.MaxRainParticles > 0 && presentation.MaxRainParticles <= 720,
                    "bounded camera-local rain presentation is missing");
                AudioSource[] sources = weather.GetComponentsInChildren<AudioSource>(true);
                Require(sources.Length == 3 && sources.All(source => source.clip != null &&
                        source.clip.channels == 1 && source.clip.frequency == 48000),
                    "weather audio is not three bounded 48 kHz mono sources");
                Require(UnityEngine.Object.FindFirstObjectByType<ClockHUD>() != null,
                    "diegetic clock/forecast HUD is missing");
                Canvas.ForceUpdateCanvases();
                RectTransform clockPill = Resources.FindObjectsOfTypeAll<RectTransform>()
                    .FirstOrDefault(rect => rect != null && rect.name == "ClockPill" &&
                        rect.gameObject.scene.IsValid());
                Require(clockPill != null && clockPill.rect.width >= 300f &&
                        clockPill.rect.height >= 76f &&
                        clockPill.GetComponentsInChildren<Image>(true).Count(image =>
                            image != null && image.name.EndsWith("Icon", StringComparison.Ordinal) &&
                            image.sprite != null) == 2,
                    "clock HUD is missing its bounded two-row card or time/weather icons");
                foreach (TMP_Text text in clockPill.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.ForceMeshUpdate();
                    Require(!text.isTextOverflowing && text.fontSize >= 9.5f,
                        "clock HUD copy escapes its container or becomes unreadably small: " +
                        text.name + " = " + text.text);
                }

                return "DYNAMIC WEATHER — PASS: deterministic six-period forecast, smooth state profiles, " +
                       "single-owner sun/sky/fog integration, camera-local rain, shelter exposure, wind, " +
                       "procedural rain/thunder audio, wet-weather growth/respawn, delivery premiums, " +
                       "reduced-motion scaling, and bounded icon-led forecast HUD";
            }
            finally
            {
                try
                {
                    clock.SetTime(originalDay, originalHour);
                    weather.RefreshImmediate();
                    lighting.Apply(originalHour);
                }
                finally
                {
                    SaveManager.EditorSaveDirectoryOverride = originalOverride;
                    SaveManager.SetActiveSlot(originalSlot);
                    if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
                }
            }
        }

        private static void VerifyScheduleDomain()
        {
            var seen = new bool[Enum.GetValues(typeof(WeatherKind)).Length];
            for (int day = 1; day <= 40; day++)
                for (int period = 0; period < 6; period++)
                {
                    WeatherKind first = WeatherSystem.Resolve(day, period);
                    WeatherKind second = WeatherSystem.Resolve(day, period);
                    Require(first == second, "weather schedule is not deterministic");
                    seen[(int)first] = true;
                    WeatherState profile = WeatherSystem.Profile(first);
                    Require(profile.Precipitation >= 0f && profile.Precipitation <= 1f &&
                            profile.Wind >= 0f && profile.Wind <= 1f &&
                            profile.FogMultiplier >= 1f,
                        first + " profile is outside production bounds");
                }
            Require(seen.All(value => value), "40-day schedule does not exercise every weather type");
            for (int period = 0; period < 6; period++)
                Require(WeatherSystem.Resolve(1, period) != WeatherKind.Storm,
                    "onboarding day contains a storm");
            Require(WeatherSystem.GrowthMultiplierAt(Find(WeatherKind.Rain).day,
                        Find(WeatherKind.Rain).period * 4f + 1f) >
                    WeatherSystem.GrowthMultiplierAt(Find(WeatherKind.Clear).day,
                        Find(WeatherKind.Clear).period * 4f + 1f),
                "wet weather does not accelerate cultivated growth");
            Require(Enumerable.Range(1, 40).Any(day =>
                    WeatherSystem.AdjustedWildRespawnDays(3, day) < 3),
                "wet days never shorten a wild flush cooldown");
        }

        private static (int day, int period) Find(WeatherKind kind, int requiredPeriod = -1)
        {
            for (int day = 1; day <= 120; day++)
                for (int period = 0; period < 6; period++)
                    if ((requiredPeriod < 0 || period == requiredPeriod) &&
                        WeatherSystem.Resolve(day, period) == kind)
                        return (day, period);
            return (0, 0);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[WeatherSystemVerifier] " + message);
        }
    }
}
#endif
