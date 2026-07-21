using System;
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
    /// Promotes Almy's authored lesson from a conventional talking-head exchange into the same
    /// voiced, illustrated presentation language used by Tobin's journal. The original dialogue
    /// clips remain canonical: four lesson lines move into the story moment while the personal
    /// question and answer remain in the world conversation.
    /// </summary>
    public static class AlmyLessonStoryMomentImporter
    {
        private const string MenuPath = "Hollowfen/Story/Build Almy Illustrated Lesson";
        private const string DialoguePath =
            "Assets/_Hollowfen/Data/Dialogue/Dialogue_Act2_AlmyGarden_Lesson.asset";
        private const string MomentPath =
            "Assets/_Hollowfen/Data/StoryMoments/StoryMoment_Act2_AlmyLesson.asset";
        private const string ImageRoot = "Assets/_Hollowfen/UI/StoryMoments/AlmyLesson/";
        private const string VoiceRoot =
            "Assets/_Hollowfen/Audio/VO/Dialogue_Act2_AlmyGarden_Lesson/";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            ImportStorySprite(ImageRoot + "almy-lesson-observe.png");
            ImportStorySprite(ImageRoot + "almy-lesson-compare.png");
            ImportStorySprite(ImageRoot + "almy-lesson-harvest.png");

            StoryMomentData moment = AssetDatabase.LoadAssetAtPath<StoryMomentData>(MomentPath);
            if (moment == null)
            {
                moment = ScriptableObject.CreateInstance<StoryMomentData>();
                AssetDatabase.CreateAsset(moment, MomentPath);
            }

            Set(moment, "_id", "act2.almy_lessons.illustrated_lesson");
            Set(moment, "_presentation", StoryMomentPresentation.IllustratedNarration);
            Set(moment, "_trigger", StoryMomentTrigger.DialogueTransition);
            Set(moment, "_storyCard", FindById<StoryCardData>("almy_lessons",
                "Assets/_Hollowfen/Data/StoryCards"));
            Set(moment, "_captionIds", new[]
            {
                "story.almy_lesson.legacy",
                "story.almy_lesson.warning",
                "story.almy_lesson.answer",
                "story.almy_lesson.patience",
            });
            Set(moment, "_captionFallbacks", new[]
            {
                "Your grandmother taught me three things. How to read rot. How to feed it. How to leave enough behind.",
                "That sounds less like a lesson and more like a warning.",
                "Most useful lessons are.",
                "Do not rush growth. Rushed mushrooms teach regret. The beds wait by your mill wall. Two plugs of Wood Ear spawn — show me you listened.",
            });
            Set(moment, "_voiceClips", new[]
            {
                Require<AudioClip>(VoiceRoot + "00_Almy.wav"),
                Require<AudioClip>(VoiceRoot + "01_Wren.wav"),
                Require<AudioClip>(VoiceRoot + "02_Almy.wav"),
                Require<AudioClip>(VoiceRoot + "06_Almy.wav"),
            });
            Set(moment, "_images", new[]
            {
                Require<Sprite>(ImageRoot + "almy-lesson-observe.png"),
                Require<Sprite>(ImageRoot + "almy-lesson-compare.png"),
                Require<Sprite>(ImageRoot + "almy-lesson-harvest.png"),
            });
            Set(moment, "_beatImages", new[] { 0, 1, 1, 2 });
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

            DialogueData dialogue = Require<DialogueData>(DialoguePath);
            DialogueLine[] lines = dialogue.Lines;
            if (lines == null || (lines.Length != 7 && lines.Length != 3))
                throw new InvalidOperationException(
                    "Almy lesson dialogue must contain its original 7 lines or the revised 3 lines.");
            DialogueLine[] revised = lines.Length == 7
                ? new[] { lines[3], lines[4], lines[5] }
                : lines.ToArray();
            revised[0].voiceClip = Require<AudioClip>(VoiceRoot + "00_Wren.wav");
            revised[1].voiceClip = Require<AudioClip>(VoiceRoot + "01_Almy.wav");
            revised[2].voiceClip = Require<AudioClip>(VoiceRoot + "02_Wren.wav");
            Set(dialogue, "_lines", revised);
            Set(dialogue, "_transitionMoment", moment);
            EditorUtility.SetDirty(dialogue);

            QuestData quest = Require<QuestData>(
                "Assets/_Hollowfen/Data/Quests/Quest_Act2_08_AlmyTeach.asset");
            Set(quest, "_storyMoment", moment);
            EditorUtility.SetDirty(quest);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AlmyLessonStoryMomentImporter] Built 3-image, 4-voice illustrated lesson. " +
                      Verify());
        }

        [MenuItem("Hollowfen/Verify/Almy Illustrated Lesson")]
        public static void VerifyMenu()
        {
            string result = Verify();
            if (result.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log(result);
            else Debug.LogError(result);
        }

        public static string Verify()
        {
            StoryMomentData moment = AssetDatabase.LoadAssetAtPath<StoryMomentData>(MomentPath);
            DialogueData dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(DialoguePath);
            if (moment == null || dialogue == null) return "FAIL — lesson data is missing.";
            if (moment.Images == null || moment.Images.Length != 3 || moment.Images.Any(image => image == null))
                return "FAIL — lesson does not have three imported illustrations.";
            if (moment.VoiceClips == null || moment.VoiceClips.Length != 4 ||
                moment.VoiceClips.Any(clip => clip == null))
                return "FAIL — lesson does not preserve its four authored voice clips.";
            if (moment.ResolveCaptions().Length != 4 || moment.BeatImages.Length != 4)
                return "FAIL — lesson captions and image beats are not aligned.";
            if (dialogue.TransitionMoment != moment || dialogue.Lines == null || dialogue.Lines.Length != 3)
                return "FAIL — Almy's in-world conversation is not wired to the illustrated lesson.";
            QuestData quest = AssetDatabase.LoadAssetAtPath<QuestData>(
                "Assets/_Hollowfen/Data/Quests/Quest_Act2_08_AlmyTeach.asset");
            if (quest == null || quest.StoryMoment != moment)
                return "FAIL — Almy's lesson quest does not own its illustrated moment.";
            return "PASS — Almy's conversation flows into 4 voiced beats across 3 new illustrations.";
        }

        private static void ImportStorySprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing story image " + path);
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (changed) importer.SaveAndReimport();
        }

        private static T FindById<T>(string id, string root) where T : UnityEngine.Object
        {
            T match = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { root })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(asset => asset != null && ReadId(asset) == id);
            if (match == null) throw new InvalidOperationException("Missing " + typeof(T).Name + " '" + id + "'.");
            return match;
        }

        private static string ReadId(UnityEngine.Object asset)
        {
            PropertyInfo property = asset.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
            return property != null ? property.GetValue(asset) as string : null;
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
