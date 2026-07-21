#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hollowfen.Audio;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Focused Play Mode verification for the low-repetition soundtrack library.</summary>
    public static class MusicPlaylistVerifier
    {
        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            var music = UnityEngine.Object.FindAnyObjectByType<MusicManager>();
            Require(music != null, "MusicManager is missing");
            Require(music.UsesPlaylist, "MusicManager did not enter playlist mode");
            Require(music.PlaylistClipCount == 9,
                "expected Misty Forest plus eight companion compositions");
            Require(music.SourceCount == 2 && music.Output != null,
                "playlist is not using the two-bank music mixer route");
            Require(music.MinimumSilenceSeconds >= 15f &&
                    music.MaximumSilenceSeconds > music.MinimumSilenceSeconds &&
                    music.MaximumSilenceSeconds <= 180f,
                "quiet interval range is missing or unreasonable");

            var sources = music.GetComponentsInChildren<AudioSource>(true);
            int musicSourceCount = 0;
            for (int i = 0; i < sources.Length; i++)
            {
                if (!sources[i].name.StartsWith("MusicBank_", StringComparison.Ordinal)) continue;
                musicSourceCount++;
                Require(!sources[i].loop, "playlist source still loops a single clip");
            }
            Require(musicSourceCount == 2, "expected exactly two runtime music sources");

            var serialized = new SerializedObject(music);
            var reference = serialized.FindProperty("_track").objectReferenceValue as AudioClip;
            var playlist = serialized.FindProperty("_playlist");
            var library = new List<AudioClip> { reference };
            for (int i = 0; i < playlist.arraySize; i++)
                library.Add(playlist.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip);
            Require(new HashSet<AudioClip>(library).Count == 9,
                "soundtrack contains a null or duplicate clip");
            for (int i = 0; i < library.Count; i++) VerifyClip(library[i], i > 0);

            var seen = new HashSet<AudioClip>();
            AudioClip current = music.CurrentClip;
            Require(current != null, "no composition started");
            seen.Add(current);
            for (int i = 1; i < music.PlaylistClipCount; i++)
            {
                AudioClip previous = current;
                music.AdvancePlaylistImmediate();
                current = music.CurrentClip;
                Require(current != null && current != previous,
                    "playlist repeated the same composition consecutively");
                Require(seen.Add(current),
                    "shuffle bag repeated a composition before exhausting the library");
            }
            Require(seen.Count == music.PlaylistClipCount,
                "shuffle bag did not visit every composition once");
            AudioClip cycleEnd = current;
            music.AdvancePlaylistImmediate();
            Require(music.CurrentClip != null && music.CurrentClip != cycleEnd,
                "shuffle refill repeated the prior cycle's final composition");

            return "MUSIC PLAYLIST — PASS: 9 streamed compositions, full-bag shuffle, no immediate repeats, two-source mixer routing, and randomized quiet intervals";
        }

        private static void VerifyClip(AudioClip clip, bool shouldStream)
        {
            Require(clip != null, "soundtrack clip is missing");
            Require(clip.frequency == 48000 && clip.channels == 2,
                clip.name + " is not 48 kHz stereo");
            Require(clip.length >= 150f && clip.length <= 200f,
                clip.name + " is outside the intended 2.5–3.3 minute cue range");
            if (!shouldStream) return;

            string path = AssetDatabase.GetAssetPath(clip);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            Require(importer != null, clip.name + " has no AudioImporter");
            var settings = importer.defaultSampleSettings;
            Require(settings.loadType == AudioClipLoadType.Streaming && !settings.preloadAudioData,
                clip.name + " is not configured for low-memory streaming");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[MusicPlaylistVerifier] " + message);
        }
    }
}
#endif
