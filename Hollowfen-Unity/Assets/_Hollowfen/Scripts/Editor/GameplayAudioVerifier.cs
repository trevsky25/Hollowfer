#if UNITY_EDITOR
using System;
using System.Linq;
using Hollowfen.Audio;
using Hollowfen.Dialogue;
using StarterAssets;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Focused Play Mode proof for complete dialogue VO and tactile feedback dispatch.</summary>
    public static class GameplayAudioVerifier
    {
        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            VerifyFeedbackLibrary();
            VerifyFootsteps();
            var dialogues = LoadDialogues();
            int lineCount = VerifyVoiceCoverage(dialogues);
            VerifyDialogueDispatch(dialogues.First(dialogue => dialogue.name == "Dialogue_Act1_Bram_Repeat"));
            return $"GAMEPLAY AUDIO — PASS: {Enum.GetValues(typeof(GameplaySfx.Cue)).Length} mixer-routed " +
                   $"48 kHz feedback cues and {lineCount}/{lineCount} spoken lines across {dialogues.Length} dialogues";
        }

        private static void VerifyFootsteps()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<ThirdPersonController>();
            Require(controller != null, "player ThirdPersonController is missing");
            Require(controller.FootstepAudioClips != null && controller.FootstepAudioClips.Length >= 6,
                "player footstep variation bank is missing");
            Require(controller.LandingAudioClip != null, "player landing clip is missing");
            var source = controller.FootstepSource;
            Require(source != null && source.spatialBlend > .99f,
                "player footsteps do not use a dedicated spatial AudioSource");
            Require(source.outputAudioMixerGroup == GameplaySfx.Output,
                "player footsteps bypass the gameplay SFX mixer");
        }

        private static void VerifyFeedbackLibrary()
        {
            Require(GameplaySfx.Output != null, "gameplay cues are not routed to the SFX mixer");
            foreach (GameplaySfx.Cue cue in Enum.GetValues(typeof(GameplaySfx.Cue)))
            {
                var clip = GameplaySfx.GetClip(cue);
                Require(clip != null, cue + " did not build a clip");
                Require(clip.frequency == GameplaySfx.SampleRate && clip.channels == 1,
                    cue + " is not 48 kHz mono");
                var samples = new float[Mathf.Min(clip.samples, 24000)];
                Require(clip.GetData(samples, 0), "could not inspect " + cue);
                float peak = samples.Max(sample => Mathf.Abs(sample));
                Require(peak > .002f && peak < .96f, cue + " is silent or clipping (peak " + peak + ")");
            }

            int before = GameplaySfx.CueCount;
            GameplaySfx.DeliveryComplete();
            Require(GameplaySfx.CueCount == before + 1 &&
                    GameplaySfx.LastCue == GameplaySfx.Cue.DeliveryComplete,
                "delivery action did not dispatch its authored cue");
        }

        private static int VerifyVoiceCoverage(DialogueData[] dialogues)
        {
            Require(dialogues.Length == 75, "expected 75 authored dialogues, found " + dialogues.Length);
            int lineCount = 0;
            int wrenLines = 0;
            int castLines = 0;
            foreach (var dialogue in dialogues)
            {
                string assetName = dialogue.name;
                for (int index = 0; index < dialogue.Lines.Length; index++)
                {
                    lineCount++;
                    var line = dialogue.Lines[index];
                    Require(line.voiceClip != null,
                        $"{assetName} line {index} ({line.speaker}) has no voice clip");
                    string expected = DialogueVoiceoverImporter.ClipPath(assetName, index, line.speaker);
                    Require(AssetDatabase.GetAssetPath(line.voiceClip) == expected,
                        $"{assetName} line {index} is wired to the wrong voice file");
                    Require(line.voiceClip.channels == 1 && line.voiceClip.frequency == 24000,
                        expected + " is not 24 kHz mono source VO");
                    if (line.speaker == "Wren") wrenLines++;
                    else castLines++;
                }
            }
            Require(lineCount == 267, "expected 267 dialogue lines, found " + lineCount);
            Require(wrenLines == 107 && castLines == 160,
                $"cast split drifted (Wren {wrenLines}, other cast {castLines})");
            return lineCount;
        }

        private static void VerifyDialogueDispatch(DialogueData dialogue)
        {
            var screen = DialogueScreen.Instance;
            Require(screen != null, "DialogueScreen is missing");
            Require(screen.VoiceOutput != null, "DialogueScreen voice source is not mixer-routed");
            if (screen.IsOpen) screen.Close();
            screen.Open(dialogue);
            Require(screen.IsOpen, "dialogue did not open");
            Require(screen.CurrentVoiceClip == dialogue.Lines[0].voiceClip,
                "showing the first line did not dispatch its voice clip");
            screen.Close();
            Require(!screen.IsOpen, "dialogue did not close after voice dispatch check");
        }

        private static DialogueData[] LoadDialogues() =>
            AssetDatabase.FindAssets("t:DialogueData", new[] { "Assets/_Hollowfen/Data/Dialogue" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DialogueData>)
                .Where(dialogue => dialogue != null)
                .OrderBy(dialogue => dialogue.name, StringComparer.Ordinal)
                .ToArray();

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[GameplayAudioVerifier] " + message);
        }
    }
}
#endif
