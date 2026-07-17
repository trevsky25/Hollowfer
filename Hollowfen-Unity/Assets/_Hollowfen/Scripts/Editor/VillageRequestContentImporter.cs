#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
            public int RewardCopper;
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
            var festivalFinale = RewriteFestivalFinale();
            WireFestivalKickoff(festivalQuest, festivalKickoff);

            var definitions = Definitions(festivalQuest, festivalFinale);
            var authored = definitions.Select(UpsertRequest).OrderBy(request => request.Id).ToArray();
            UpsertDatabase(authored);
            EnsureFestivalLacewigNode();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"VILLAGE REQUEST CONTENT — BUILT: {authored.Length} requests (9 rotating + 1 story gathering), festival split into kickoff/delivery/finale, and Lacewig world source verified";
        }

        private static Definition[] Definitions(QuestData festivalQuest, DialogueData festivalFinale) => new[]
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
                "request.edda.wood_ear_poultice", "edda_grandfather", 18, 1, 1,
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
                new[] { "fieldCap", "woodEar" }, new[] { 2, 1 }, flags: new[] { "theo_met" }),

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
        };

        private static Definition D(string file, string id, string npc, VillageRequestKind kind,
            string copyRoot, string card, int copper, int relationship, int knowledge,
            string[] species, int[] counts, string[] flags = null, string[] quests = null) =>
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
            Set(request, "_requiredSpecies", definition.SpeciesIds.Select(Mushroom).ToArray());
            Set(request, "_requiredCounts", definition.Counts);
            Set(request, "_rewardCopper", definition.RewardCopper);
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

        private static DialogueData RewriteFestivalFinale()
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
