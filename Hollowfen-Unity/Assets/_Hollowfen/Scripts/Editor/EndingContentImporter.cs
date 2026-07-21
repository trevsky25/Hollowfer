#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Idempotent authoring pass for the four canonical endings. Narrative copy remains in
    /// ScriptableObjects; the runtime resolver only knows how to evaluate and commit it.
    /// </summary>
    public static class EndingContentImporter
    {
        private const string EndingRoot = "Assets/_Hollowfen/Data/Endings";
        private const string DialogueRoot = "Assets/_Hollowfen/Data/Dialogue";
        private const string MeetingPath = DialogueRoot + "/Dialogue_Act4_MeetAldric.asset";

        [MenuItem("Hollowfen/Content/Rebuild Ending Flow")]
        private static void RebuildMenu() => Debug.Log(RebuildAll());

        public static string RebuildAll()
        {
            EnsureFolder(EndingRoot);

            var freeDialogue = UpsertDialogue(
                DialogueRoot + "/Dialogue_Ending_FreeHollow.asset",
                "ending.free_hollow.resolution",
                Line("Wren", "No."),
                Line("Aldric", "That is rarely the first word in a negotiation."),
                Line("Wren", "It is the last word in this one."),
                Line("Aldric", "You may regret refusing protection."),
                Line("Wren", "I expect to. That does not make it wrong."));

            var patronageDialogue = UpsertDialogue(
                DialogueRoot + "/Dialogue_Ending_LordlyPatronage.asset",
                "ending.lordly_patronage.resolution",
                Line("Aldric", "You are making the practical choice."),
                Line("Wren", "Do not make it smaller by praising it."),
                Line("Aldric", "Very well."),
                Line("Wren", "The village eats. That is the sentence I will use when I cannot sleep."));

            var capitalDialogue = UpsertDialogue(
                DialogueRoot + "/Dialogue_Ending_Capital.asset",
                "ending.capital.resolution",
                Line("Theo", "Last chance to decide I am a terrible idea."),
                Line("Wren", "You are a terrible idea."),
                Line("Theo", "And?"),
                Line("Wren", "And the road is open."),
                Line("Edda", "Will you write?"),
                Line("Wren", "Yes. And if I stop, come be angry at me."));

            var witchDialogue = UpsertDialogue(
                DialogueRoot + "/Dialogue_Ending_WitchsPath.asset",
                "ending.witch_path.resolution",
                Line("Hollin", "What do we call this?"),
                Line("Wren", "Work."),
                Line("Hollin", "People prefer grander names."),
                Line("Wren", "Then people can be disappointed."),
                Line("Almy", "Names arrive after work."));

            var free = UpsertEnding(
                EndingRoot + "/Ending_FreeHollow.asset",
                "free_hollow", "ending_free_hollow", "Refuse the monopoly",
                "Hollowfen stays its own. The village will have less protection, and its future will belong to the people who live there.",
                "Hollowfen needs greater hope, proof of Aldric's harm, and the leverage to stand alone.",
                50, 0,
                new[] { "final_choice_available", "wenmar_tax_paid", "clearcut_evidence_found", "aldermark_sample_collected" },
                Array.Empty<string>(), Array.Empty<int>(), freeDialogue,
                Card("StoryCard_27_EndingFreeHollow.asset"),
                new[]
                {
                    "Wren said no, and the saying took a long time.",
                    "She refused the monopoly, accepted a smaller tax relief, traded Aldermark knowledge for a written limit on upstream cutting, and returned to Hollowfen with no banner behind her.",
                    "A year later, the square was full. Not rich. Full.",
                    "Foragers came from neighboring villages to learn from Almy. Theo still bought what Wren chose to sell. Edda taught children to turn mushrooms over before trusting them. Marra's Goldfoot stew brought travelers through the rain.",
                    "The mill wheel did not turn. The Wend had not come back. But Hollowfen no longer waited for the river to decide whether it deserved to live.",
                    "Wren wrote the next page of Tobin's journal in her own hand.",
                },
                new[] { "aldric_offer_refused", "hollowfen_independent" },
                "ACH_ENDING_FREE_HOLLOW");

            var patronage = UpsertEnding(
                EndingRoot + "/Ending_LordlyPatronage.asset",
                "lordly_patronage", "ending_lordly_patronage", "Accept Aldric's patronage",
                "The village eats. Its roofs and well will be repaired beneath Aldric's banner, and Wren will live with the price of that safety.",
                "Lord Aldric cannot offer terms until the meeting reaches its decision.",
                0, 0, new[] { "final_choice_available" },
                Array.Empty<string>(), Array.Empty<int>(), patronageDialogue,
                Card("StoryCard_28_EndingLordlyPatronage.asset"),
                new[]
                {
                    "Wren signed. The village ate. Those two facts stood beside each other and refused to become simple.",
                    "Roofs were repaired before winter. The well was rebuilt in clean stone. A paid sluice brought enough water to turn the mill wheel for show and some grinding. Aldric's banner hung near the chapel gate.",
                    "Marra had better copper pots. Bram hired help. Pell's ledger grew fat with deliveries, stipends, inspections, and mercies discovered after paperwork instructed them.",
                    "Theo left for the Capital alone. Edda asked to go with him. Wren said yes because saying no would have made the bargain worse than it already was.",
                    "At the lane's edge, Edda looked back once. Wren kept her hand raised until the wagon disappeared.",
                    "The village prospered. Wren lived with that. So did the village.",
                },
                new[] { "aldric_offer_accepted", "hollowfen_under_patronage" },
                "ACH_ENDING_LORDLY_PATRONAGE");

            var capital = UpsertEnding(
                EndingRoot + "/Ending_Capital.asset",
                "capital", "ending_capital", "Leave for the Capital",
                "Wren takes the open road with Theo. Hollowfen continues without her, while a different hunger waits beyond the village.",
                "Theo must trust Wren, and his Capital offer must still be open.",
                0, 0, new[] { "final_choice_available", "theo_capital_offer_received" },
                new[] { "theo" }, new[] { 18 }, capitalDialogue,
                Card("StoryCard_29_EndingCapital.asset"),
                new[]
                {
                    "Wren left in spring. That was kinder than winter and crueler than autumn. Spring made departures look like beginnings whether they were or not.",
                    "Theo's Capital kitchen became famous within a year. Infamous within three. Wren cooked Goldfoot stew for people who had never seen mud except as inconvenience.",
                    "She learned suppliers, knives, ledgers, staff, critics, and hunger of a different sort. She sent money home until Hollowfen stopped needing it, and letters until Edda answered with more confidence than spelling.",
                    "Hollin stayed. Almy gave her the seedbook before the first frost and pretended it was only because her eyes were tired.",
                    "Years later, Wren stood alone at a copper-topped table. An unopened letter from Hollowfen sat beside one bowl of golden stew.",
                    "For a moment, she almost did what her father had done. Then she opened it.",
                },
                new[] { "aldric_offer_refused", "theo_capital_offer_accepted", "wren_left_hollowfen" },
                "ACH_ENDING_CAPITAL");

            var witch = UpsertEnding(
                EndingRoot + "/Ending_WitchsPath.asset",
                "witch_path", "ending_witch_path", "Take the Witch's Path",
                "Wren and Hollin restore the cottage and the old work together. Hollowfen becomes a quieter place of questions, names, and careful teaching.",
                "Wren needs deeper knowledge, Hollin's trust, and Sable's cottage and seedbook before this path can be named.",
                0, 35,
                new[] { "final_choice_available", "witch_cottage_found", "sable_seedbook_recovered" },
                new[] { "hollin" }, new[] { 15 }, witchDialogue,
                Card("StoryCard_30_EndingWitchsPath.asset"),
                new[]
                {
                    "Wren refused Aldric and did not follow Theo. That left the path no one had named clearly enough to count as a plan.",
                    "The Witch's Cottage was repaired by first snow. Joren patched the hinges. Edda hauled kindling. Hollin trimmed ivy from the shutter and stood a long time before opening it.",
                    "They did not call themselves witches. Almy laughed when Wren said so, then coughed for long enough that no one laughed with her. Names arrive after work, she said.",
                    "So they worked. They walked the Deep Wood, named what was there, wrote what had not yet been written, and taught carefully.",
                    "Hollowfen prospered more quietly than it might have under Aldric, less brightly than it might have under trade. People came from Veyrwick with questions and left more thoughtful than they came.",
                    "At first snow, Wren and Hollin walked the path to the cottage together, neither leading.",
                },
                new[] { "aldric_offer_refused", "theo_capital_offer_declined", "witch_cottage_restored", "old_knowledge_restored" },
                "ACH_ENDING_WITCH_PATH");

            WireMeeting(free, patronage, capital, witch);
            SetEndingCardsChoiceLocked(free, patronage, capital, witch);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return "[EndingContentImporter] Wired four exclusive, data-driven endings into the Aldric decision.";
        }

        private static EndingData UpsertEnding(
            string path, string id, string flag, string choice, string context, string lockedHint,
            int hope, int knowledge, string[] requiredFlags, string[] npcIds, int[] relationships,
            DialogueData dialogue, StoryCardData card, string[] epilogue, string[] consequences, string achievement)
        {
            var ending = AssetDatabase.LoadAssetAtPath<EndingData>(path);
            if (ending == null)
            {
                ending = ScriptableObject.CreateInstance<EndingData>();
                AssetDatabase.CreateAsset(ending, path);
            }
            Set(ending, "_id", id);
            Set(ending, "_endingFlagId", flag);
            Set(ending, "_choiceText", choice);
            Set(ending, "_choiceContext", context);
            Set(ending, "_lockedHint", lockedHint);
            Set(ending, "_minimumVillageHope", hope);
            Set(ending, "_minimumKnowledge", knowledge);
            Set(ending, "_requiredFlagIds", requiredFlags);
            Set(ending, "_relationshipNpcIds", npcIds);
            Set(ending, "_minimumRelationshipValues", relationships);
            Set(ending, "_resolutionDialogue", dialogue);
            Set(ending, "_storyCard", card);
            Set(ending, "_epilogueCaptions", epilogue);
            Set(ending, "_consequenceFlagIds", consequences);
            Set(ending, "_achievementId", achievement);
            EditorUtility.SetDirty(ending);
            return ending;
        }

        private static DialogueData UpsertDialogue(string path, string id, params DialogueLine[] lines)
        {
            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (dialogue == null)
            {
                dialogue = ScriptableObject.CreateInstance<DialogueData>();
                AssetDatabase.CreateAsset(dialogue, path);
            }
            Set(dialogue, "_id", id);
            Set(dialogue, "_lines", lines);
            Set(dialogue, "_unlockStoryCard", null);
            Set(dialogue, "_completeQuest", null);
            Set(dialogue, "_giveItemId", null);
            Set(dialogue, "_grantCoinsCopper", 0);
            Set(dialogue, "_spendsCoinsCopper", 0);
            Set(dialogue, "_sellsForageBasket", false);
            Set(dialogue, "_basketCopperPerItem", 0);
            Set(dialogue, "_grantForage", null);
            Set(dialogue, "_grantForageCount", 1);
            Set(dialogue, "_consumeForage", null);
            Set(dialogue, "_consumeForageCount", 1);
            Set(dialogue, "_setFlagIds", Array.Empty<string>());
            Set(dialogue, "_villageHopeDelta", 0);
            Set(dialogue, "_knowledgeDelta", 0);
            Set(dialogue, "_relationshipNpcIds", Array.Empty<string>());
            Set(dialogue, "_relationshipDeltas", Array.Empty<int>());
            Set(dialogue, "_nextDialog", null);
            Set(dialogue, "_choices", Array.Empty<DialogueChoice>());
            EditorUtility.SetDirty(dialogue);
            return dialogue;
        }

        private static void WireMeeting(params EndingData[] endings)
        {
            var meeting = AssetDatabase.LoadAssetAtPath<DialogueData>(MeetingPath);
            if (meeting == null) throw new InvalidOperationException("Missing final meeting dialogue at " + MeetingPath);
            var choices = new DialogueChoice[endings.Length];
            for (int i = 0; i < endings.Length; i++)
            {
                choices[i] = new DialogueChoice
                {
                    text = endings[i].ChoiceText,
                    next = null,
                    setsFlagId = null,
                    ending = endings[i],
                };
            }
            Set(meeting, "_nextDialog", null);
            Set(meeting, "_choices", choices);
            EditorUtility.SetDirty(meeting);
        }

        private static void SetEndingCardsChoiceLocked(params EndingData[] endings)
        {
            foreach (var ending in endings)
            {
                if (ending.StoryCard == null) continue;
                Set(ending.StoryCard, "_unlockAt", -1);
                EditorUtility.SetDirty(ending.StoryCard);
            }
        }

        private static DialogueLine Line(string speaker, string text) =>
            new DialogueLine { speaker = speaker, text = text, isCloseup = true, voiceClip = null };

        private static StoryCardData Card(string fileName)
        {
            var card = AssetDatabase.LoadAssetAtPath<StoryCardData>("Assets/_Hollowfen/Data/StoryCards/" + fileName);
            if (card == null) throw new InvalidOperationException("Missing ending story card " + fileName);
            return card;
        }

        private static void Set(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
            field.SetValue(target, value);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Invalid asset folder " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
