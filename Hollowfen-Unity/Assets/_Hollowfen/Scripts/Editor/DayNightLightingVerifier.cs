#if UNITY_EDITOR
using System;
using Hollowfen.GameTime;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Focused, state-mutating Play Mode checks for the visual time-of-day cycle.</summary>
    public static class DayNightLightingVerifier
    {
        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            var clock = TimeManager.Instance;
            var lighting = DayNightLighting.Instance;
            Require(clock != null, "TimeManager is missing");
            Require(lighting != null, "DayNightLighting is missing");
            Require(RenderSettings.sun != null, "scene sun is missing");

            int day = clock.Day;
            float hour = clock.Hour;
            try
            {
                clock.SetTime(day, 12f);
                float dayExposure = lighting.CurrentExposure;
                float daySky = lighting.CurrentSkyExposure;
                float dayFog = lighting.CurrentFogDensity;
                float dayKey = RenderSettings.sun.intensity;
                float dayAmbient = Luminance(RenderSettings.ambientSkyColor) * RenderSettings.ambientIntensity;

                clock.SetTime(day, 19f);
                float duskBlend = lighting.NightBlend;
                float duskExposure = lighting.CurrentExposure;
                Require(duskBlend > .25f && duskBlend < .85f, "dusk does not blend between day and night");

                clock.SetTime(day, 23f);
                float nightAmbient = Luminance(RenderSettings.ambientSkyColor) * RenderSettings.ambientIntensity;
                Require(clock.IsNight, "23:00 is not considered night");
                Require(lighting.NightBlend > .95f, "night blend never reaches full night");
                Require(lighting.CurrentExposure < dayExposure - .9f, "night exposure is not materially darker than day");
                Require(lighting.CurrentSkyExposure < daySky * .3f, "night sky remains too close to daytime exposure");
                Require(lighting.CurrentFogDensity > dayFog * 1.8f, "night fog does not deepen");
                Require(RenderSettings.sun.intensity < dayKey * .2f, "moon key remains too bright");
                Require(RenderSettings.sun.color.b > RenderSettings.sun.color.r, "moon key is not cool-toned");
                Require(nightAmbient < dayAmbient * .15f, "night ambient light remains too bright");
                Require(duskExposure < dayExposure && duskExposure > lighting.CurrentExposure,
                    "exposure does not transition progressively through dusk");

                var practicals = UnityEngine.Object.FindObjectsByType<NightLight>(FindObjectsInactive.Include);
                Require(practicals.Length == 6, "expected six authored village practical lights");
                foreach (var practical in practicals)
                {
                    practical.RefreshImmediate();
                    Require(practical.Light != null && practical.Light.enabled && practical.Light.intensity > .5f,
                        practical.name + " did not illuminate at night");
                }

                clock.SetTime(day, 12f);
                foreach (var practical in practicals)
                {
                    practical.RefreshImmediate();
                    Require(!practical.Light.enabled, practical.name + " remained on at noon");
                }

                return "DAY / NIGHT LIGHTING — PASS: smooth dawn/day/golden-hour/dusk/night exposure, cool readable moonlight, darker sky and ambient, deeper night fog, and 6 warm village practicals";
            }
            finally
            {
                clock.SetTime(day, hour);
                foreach (var practical in UnityEngine.Object.FindObjectsByType<NightLight>(FindObjectsInactive.Include))
                    practical.RefreshImmediate();
            }
        }

        private static float Luminance(Color color) =>
            color.r * .2126f + color.g * .7152f + color.b * .0722f;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[DayNightLightingVerifier] " + message);
        }
    }
}
#endif
