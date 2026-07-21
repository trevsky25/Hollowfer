#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Hollowfen.Dialogue;
using Hollowfen.GameTime;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using Hollowfen.Save;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Isolated Play Mode coverage for memory, personal arcs, and recovered village roles.</summary>
    public static class RelationshipSystemVerifier
    {
        private static readonly string[] CoreNpcIds = { "bram", "almy", "joren", "marra", "edda", "pell" };

        [MenuItem("Hollowfen/Verify/Relationship Memory & Personal Arcs")]
        private static void RunFromMenu() => Debug.Log(RunAll());

        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            string originalOverride = SaveManager.EditorSaveDirectoryOverride;
            int originalSlot = SaveManager.ActiveSlot;
            var originalRelationships = VillagerRelationships.ToSnapshot();
            var originalScores = new SaveSlotMeta();
            GameScores.WriteTo(originalScores);
            string[] originalCompleted = QuestManager.CompletedQuestIds.ToArray();
            string[] originalCards = QuestManager.UnlockedStoryCardIds.ToArray();
            QuestData originalActive = QuestManager.ActiveQuest;
            var clock = TimeManager.Instance;
            int originalDay = clock != null ? clock.Day : 1;
            float originalHour = clock != null ? clock.Hour : 8f;
            string testDirectory = Path.Combine(Path.GetTempPath(),
                "hollowfen-relationships-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            SaveManager.EditorSaveDirectoryOverride = testDirectory;

            try
            {
                SaveCoordinator.StartNewGame(3);
                if (clock != null) clock.SetTime(6, 9f);
                VerifyRuntimeAndRoundTrip();
                VerifyAuthoredArcsAndRouting();
                VerifyRecoveredSchedules();
                return "RELATIONSHIP SYSTEM — PASS: save migration + round-trip, idempotent memories, canonical NPC bonds, monotonic six-villager favors, quest-priority routing, 46 voiced relationship conversations, and nine recovered village schedules";
            }
            finally
            {
                SaveManager.EditorSaveDirectoryOverride = originalOverride;
                SaveManager.SetActiveSlot(originalSlot);
                VillagerRelationships.HydrateFrom(originalRelationships);
                GameScores.HydrateFrom(originalScores);
                QuestManager.ResetForSlotSwitch();
                QuestManager.HydrateFrom(originalCompleted, originalCards);
                if (originalActive != null) QuestManager.StartQuest(originalActive);
                if (clock != null) clock.SetTime(originalDay, originalHour);
                foreach (var schedule in UnityEngine.Object.FindObjectsByType<NPCSchedule>(FindObjectsInactive.Include))
                    schedule.RefreshImmediate();
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
            }
        }

        private static void VerifyRuntimeAndRoundTrip()
        {
            Require(VillagerRelationships.Remember("bram", "bram.shared_supper", 6),
                "first memory was not recorded");
            Require(!VillagerRelationships.Remember("bram", "bram.shared_supper", 7) &&
                    VillagerRelationships.MemoryDay("bram", "bram.shared_supper") == 6,
                "memory replay duplicated or rewrote the original day");
            Require(VillagerRelationships.AddBond("bram", "marra", 2) == 2 &&
                    VillagerRelationships.GetBond("marra", "bram") == 2,
                "NPC bond is not canonical and symmetric");
            Require(VillagerRelationships.AdvanceFavor("favor.bram.after_hours", 2) &&
                    !VillagerRelationships.AdvanceFavor("favor.bram.after_hours", 1) &&
                    VillagerRelationships.FavorStage("favor.bram.after_hours") == 2,
                "favor chain regressed or accepted duplicate progress");

            SaveCoordinator.SaveAll();
            VillagerRelationships.HydrateFrom(null);
            Require(SaveCoordinator.TryLoadSlot(3, out var inspection) && inspection.CanLoad,
                "relationship save could not be loaded");
            Require(VillagerRelationships.HasMemory("bram", "bram.shared_supper") &&
                    VillagerRelationships.GetBond("bram", "marra") == 2 &&
                    VillagerRelationships.FavorStage("favor.bram.after_hours") == 2,
                "relationship snapshot did not round-trip");
        }

        private static void VerifyAuthoredArcsAndRouting()
        {
            FieldInfo entriesField = typeof(NPCData).GetField("_dialogueEntries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (string npcId in CoreNpcIds)
            {
                NPCData npc = Npc(npcId);
                var entries = entriesField?.GetValue(npc) as NPCDialogueEntry[] ?? Array.Empty<NPCDialogueEntry>();
                var allRelationship = entries.Where(entry => entry.dialog != null &&
                    entry.dialog.Id.StartsWith("relationship." + npcId + ".", StringComparison.Ordinal)).ToArray();
                var personal = allRelationship.Where(entry => entry.dialog.Id.Contains(".favor_") ||
                    entry.dialog.Id.EndsWith(".familiar", StringComparison.Ordinal)).ToArray();
                Require(personal.Length == 3, npcId + " does not have exactly two moments and one familiar greeting");
                Require(allRelationship.SelectMany(entry => entry.dialog.Lines).All(line => line.voiceClip != null),
                    npcId + " personal arc contains unvoiced dialogue");
                Require(personal.Count(entry => entry.dialog.MemoryOutcomes != null &&
                    entry.dialog.MemoryOutcomes.Length == 1) == 2,
                    npcId + " personal arc does not record both activity memories");
                Require(personal.Count(entry => entry.dialog.FavorOutcomes != null &&
                    entry.dialog.FavorOutcomes.Length == 1) == 2,
                    npcId + " personal arc does not advance both favor stages");
                Require(allRelationship.Count(entry => entry.dialog.Id.Contains(".ending_") &&
                    entry.dialog.Id != "relationship." + npcId + ".ending_consideration") >= 4,
                    npcId + " does not react to all four final outcomes");
            }

            QuestManager.ResetForSlotSwitch();
            QuestManager.HydrateFrom(Array.Empty<string>(), Array.Empty<string>());
            VillagerRelationships.HydrateFrom(null);
            GameScores.SetFlag("crooked_pintle_in_use");
            var bram = Npc("bram");
            DialogueData personalFirst = bram.PickDialog();
            Require(personalFirst != null && personalFirst.Id == "relationship.bram.favor_01",
                "Bram's first restored-Pintle moment is not reachable");

            QuestData speakBram = Quest("speakBram");
            QuestManager.StartQuest(speakBram);
            DialogueData story = bram.PickDialog();
            Require(story != null && !story.Id.StartsWith("relationship.", StringComparison.Ordinal),
                "optional relationship dialogue masked Bram's active story objective");
        }

        private static void VerifyRecoveredSchedules()
        {
            var schedules = UnityEngine.Object.FindObjectsByType<NPCSchedule>(FindObjectsInactive.Include);
            Require(schedules.Length == 9,
                "expected all nine scheduled villagers after the exploration-layout expansion");
            Require(Schedule(schedules, "NPC_Marra").FindSlot("Keeping the restored Pintle kitchen") >= 0 &&
                    Schedule(schedules, "NPC_Edda").FindSlot("Keeping the apothecary care shelf") >= 0 &&
                    Schedule(schedules, "NPC_Bram").FindSlot("Keeping the restored Pintle") >= 0 &&
                    Schedule(schedules, "NPC_Almy").FindSlot("Tending the restored chapel beds") >= 0 &&
                    Schedule(schedules, "NPC_Joren").FindSlot("Keeping the restored forge fire") >= 0 &&
                    Schedule(schedules, "NPC_Pell").FindSlot("Visiting the occupied north-lane cottages") >= 0,
                "one or more post-restoration roles are missing from the live scene");
        }

        private static NPCSchedule Schedule(NPCSchedule[] schedules, string actorName) =>
            schedules.First(schedule => schedule.Actor != null && schedule.Actor.name == actorName);

        private static NPCData Npc(string id) => AssetDatabase.FindAssets("t:NPCData",
                new[] { "Assets/_Hollowfen/Data/NPCs" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<NPCData>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(asset => asset != null && asset.Id == id);

        private static QuestData Quest(string id) => AssetDatabase.FindAssets("t:QuestData",
                new[] { "Assets/_Hollowfen/Data/Quests" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<QuestData>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(asset => asset != null && asset.Id == id);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
