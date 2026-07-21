#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hollowfen.Apothecary;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using Hollowfen.Foraging;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using Hollowfen.Requests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    /// <summary>Idempotent authoring pass for rotating village work and the festival gathering.</summary>
    public static class VillageRequestContentImporter
    {
        private const string RequestRoot = "Assets/_Hollowfen/Data/Requests";
        private const string DatabasePath = "Assets/_Hollowfen/Resources/VillageRequestDatabase.asset";
        private const string DialogueRoot = "Assets/_Hollowfen/Data/Dialogue";
        // Dialogue VO tooling keys folders by asset filename and expects all authored dialogue
        // assets directly under the canonical root.
        private const string ApothecaryDialogueRoot = DialogueRoot;
        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";

        private sealed class Definition
        {
            public string FileName;
            public string Id;
            public string NpcId;
            public VillageRequestKind Kind;
            public string CopyRoot;
            public string StoryCardId;
            public string[] SpeciesIds;
            public int[] Counts;
            public string[] PreparationIds = Array.Empty<string>();
            public int[] PreparationCounts = Array.Empty<int>();
            public int RewardCopper;
            public int WetWeatherBonusCopper;
            public int Relationship;
            public int Knowledge;
            public string[] RequiredFlags = Array.Empty<string>();
            public string[] RequiredQuests = Array.Empty<string>();
            public string ActiveQuestId;
            public bool OneShot;
            public string[] CompletionFlags = Array.Empty<string>();
            public QuestData CompleteQuest;
            public DialogueData CompletionDialogue;
        }

        [MenuItem("Hollowfen/Village Requests/Build Request Content")]
        private static void BuildMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            EnsureFolder(RequestRoot);
            EnsureFolder(ApothecaryDialogueRoot);

            var festivalQuest = Quest("festivalHosted");
            var festivalKickoff = UpsertDialogue(
                DialogueRoot + "/Dialogue_Act3_Festival_Gathering.asset",
                "act3.festival.gathering",
                new[]
                {
                    Line("Marra", "Four dishes by sundown. If anyone calls it rustic, I will make them eat the garnish.", true),
                    Line("Bram", "I remember where the lantern hooks go. Mostly."),
                    Line("Pell", "This year's page may need more ink."),
                },
                new[] { "festival_gathering_active" });
            var festivalFinale = RewriteFestivalFinale(festivalQuest);
            WireFestivalKickoff(festivalQuest, festivalKickoff);

            var apothecaryDialogues = BuildApothecaryDialogues();
            WireApothecaryStory(apothecaryDialogues);

            var definitions = Definitions(festivalQuest, festivalFinale, apothecaryDialogues);
            var authored = definitions.Select(UpsertRequest).OrderBy(request => request.Id).ToArray();
            UpsertDatabase(authored);
            EnsureFestivalLacewigNode();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"VILLAGE REQUEST CONTENT — BUILT: {authored.Length} requests (12 rotating + 4 story deliveries), festival split into kickoff/delivery/finale, eight apothecary conversations, and Lacewig world source verified";
        }

        private static Definition[] Definitions(QuestData festivalQuest, DialogueData festivalFinale,
            IReadOnlyDictionary<string, DialogueData> apothecaryDialogues) => new[]
        {
            D("Request_Marra_FieldBasket", "marra_field_basket", "marra", VillageRequestKind.Kitchen,
                "request.marra.field_basket", "marra_kitchen", 18, 1, 0,
                new[] { "fieldCap", "fieldMushroom" }, new[] { 2, 1 }, quests: new[] { "firstSale" }),
            D("Request_Marra_GoldfootStew", "marra_goldfoot_stew", "marra", VillageRequestKind.Kitchen,
                "request.marra.goldfoot_stew", "marra_kitchen", 28, 1, 0,
                new[] { "goldfoot", "woodEar" }, new[] { 1, 2 }, quests: new[] { "firstSale" }),
            D("Request_Marra_WoodlandBroth", "marra_woodland_broth", "marra", VillageRequestKind.Kitchen,
                "request.marra.woodland_broth", "marra_kitchen", 18, 1, 0,
                new[] { "oyster", "pinecrest" }, new[] { 1, 1 },
                flags: new[] { "foraging_knife_unlocked" }, quests: new[] { "firstSale" }),

            D("Request_Edda_BrightsporeDraught", "edda_brightspore_draught", "edda", VillageRequestKind.Medicine,
                "request.edda.brightspore_draught", "edda_grandfather", 24, 1, 1,
                new[] { "brightspore", "woodEar" }, new[] { 1, 1 },
                flags: new[] { "apprentice_system_unlocked" }),
            D("Request_Edda_WoodEarPoultice", "edda_wood_ear_poultice", "edda", VillageRequestKind.Medicine,
                "request.edda.wood_ear_poultice", "edda_grandfather", 24, 1, 1,
                new[] { "woodEar", "pinecrest" }, new[] { 2, 1 },
                flags: new[] { "apprentice_system_unlocked" }),
            D("Request_Edda_ShelfTonic", "edda_shelf_tonic", "edda", VillageRequestKind.Medicine,
                "request.edda.shelf_tonic", "edda_grandfather", 24, 1, 1,
                new[] { "oyster", "woodEar" }, new[] { 2, 1 },
                flags: new[] { "apprentice_system_unlocked", "foraging_knife_unlocked" }),

            D("Request_Theo_MossRoadCrate", "theo_moss_road_crate", "theo", VillageRequestKind.Market,
                "request.theo.moss_road_crate", "theo_trade", 36, 1, 0,
                new[] { "goldfoot", "pinecrest" }, new[] { 2, 1 }, flags: new[] { "theo_met" }),
            D("Request_Theo_InnCellarCrate", "theo_inn_cellar_crate", "theo", VillageRequestKind.Market,
                "request.theo.inn_cellar_crate", "theo_trade", 34, 1, 0,
                new[] { "oyster", "fieldMushroom" }, new[] { 2, 2 },
                flags: new[] { "theo_met", "foraging_knife_unlocked" }),
            D("Request_Theo_RainMarketCrate", "theo_rain_market_crate", "theo", VillageRequestKind.Market,
                "request.theo.rain_market_crate", "theo_trade", 24, 1, 0,
                new[] { "fieldCap", "woodEar" }, new[] { 2, 1 }, flags: new[] { "theo_met" },
                wetBonus: 4),

            new Definition
            {
                FileName = "Request_Festival_FourDishes",
                Id = "festival_four_dishes",
                NpcId = "marra",
                Kind = VillageRequestKind.Gathering,
                CopyRoot = "request.festival.four_dishes",
                StoryCardId = "first_festival",
                SpeciesIds = new[] { "goldfoot", "lacewig", "fieldCap", "brightspore" },
                Counts = new[] { 1, 1, 1, 1 },
                RequiredFlags = new[] { "festival_gathering_active" },
                ActiveQuestId = "festivalHosted",
                OneShot = true,
                CompletionFlags = new[] { "festival_prepared", "festival_hosted" },
                CompleteQuest = festivalQuest,
                CompletionDialogue = festivalFinale,
            },

            PD("Request_Apothecary_Theo_FieldInkStory", "apothecary_theo_field_ink_story",
                "theo", VillageRequestKind.Market, "request.apothecary.theo_ink", "theo_trade",
                22, 2, 1, "field_ink", flags: new[]
                {
                    "theo_met", "apothecary_prepared_field_ink",
                }, oneShot: true, completionFlags: new[]
                {
                    "apothecary_field_ink_delivered",
                }, completionDialogue: apothecaryDialogues["theo_delivery"]),
            PD("Request_Apothecary_Marra_GoldfootStory", "apothecary_marra_goldfoot_story",
                "marra", VillageRequestKind.Kitchen, "request.apothecary.marra_broth", "marra_kitchen",
                24, 2, 1, "goldfoot_broth", flags: new[]
                {
                    "apothecary_field_ink_delivered", "apothecary_prepared_goldfoot_broth",
                }, oneShot: true, completionFlags: new[]
                {
                    "apothecary_goldfoot_delivered",
                }, completionDialogue: apothecaryDialogues["marra_delivery"]),
            PD("Request_Apothecary_Edda_TonicStory", "apothecary_edda_tonic_story",
                "edda", VillageRequestKind.Medicine, "request.apothecary.edda_tonic", "edda_grandfather",
                26, 3, 2, "brightspore_tonic", flags: new[]
                {
                    "apothecary_goldfoot_delivered", "apothecary_prepared_brightspore_tonic",
                    "apprentice_system_unlocked",
                }, oneShot: true, completionFlags: new[]
                {
                    "apothecary_tonic_delivered", "apothecary_story_complete",
                }, completionDialogue: apothecaryDialogues["edda_delivery"]),

            PD("Request_Apothecary_Theo_FieldInkRepeat", "apothecary_theo_field_ink_repeat",
                "theo", VillageRequestKind.Market, "request.apothecary.theo_ink_repeat", "theo_trade",
                18, 1, 0, "field_ink", flags: new[] { "apothecary_field_ink_delivered" },
                wetBonus: 4),
            PD("Request_Apothecary_Marra_BrothRepeat", "apothecary_marra_broth_repeat",
                "marra", VillageRequestKind.Kitchen, "request.apothecary.marra_broth_repeat", "marra_kitchen",
                20, 1, 0, "goldfoot_broth", flags: new[] { "apothecary_goldfoot_delivered" }),
            PD("Request_Apothecary_Edda_TonicRepeat", "apothecary_edda_tonic_repeat",
                "edda", VillageRequestKind.Medicine, "request.apothecary.edda_tonic_repeat", "edda_grandfather",
                22, 1, 0, "brightspore_tonic", flags: new[] { "apothecary_tonic_delivered" }),
        };

        private static Definition D(string file, string id, string npc, VillageRequestKind kind,
            string copyRoot, string card, int copper, int relationship, int knowledge,
            string[] species, int[] counts, string[] flags = null, string[] quests = null,
            int wetBonus = 0) =>
            new Definition
            {
                FileName = file,
                Id = id,
                NpcId = npc,
                Kind = kind,
                CopyRoot = copyRoot,
                StoryCardId = card,
                SpeciesIds = species,
                Counts = counts,
                RewardCopper = copper,
                Relationship = relationship,
                Knowledge = knowledge,
                RequiredFlags = flags ?? Array.Empty<string>(),
                RequiredQuests = quests ?? Array.Empty<string>(),
                WetWeatherBonusCopper = wetBonus,
            };

        private static Definition PD(string file, string id, string npc, VillageRequestKind kind,
            string copyRoot, string card, int copper, int relationship, int knowledge,
            string preparationId, string[] flags = null, bool oneShot = false,
            string[] completionFlags = null, DialogueData completionDialogue = null,
            int wetBonus = 0) =>
            new Definition
            {
                FileName = file,
                Id = id,
                NpcId = npc,
                Kind = kind,
                CopyRoot = copyRoot,
                StoryCardId = card,
                SpeciesIds = Array.Empty<string>(),
                Counts = Array.Empty<int>(),
                PreparationIds = new[] { preparationId },
                PreparationCounts = new[] { 1 },
                RewardCopper = copper,
                Relationship = relationship,
                Knowledge = knowledge,
                RequiredFlags = flags ?? Array.Empty<string>(),
                OneShot = oneShot,
                CompletionFlags = completionFlags ?? Array.Empty<string>(),
                CompletionDialogue = completionDialogue,
                WetWeatherBonusCopper = wetBonus,
            };

        private static VillageRequestData UpsertRequest(Definition definition)
        {
            string path = RequestRoot + "/" + definition.FileName + ".asset";
            var request = AssetDatabase.LoadAssetAtPath<VillageRequestData>(path);
            if (request == null)
            {
                request = ScriptableObject.CreateInstance<VillageRequestData>();
                AssetDatabase.CreateAsset(request, path);
            }
            Set(request, "_id", definition.Id);
            Set(request, "_npcId", definition.NpcId);
            Set(request, "_kind", definition.Kind);
            Set(request, "_titleId", definition.CopyRoot + ".title");
            Set(request, "_descriptionId", definition.CopyRoot + ".body");
            Set(request, "_requesterLineId", definition.CopyRoot + ".line");
            Set(request, "_heroImage", StoryCard(definition.StoryCardId).Image);
            Set(request, "_requiredSpecies", (definition.SpeciesIds ?? Array.Empty<string>()).
                Select(Mushroom).ToArray());
            Set(request, "_requiredCounts", definition.Counts ?? Array.Empty<int>());
            Set(request, "_requiredPreparations", (definition.PreparationIds ?? Array.Empty<string>()).
                Select(Preparation).ToArray());
            Set(request, "_requiredPreparationCounts",
                definition.PreparationCounts ?? Array.Empty<int>());
            Set(request, "_rewardCopper", definition.RewardCopper);
            Set(request, "_wetWeatherBonusCopper", definition.WetWeatherBonusCopper);
            Set(request, "_firstCompletionRelationshipDelta", definition.Relationship);
            Set(request, "_firstCompletionKnowledgeDelta", definition.Knowledge);
            Set(request, "_requiredFlagIds", definition.RequiredFlags);
            Set(request, "_requiredCompletedQuestIds", definition.RequiredQuests);
            Set(request, "_activeQuestId", definition.ActiveQuestId ?? "");
            Set(request, "_oneShot", definition.OneShot);
            Set(request, "_completionFlagIds", definition.CompletionFlags);
            Set(request, "_completeQuest", definition.CompleteQuest);
            Set(request, "_completionDialogue", definition.CompletionDialogue);
            EditorUtility.SetDirty(request);
            return request;
        }

        private static IReadOnlyDictionary<string, DialogueData> BuildApothecaryDialogues()
        {
            var result = new Dictionary<string, DialogueData>(StringComparer.Ordinal)
            {
                ["almy_lesson"] = UpsertDialogue(
                    ApothecaryDialogueRoot + "/Dialogue_Apothecary_Almy_FirstLesson.asset",
                    "apothecary.almy.first_lesson",
                    new[]
                    {
                        Line("Almy", "Your father's journal was never only a field book. Those small marks beside the margins are bench marks."),
                        Line("Wren", "He had a room for this above the mill, and still wrote as if no one would ever stand there."),
                        Line("Almy", "Grief made him private. Do not mistake privacy for wisdom. Name every specimen, measure twice, and write what leaves the shelf.", true),
                        Line("Wren", "Then the first thing I prepare will be a record."),
                    }, new[] { "apothecary_almy_lesson_seen" }),
                ["bram_memory"] = UpsertDialogue(
                    ApothecaryDialogueRoot + "/Dialogue_Apothecary_Bram_TobinMemory.asset",
                    "apothecary.bram.tobin_memory",
                    new[]
                    {
                        Line("Bram", "Tobin used to come down after midnight with ink on both cuffs and ask whether the rain had changed its mind."),
                        Line("Wren", "Did it?"),
                        Line("Bram", "Never. But he kept better notes than the weather did."),
                    }, new[] { "apothecary_bram_memory_seen" }),
                ["joren_work"] = UpsertDialogue(
                    ApothecaryDialogueRoot + "/Dialogue_Apothecary_Joren_Worksite.asset",
                    "apothecary.joren.worksite",
                    new[]
                    {
                        Line("Joren", "The stone is sound. It is the hinges, flue, and table feet that have forgotten their work."),
                        Line("Wren", "Can they remember?"),
                        Line("Joren", "Wood remembers pressure. Iron remembers heat. People are the troublesome material."),
                    }, Array.Empty<string>()),
                ["pell_ledger"] = UpsertDialogue(
                    ApothecaryDialogueRoot + "/Dialogue_Apothecary_Pell_Ledger.asset",
                    "apothecary.pell.ledger",
                    new[]
                    {
                        Line("Pell", "A workshop reopening counts as trade, care, inheritance, and fire inspection. I have given it four lines."),
                        Line("Wren", "Only four?"),
                        Line("Pell", "Ink is not free. Apparently that is now your concern."),
                    }, Array.Empty<string>()),
                ["hollin_reflection"] = UpsertDialogue(
                    ApothecaryDialogueRoot + "/Dialogue_Apothecary_Hollin_Reflection.asset",
                    "apothecary.hollin.reflection",
                    new[]
                    {
                        Line("Hollin", "Sable kept her knowledge behind a door. Your father kept his inside a book."),
                        Line("Wren", "And I put mine on labels."),
                        Line("Hollin", "Yes. A village can argue with a label. A secret only waits to be lost.", true),
                    }, new[] { "apothecary_hollin_reflection_seen" }),
                ["theo_delivery"] = UpsertDialogue(
                    ApothecaryDialogueRoot + "/Dialogue_Apothecary_Theo_FieldInkDelivery.asset",
                    "apothecary.theo.field_ink_delivery",
                    new[]
                    {
                        Line("Wren", "One stoppered inkwell. It dried dark enough to survive my thumb."),
                        Line("Theo", "And the road, I hope. Your father sold me notes once. The rain bought most of them before I reached the ridge."),
                        Line("Theo", "This is better work, Wren. Not because it is grand. Because it arrives readable.", true),
                    }, Array.Empty<string>()),
                ["marra_delivery"] = UpsertDialogue(
                    ApothecaryDialogueRoot + "/Dialogue_Apothecary_Marra_BrothDelivery.asset",
                    "apothecary.marra.broth_delivery",
                    new[]
                    {
                        Line("Wren", "The first covered jar from the restored bench."),
                        Line("Marra", "Label straight. Lid sound. Smells like Goldfoot without shouting about it."),
                        Line("Marra", "Tobin kept secrets. You are keeping stock. That may be the more useful inheritance.", true),
                    }, Array.Empty<string>()),
                ["edda_delivery"] = UpsertDialogue(
                    ApothecaryDialogueRoot + "/Dialogue_Apothecary_Edda_TonicDelivery.asset",
                    "apothecary.edda.tonic_delivery",
                    new[]
                    {
                        Line("Wren", "Sealed, dated, and entered twice. Once in the workshop ledger, once in mine."),
                        Line("Edda", "Good. A frightened person reads badly. The label must do the remembering for them."),
                        Line("Edda", "This belongs to Hollowfen's shelf, not to rumor. That is how we keep care from becoming harm.", true),
                        Line("Wren", "Then the room is open."),
                    }, Array.Empty<string>()),
            };

            ConfigureDialogueScores(result["almy_lesson"], 1, "almy", 2);
            ConfigureDialogueScores(result["bram_memory"], 0, "bram", 1);
            ConfigureDialogueScores(result["hollin_reflection"], 2, "hollin", 1);
            ConfigureDialogueMemory(result["almy_lesson"], "almy", "almy.apothecary_first_lesson");
            ConfigureDialogueMemory(result["bram_memory"], "bram", "bram.shared_tobin_weather_story");
            ConfigureDialogueMemory(result["marra_delivery"], "marra", "marra.received_first_apothecary_broth");
            ConfigureDialogueMemory(result["edda_delivery"], "edda", "edda.opened_village_care_shelf");
            return result;
        }

        private static void ConfigureDialogueMemory(DialogueData dialogue, string npcId, string memoryId)
        {
            Set(dialogue, "_memoryOutcomes", new[]
            {
                new DialogueMemoryOutcome { npcId = npcId, memoryId = memoryId },
            });
            EditorUtility.SetDirty(dialogue);
        }

        private static void ConfigureDialogueScores(DialogueData dialogue, int knowledge,
            string npcId, int relationship)
        {
            Set(dialogue, "_knowledgeDelta", knowledge);
            Set(dialogue, "_relationshipNpcIds",
                string.IsNullOrWhiteSpace(npcId) ? Array.Empty<string>() : new[] { npcId });
            Set(dialogue, "_relationshipDeltas",
                string.IsNullOrWhiteSpace(npcId) ? Array.Empty<int>() : new[] { relationship });
            EditorUtility.SetDirty(dialogue);
        }

        private static void WireApothecaryStory(IReadOnlyDictionary<string, DialogueData> dialogues)
        {
            PrependNpcDialogue("almy", Entry("tobin_workshop_in_use",
                "apothecary_almy_lesson_seen", dialogues["almy_lesson"]));
            PrependNpcDialogue("bram", Entry("tobin_workshop_in_use",
                "apothecary_bram_memory_seen", dialogues["bram_memory"]));
            PrependNpcDialogue("joren", Entry("tobin_workshop_work_started",
                "tobin_workshop_restored", dialogues["joren_work"]));
            PrependNpcDialogue("pell", Entry("tobin_workshop_work_started",
                "tobin_workshop_restored", dialogues["pell_ledger"]));
            PrependNpcDialogue("hollin", Entry("apothecary_story_complete",
                "apothecary_hollin_reflection_seen", dialogues["hollin_reflection"]));
        }

        private static NPCDialogueEntry Entry(string requiredFlag, string blockedFlag,
            DialogueData dialogue) => new NPCDialogueEntry
            {
                requiresFlagId = requiredFlag,
                blockedByFlagId = blockedFlag,
                dialog = dialogue,
            };

        private static void PrependNpcDialogue(string npcId, NPCDialogueEntry entry)
        {
            var npc = Npc(npcId);
            var field = typeof(NPCData).GetField("_dialogueEntries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = field?.GetValue(npc) as NPCDialogueEntry[] ?? Array.Empty<NPCDialogueEntry>();
            string dialogueId = entry.dialog != null ? entry.dialog.Id : string.Empty;
            var authored = new List<NPCDialogueEntry> { entry };
            authored.AddRange(entries.Where(existing => existing.dialog == null ||
                existing.dialog.Id != dialogueId));
            field?.SetValue(npc, authored.ToArray());
            EditorUtility.SetDirty(npc);
        }

        private static void UpsertDatabase(VillageRequestData[] requests)
        {
            var database = AssetDatabase.LoadAssetAtPath<VillageRequestDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<VillageRequestDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }
            Set(database, "_requests", requests);
            EditorUtility.SetDirty(database);
        }

        private static DialogueData UpsertDialogue(string path, string id, DialogueLine[] lines, string[] flags)
        {
            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (dialogue == null)
            {
                dialogue = ScriptableObject.CreateInstance<DialogueData>();
                AssetDatabase.CreateAsset(dialogue, path);
            }
            Set(dialogue, "_id", id);
            Set(dialogue, "_lines", lines);
            ResetDialogueOutcomes(dialogue);
            Set(dialogue, "_setFlagIds", flags ?? Array.Empty<string>());
            EditorUtility.SetDirty(dialogue);
            return dialogue;
        }

        private static DialogueData RewriteFestivalFinale(QuestData festivalQuest)
        {
            var finale = Dialogue("act3.festival");
            Set(finale, "_lines", new[]
            {
                Line("Wren", "The square filled before the lanterns were lit. Halfway through the evening I slipped behind the inn, where the gratitude was getting too thick to breathe.", true),
                Line("Edda", "You are hiding."),
                Line("Wren", "I am washing bowls privately."),
                Line("Edda", "That is hiding with dishes."),
                Line("Wren", "It was the first festival in three years. I let her be right about the bowls.", true),
            });
            ResetDialogueOutcomes(finale);
            Set(finale, "_transitionMoment", festivalQuest != null ? festivalQuest.StoryMoment : null);
            EditorUtility.SetDirty(finale);
            return finale;
        }

        private static void ResetDialogueOutcomes(DialogueData dialogue)
        {
            Set(dialogue, "_unlockStoryCard", null);
            Set(dialogue, "_completeQuest", null);
            Set(dialogue, "_giveItemId", null);
            Set(dialogue, "_grantCoinsCopper", 0);
            Set(dialogue, "_spendsCoinsCopper", 0);
            Set(dialogue, "_sellsForageBasket", false);
            Set(dialogue, "_basketCopperPerItem", 0);
            Set(dialogue, "_basketBuyer", MushroomBuyer.None);
            Set(dialogue, "_grantForage", null);
            Set(dialogue, "_grantForageCount", 1);
            Set(dialogue, "_consumeForage", null);
            Set(dialogue, "_consumeForageCount", 1);
            Set(dialogue, "_setFlagIds", Array.Empty<string>());
            Set(dialogue, "_villageHopeDelta", 0);
            Set(dialogue, "_knowledgeDelta", 0);
            Set(dialogue, "_relationshipNpcIds", Array.Empty<string>());
            Set(dialogue, "_relationshipDeltas", Array.Empty<int>());
            Set(dialogue, "_memoryOutcomes", Array.Empty<DialogueMemoryOutcome>());
            Set(dialogue, "_bondOutcomes", Array.Empty<DialogueBondOutcome>());
            Set(dialogue, "_favorOutcomes", Array.Empty<DialogueFavorOutcome>());
            Set(dialogue, "_advanceMinutes", 0);
            Set(dialogue, "_transitionMoment", null);
            Set(dialogue, "_nextDialog", null);
            Set(dialogue, "_choices", Array.Empty<DialogueChoice>());
        }

        private static void WireFestivalKickoff(QuestData festivalQuest, DialogueData kickoff)
        {
            var marra = Npc("marra");
            var field = typeof(NPCData).GetField("_dialogueEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = field?.GetValue(marra) as NPCDialogueEntry[];
            if (entries == null) throw new InvalidOperationException("Marra has no dialogue entries.");
            int index = Array.FindIndex(entries, entry => entry.activeQuest == festivalQuest);
            if (index < 0) throw new InvalidOperationException("Marra has no festivalHosted dialogue entry.");
            entries[index].dialog = kickoff;
            field.SetValue(marra, entries);
            EditorUtility.SetDirty(marra);
        }

        private static void EnsureFestivalLacewigNode()
        {
            if (EditorSceneManager.GetActiveScene().path != GameplayScene)
                EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            var all = UnityEngine.Object.FindObjectsByType<MushroomNode>(FindObjectsInactive.Include)
                .Where(node => node.gameObject.scene == scene && !node.IsCultivated)
                .ToArray();
            var existing = all.FirstOrDefault(node => node.Data != null && node.Data.Id == "lacewig");
            if (existing != null)
            {
                EnsureNodeId(existing, "wild.lacewig.001");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return;
            }

            var source = all.FirstOrDefault(node => node.Data != null && node.Data.Id == "oyster");
            var lacewig = Mushroom("lacewig");
            if (source == null || lacewig.WorldPrefab == null)
                throw new InvalidOperationException("Cannot place festival Lacewig: Oyster source or Lacewig prefab is missing.");
            var instance = PrefabUtility.InstantiatePrefab(lacewig.WorldPrefab, scene) as GameObject;
            if (instance == null) throw new InvalidOperationException("Could not instantiate the Lacewig world prefab.");
            instance.name = "_RequestSource_Lacewig";
            instance.transform.SetParent(source.transform.parent, true);
            instance.transform.SetPositionAndRotation(source.transform.position + new Vector3(1.15f, 0f, 0.65f), source.transform.rotation);
            var node = instance.GetComponent<MushroomNode>();
            if (node == null) throw new InvalidOperationException("Lacewig world prefab has no MushroomNode.");
            EnsureNodeId(node, "wild.lacewig.001");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureNodeId(MushroomNode node, string id)
        {
            var serialized = new SerializedObject(node);
            serialized.FindProperty("_nodeId").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(node);
        }

        private static DialogueLine Line(string speaker, string text, bool closeup = false) =>
            new DialogueLine { speaker = speaker, text = text, isCloseup = closeup, voiceClip = null };

        private static MushroomFieldGuideData Mushroom(string id) =>
            FindById<MushroomFieldGuideData>(id, asset => asset.Id, "Assets/_Hollowfen/Data/Mushrooms");

        private static PreparationRecipeData Preparation(string id) =>
            FindById<PreparationRecipeData>(id, asset => asset.Id, "Assets/_Hollowfen/Data/Apothecary");

        private static StoryCardData StoryCard(string id) =>
            FindById<StoryCardData>(id, asset => asset.Id, "Assets/_Hollowfen/Data/StoryCards");

        private static QuestData Quest(string id) =>
            FindById<QuestData>(id, asset => asset.Id, "Assets/_Hollowfen/Data/Quests");

        private static DialogueData Dialogue(string id) =>
            FindById<DialogueData>(id, asset => asset.Id, DialogueRoot);

        private static NPCData Npc(string id) =>
            FindById<NPCData>(id, asset => asset.Id, "Assets/_Hollowfen/Data/NPCs");

        private static T FindById<T>(string id, Func<T, string> selector, string root) where T : UnityEngine.Object
        {
            var match = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { root })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(asset => asset != null && selector(asset) == id);
            if (match == null) throw new InvalidOperationException($"Missing {typeof(T).Name} '{id}' under {root}.");
            return match;
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
