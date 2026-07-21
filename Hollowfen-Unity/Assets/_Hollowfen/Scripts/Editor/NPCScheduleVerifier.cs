#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Hollowfen.Apothecary;
using Hollowfen.Foraging;
using Hollowfen.GameTime;
using Hollowfen.Map;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using Hollowfen.Restoration;
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
        private const string FirstSaleQuestPath =
            "Assets/_Hollowfen/Data/Quests/Quest_Act1_06_FirstSale.asset";
        private const string MeetAlmyQuestPath =
            "Assets/_Hollowfen/Data/Quests/Quest_Act1_07_MeetAlmy.asset";
        private const string CottagesQuestPath =
            "Assets/_Hollowfen/Data/Quests/Quest_Act2_14_CottagesReopen.asset";
        private const string FirstTaxQuestPath =
            "Assets/_Hollowfen/Data/Quests/Quest_Act2_10_FirstTax.asset";
        private const string CaldenWarningQuestPath =
            "Assets/_Hollowfen/Data/Quests/Quest_Act2_15_CaldenWarning.asset";
        private const string CaldenReconcileQuestPath =
            "Assets/_Hollowfen/Data/Quests/Quest_Act3_19_CaldenReconcile.asset";
        private const string EddaApprenticeQuestPath =
            "Assets/_Hollowfen/Data/Quests/Quest_Act3_20_EddaApprentice.asset";
        private const string AldricLetterQuestPath =
            "Assets/_Hollowfen/Data/Quests/Quest_Act3_23_AldricLetter.asset";

        [MenuItem("Hollowfen/Verify/NPC Schedules")]
        private static void RunFromMenu() => Debug.Log(RunAll());

        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            var clock = TimeManager.Instance;
            Require(clock != null, "TimeManager is missing");
            var schedules = UnityEngine.Object.FindObjectsByType<NPCSchedule>(FindObjectsInactive.Include);
            Require(schedules.Length == 9, "expected exactly nine NPC schedules");
            Require(schedules.All(schedule => schedule.Actor != null && schedule.gameObject.activeInHierarchy),
                "a schedule host or actor reference is missing");
            Require(schedules.Select(schedule => schedule.Actor).Distinct().Count() == schedules.Length,
                "two schedules control the same actor");

            var theo = Find(schedules, "theo");
            var joren = Find(schedules, "joren");
            var bram = Find(schedules, "bram");
            var pell = Find(schedules, "pell");
            var almy = Find(schedules, "almy");
            var marra = Find(schedules, "marra");
            var edda = Find(schedules, "edda");
            var voss = Find(schedules, "voss");
            var calden = Find(schedules, "calden");
            var capitalQuest = AssetDatabase.LoadAssetAtPath<QuestData>(CapitalQuestPath);
            var firstSaleQuest = AssetDatabase.LoadAssetAtPath<QuestData>(FirstSaleQuestPath);
            var meetAlmyQuest = AssetDatabase.LoadAssetAtPath<QuestData>(MeetAlmyQuestPath);
            var cottagesQuest = AssetDatabase.LoadAssetAtPath<QuestData>(CottagesQuestPath);
            var firstTaxQuest = AssetDatabase.LoadAssetAtPath<QuestData>(FirstTaxQuestPath);
            var caldenWarningQuest = AssetDatabase.LoadAssetAtPath<QuestData>(CaldenWarningQuestPath);
            var caldenReconcileQuest = AssetDatabase.LoadAssetAtPath<QuestData>(CaldenReconcileQuestPath);
            var eddaApprenticeQuest = AssetDatabase.LoadAssetAtPath<QuestData>(EddaApprenticeQuestPath);
            var aldricLetterQuest = AssetDatabase.LoadAssetAtPath<QuestData>(AldricLetterQuestPath);
            Require(capitalQuest != null && capitalQuest.WaypointLocation != null &&
                    capitalQuest.WaypointLocation.Id == "crooked_pintle",
                "Theo's Capital quest does not point to the Crooked Pintle");
            Require(firstSaleQuest != null, "First Sale quest is missing");
            Require(cottagesQuest != null, "Cottages Reopen quest is missing");
            Require(meetAlmyQuest != null && meetAlmyQuest.WaypointLocation != null &&
                    meetAlmyQuest.WaypointLocation.Id == "fathers_mill",
                "A Knock at the Door no longer points to Father's Mill");
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
            var originalRestoration = RestorationProjects.ToSnapshot();
            var originalCases = ApothecaryCases.ToSnapshot();
            string originalSaveOverride = SaveManager.EditorSaveDirectoryOverride;
            int originalSaveSlot = SaveManager.ActiveSlot;
            string testDirectory = Path.Combine(Path.GetTempPath(),
                "hollowfen-npc-schedules-" + Guid.NewGuid().ToString("N"));
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            Vector3 originalPlayerPosition = playerObject != null ? playerObject.transform.position : Vector3.zero;
            Quaternion originalPlayerRotation = playerObject != null ? playerObject.transform.rotation : Quaternion.identity;

            Directory.CreateDirectory(testDirectory);
            SaveManager.EditorSaveDirectoryOverride = testDirectory;
            SaveManager.SetActiveSlot(3);
            SaveManager.WritePlaceholderToSlot(3);
            try
            {
                VerifyTheo(clock, theo, capitalQuest);
                VerifyOrdinaryRoutines(clock, joren, bram, pell, firstSaleQuest);
                VerifyRestorationCrew(clock, joren, bram, pell, cottagesQuest);
                VerifyAlmyQuestStaging(almy, meetAlmyQuest, playerObject);
                VerifyStoryArrivals(voss, calden, edda, firstTaxQuest,
                    caldenWarningQuest, caldenReconcileQuest, eddaApprenticeQuest,
                    aldricLetterQuest);
                VerifyApothecaryAppointments(clock, bram, edda);
                VerifyNoVisiblePop(clock, joren, playerObject);
                Require(marra.Actor != null, "Marra's schedule actor is missing");
                return "NPC SCHEDULES — PASS: 9 derived routines, Almy/Edda/Calden/Voss quest staging, Bram's First Sale Pintle staging, " +
                       "three time-bounded cottage-restoration roles, " +
                       "physical apothecary intake and due-day follow-up appointments with Edda, " +
                       "milestone-gated evenings, restored east-market Theo anchor, Pintle Capital override, " +
                       "wrapping night windows, and near-player pop deferral";
            }
            finally
            {
                GameScores.HydrateFrom(originalScores);
                RestorationProjects.HydrateFrom(originalRestoration);
                ApothecaryCases.HydrateFrom(originalCases);
                QuestManager.ResetForSlotSwitch();
                QuestManager.HydrateFrom(originalCompleted, originalCards);
                if (originalActive != null) QuestManager.StartQuest(originalActive);
                clock.SetTime(originalDay, originalHour);
                if (playerObject != null)
                    playerObject.transform.SetPositionAndRotation(originalPlayerPosition, originalPlayerRotation);
                foreach (var schedule in schedules) schedule.RefreshImmediate();
                if (originalWaypoint != null) LocationRegistry.SetWaypoint(originalWaypoint);
                else LocationRegistry.ClearWaypoint();
                SaveManager.SetActiveSlot(originalSaveSlot);
                SaveManager.EditorSaveDirectoryOverride = originalSaveOverride;
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
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
            NPCSchedule bram, NPCSchedule pell, QuestData firstSaleQuest)
        {
            SetProgress(Array.Empty<string>(), firstSaleQuest);
            clock.SetTime(clock.Day, 12f);
            bram.RefreshImmediate();
            Require(bram.CurrentSlotLabel == "First sale at the Pintle" &&
                    bram.CurrentDestination != null && bram.CurrentDestination.name == "Bram_Pintle",
                "Bram was not staged at the Pintle for his lines in Marra's First Sale conversation");

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
            bool originalSuspended = PlayerInteractor.Suspended;
            try
            {
                // Direct-scene Play Mode begins under the welcome presentation. This focused
                // schedule proof temporarily removes only that input lock so CanRelocate can
                // be exercised, then restores the presentation-owned state below.
                PlayerInteractor.Suspended = false;
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
            finally
            {
                PlayerInteractor.Suspended = originalSuspended;
            }
        }

        private static void VerifyRestorationCrew(TimeManager clock, NPCSchedule joren,
            NPCSchedule bram, NPCSchedule pell, QuestData cottagesQuest)
        {
            SetProgress(Array.Empty<string>(), cottagesQuest,
                "shutters_funded", "cottages_reopened_1");
            clock.SetTime(clock.Day, 12f);
            Refresh(joren, bram, pell);
            Require(joren.CurrentSlotLabel == "Repairing the north-lane cottage" &&
                    bram.CurrentSlotLabel == "Bringing a meal to the cottage crew" &&
                    pell.CurrentSlotLabel == "Overseeing the cottage repairs",
                "the daytime cottage crew did not assemble during WorkUnderway");
            Require(new[] { joren.CurrentDestination, bram.CurrentDestination, pell.CurrentDestination }
                    .All(destination => destination != null) &&
                    new[] { joren.CurrentDestination, bram.CurrentDestination, pell.CurrentDestination }
                    .Distinct().Count() == 3,
                "restoration workers are missing distinct authored anchors");

            clock.SetTime(clock.Day, 13.5f);
            Refresh(joren, bram, pell);
            Require(joren.CurrentSlotLabel == "Repairing the north-lane cottage" &&
                    pell.CurrentSlotLabel == "Overseeing the cottage repairs" &&
                    bram.CurrentSlotLabel == "At the village well",
                "Bram's meal visit did not end while the repair shift continued");

            clock.SetTime(clock.Day, 19f);
            Refresh(joren, bram, pell);
            Require(joren.CurrentSlotLabel == "Working at the forge" &&
                    bram.CurrentSlotLabel == "At the village well" &&
                    pell.CurrentSlotLabel == "At the village well",
                "the cottage crew did not disperse after the daytime work window");

            SetProgress(Array.Empty<string>(), cottagesQuest,
                "shutters_funded", "cottages_reopened_1", "cottages_reopened_2");
            clock.SetTime(clock.Day, 12f);
            Refresh(joren, bram, pell);
            Require(joren.CurrentSlotLabel == "Working at the forge" &&
                    bram.CurrentSlotLabel == "At the village well" &&
                    pell.CurrentSlotLabel == "At the village well",
                "the workers returned to a completed cottage after the dawn flag");
        }

        private static void VerifyAlmyQuestStaging(NPCSchedule almy, QuestData meetAlmyQuest,
            GameObject player)
        {
            SetProgress(Array.Empty<string>(), null);
            almy.RefreshImmediate();
            Require(almy.CurrentSlotLabel == "At her western doorway",
                "Almy's ordinary location is not her western doorway");
            Transform home = almy.CurrentDestination;
            int millIndex = almy.FindSlot("Waiting at the mill door");
            Transform mill = almy.GetSlotDestination(millIndex);
            Require(home != null && mill != null && home != mill,
                "Almy's home and mill-door anchors are missing or identical");

            // Starting or loading the quest while Wren is already at the threshold must still
            // place the required actor. This is the deliberate exception to ordinary pop deferral.
            if (player != null) player.transform.position = mill.position;
            SetProgress(Array.Empty<string>(), meetAlmyQuest);
            almy.Refresh(false);
            Require(almy.CurrentSlotLabel == "Waiting at the mill door" && almy.Actor.activeSelf &&
                    Vector3.Distance(almy.Actor.transform.position, mill.position) < .01f,
                "Almy did not appear at the mill when meetAlmy became active near the player");

            SetProgress(new[] { "meetAlmy" }, null);
            almy.RefreshImmediate();
            Require(almy.CurrentSlotLabel == "At her western doorway" &&
                    Vector3.Distance(almy.Actor.transform.position, home.position) < .01f,
                "Almy did not return to her western doorway after the mill conversation");
        }

        private static void VerifyStoryArrivals(NPCSchedule voss, NPCSchedule calden,
            NPCSchedule edda, QuestData firstTax, QuestData caldenWarning,
            QuestData caldenReconcile, QuestData eddaApprentice, QuestData aldricLetter)
        {
            Require(firstTax != null && firstTax.WaypointLocation != null &&
                    firstTax.WaypointLocation.Id == "theos_wagon",
                "the tax quest does not point to Voss's east-market ledger");
            Require(aldricLetter != null && aldricLetter.WaypointLocation != null &&
                    aldricLetter.WaypointLocation.Id == "fathers_mill",
                "A Sealed Letter does not point to Voss at the mill door");

            SetProgress(Array.Empty<string>(), firstTax);
            voss.RefreshImmediate();
            Require(voss.CurrentSlotLabel == "Collecting the Wenmar tax at the east market" &&
                    voss.Actor.activeSelf && voss.CurrentDestination != null &&
                    voss.CurrentDestination.name == "Voss_EastMarket",
                "Voss was not staged at the east market for the tax quest");

            SetProgress(new[] { "firstTax" }, aldricLetter);
            voss.RefreshImmediate();
            Require(voss.CurrentSlotLabel == "Delivering Aldric's letter at the mill" &&
                    voss.CurrentDestination != null && voss.CurrentDestination.name == "Voss_MillDoor",
                "Voss did not arrive at Wren's mill door with Aldric's letter");

            SetProgress(Array.Empty<string>(), caldenWarning);
            calden.RefreshImmediate();
            Require(calden.CurrentSlotLabel == "Warning Wren at the mill",
                "Calden skipped his written mill warning");
            SetProgress(Array.Empty<string>(), caldenWarning, "calden_warning_received");
            calden.RefreshImmediate();
            Require(calden.CurrentSlotLabel == "Waiting at the locked chapel garden",
                "Calden did not move from the mill to the chapel gate after his warning");
            SetProgress(new[] { "caldenWarning" }, caldenReconcile);
            calden.RefreshImmediate();
            Require(calden.CurrentSlotLabel == "Opening the chapel garden",
                "Calden was not staged at the chapel for reconciliation");

            SetProgress(new[] { "theoTrade" }, eddaApprentice, "theo_trade_unlocked");
            edda.RefreshImmediate();
            Require(edda.CurrentSlotLabel == "Asking at the mill door" && edda.Actor.activeSelf,
                "Edda did not arrive at the mill for her apprenticeship scene");
            SetProgress(new[] { "theoTrade", "eddaApprentice" }, null, "theo_trade_unlocked");
            edda.RefreshImmediate();
            Require(edda.CurrentSlotLabel == "At her cottage",
                "Edda did not return to her cottage after the apprenticeship scene");
        }

        private static void VerifyApothecaryAppointments(TimeManager clock, NPCSchedule bram,
            NPCSchedule edda)
        {
            int day = Mathf.Max(2, clock.Day);
            SetProgress(Array.Empty<string>(), null, "apothecary_story_complete");
            ApothecaryCases.HydrateFrom(new ApothecaryCaseSnapshot
            {
                Ids = new[] { "bram_rain_shiver" },
                Stages = new[] { (int)ApothecaryCaseStage.Investigating },
                StartedDays = new[] { day },
                EvidenceMasks = new[] { 0 },
                InterviewMasks = new[] { 0 },
                DecisionIds = new[] { "" },
                FollowUpDays = new[] { 0 },
                ResolvedDays = new[] { 0 },
            });
            clock.SetTime(day, 10f);
            Refresh(bram, edda);
            Require(bram.CurrentSlotLabel == "Rain-shiver appointment" &&
                    edda.CurrentSlotLabel == "Mentoring Bram's appointment" &&
                    bram.CurrentDestination != edda.CurrentDestination,
                "Bram and Edda were not physically staged at distinct casework marks");

            ApothecaryCases.HydrateFrom(new ApothecaryCaseSnapshot
            {
                Ids = new[] { "bram_rain_shiver" },
                Stages = new[] { (int)ApothecaryCaseStage.AwaitingFollowUp },
                StartedDays = new[] { day },
                EvidenceMasks = new[] { 3 },
                InterviewMasks = new[] { 3 },
                DecisionIds = new[] { "careful" },
                FollowUpDays = new[] { day + 1 },
                ResolvedDays = new[] { 0 },
            });
            Refresh(bram, edda);
            Require(bram.CurrentSlotLabel != "Rain-shiver follow-up" &&
                    edda.CurrentSlotLabel != "Receiving Bram's follow-up",
                "the patient returned before the recorded follow-up day");
            clock.SetTime(day + 1, 10f);
            Refresh(bram, edda);
            Require(bram.CurrentSlotLabel == "Rain-shiver follow-up" &&
                    edda.CurrentSlotLabel == "Receiving Bram's follow-up",
                "the patient and mentor did not return when follow-up became due");
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
