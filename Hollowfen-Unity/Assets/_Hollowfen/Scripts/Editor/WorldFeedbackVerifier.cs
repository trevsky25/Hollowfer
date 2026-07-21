#if UNITY_EDITOR
using System;
using System.Linq;
using Hollowfen.Audio;
using Hollowfen.GameTime;
using Hollowfen.Map;
using Hollowfen.UI;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Focused Play Mode verification for regional ambience, score state, triggers, and toast.</summary>
    public static class WorldFeedbackVerifier
    {
        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            var clock = TimeManager.Instance;
            var ambience = AmbienceManager.Instance;
            var music = UnityEngine.Object.FindAnyObjectByType<MusicManager>();
            var toast = UnityEngine.Object.FindAnyObjectByType<RegionArrivalToast>();
            Require(clock != null, "TimeManager is missing");
            Require(ambience != null, "gameplay AmbienceManager is missing");
            Require(music != null, "MusicManager is missing");
            Require(toast != null, "RegionArrivalToast is missing");
            Require(RegionCatalog.Count == 4, "expected four canonical region presentations");

            var triggers = UnityEngine.Object.FindObjectsByType<RegionTrigger>(FindObjectsInactive.Include);
            Require(triggers.Count(t => t.RegionId == "village") >= 2,
                "the starting/southern village lacks region coverage");
            Require(triggers.Count(t => t.RegionId == "wend") >= 2,
                "the Wend/clear-cut lacks region coverage");
            Require(triggers.Any(t => t.RegionId == "old_wood"), "Old Wood trigger is missing");
            Require(triggers.Any(t => t.RegionId == "manor"), "manor trigger is missing");
            Require(triggers.All(t => t.GetComponent<BoxCollider>() != null &&
                                      t.GetComponent<BoxCollider>().isTrigger),
                "a RegionTrigger lacks a trigger BoxCollider");

            int originalDay = clock.Day;
            float originalHour = clock.Hour;
            string originalAmbienceRegion = ambience.CurrentRegion;
            string originalMusicRegion = music.CurrentRegion;
            bool hadAmbiencePref = PlayerPrefs.HasKey("audio.ambience");
            float originalAmbiencePref = PlayerPrefs.GetFloat("audio.ambience", .8f);

            try
            {
                VerifyRegionRouting(triggers, ambience, music, toast);
                VerifyAmbience(clock, ambience);
                VerifyMusic(clock, music);
                VerifyToast(toast);
                return "WORLD FEEDBACK — PASS: 4 localized regions, 6 trigger volumes, 48 kHz day/night ambience, region crossfades, adaptive score state, and non-blocking arrival title";
            }
            finally
            {
                clock.SetTime(originalDay, originalHour);
                ambience.SetUserVolume(originalAmbiencePref);
                ambience.SetRegionImmediate(string.IsNullOrEmpty(originalAmbienceRegion)
                    ? "village" : originalAmbienceRegion);
                music.SetRegionImmediate(string.IsNullOrEmpty(originalMusicRegion)
                    ? "village" : originalMusicRegion);
                ambience.RefreshImmediate();
                music.RefreshImmediate();
                toast.HideImmediate();
                if (hadAmbiencePref) PlayerPrefs.SetFloat("audio.ambience", originalAmbiencePref);
                else PlayerPrefs.DeleteKey("audio.ambience");
            }
        }

        private static void VerifyRegionRouting(RegionTrigger[] triggers, AmbienceManager ambience,
            MusicManager music, RegionArrivalToast toast)
        {
            var manor = triggers.FirstOrDefault(trigger => trigger.RegionId == "manor");
            Require(manor != null, "manor integration trigger is missing");
            bool manorWasActive = LocationRegistry.CurrentRegion == "manor";
            if (manorWasActive) LocationRegistry.PopRegion(manor);

            ambience.SetRegionImmediate("village");
            music.SetRegionImmediate("village");
            toast.HideImmediate();
            LocationRegistry.PushRegion(manor);
            Require(LocationRegistry.CurrentRegion == "manor",
                "LocationRegistry did not select the highest-priority manor volume");
            Require(ambience.IsTransitioning && ambience.PendingRegion == "manor",
                "RegionChanged did not route into the ambience crossfade");
            Require(music.CurrentRegion == "manor",
                "RegionChanged did not route into the adaptive score state");
            Require(toast.LastRegionId == "manor" && toast.IsShowing,
                "RegionChanged did not route into the arrival title");
            ambience.CompleteTransitionImmediate();

            LocationRegistry.PopRegion(manor);
            if (manorWasActive) LocationRegistry.PushRegion(manor);
        }

        private static void VerifyAmbience(TimeManager clock, AmbienceManager ambience)
        {
            Require(ambience.Output != null, "ambience is not mixer-routed");
            Require(ambience.LiveSourceCount == 4, "ambience does not own two day/night banks");
            ambience.SetUserVolume(1f);
            ambience.SetRegionImmediate("village");
            clock.SetTime(clock.Day, 12f);
            ambience.RefreshImmediate();
            Require(ambience.CurrentRegion == "village", "village ambience did not resolve");
            Require(ambience.CurrentDayVolume > ambience.CurrentNightVolume * 4f,
                "noon did not favor the day ambience");
            AudioClip villageDay = ambience.ActiveDayClip;
            AudioClip villageNight = ambience.ActiveNightClip;
            VerifyClip(villageDay, "village day");
            VerifyClip(villageNight, "village night");
            Require(Difference(villageDay, villageNight) > .0005f,
                "day and night ambience synthesized the same signal");

            clock.SetTime(clock.Day, 23f);
            ambience.RefreshImmediate();
            Require(ambience.CurrentNightVolume > ambience.CurrentDayVolume * 4f,
                "late night did not favor the night ambience");

            ambience.BeginRegionTransition("old_wood");
            Require(ambience.IsTransitioning && ambience.PendingRegion == "old_wood",
                "Old Wood region did not enter the crossfade bank");
            ambience.CompleteTransitionImmediate();
            Require(ambience.CurrentRegion == "old_wood" &&
                    ambience.ActiveDayClip != villageDay && ambience.ActiveNightClip != villageNight,
                "Old Wood did not receive a distinct cached soundscape");
            Require(ambience.SynthesizedProfileCount >= 2,
                "regional ambience profiles were not cached");

            ambience.SetUserVolume(0f);
            ambience.RefreshImmediate();
            Require(ambience.CurrentDayVolume <= .0001f && ambience.CurrentNightVolume <= .0001f,
                "Ambience setting did not mute all four sources");
            ambience.SetUserVolume(1f);
        }

        private static void VerifyMusic(TimeManager clock, MusicManager music)
        {
            Require(music.Output != null, "music is not mixer-routed");
            Require(music.SourceCount == 2, "adaptive music does not own two crossfade banks");
            clock.SetTime(clock.Day, 12f);
            music.SetRegionImmediate("village");
            music.RefreshImmediate();
            float villageVolume = music.CurrentTargetVolume;
            float villageCutoff = music.CurrentLowPassHz;
            Require(villageVolume > 0f && villageCutoff > 20000f,
                "village daytime music state is not open/full");

            music.SetRegionImmediate("old_wood");
            music.RefreshImmediate();
            Require(music.CurrentTargetVolume < villageVolume &&
                    music.CurrentLowPassHz < villageCutoff,
                "Old Wood did not restrain the inherited score");

            float oldWoodDayVolume = music.CurrentTargetVolume;
            float oldWoodDayCutoff = music.CurrentLowPassHz;
            clock.SetTime(clock.Day, 23f);
            music.RefreshImmediate();
            Require(music.CurrentTargetVolume < oldWoodDayVolume &&
                    music.CurrentLowPassHz < oldWoodDayCutoff,
                "night did not soften the regional score state");
        }

        private static void VerifyToast(RegionArrivalToast toast)
        {
            int before = toast.PresentationCount;
            toast.PreviewRegion("wend");
            Require(toast.LastRegionId == "wend" && toast.PresentationCount == before + 1,
                "region title did not accept the Wend presentation");
            Require(toast.DisplayedTitle == RegionCatalog.DisplayName("wend") &&
                    toast.DisplayedSubtitle == RegionCatalog.Subtitle("wend"),
                "region title is not using shared localized catalog copy");
            Require(toast.IsShowing, "arrival presentation did not begin");
            Require(toast.ShownTopInset >= 132f,
                "arrival title intrudes into the compass/waypoint safe band");
        }

        private static void VerifyClip(AudioClip clip, string label)
        {
            Require(clip != null, label + " clip is missing");
            Require(clip.frequency == 48000 && clip.channels == 1,
                label + " is not 48 kHz mono");
            Require(Mathf.Abs(clip.length - 12f) < .05f, label + " loop length drifted");
            var data = new float[Mathf.Min(clip.samples, 48000)];
            Require(clip.GetData(data, 0), "could not inspect " + label);
            float peak = 0f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            Require(peak > .002f && peak < .9f, label + " is silent or clipping");
        }

        private static float Difference(AudioClip a, AudioClip b)
        {
            int count = Mathf.Min(24000, Mathf.Min(a.samples, b.samples));
            var left = new float[count];
            var right = new float[count];
            a.GetData(left, 0);
            b.GetData(right, 0);
            double sum = 0d;
            for (int i = 0; i < count; i++) sum += Mathf.Abs(left[i] - right[i]);
            return (float)(sum / count);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[WorldFeedbackVerifier] " + message);
        }
    }
}
#endif
