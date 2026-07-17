#if UNITY_EDITOR
using System;
using System.Linq;
using Hollowfen.GameTime;
using Hollowfen.Map;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using Hollowfen.Save;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Focused Play Mode coverage for time/quest-aware NPC placement.</summary>
    public static class NPCScheduleVerifier
    {
        private const string CapitalQuestPath =
            "Assets/_Hollowfen/Data/Quests/Quest_Act3_21_TheoCapitalOffer.asset";

        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            var clock = TimeManager.Instance;
            Require(clock != null, "TimeManager is missing");
            var schedules = UnityEngine.Object.FindObjectsByType<NPCSchedule>(FindObjectsInactive.Include);
            Require(schedules.Length == 4, "expected exactly four initial NPC schedules");
            Require(schedules.All(schedule => schedule.Actor != null && schedule.gameObject.activeInHierarchy),
                "a schedule host or actor reference is missing");
            Require(schedules.Select(schedule => schedule.Actor).Distinct().Count() == schedules.Length,
                "two schedules control the same actor");

            var theo = Find(schedules, "theo");
            var joren = Find(schedules, "joren");
            var bram = Find(schedules, "bram");
            var pell = Find(schedules, "pell");
            var capitalQuest = AssetDatabase.LoadAssetAtPath<QuestData>(CapitalQuestPath);
            Require(capitalQuest != null && capitalQuest.WaypointLocation != null &&
                    capitalQuest.WaypointLocation.Id == "crooked_pintle",
                "Theo's Capital quest does not point to the Crooked Pintle");
            Require(theo.Actor.transform.parent != null && theo.Actor.transform.parent.name == "Actors",
                "Theo is still parented beneath the wagon visibility group");

            var originalScores = new SaveSlotMeta();
            GameScores.WriteTo(originalScores);
            string[] originalCompleted = QuestManager.CompletedQuestIds.ToArray();
            string[] originalCards = QuestManager.UnlockedStoryCardIds.ToArray();
            QuestData originalActive = QuestManager.ActiveQuest;
            LocationMarker originalWaypoint = LocationRegistry.ActiveWaypoint;
            int originalDay = clock.Day;
            float originalHour = clock.Hour;
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            Vector3 originalPlayerPosition = playerObject != null ? playerObject.transform.position : Vector3.zero;
            Quaternion originalPlayerRotation = playerObject != null ? playerObject.transform.rotation : Quaternion.identity;

            try
            {
                VerifyTheo(clock, theo, capitalQuest);
                VerifyOrdinaryRoutines(clock, joren, bram, pell);
                VerifyNoVisiblePop(clock, joren, playerObject);
                return "NPC SCHEDULES — PASS: 4 derived routines, milestone-gated evenings, Theo wagon/Pintle story override, corrected Capital waypoint, wrapping night windows, and near-player pop deferral";
            }
            finally
            {
                GameScores.HydrateFrom(originalScores);
                QuestManager.ResetForSlotSwitch();
                QuestManager.HydrateFrom(originalCompleted, originalCards);
                if (originalActive != null) QuestManager.StartQuest(originalActive);
                clock.SetTime(originalDay, originalHour);
                if (playerObject != null)
                    playerObject.transform.SetPositionAndRotation(originalPlayerPosition, originalPlayerRotation);
                foreach (var schedule in schedules) schedule.RefreshImmediate();
                if (originalWaypoint != null) LocationRegistry.SetWaypoint(originalWaypoint);
                else LocationRegistry.ClearWaypoint();
            }
        }

        private static void VerifyTheo(TimeManager clock, NPCSchedule theo, QuestData capitalQuest)
        {
            SetProgress(Array.Empty<string>(), null, "theo_wagon_arrived");
            clock.SetTime(clock.Day, 23f);
            theo.RefreshImmediate();
            Require(theo.CurrentSlotLabel == "Trading at the wagon" && theo.Actor.activeSelf,
                "Theo left the wagon at night before the inn routine unlocked");
            Transform wagon = theo.CurrentDestination;

            SetProgress(new[] { "eddaApprentice" }, null, "theo_wagon_arrived");
            clock.SetTime(clock.Day, 12f);
            theo.RefreshImmediate();
            Require(theo.CurrentSlotLabel == "Trading at the wagon" && theo.CurrentDestination == wagon,
                "Theo abandoned daytime trade after the inn routine unlocked");
            clock.SetTime(clock.Day, 23f);
            theo.RefreshImmediate();
            Require(theo.CurrentSlotLabel == "Evening at the Pintle",
                "Theo did not move to the Pintle after his evening routine unlocked");
            Transform inn = theo.CurrentDestination;
            Require(inn != null && inn != wagon, "Theo's wagon and Pintle anchors are the same");

            SetProgress(new[] { "eddaApprentice" }, capitalQuest, "theo_wagon_arrived");
            clock.SetTime(clock.Day, 12f);
            theo.RefreshImmediate();
            Require(theo.CurrentSlotLabel == "Capital offer at the Pintle" && theo.CurrentDestination == inn,
                "the Capital-offer story override did not keep Theo at the Pintle by day");

            SetProgress(new[] { "eddaApprentice" }, null);
            theo.RefreshImmediate();
            Require(theo.CurrentSlotIndex == -1 && !theo.Actor.activeSelf,
                "Theo appeared before his wagon-arrival flag despite later story progress");
        }

        private static void VerifyOrdinaryRoutines(TimeManager clock, NPCSchedule joren,
            NPCSchedule bram, NPCSchedule pell)
        {
            SetProgress(Array.Empty<string>(), null);
            clock.SetTime(clock.Day, 23f);
            joren.RefreshImmediate();
            bram.RefreshImmediate();
            pell.RefreshImmediate();
            Require(joren.CurrentSlotLabel == "Working at the forge",
                "Joren's evening routine unlocked before the knife story completed");
            Require(bram.CurrentSlotLabel == "At the village well",
                "Bram's evening routine unlocked before Act I's handoff");
            Require(pell.CurrentSlotLabel == "At the village well",
                "Pell's evening routine unlocked before the cottage repairs");

            SetProgress(new[] { "forgeKnife", "meetAlmy", "cottagesReopen" }, null);
            clock.SetTime(clock.Day, 18.49f);
            Refresh(joren, bram, pell);
            Require(joren.CurrentSlotLabel == "Working at the forge" &&
                    bram.CurrentSlotLabel == "At the village well" &&
                    pell.CurrentSlotLabel == "At the village well",
                "an evening routine started before 18:30");

            clock.SetTime(clock.Day, 18.5f);
            Refresh(joren, bram, pell);
            Require(AllAtPintle(joren, bram, pell), "an unlocked NPC missed the 18:30 Pintle gathering");
            clock.SetTime(clock.Day, 6.99f);
            Refresh(joren, bram, pell);
            Require(AllAtPintle(joren, bram, pell), "wrapping night window ended before 07:00");
            clock.SetTime(clock.Day, 7f);
            Refresh(joren, bram, pell);
            Require(joren.CurrentSlotLabel == "Working at the forge" &&
                    bram.CurrentSlotLabel == "At the village well" &&
                    pell.CurrentSlotLabel == "At the village well",
                "an NPC remained at the Pintle after 07:00");
        }

        private static void VerifyNoVisiblePop(TimeManager clock, NPCSchedule joren, GameObject player)
        {
            if (player == null) return;
            SetProgress(new[] { "forgeKnife" }, null);
            clock.SetTime(clock.Day, 12f);
            joren.RefreshImmediate();
            Transform forge = joren.CurrentDestination;
            int eveningIndex = joren.FindSlot("Evening at the Pintle");
            Transform inn = joren.GetSlotDestination(eveningIndex);
            Require(forge != null && inn != null, "Joren's routine anchors are missing");

            player.transform.position = inn.position;
            clock.SetTime(clock.Day, 23f);
            bool movedInView = joren.Refresh(false);
            Require(!movedInView && joren.CurrentDestination == forge &&
                    joren.PendingSlotIndex == eveningIndex,
                "Joren visibly teleported while the player stood at his destination");

            player.transform.position = inn.position + new Vector3(100f, 0f, 100f);
            Require(joren.Refresh(false) && joren.CurrentDestination == inn,
                "Joren did not complete a deferred move after leaving the player's view");
        }

        private static bool AllAtPintle(params NPCSchedule[] schedules) =>
            schedules.All(schedule => schedule.CurrentSlotLabel == "Evening at the Pintle");

        private static void Refresh(params NPCSchedule[] schedules)
        {
            foreach (var schedule in schedules) schedule.RefreshImmediate();
        }

        private static void SetProgress(string[] completed, QuestData active, params string[] flags)
        {
            GameScores.HydrateFrom(new SaveSlotMeta { GameFlagIds = flags ?? Array.Empty<string>() });
            QuestManager.ResetForSlotSwitch();
            QuestManager.HydrateFrom(completed ?? Array.Empty<string>(), Array.Empty<string>());
            if (active != null) QuestManager.StartQuest(active);
        }

        private static NPCSchedule Find(NPCSchedule[] schedules, string npcId)
        {
            var result = schedules.FirstOrDefault(schedule =>
            {
                var interactable = schedule.Actor != null ? schedule.Actor.GetComponent<NPCInteractable>() : null;
                return interactable != null && interactable.Data != null && interactable.Data.Id == npcId;
            });
            Require(result != null, "missing schedule for " + npcId);
            return result;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[NPCScheduleVerifier] " + message);
        }
    }
}
#endif
