#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Hollowfen.Dialogue;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Deterministically wires index-matched cast WAVs into every DialogueData line and applies
    /// the shared spoken-word import policy. Safe to re-run after dialogue or voice changes.
    /// </summary>
    public static class DialogueVoiceoverImporter
    {
        private const string DialogueRoot = "Assets/_Hollowfen/Data/Dialogue";
        private const string VoiceRoot = "Assets/_Hollowfen/Audio/VO";
        public const string ManifestPath = VoiceRoot + "/dialogue_manifest.json";

        [Serializable]
        public sealed class VoiceManifest
        {
            public int version;
            public VoiceManifestEntry[] entries;
        }

        [Serializable]
        public sealed class VoiceManifestEntry
        {
            public string dialogue;
            public int line;
            public string speaker;
            public string textSha256;
            public string clip;
        }

        [MenuItem("Hollowfen/Audio/Wire Complete Dialogue Voiceover")]
        public static void ApplyAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var dialogues = LoadDialogues();
            var expected = ExpectedClips(dialogues);
            var manifestProblems = ManifestProblems(dialogues);
            if (manifestProblems.Count > 0)
                throw new InvalidOperationException("Voiceover manifest is incomplete or stale:\n  " +
                    string.Join("\n  ", manifestProblems.Take(12)));
            var missing = expected.Where(item => !File.Exists(ToAbsolutePath(item.ClipPath))).ToList();
            if (missing.Count > 0)
            {
                string sample = string.Join("\n", missing.Take(12).Select(item => "  " + item.ClipPath));
                throw new InvalidOperationException(
                    $"Voiceover wiring stopped: {missing.Count} expected WAV files are missing.\n{sample}" +
                    (missing.Count > 12 ? "\n  ..." : string.Empty));
            }

            var changedImporters = new List<string>();
            foreach (var path in expected.Select(item => item.ClipPath).Distinct(StringComparer.Ordinal))
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                    throw new InvalidOperationException("Expected an AudioImporter at " + path);
                if (ApplySpokenWordSettings(importer))
                {
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    changedImporters.Add(path);
                }
            }
            foreach (var path in changedImporters)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            int assigned = 0;
            foreach (var dialogue in dialogues)
            {
                string assetPath = AssetDatabase.GetAssetPath(dialogue);
                string assetName = Path.GetFileNameWithoutExtension(assetPath);
                var serialized = new SerializedObject(dialogue);
                var lines = serialized.FindProperty("_lines");
                for (int index = 0; index < lines.arraySize; index++)
                {
                    var line = lines.GetArrayElementAtIndex(index);
                    string speaker = line.FindPropertyRelative("speaker").stringValue;
                    string clipPath = ClipPath(assetName, index, speaker);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                    if (clip == null) throw new InvalidOperationException("Voice clip failed to import: " + clipPath);
                    line.FindPropertyRelative("voiceClip").objectReferenceValue = clip;
                    assigned++;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dialogue);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[DialogueVO] Wired {assigned} lines across {dialogues.Count} dialogues; " +
                      $"updated {changedImporters.Count} spoken-word importers.");
        }

        public static string ClipPath(string dialogueAssetName, int lineIndex, string speaker) =>
            $"{VoiceRoot}/{dialogueAssetName}/{lineIndex:00}_{SanitizeSpeaker(speaker)}.wav";

        public static string SanitizeSpeaker(string speaker)
        {
            string token = Regex.Replace((speaker ?? string.Empty).Trim(), "[^A-Za-z0-9]+", "_").Trim('_');
            if (string.IsNullOrEmpty(token))
                throw new ArgumentException("Speaker has no filename-safe characters.", nameof(speaker));
            return token;
        }

        public static List<string> ManifestProblems(IEnumerable<DialogueData> dialogues)
        {
            var problems = new List<string>();
            string absolute = ToAbsolutePath(ManifestPath);
            if (!File.Exists(absolute))
            {
                problems.Add("missing " + ManifestPath);
                return problems;
            }

            var manifest = JsonUtility.FromJson<VoiceManifest>(File.ReadAllText(absolute));
            if (manifest == null || manifest.version != 1 || manifest.entries == null)
            {
                problems.Add("manifest is unreadable or has an unsupported version");
                return problems;
            }

            var byKey = new Dictionary<string, VoiceManifestEntry>(StringComparer.Ordinal);
            foreach (var entry in manifest.entries)
            {
                string key = entry.dialogue + ":" + entry.line;
                if (byKey.ContainsKey(key)) problems.Add("duplicate manifest entry " + key);
                else byKey.Add(key, entry);
            }

            List<ExpectedClip> expected;
            try
            {
                expected = ExpectedClips(dialogues);
            }
            catch (ArgumentException error)
            {
                problems.Add("cannot derive expected clip paths: " + error.Message);
                return problems;
            }
            foreach (var item in expected)
            {
                string key = item.AssetName + ":" + item.LineIndex;
                if (!byKey.TryGetValue(key, out var entry))
                {
                    problems.Add("missing manifest entry " + key);
                    continue;
                }
                string relativeClip = item.ClipPath.Substring(VoiceRoot.Length + 1);
                if (entry.speaker != item.Speaker || entry.clip != relativeClip ||
                    entry.textSha256 != item.TextSha256)
                    problems.Add("stale manifest entry " + key + " (speaker, text, or clip changed)");
            }
            if (manifest.entries.Length != expected.Count)
                problems.Add($"manifest has {manifest.entries.Length} entries; expected {expected.Count}");
            return problems;
        }

        private static List<DialogueData> LoadDialogues() =>
            AssetDatabase.FindAssets("t:DialogueData", new[] { DialogueRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DialogueData>)
                .Where(dialogue => dialogue != null)
                .OrderBy(dialogue => dialogue.name, StringComparer.Ordinal)
                .ToList();

        private static List<ExpectedClip> ExpectedClips(IEnumerable<DialogueData> dialogues)
        {
            var result = new List<ExpectedClip>();
            foreach (var dialogue in dialogues)
            {
                string assetName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(dialogue));
                for (int index = 0; index < dialogue.Lines.Length; index++)
                {
                    var line = dialogue.Lines[index];
                    result.Add(new ExpectedClip(assetName, index, line.speaker,
                        TextSha256(line.text), ClipPath(assetName, index, line.speaker)));
                }
            }
            return result;
        }

        private static string TextSha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        private static bool ApplySpokenWordSettings(AudioImporter importer)
        {
            bool changed = false;
            if (!importer.forceToMono) { importer.forceToMono = true; changed = true; }
            if (!importer.loadInBackground) { importer.loadInBackground = true; changed = true; }
            if (importer.ambisonic) { importer.ambisonic = false; changed = true; }

            var settings = importer.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.CompressedInMemory ||
                settings.compressionFormat != AudioCompressionFormat.Vorbis ||
                !Mathf.Approximately(settings.quality, .72f) ||
                settings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate ||
                !settings.preloadAudioData)
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = .72f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                changed = true;
            }
            return changed;
        }

        private static string ToAbsolutePath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        private readonly struct ExpectedClip
        {
            public ExpectedClip(string assetName, int lineIndex, string speaker,
                string textSha256, string clipPath)
            {
                AssetName = assetName;
                LineIndex = lineIndex;
                Speaker = speaker;
                TextSha256 = textSha256;
                ClipPath = clipPath;
            }

            public string AssetName { get; }
            public int LineIndex { get; }
            public string Speaker { get; }
            public string TextSha256 { get; }
            public string ClipPath { get; }
        }
    }
}
#endif
