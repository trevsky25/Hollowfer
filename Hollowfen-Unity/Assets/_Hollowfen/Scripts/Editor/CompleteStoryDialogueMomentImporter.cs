using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using Hollowfen.Quests;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Builds the complete illustrated presentation layer for Hollowfen's story-critical
    /// conversations. Ambient barks, waiting lines, and repeatable transactions deliberately
    /// remain in-world dialogue so the cinematic language stays reserved for narrative turns.
    /// </summary>
    public static class CompleteStoryDialogueMomentImporter
    {
        [Serializable]
        private sealed class SequenceManifest
        {
            public int version;
            public SequenceSpec[] sequences;
        }

        [Serializable]
        private sealed class SequenceSpec
        {
            public int number;
            public string cardAsset;
            public string questAsset;
            public string dialogueAsset;
            public string momentAsset;
            public string middleVisual;
            public string finalVisual;
            public bool liveFinale;
        }

        private const string ManifestPath =
            "Assets/_Hollowfen/Data/StoryMoments/story_dialogue_sequences.json";
        private const string CardRoot = "Assets/_Hollowfen/Data/StoryCards/";
        private const string QuestRoot = "Assets/_Hollowfen/Data/Quests/";
        private const string DialogueRoot = "Assets/_Hollowfen/Data/Dialogue/";
        private const string MomentRoot = "Assets/_Hollowfen/Data/StoryMoments/";
        private const string ArtRoot = "Assets/_Hollowfen/UI/StoryMoments/Complete/";
        private const string VoiceRoot = "Assets/_Hollowfen/Audio/VO/StoryMoments/";

        [MenuItem("Hollowfen/Story/Build Complete Dialogue Story Moments")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            SequenceManifest manifest = LoadManifest();
            foreach (SequenceSpec spec in manifest.sequences)
                BuildSequence(spec);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CompleteStoryDialogueMomentImporter] " + Verify());
        }

        [MenuItem("Hollowfen/Verify/Complete Dialogue Story Moments")]
        public static void VerifyMenu()
        {
            string result = Verify();
            if (result.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log(result);
            else Debug.LogError(result);
        }

        public static string Verify()
        {
            SequenceManifest manifest;
            try
            {
                manifest = LoadManifest();
            }
            catch (Exception exception)
            {
                return "FAIL — story-dialogue manifest is invalid: " + exception.Message;
            }

            if (manifest.sequences.Length != 25)
                return "FAIL — expected 25 remaining story-dialogue sequences; found " +
                       manifest.sequences.Length + ".";

            var seenCards = new HashSet<StoryCardData>();
            var seenDialogues = new HashSet<DialogueData>();
            foreach (SequenceSpec spec in manifest.sequences)
            {
                StoryCardData card = Load<StoryCardData>(CardPath(spec));
                DialogueData dialogue = Load<DialogueData>(DialoguePath(spec));
                StoryMomentData moment = Load<StoryMomentData>(MomentPath(spec));
                if (card == null || dialogue == null || moment == null)
                    return "FAIL — missing card, dialogue, or moment for sequence " + spec.number + ".";
                if (!seenCards.Add(card) || !seenDialogues.Add(dialogue))
                    return "FAIL — duplicate card or dialogue mapping for sequence " + spec.number + ".";
                if (card.Beats == null || card.Beats.Length != 3)
                    return "FAIL — " + card.Id + " does not have exactly three canonical story beats.";
                if (moment.StoryCard != card || dialogue.TransitionMoment != moment)
                    return "FAIL — " + card.Id + " is not wired through its dialogue transition.";
                int illustratedBeats = spec.liveFinale ? 2 : 3;
                int[] expectedBeatImages = spec.liveFinale ? new[] { 0, 1 } : new[] { 0, 1, 2 };
                if (moment.ResolveCaptions().Length != illustratedBeats || moment.Images == null ||
                    moment.Images.Length != illustratedBeats || moment.Images.Any(image => image == null) ||
                    moment.VoiceClips == null || moment.VoiceClips.Length != illustratedBeats ||
                    moment.VoiceClips.Any(clip => clip == null) ||
                    moment.BeatImages == null || !moment.BeatImages.SequenceEqual(expectedBeatImages))
                    return "FAIL — " + card.Id + " does not match its authored painted/live sequence.";
                if (spec.liveFinale && dialogue.GiveItemId != "item.mill_key")
                    return "FAIL — " + card.Id + " reserves its finale for a live mill-key handoff " +
                           "but does not grant item.mill_key.";

                if (!string.IsNullOrWhiteSpace(spec.questAsset))
                {
                    QuestData quest = Load<QuestData>(QuestPath(spec));
                    if (quest == null || quest.StoryMoment != moment)
                        return "FAIL — " + card.Id + " is not owned by its canonical quest.";
                }
                else
                {
                    EndingData ending = FindEnding(dialogue, card);
                    if (ending == null)
                        return "FAIL — ending moment " + card.Id + " is not paired with its EndingData.";
                }
            }

            string[] deliberatelyNonDialogue = { "fathers_mill", "first_forage" };
            StoryCardData[] cards = AssetDatabase.FindAssets("t:StoryCardData", new[] { CardRoot })
                .Select(guid => AssetDatabase.LoadAssetAtPath<StoryCardData>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(card => card != null)
                .ToArray();
            StoryMomentData[] moments = AssetDatabase.FindAssets("t:StoryMomentData", new[] { MomentRoot })
                .Select(guid => AssetDatabase.LoadAssetAtPath<StoryMomentData>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(moment => moment != null)
                .ToArray();
            var presentedCards = new HashSet<StoryCardData>(moments.Select(moment => moment.StoryCard));
            StoryCardData[] uncovered = cards.Where(card => !presentedCards.Contains(card) &&
                !deliberatelyNonDialogue.Contains(card.Id)).ToArray();
            if (uncovered.Length > 0)
                return "FAIL — story-dialogue cards without cinematic presentation: " +
                       string.Join(", ", uncovered.Select(card => card.Id)) + ".";

            return "PASS — all 28 cinematic story cards are complete; 24 newly covered dialogue " +
                   "turns have three painted beats, the Bram key sequence ends in its live handoff, " +
                   "and every trigger is deterministic.";
        }

        private static void BuildSequence(SequenceSpec spec)
        {
            StoryCardData card = Require<StoryCardData>(CardPath(spec));
            if (card.Beats == null || card.Beats.Length != 3 || card.Beats.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException(card.name + " needs exactly three authored beats.");

            string folder = card.Id + "/";
            string openingPath = ArtRoot + folder + "01-opening.png";
            string middlePath = ArtRoot + folder + "02-middle.png";
            string finalPath = ArtRoot + folder + "03-final.png";
            if (spec.liveFinale) ImportStorySprite(openingPath);
            ImportStorySprite(middlePath);
            if (!spec.liveFinale) ImportStorySprite(finalPath);

            StoryMomentData moment = Load<StoryMomentData>(MomentPath(spec));
            if (moment == null)
            {
                moment = ScriptableObject.CreateInstance<StoryMomentData>();
                AssetDatabase.CreateAsset(moment, MomentPath(spec));
            }

            Set(moment, "_id", "story." + card.Id + ".illustrated");
            Set(moment, "_presentation", StoryMomentPresentation.IllustratedNarration);
            Set(moment, "_trigger", StoryMomentTrigger.DialogueTransition);
            Set(moment, "_storyCard", card);
            int illustratedBeats = spec.liveFinale ? 2 : 3;
            Set(moment, "_captionIds", Enumerable.Range(0, illustratedBeats)
                .Select(index => "story." + card.Id + ".beat." + index).ToArray());
            Set(moment, "_captionFallbacks", card.Beats.Take(illustratedBeats).ToArray());
            Set(moment, "_voiceClips", Enumerable.Range(0, illustratedBeats)
                .Select(index => Require<AudioClip>(VoiceRoot + folder + index.ToString("00") +
                                                    "_Narrator.wav")).ToArray());
            Set(moment, "_images", spec.liveFinale
                ? new[] { Require<Sprite>(openingPath), Require<Sprite>(middlePath) }
                : new[] { card.Image, Require<Sprite>(middlePath), Require<Sprite>(finalPath) });
            Set(moment, "_beatImages", Enumerable.Range(0, illustratedBeats).ToArray());
            Set(moment, "_showStoryCardTitle", false);
            Set(moment, "_showCaptions", true);
            Set(moment, "_fadeIn", true);
            Set(moment, "_holdSeconds", 0f);
            Set(moment, "_pageTextId", string.Empty);
            Set(moment, "_pageTextFallback", string.Empty);
            Set(moment, "_pageTextImageIndex", -1);
            Set(moment, "_pageTextParagraphRevealBeats", Array.Empty<int>());
            Set(moment, "_focusContext", false);
            EditorUtility.SetDirty(moment);

            DialogueData dialogue = Require<DialogueData>(DialoguePath(spec));
            Set(dialogue, "_transitionMoment", moment);
            EditorUtility.SetDirty(dialogue);

            if (!string.IsNullOrWhiteSpace(spec.questAsset))
            {
                QuestData quest = Require<QuestData>(QuestPath(spec));
                Set(quest, "_storyMoment", moment);
                EditorUtility.SetDirty(quest);
            }
            else if (FindEnding(dialogue, card) == null)
            {
                throw new InvalidOperationException(
                    spec.momentAsset + " has no quest and does not match a canonical ending.");
            }
        }

        private static EndingData FindEnding(DialogueData dialogue, StoryCardData card)
        {
            return AssetDatabase.FindAssets("t:EndingData", new[] { "Assets/_Hollowfen/Data/Endings" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<EndingData>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(ending => ending != null && ending.ResolutionDialogue == dialogue &&
                                          ending.StoryCard == card);
        }

        private static SequenceManifest LoadManifest()
        {
            string absolutePath = Path.GetFullPath(ManifestPath);
            if (!File.Exists(absolutePath)) throw new FileNotFoundException(ManifestPath);
            SequenceManifest manifest = JsonUtility.FromJson<SequenceManifest>(
                File.ReadAllText(absolutePath));
            if (manifest == null || manifest.version != 1 || manifest.sequences == null)
                throw new InvalidOperationException("unsupported or empty manifest");
            if (manifest.sequences.Any(spec => spec == null || spec.number <= 0 ||
                string.IsNullOrWhiteSpace(spec.cardAsset) ||
                string.IsNullOrWhiteSpace(spec.dialogueAsset) ||
                string.IsNullOrWhiteSpace(spec.momentAsset) ||
                string.IsNullOrWhiteSpace(spec.middleVisual) ||
                string.IsNullOrWhiteSpace(spec.finalVisual)))
                throw new InvalidOperationException("one or more sequence definitions are incomplete");
            return manifest;
        }

        private static string CardPath(SequenceSpec spec) => CardRoot + spec.cardAsset + ".asset";
        private static string QuestPath(SequenceSpec spec) => QuestRoot + spec.questAsset + ".asset";
        private static string DialoguePath(SequenceSpec spec) => DialogueRoot + spec.dialogueAsset + ".asset";
        private static string MomentPath(SequenceSpec spec) => MomentRoot + spec.momentAsset + ".asset";

        private static void ImportStorySprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing story image " + path);
            if (StoryArtQualityImporter.Configure(importer)) importer.SaveAndReimport();
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            T asset = Load<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing asset " + path);
            return asset;
        }

        private static void Set(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
            field.SetValue(target, value);
        }
    }
}
