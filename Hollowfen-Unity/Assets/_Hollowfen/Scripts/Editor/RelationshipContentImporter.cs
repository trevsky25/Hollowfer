#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hollowfen.Dialogue;
using Hollowfen.NPCs;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Authors the first complete personal-arc layer: two optional activities and a durable
    /// memory-aware greeting for each of Hollowfen's six core villagers.
    /// </summary>
    public static class RelationshipContentImporter
    {
        // Keep dialogue assets directly under the canonical root: the deterministic VO
        // generator keys its cast folders by filename and intentionally rejects hidden trees.
        private const string Root = "Assets/_Hollowfen/Data/Dialogue";
        private const string LegacyRoot = Root + "/Relationships";

        private sealed class Arc
        {
            public string NpcId;
            public string RequiredFlagOne;
            public string RequiredFlagTwo;
            public string FavorId;
            public string MemoryOne;
            public string MemoryTwo;
            public string BondOne;
            public string BondTwo;
            public int FirstMinutes;
            public int SecondMinutes;
            public DialogueLine[] FirstLines;
            public DialogueLine[] SecondLines;
            public DialogueLine[] FamiliarLines;
            public DialogueLine[] ConsiderationLines;
        }

        private sealed class EndingReaction
        {
            public string Suffix;
            public string Flag;
            public string MemorySuffix;
            public DialogueLine[] Lines;
        }

        [MenuItem("Hollowfen/Relationships/Build Personal Arcs")]
        public static void BuildMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            EnsureFolder(Root);
            foreach (var arc in Arcs()) BuildArc(arc);
            if (AssetDatabase.IsValidFolder(LegacyRoot) &&
                !AssetDatabase.FindAssets("", new[] { LegacyRoot }).Any())
                AssetDatabase.DeleteAsset(LegacyRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "RELATIONSHIP CONTENT — BUILT: 6 core villager arcs, 12 optional activities, 18 memory-aware conversations";
        }

        private static void BuildArc(Arc arc)
        {
            string title = char.ToUpperInvariant(arc.NpcId[0]) + arc.NpcId.Substring(1);
            DialogueData first = UpsertDialogue(
                $"{Root}/Dialogue_Relationship_{title}_01.asset",
                $"relationship.{arc.NpcId}.favor_01", arc.FirstLines,
                arc.NpcId, arc.MemoryOne, arc.FavorId, 1, arc.BondOne, arc.FirstMinutes, 2);
            DialogueData second = UpsertDialogue(
                $"{Root}/Dialogue_Relationship_{title}_02.asset",
                $"relationship.{arc.NpcId}.favor_02", arc.SecondLines,
                arc.NpcId, arc.MemoryTwo, arc.FavorId, 2, arc.BondTwo, arc.SecondMinutes, 2);
            DialogueData familiar = UpsertDialogue(
                $"{Root}/Dialogue_Relationship_{title}_Familiar.asset",
                $"relationship.{arc.NpcId}.familiar", arc.FamiliarLines,
                null, null, null, 0, null, 0, 0);

            var entries = new List<NPCDialogueEntry>();
            foreach (var reaction in EndingReactions(arc.NpcId))
            {
                DialogueData endingDialogue = UpsertDialogue(
                    $"{Root}/Dialogue_Relationship_{title}_Ending{reaction.Suffix}.asset",
                    $"relationship.{arc.NpcId}.ending_{reaction.MemorySuffix}", reaction.Lines,
                    arc.NpcId, $"{arc.NpcId}.ending_{reaction.MemorySuffix}_reaction",
                    null, 0, null, 0, 0);
                entries.Add(new NPCDialogueEntry
                {
                    requiresFlagId = reaction.Flag,
                    blockedByMemoryId = $"{arc.NpcId}.ending_{reaction.MemorySuffix}_reaction",
                    dialog = endingDialogue,
                });
            }
            if (arc.ConsiderationLines != null && arc.ConsiderationLines.Length > 0)
            {
                DialogueData consideration = UpsertDialogue(
                    $"{Root}/Dialogue_Relationship_{title}_Consideration.asset",
                    $"relationship.{arc.NpcId}.ending_consideration", arc.ConsiderationLines,
                    arc.NpcId, $"{arc.NpcId}.weighed_final_choice", null, 0, null, 0, 0);
                entries.Add(new NPCDialogueEntry
                {
                    requiresFlagId = "final_choice_available",
                    blockedByFlagId = "game_complete",
                    blockedByMemoryId = $"{arc.NpcId}.weighed_final_choice",
                    dialog = consideration,
                });
            }
            entries.AddRange(new[]
            {
                new NPCDialogueEntry
                {
                    requiresFlagId = arc.RequiredFlagTwo,
                    requiresMemoryId = arc.MemoryOne,
                    blockedByMemoryId = arc.MemoryTwo,
                    favorId = arc.FavorId,
                    minimumFavorStage = 1,
                    maximumFavorStage = 1,
                    dialog = second,
                },
                new NPCDialogueEntry
                {
                    requiresFlagId = arc.RequiredFlagOne,
                    blockedByMemoryId = arc.MemoryOne,
                    favorId = arc.FavorId,
                    minimumFavorStage = 0,
                    maximumFavorStage = 0,
                    dialog = first,
                },
                new NPCDialogueEntry
                {
                    requiresFlagId = arc.RequiredFlagTwo,
                    requiresMemoryId = arc.MemoryTwo,
                    favorId = arc.FavorId,
                    minimumFavorStage = 2,
                    usesMinimumRelationship = true,
                    minimumRelationship = 4,
                    dialog = familiar,
                },
            });
            PrependAuthoredEntries(arc.NpcId, entries);
        }

        private static EndingReaction[] EndingReactions(string npcId)
        {
            switch (npcId)
            {
                case "bram": return Endings("Bram",
                    "Then you chose the village, with no hand above it. Frightening. Tobin would have called that a beginning.",
                    "A lord's roof keeps rain off. Mind that no one forgets who owns the rafters.",
                    "Veyrwick will call it progress before the first cart arrives. Make them learn our names before our prices.",
                    "Sable's road is older than the Pintle and lonelier. If you walk it, keep one lantern facing home.");
                case "almy": return Endings("Almy",
                    "Freedom is not a harvest; it is the tending after. You chose the harder season, and I am glad.",
                    "Protection can shelter a root or keep it in a pot. We will watch which kind Aldric means.",
                    "Carry Hollowfen's knowledge outward if you must, but do not let the capital rename what it did not learn.",
                    "You chose the old road. I will not pretend it is gentle, only that you will not be the first to walk it.");
                case "joren": return Endings("Joren",
                    "No patron, no purchaser. Good. Then every weak joint is ours to mend and every sound one ours to trust.",
                    "A lord's seal can hold a bargain. It can also hide a crack. I will test both.",
                    "Capital buys iron by weight. Hollowfen will have to teach it that workmanship is not measured that way.",
                    "You chose what grows beyond straight lines. I make hinges, Wren. Even strange doors need them.");
                case "marra": return Endings("Marra",
                    "Then Hollowfen keeps its own table. We will be hungry some winters, but the portions will be ours to divide.",
                    "Aldric may send grain. He may also send appetites. I will count both at the kitchen door.",
                    "Contracts from Veyrwick will fill the pantry and empty it in new ways. I hope you read the small print hungry.",
                    "If the woods are part of our bargain now, they will not be fed lies from my kitchen.");
                case "edda": return Endings("Edda",
                    "Then the care shelf answers to the people who reach for it. That is worth the uncertainty.",
                    "Protection can keep medicine stocked. I will still write every dose where Aldric's clerks can see it.",
                    "If the capital funds our care, it will also receive our records—complete enough that neglect cannot hide in totals.",
                    "Old knowledge is still accountable knowledge. I will help you prove that, even to Almy.");
                case "pell": return Endings("Pell",
                    "I have entered Hollowfen under its own name. There was more room on the line than I expected.",
                    "I left a space after Aldric's name. Patronage creates footnotes quickly.",
                    "The capital prefers columns. I have retained the margins; Hollowfen's important facts keep moving there.",
                    "For the first time in this ledger, the margin has become the heading. I suppose records can learn.");
                default: return Array.Empty<EndingReaction>();
            }
        }

        private static EndingReaction[] Endings(string speaker, string free, string lordly, string capital, string witch) => new[]
        {
            new EndingReaction { Suffix = "FreeHollow", Flag = "ending_free_hollow", MemorySuffix = "free_hollow", Lines = Lines(L(speaker, free, true)) },
            new EndingReaction { Suffix = "LordlyPatronage", Flag = "ending_lordly_patronage", MemorySuffix = "lordly_patronage", Lines = Lines(L(speaker, lordly, true)) },
            new EndingReaction { Suffix = "Capital", Flag = "ending_capital", MemorySuffix = "capital", Lines = Lines(L(speaker, capital, true)) },
            new EndingReaction { Suffix = "WitchPath", Flag = "ending_witch_path", MemorySuffix = "witch_path", Lines = Lines(L(speaker, witch, true)) },
        };

        private static DialogueData UpsertDialogue(string path, string id, DialogueLine[] lines,
            string memoryNpc, string memoryId, string favorId, int favorStage,
            string bondNpc, int advanceMinutes, int relationshipDelta)
        {
            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (dialogue == null)
            {
                string legacyPath = LegacyRoot + "/" + Path.GetFileName(path);
                if (AssetDatabase.LoadAssetAtPath<DialogueData>(legacyPath) != null)
                {
                    string moveError = AssetDatabase.MoveAsset(legacyPath, path);
                    if (!string.IsNullOrEmpty(moveError))
                        throw new InvalidOperationException("Could not migrate relationship dialogue: " + moveError);
                    dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
                }
            }
            if (dialogue == null)
            {
                dialogue = ScriptableObject.CreateInstance<DialogueData>();
                AssetDatabase.CreateAsset(dialogue, path);
            }

            Set(dialogue, "_id", id);
            Set(dialogue, "_lines", lines ?? Array.Empty<DialogueLine>());
            Set(dialogue, "_mushroomHandoff", default(DialogueMushroomHandoffCue));
            Set(dialogue, "_unlockStoryCard", null);
            Set(dialogue, "_completeQuest", null);
            Set(dialogue, "_giveItemId", "");
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
            Set(dialogue, "_relationshipNpcIds", relationshipDelta != 0 && !string.IsNullOrWhiteSpace(memoryNpc)
                ? new[] { memoryNpc }
                : Array.Empty<string>());
            Set(dialogue, "_relationshipDeltas", relationshipDelta != 0 && !string.IsNullOrWhiteSpace(memoryNpc)
                ? new[] { relationshipDelta }
                : Array.Empty<int>());
            Set(dialogue, "_memoryOutcomes", !string.IsNullOrWhiteSpace(memoryNpc) && !string.IsNullOrWhiteSpace(memoryId)
                ? new[] { new DialogueMemoryOutcome { npcId = memoryNpc, memoryId = memoryId } }
                : Array.Empty<DialogueMemoryOutcome>());
            Set(dialogue, "_bondOutcomes", !string.IsNullOrWhiteSpace(memoryNpc) && !string.IsNullOrWhiteSpace(bondNpc)
                ? new[] { new DialogueBondOutcome { firstNpcId = memoryNpc, secondNpcId = bondNpc, delta = 1 } }
                : Array.Empty<DialogueBondOutcome>());
            Set(dialogue, "_favorOutcomes", !string.IsNullOrWhiteSpace(favorId) && favorStage > 0
                ? new[] { new DialogueFavorOutcome { favorId = favorId, stage = favorStage } }
                : Array.Empty<DialogueFavorOutcome>());
            Set(dialogue, "_advanceMinutes", Mathf.Max(0, advanceMinutes));
            Set(dialogue, "_transitionMoment", null);
            Set(dialogue, "_nextDialog", null);
            Set(dialogue, "_choices", Array.Empty<DialogueChoice>());
            EditorUtility.SetDirty(dialogue);
            return dialogue;
        }

        private static void PrependAuthoredEntries(string npcId, IReadOnlyList<NPCDialogueEntry> authored)
        {
            NPCData npc = Npc(npcId);
            FieldInfo field = typeof(NPCData).GetField("_dialogueEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            var existing = field?.GetValue(npc) as NPCDialogueEntry[] ?? Array.Empty<NPCDialogueEntry>();
            var authoredIds = new HashSet<string>(authored.Where(entry => entry.dialog != null)
                .Select(entry => entry.dialog.Id), StringComparer.Ordinal);
            var merged = new List<NPCDialogueEntry>(authored);
            merged.AddRange(existing.Where(entry => entry.dialog == null || !authoredIds.Contains(entry.dialog.Id)));
            field?.SetValue(npc, merged.ToArray());
            EditorUtility.SetDirty(npc);
        }

        private static Arc[] Arcs() => new[]
        {
            new Arc
            {
                NpcId = "bram", RequiredFlagOne = "crooked_pintle_in_use", RequiredFlagTwo = "festival_hosted",
                FavorId = "favor.bram.after_hours", MemoryOne = "bram.shared_supper", MemoryTwo = "bram.last_lantern",
                BondOne = "marra", BondTwo = "marra", FirstMinutes = 45, SecondMinutes = 35,
                FirstLines = Lines(
                    L("Bram", "The Pintle has tables again. One of them still rocks, which is how I know it is truly ours."),
                    L("Wren", "I can hold the bowl and the table at once."),
                    L("Bram", "You can sit. Marra left stew, and I have spent three years saving a story your father would hate me for telling."),
                    L("Wren", "Then start before the stew gets cold.", true)),
                SecondLines = Lines(
                    L("Bram", "Walk the shutters with me? After a full room, the quiet arrives too quickly."),
                    L("Wren", "Which lantern goes out last?"),
                    L("Bram", "The one over Tobin's corner. Habit is a poor memorial, but it kept a place for you."),
                    L("Wren", "Tonight we can call it something better than habit.", true)),
                FamiliarLines = Lines(L("Bram", "Your corner bowl is under the counter. Marra says that is favoritism. She filled it herself.")),
            },
            new Arc
            {
                NpcId = "almy", RequiredFlagOne = "chapel_garden_in_use", RequiredFlagTwo = "apothecary_story_complete",
                FavorId = "favor.almy.garden_walks", MemoryOne = "almy.walked_seed_rows", MemoryTwo = "almy.shared_sable_note",
                BondOne = "edda", BondTwo = "edda", FirstMinutes = 35, SecondMinutes = 50,
                FirstLines = Lines(
                    L("Almy", "Walk the new beds with me. A garden tells on its keeper before the keeper knows to confess."),
                    L("Wren", "What are mine saying?"),
                    L("Almy", "That you water like someone apologizing. Roots prefer confidence."),
                    L("Wren", "Show me where to stand, then.", true)),
                SecondLines = Lines(
                    L("Almy", "I found one of Sable's notes pressed beneath the seed ledger. I nearly kept it to myself."),
                    L("Wren", "But you brought it."),
                    L("Almy", "Edda reminded me that a guarded truth can still become a lost truth. Your workshop has changed more than its shelves."),
                    L("Wren", "We will copy it twice. Then we will argue with it.", true)),
                FamiliarLines = Lines(L("Almy", "The western row is yours today. Water without apologizing.")),
            },
            new Arc
            {
                NpcId = "joren", RequiredFlagOne = "forge_in_use", RequiredFlagTwo = "tobin_workshop_in_use",
                FavorId = "favor.joren.working_silence", MemoryOne = "joren.shared_first_fire", MemoryTwo = "joren.made_shelf_bracket",
                BondOne = "pell", BondTwo = "almy", FirstMinutes = 40, SecondMinutes = 55,
                FirstLines = Lines(
                    L("Joren", "First proper fire since the roof failed. Bellows are yours if you want the honor."),
                    L("Wren", "How hard?"),
                    L("Joren", "Hard enough to wake it. Not hard enough to make it angry."),
                    L("Wren", "That sounds like advice about more than a forge."),
                    L("Joren", "Most useful advice is stolen from another trade.", true)),
                SecondLines = Lines(
                    L("Joren", "Your apothecary shelf leans. I made a bracket. You can hold it or criticize it, not both."),
                    L("Wren", "I will hold it and criticize you quietly."),
                    L("Joren", "Good. Quiet work is the closest I come to company."),
                    L("Wren", "Then I will be excellent company.", true)),
                FamiliarLines = Lines(L("Joren", "There is room at the bench if you do not mind silence. You have learned not to.")),
                ConsiderationLines = Lines(L("Joren", "Every offer is a tool. Before you take one, ask who keeps hold of the handle.", true)),
            },
            new Arc
            {
                NpcId = "marra", RequiredFlagOne = "crooked_pintle_in_use", RequiredFlagTwo = "festival_hosted",
                FavorId = "favor.marra.kitchen_table", MemoryOne = "marra.shared_kitchen_supper", MemoryTwo = "marra.cleaned_after_festival",
                BondOne = "bram", BondTwo = "edda", FirstMinutes = 45, SecondMinutes = 50,
                FirstLines = Lines(
                    L("Marra", "You have delivered to this kitchen often enough to earn the dangerous side of the table."),
                    L("Wren", "The side nearest the knives?"),
                    L("Marra", "The side where I ask whether you have eaten and refuse every dishonest answer."),
                    L("Wren", "Then no. Not properly."),
                    L("Marra", "Sit.", true)),
                SecondLines = Lines(
                    L("Marra", "Festivals end in three things: wax, dishes, and people disappearing before either is cleared."),
                    L("Wren", "I brought sleeves."),
                    L("Marra", "Edda brought salve. Bram brought opinions. You may stay."),
                    L("Wren", "That is nearly affectionate."),
                    L("Marra", "Do not make me withdraw it.", true)),
                FamiliarLines = Lines(L("Marra", "Wash your hands. There is always a place here, but never for idle fingers.")),
                ConsiderationLines = Lines(L("Marra", "Choose the table where Hollowfen still gets to decide who eats first—and who is never turned away.", true)),
            },
            new Arc
            {
                NpcId = "edda", RequiredFlagOne = "apothecary_story_complete", RequiredFlagTwo = "chapel_garden_in_use",
                FavorId = "favor.edda.care_rounds", MemoryOne = "edda.shared_shelf_round", MemoryTwo = "edda.walked_care_round",
                BondOne = "almy", BondTwo = "pell", FirstMinutes = 40, SecondMinutes = 55,
                FirstLines = Lines(
                    L("Edda", "Come check the care shelf with me. Preparing a tonic is half the work. Finding it where frightened hands expect it is the other half."),
                    L("Wren", "Labels outward, oldest forward?"),
                    L("Edda", "And every empty space explained. An unexplained empty shelf becomes a rumor by supper."),
                    L("Wren", "Then we leave no room for rumors.", true)),
                SecondLines = Lines(
                    L("Edda", "I have three cottage calls. Walk with me? Pell writes names, but sometimes names need faces beside them."),
                    L("Wren", "Tell me what to carry."),
                    L("Edda", "Nothing. Today you listen. Care is not always an ingredient."),
                    L("Wren", "I can learn that too.", true)),
                FamiliarLines = Lines(L("Edda", "I left the shelf list open for you. Your notes are clearer than mine now; do not look pleased about it.")),
                ConsiderationLines = Lines(L("Edda", "Ask which choice leaves care where frightened people can reach it. Promises matter less than access.", true)),
            },
            new Arc
            {
                NpcId = "pell", RequiredFlagOne = "cottages_reopened_2", RequiredFlagTwo = "festival_hosted",
                FavorId = "favor.pell.village_ledger", MemoryOne = "pell.entered_returning_names", MemoryTwo = "pell.walked_occupied_cottages",
                BondOne = "joren", BondTwo = "edda", FirstMinutes = 35, SecondMinutes = 55,
                FirstLines = Lines(
                    L("Pell", "The cottage ledger needs names, not numbers. Joren says this is sentiment interfering with columns."),
                    L("Wren", "Joren repaired the nameplates."),
                    L("Pell", "Precisely. His sentiment has hinges."),
                    L("Wren", "Read them to me. I will make a clean copy.", true)),
                SecondLines = Lines(
                    L("Pell", "I inspect occupied cottages differently now. A sound roof matters. So does whether anyone sings beneath it."),
                    L("Wren", "Is singing in the official form?"),
                    L("Pell", "It is in the margin. Edda has made the margins increasingly difficult to ignore."),
                    L("Wren", "Let us walk them before the ink dries.", true)),
                FamiliarLines = Lines(L("Pell", "I saved you the margin. It appears to be where the useful facts have moved.")),
                ConsiderationLines = Lines(L("Pell", "Every ending becomes a record. Choose the one whose omissions you are prepared to answer for.", true)),
            },
        };

        private static DialogueLine[] Lines(params DialogueLine[] lines) => lines;
        private static DialogueLine L(string speaker, string text, bool closeup = false) =>
            new DialogueLine { speaker = speaker, text = text, isCloseup = closeup, voiceClip = null };

        private static NPCData Npc(string id)
        {
            var match = AssetDatabase.FindAssets("t:NPCData", new[] { "Assets/_Hollowfen/Data/NPCs" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<NPCData>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(asset => asset != null && string.Equals(asset.Id, id, StringComparison.Ordinal));
            if (match == null) throw new InvalidOperationException("Missing NPCData '" + id + "'.");
            return match;
        }

        private static void Set(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
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
