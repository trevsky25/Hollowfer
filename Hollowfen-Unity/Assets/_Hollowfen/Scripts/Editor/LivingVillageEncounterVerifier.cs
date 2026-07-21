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
using Hollowfen.Weather;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Focused Play Mode proof for paired, place-bound restored-village encounters.</summary>
    public static class LivingVillageEncounterVerifier
    {
        private sealed class Expected
        {
            public string DialogueId;
            public string Slot;
            public string RequiredFlag;
            public string CompletedFlag;
            public string FirstNpc;
            public string SecondNpc;
            public string FirstMemory;
            public string SecondMemory;
            public float Hour;
            public bool Wet;
        }

        [MenuItem("Hollowfen/Verify/Living Village Encounters")]
        private static void RunMenu() => Debug.Log(RunAll());

        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            VerifyAuthoredContent();

            string originalOverride = SaveManager.EditorSaveDirectoryOverride;
            int originalSlot = SaveManager.ActiveSlot;
            var originalRelationships = VillagerRelationships.ToSnapshot();
            var originalScores = new SaveSlotMeta();
            GameScores.WriteTo(originalScores);
            string[] originalCompleted = QuestManager.CompletedQuestIds.ToArray();
            string[] originalCards = QuestManager.UnlockedStoryCardIds.ToArray();
            QuestData originalActive = QuestManager.ActiveQuest;
            TimeManager clock = TimeManager.Instance;
            int originalDay = clock != null ? clock.Day : 1;
            float originalHour = clock != null ? clock.Hour : 8f;
            string testDirectory = Path.Combine(Path.GetTempPath(),
                "hollowfen-living-village-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            SaveManager.EditorSaveDirectoryOverride = testDirectory;

            try
            {
                SaveCoordinator.StartNewGame(2);
                QuestManager.ResetForSlotSwitch();
                QuestManager.HydrateFrom(Array.Empty<string>(), Array.Empty<string>());
                VillagerRelationships.HydrateFrom(null);
                foreach (Expected expected in Expectations()) VerifyRuntimeEncounter(expected, clock);
                return "LIVING VILLAGE — PASS: four paired restored-place encounters, exact " +
                       "schedule-gated routing, wet-weather arrival, 20 voiced lines, eight " +
                       "durable memories, four NPC bonds, and one-shot dispersal";
            }
            finally
            {
                if (DialogueScreen.Instance != null && DialogueScreen.Instance.IsOpen)
                    DialogueScreen.Instance.Close();
                SaveManager.EditorSaveDirectoryOverride = originalOverride;
                SaveManager.SetActiveSlot(originalSlot);
                VillagerRelationships.HydrateFrom(originalRelationships);
                GameScores.HydrateFrom(originalScores);
                QuestManager.ResetForSlotSwitch();
                QuestManager.HydrateFrom(originalCompleted, originalCards);
                if (originalActive != null) QuestManager.StartQuest(originalActive);
                if (clock != null) clock.SetTime(originalDay, originalHour);
                foreach (NPCSchedule schedule in UnityEngine.Object.FindObjectsByType<NPCSchedule>(
                             FindObjectsInactive.Include)) schedule.RefreshImmediate();
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
            }
        }

        private static void VerifyAuthoredContent()
        {
            foreach (Expected expected in Expectations())
            {
                DialogueData dialogue = Dialogue(expected.DialogueId);
                Require(dialogue.Lines != null && dialogue.Lines.Length == 5,
                    expected.DialogueId + " should contain five conversational beats");
                Require(dialogue.Lines.All(line => line.voiceClip != null),
                    expected.DialogueId + " contains an unvoiced line");
                Require(dialogue.SetFlagIds != null && dialogue.SetFlagIds.Length == 1 &&
                        dialogue.SetFlagIds[0] == expected.CompletedFlag,
                    expected.DialogueId + " does not close its one-shot flag");
                Require(dialogue.AtomicSocialOutcomes,
                    expected.DialogueId + " does not commit its one-shot outcomes atomically");
                Require(dialogue.MemoryOutcomes != null && dialogue.MemoryOutcomes.Length == 2 &&
                        dialogue.MemoryOutcomes.Any(outcome => outcome.npcId == expected.FirstNpc &&
                            outcome.memoryId == expected.FirstMemory) &&
                        dialogue.MemoryOutcomes.Any(outcome => outcome.npcId == expected.SecondNpc &&
                            outcome.memoryId == expected.SecondMemory),
                    expected.DialogueId + " does not remember both participants");
                Require(dialogue.BondOutcomes != null && dialogue.BondOutcomes.Length == 1 &&
                        dialogue.BondOutcomes[0].delta == 2,
                    expected.DialogueId + " does not change its paired NPC bond");
                VerifyNpcEntry(expected.FirstNpc, expected);
                VerifyNpcEntry(expected.SecondNpc, expected);
            }

            var schedules = UnityEngine.Object.FindObjectsByType<NPCSchedule>(FindObjectsInactive.Include);
            foreach (Expected expected in Expectations())
                Require(schedules.Count(schedule => schedule.FindSlot(expected.Slot) >= 0) == 2,
                    expected.Slot + " should stage exactly two villagers");
        }

        private static void VerifyNpcEntry(string npcId, Expected expected)
        {
            FieldInfo field = typeof(NPCData).GetField("_dialogueEntries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = field?.GetValue(Npc(npcId)) as NPCDialogueEntry[] ??
                          Array.Empty<NPCDialogueEntry>();
            NPCDialogueEntry[] matches = entries.Where(entry => entry.dialog != null &&
                entry.dialog.Id == expected.DialogueId).ToArray();
            Require(matches.Length == 1 && matches[0].requiresFlagId == expected.RequiredFlag &&
                    matches[0].blockedByFlagId == expected.CompletedFlag &&
                    matches[0].requiresScheduleSlotLabel == expected.Slot &&
                    matches[0].requiresPartnerNpcId == (npcId == expected.FirstNpc
                        ? expected.SecondNpc : expected.FirstNpc) &&
                    matches[0].requiresPartnerScheduleSlotLabel == expected.Slot,
                npcId + " does not own the exact spatial encounter gate for " + expected.DialogueId);
        }

        private static void VerifyRuntimeEncounter(Expected expected, TimeManager clock)
        {
            Require(clock != null, "TimeManager is unavailable");
            GameScores.SetFlag(expected.RequiredFlag);
            int day = expected.Wet ? FindWetDay(expected.Hour) : 2;
            clock.SetTime(day, expected.Hour);
            if (WeatherSystem.Instance != null) WeatherSystem.Instance.RefreshImmediate();

            NPCSchedule firstSchedule = Schedule(expected.FirstNpc);
            NPCSchedule secondSchedule = Schedule(expected.SecondNpc);
            firstSchedule.RefreshImmediate();
            secondSchedule.RefreshImmediate();
            Require(firstSchedule.CurrentSlotLabel == expected.Slot &&
                    secondSchedule.CurrentSlotLabel == expected.Slot,
                expected.DialogueId + " did not bring both villagers to its physical location");

            NPCData npc = Npc(expected.FirstNpc);
            FieldInfo currentSlot = typeof(NPCSchedule).GetField("_currentSlotIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            int pairedIndex = secondSchedule.CurrentSlotIndex;
            int absentIndex = Enumerable.Range(0, secondSchedule.SlotCount)
                .FirstOrDefault(index => index != pairedIndex);
            currentSlot?.SetValue(secondSchedule, absentIndex);
            DialogueData missingPartner = npc.PickDialog(expected.Slot);
            Require(missingPartner == null || missingPartner.Id != expected.DialogueId,
                expected.DialogueId + " remained available after its partner left the scene");
            currentSlot?.SetValue(secondSchedule, pairedIndex);

            DialogueData routed = npc.PickDialog(expected.Slot);
            Require(routed != null && routed.Id == expected.DialogueId,
                expected.DialogueId + " was not reachable at the authored schedule slot");
            DialogueData elsewhere = npc.PickDialog("not-this-place");
            Require(elsewhere == null || elsewhere.Id != expected.DialogueId,
                expected.DialogueId + " leaked outside its restored location");

            int beforeBond = VillagerRelationships.GetBond(expected.FirstNpc, expected.SecondNpc);
            DialogueScreen screen = DialogueScreen.Instance;
            Require(screen != null, "DialogueScreen is unavailable");
            screen.Open(routed);
            MethodInfo finish = typeof(DialogueScreen).GetMethod("FinishDialog",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(finish != null, "DialogueScreen finish seam is unavailable");

            long revision = SaveManager.InspectSlot(SaveManager.ActiveSlot).Revision;
            SaveManager.EditorRejectNextAtomicCommit = true;
            finish.Invoke(screen, null);
            Require(!GameScores.HasFlag(expected.CompletedFlag) &&
                    !VillagerRelationships.HasMemory(expected.FirstNpc, expected.FirstMemory) &&
                    !VillagerRelationships.HasMemory(expected.SecondNpc, expected.SecondMemory) &&
                    VillagerRelationships.GetBond(expected.FirstNpc, expected.SecondNpc) == beforeBond &&
                    SaveManager.InspectSlot(SaveManager.ActiveSlot).Revision == revision && screen.IsOpen,
                expected.DialogueId + " leaked a one-shot outcome after a rejected save");

            finish.Invoke(screen, null);

            Require(GameScores.HasFlag(expected.CompletedFlag) &&
                    VillagerRelationships.HasMemory(expected.FirstNpc, expected.FirstMemory) &&
                    VillagerRelationships.HasMemory(expected.SecondNpc, expected.SecondMemory) &&
                    VillagerRelationships.GetBond(expected.FirstNpc, expected.SecondNpc) == beforeBond + 2,
                expected.DialogueId + " did not persist its completion, memories, and bond");
            firstSchedule.RefreshImmediate();
            secondSchedule.RefreshImmediate();
            Require(firstSchedule.CurrentSlotLabel != expected.Slot &&
                    secondSchedule.CurrentSlotLabel != expected.Slot,
                expected.DialogueId + " participants did not disperse after the one-shot moment");
        }

        private static int FindWetDay(float hour)
        {
            int period = Mathf.FloorToInt(Mathf.Repeat(hour, 24f) / WeatherSystem.PeriodHours);
            for (int day = 1; day <= 80; day++)
                if (WeatherSystem.Profile(WeatherSystem.Resolve(day, period)).Precipitation >= .08f)
                    return day;
            throw new InvalidOperationException("deterministic forecast contains no wet test day");
        }

        private static NPCSchedule Schedule(string npcId) =>
            UnityEngine.Object.FindObjectsByType<NPCSchedule>(FindObjectsInactive.Include)
                .First(schedule => schedule.Actor != null && schedule.Actor.GetComponent<NPCInteractable>() != null &&
                    schedule.Actor.GetComponent<NPCInteractable>().Data.Id == npcId);

        private static NPCData Npc(string id) => AssetDatabase.FindAssets("t:NPCData",
                new[] { "Assets/_Hollowfen/Data/NPCs" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<NPCData>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(asset => asset != null && asset.Id == id);

        private static DialogueData Dialogue(string id) => AssetDatabase.FindAssets("t:DialogueData",
                new[] { "Assets/_Hollowfen/Data/Dialogue" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<DialogueData>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(asset => asset != null && asset.Id == id);

        private static Expected[] Expectations() => new[]
        {
            E("encounter.apothecary_rain_ledger", "Rain ledger at Tobin's apothecary",
                "apothecary_story_complete", "encounter.apothecary_rain_seen", "bram", "edda",
                "bram.shared_rain_ledger", "edda.shared_rain_ledger", 14.25f, true),
            E("encounter.forge_ledger", "Settling the forge ledger", "forge_in_use",
                "encounter.forge_ledger_seen", "joren", "pell", "joren.settled_forge_ledger",
                "pell.settled_forge_ledger", 10.25f, false),
            E("encounter.garden_seed_exchange", "Trading seed at the chapel garden",
                "chapel_garden_in_use", "encounter.garden_seed_seen", "almy", "marra",
                "almy.traded_chapel_seed", "marra.traded_chapel_seed", 8.25f, false),
            E("encounter.pintle_last_table", "Setting the Pintle's last table",
                "crooked_pintle_in_use", "encounter.pintle_last_table_seen", "bram", "marra",
                "bram.set_last_pintle_table", "marra.set_last_pintle_table", 19.25f, false),
        };

        private static Expected E(string dialogue, string slot, string required, string completed,
            string firstNpc, string secondNpc, string firstMemory, string secondMemory, float hour,
            bool wet) => new Expected
        {
            DialogueId = dialogue,
            Slot = slot,
            RequiredFlag = required,
            CompletedFlag = completed,
            FirstNpc = firstNpc,
            SecondNpc = secondNpc,
            FirstMemory = firstMemory,
            SecondMemory = secondMemory,
            Hour = hour,
            Wet = wet,
        };

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
