using System;
using System.Reflection;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Builds Marra's first-basket paintings and live Goldfoot handoff.</summary>
    public static class MarraKitchenStoryMomentImporter
    {
        private const string MomentPath =
            "Assets/_Hollowfen/Data/StoryMoments/StoryMoment_Act1_MarraKitchen.asset";
        private const string ImageRoot = "Assets/_Hollowfen/UI/StoryMoments/MarraKitchen/";
        private const string VoiceRoot = "Assets/_Hollowfen/Audio/VO/MarraKitchen/";
        private const string CardImagePath = "Assets/_Hollowfen/UI/StoryCards/marra-kitchen.png";
        private const string DialoguePath =
            "Assets/_Hollowfen/Data/Dialogue/Dialogue_Act1_MarraKitchen_FirstBasket.asset";
        private const string GoldfootPath =
            "Assets/_Hollowfen/Data/Mushrooms/Mushroom_12_Goldfoot.asset";

        [MenuItem("Hollowfen/Story/Build Marra Kitchen Sequence")]
        public static void Build()
        {
            ImportStorySprite(CardImagePath);
            ImportStorySprite(ImageRoot + "marra-kitchen-wash.png");
            ImportStorySprite(ImageRoot + "marra-kitchen-supper.png");
            StoryMomentData moment = Require<StoryMomentData>(MomentPath);
            StoryCardData card = Require<StoryCardData>(
                "Assets/_Hollowfen/Data/StoryCards/StoryCard_06_MarraKitchen.asset");

            Set(moment, "_id", "act1.marra_kitchen.preparation");
            Set(moment, "_presentation", StoryMomentPresentation.DialogueInterstitial);
            Set(moment, "_trigger", StoryMomentTrigger.DialogueTransition);
            Set(moment, "_storyCard", card);
            Set(moment, "_captionIds", new[]
            {
                "story.marra_kitchen.interstitial",
                "story.marra_kitchen.washing",
                "story.marra_kitchen.supper",
            });
            Set(moment, "_captionFallbacks", new[]
            {
                "An hour later, the inn smelled like Wren's mother. Not exactly. Nothing was exact after enough years.",
                "Marra washed the basket in cold water, trimmed each stem, and taught Wren to listen for grit against the bowl.",
                "By dusk, the first basket had become supper. For one crowded hour, Hollowfen remembered what plenty sounded like.",
            });
            Set(moment, "_voiceClips", new[]
            {
                Require<AudioClip>(VoiceRoot + "00_Narrator.wav"),
                Require<AudioClip>(VoiceRoot + "01_Narrator.wav"),
                Require<AudioClip>(VoiceRoot + "02_Narrator.wav"),
            });
            Set(moment, "_images", new[]
            {
                card.Image,
                Require<Sprite>(ImageRoot + "marra-kitchen-wash.png"),
                Require<Sprite>(ImageRoot + "marra-kitchen-supper.png"),
            });
            Set(moment, "_beatImages", new[] { 0, 1, 2 });
            Set(moment, "_showStoryCardTitle", false);
            Set(moment, "_showCaptions", true);
            Set(moment, "_fadeIn", true);
            Set(moment, "_holdSeconds", 0f);

            DialogueData dialogue = Require<DialogueData>(DialoguePath);
            MushroomFieldGuideData goldfoot = Require<MushroomFieldGuideData>(GoldfootPath);
            Set(dialogue, "_mushroomHandoff", new DialogueMushroomHandoffCue
            {
                beforeLineIndex = 10,
                recipientSpeaker = "Marra",
                mushroom = goldfoot,
                presentationHeight = 0.27f,
            });
            EditorUtility.SetDirty(moment);
            EditorUtility.SetDirty(dialogue);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MarraKitchenStoryMomentImporter] Built kitchen sequence. " + Verify());
        }

        [MenuItem("Hollowfen/Verify/Marra Kitchen Sequence")]
        public static void VerifyMenu()
        {
            string result = Verify();
            if (result.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log(result);
            else Debug.LogError(result);
        }

        public static string Verify()
        {
            StoryMomentData moment = AssetDatabase.LoadAssetAtPath<StoryMomentData>(MomentPath);
            if (moment == null) return "FAIL — Marra's story moment is missing.";
            if (moment.ResolveCaptions().Length != 3 || moment.Images == null ||
                moment.Images.Length != 3 || moment.VoiceClips == null || moment.VoiceClips.Length != 3)
                return "FAIL — Marra's captions, voices, and paintings are not aligned.";
            for (int i = 0; i < 3; i++)
                if (moment.Images[i] == null || moment.VoiceClips[i] == null)
                    return "FAIL — Marra's sequence has a missing image or voice at beat " + i + ".";

            DialogueData dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(DialoguePath);
            DialogueMushroomHandoffCue cue = dialogue != null
                ? dialogue.MushroomHandoff
                : default(DialogueMushroomHandoffCue);
            if (!cue.IsConfigured || cue.beforeLineIndex != 10 || cue.recipientSpeaker != "Marra" ||
                cue.mushroom.Id != "goldfoot" || cue.mushroom.JournalPreviewPrefab == null)
                return "FAIL — Marra's pre-identification Goldfoot handoff is not fully authored.";

            string[] imagePaths =
            {
                CardImagePath,
                ImageRoot + "marra-kitchen-wash.png",
                ImageRoot + "marra-kitchen-supper.png",
            };
            foreach (string path in imagePaths)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null || texture.width < 3840 || texture.height < 2160)
                    return "FAIL — Marra painting is not a 4K master: " + path;
            }

            return "PASS — Marra's first basket uses 3 voiced 4K paintings and a live Goldfoot handoff.";
        }

        private static void ImportStorySprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing story image " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            StoryArtQualityImporter.Configure(importer);
            importer.SaveAndReimport();
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
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
