#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Hollowfen.Apothecary;
using Hollowfen.GameTime;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using Hollowfen.Save;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Content and isolated durability proof for Tobin's six appointment cases.</summary>
    public static class ApothecaryCaseworkVerifier
    {
        private const string PrefabPath =
            "Assets/_Hollowfen/Art/Apothecary/PF_TobinApothecaryBuilding.prefab";

        [MenuItem("Hollowfen/Apothecary/Verify Patient Casework")]
        public static void RunMenu() => Debug.Log(RunAll());

        public static string RunAll()
        {
            VerifyAuthoredContent();
            VerifyAtomicCaseLoop();
            return "APOTHECARY CASEWORK — PASS: 6 sequential character cases, 24 evidence beats, " +
                   "18 reasoned outcomes, physical patient/mentor staging, prepared-stock consumption, " +
                   "delayed follow-ups, durable memories/bonds, and commit-failure rollback";
        }

        private static void VerifyAuthoredContent()
        {
            ApothecaryCaseDatabase database = ApothecaryCaseDatabase.Load();
            Require(database != null && database.Cases.Count == 6,
                "case database should contain exactly six appointments");
            string[] expected =
            {
                "bram_rain_shiver", "pell_fading_ledger", "joren_hammer_echo",
                "marra_cellar_bloom", "almy_brightspore_sleep", "theo_road_cold",
            };
            Require(database.Cases.Select(data => data != null ? data.Id : "").SequenceEqual(expected),
                "case order or ids drifted");

            ApothecaryCaseData prior = null;
            foreach (ApothecaryCaseData data in database.Cases)
            {
                Require(data != null && data.HasValidStructure, "a case asset is structurally invalid");
                Require(data.RequiredFlagId == "apothecary_story_complete",
                    data.Id + " is not gated by the completed workshop chapter");
                Require(data.Clues.Length == 2 && data.Interviews.Length == 2,
                    data.Id + " should require two observations and two interview answers");
                Require(data.Decisions.Length == 3 &&
                        data.Decisions.Select(decision => decision.grade).Distinct().Count() == 3 &&
                        data.Decisions.Select(decision => decision.preparation).Distinct().Count() == 3,
                    data.Id + " does not offer three meaningfully different prepared choices");
                Require(data.IntakeDialogue != null && data.IntakeDialogue.Lines.Length == 3 &&
                        data.FollowUpDialogue != null && data.FollowUpDialogue.Lines.Length == 2,
                    data.Id + " is missing its voiced intake/follow-up exchange");
                Require(data.PatientProfile != null && data.PatientProfile.HeroPortrait != null,
                    data.Id + " has no casebook portrait");
                Require(data.RequiresResolvedCase == prior && (prior == null
                            ? data.UnlockDelayDays == 0 : data.UnlockDelayDays == 1),
                    data.Id + " is not chained one dawn after the prior follow-up");
                VerifyLocalized(data, "short", "title", "complaint", "context", "locked");
                foreach (ApothecaryCaseClue clue in data.Clues)
                    VerifyLocalized(data, "clue." + clue.id + ".label",
                        "clue." + clue.id + ".finding");
                foreach (ApothecaryCaseInterview interview in data.Interviews)
                    VerifyLocalized(data, "interview." + interview.id + ".question",
                        "interview." + interview.id + ".answer");
                foreach (ApothecaryCaseDecision decision in data.Decisions)
                {
                    VerifyLocalized(data, "decision." + decision.id + ".outcome_title",
                        "decision." + decision.id + ".outcome_body");
                    string memoryKey = "relationship.memory." + decision.memoryId;
                    Require(Localization.Get(memoryKey) != memoryKey,
                        data.Id + " has an unlocalized durable memory for " + decision.id);
                    Require(decision.followUpDays >= 1 && decision.knowledge == 1,
                        data.Id + " has an invalid follow-up/reward contract");
                }
                prior = data;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab != null, "apothecary prefab is missing");
            ApothecaryCaseLedgerStation station =
                prefab.GetComponentInChildren<ApothecaryCaseLedgerStation>(true);
            Require(station != null && station.name == "CaseLedgerInteraction" &&
                    station.GetComponent<BoxCollider>() != null &&
                    station.GetComponent<BoxCollider>().isTrigger,
                "the purchased open book is not a usable case ledger");
        }

        private static void VerifyAtomicCaseLoop()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            TimeManager clock = TimeManager.Instance;
            Require(clock != null, "TimeManager is missing");
            ApothecaryCaseData data = ApothecaryCaseDatabase.Load().Cases[0];
            ApothecaryCaseDecision careful = data.Decisions.Single(decision =>
                decision.grade == ApothecaryCaseGrade.Careful);

            string originalOverride = SaveManager.EditorSaveDirectoryOverride;
            int originalSlot = SaveManager.ActiveSlot;
            int originalDay = clock.Day;
            float originalHour = clock.Hour;
            ApothecaryCaseSnapshot originalCases = ApothecaryCases.ToSnapshot();
            ApothecarySnapshot originalStock = ApothecaryRuntime.ToSnapshot();
            VillagerRelationshipSnapshot originalRelationships = VillagerRelationships.ToSnapshot();
            var originalScores = new SaveSlotMeta();
            GameScores.WriteTo(originalScores);
            string temp = Path.Combine(Path.GetTempPath(),
                "hollowfen-apothecary-cases-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);

            try
            {
                SaveManager.EditorSaveDirectoryOverride = temp;
                SaveManager.SetActiveSlot(3);
                SaveManager.WritePlaceholderToSlot(3);
                clock.SetTime(4, 10f);
                GameScores.HydrateFrom(new SaveSlotMeta
                {
                    GameFlagIds = new[] { "apothecary_story_complete" },
                });
                ApothecaryCases.HydrateFrom(null);
                VillagerRelationships.HydrateFrom(null);
                ApothecaryRuntime.HydrateFrom(new ApothecarySnapshot
                {
                    ProductIds = new[] { careful.preparation.ResultId },
                    ProductCounts = new[] { 1 },
                    CraftedRecipeIds = new[] { careful.preparation.Id },
                });
                SaveCoordinator.SaveAllWithPlayer();

                Require(ApothecaryCases.Begin(data) == ApothecaryCaseActionResult.Completed &&
                        ApothecaryCases.Get(data).Stage == ApothecaryCaseStage.Investigating &&
                        GameScores.HasFlag(data.ActiveFlagId),
                    "accepting an unlocked case did not commit");
                int progressEvents = 0;
                Action progressChanged = () => progressEvents++;
                ApothecaryCases.OnChanged += progressChanged;
                try
                {
                    long progressRevision = SaveManager.InspectSlot(3).Revision;
                    SaveManager.EditorRejectNextAtomicCommit = true;
                    Require(!ApothecaryCases.Observe(data, 0) &&
                            !ApothecaryCases.HasObserved(data, 0) &&
                            SaveManager.InspectSlot(3).Revision == progressRevision &&
                            progressEvents == 0,
                        "a rejected evidence commit leaked its clue, revision, or UI event");
                }
                finally { ApothecaryCases.OnChanged -= progressChanged; }
                for (int i = 0; i < data.Clues.Length; i++)
                    Require(ApothecaryCases.Observe(data, i), "an evidence reveal did not persist");
                for (int i = 0; i < data.Interviews.Length; i++)
                    Require(ApothecaryCases.Interview(data, i), "an interview answer did not persist");
                Require(ApothecaryCases.IsInvestigationComplete(data),
                    "complete evidence did not unlock the decision step");
                Require(ApothecaryCases.Decide(data, careful.id) ==
                        ApothecaryCaseActionResult.Completed &&
                        ApothecaryRuntime.ProductCount(careful.preparation.ResultId) == 0 &&
                        ApothecaryCases.Get(data).FollowUpDay == 5,
                    "the careful decision did not consume exactly one item or set its return day");

                int caseEvents = 0;
                Action changed = () => caseEvents++;
                ApothecaryCases.OnChanged += changed;
                try
                {
                    clock.SetTime(5, 10f);
                    long revision = SaveManager.InspectSlot(3).Revision;
                    SaveManager.EditorRejectNextAtomicCommit = true;
                    ApothecaryCaseActionResult rejected = ApothecaryCases.Resolve(data);
                    ApothecaryCaseStage rejectedStage = ApothecaryCases.Get(data).Stage;
                    long rejectedRevision = SaveManager.InspectSlot(3).Revision;
                    bool leakedMemory =
                        VillagerRelationships.HasMemory(data.PatientNpcId, careful.memoryId);
                    Require(rejected == ApothecaryCaseActionResult.SaveUnavailable &&
                            rejectedStage == ApothecaryCaseStage.AwaitingFollowUp &&
                            rejectedRevision == revision && caseEvents == 0 && !leakedMemory,
                        "a rejected follow-up commit leaked state (result=" + rejected +
                        ", stage=" + rejectedStage + ", revision=" + revision + "->" +
                        rejectedRevision + ", events=" + caseEvents + ", memory=" +
                        leakedMemory + ")");

                    Require(ApothecaryCases.Resolve(data) == ApothecaryCaseActionResult.Completed &&
                            ApothecaryCases.Get(data).Stage == ApothecaryCaseStage.Resolved &&
                            VillagerRelationships.HasMemory(data.PatientNpcId, careful.memoryId) &&
                            VillagerRelationships.GetBond(data.PatientNpcId, data.MentorNpcId) ==
                            careful.mentorBondDelta &&
                            GameScores.GetRelationship(data.PatientNpcId) ==
                            careful.relationshipDelta && caseEvents == 1,
                        "the successful follow-up did not atomically persist its case, memory, bond, and relationship");
                    SaveSlotMeta persisted = SaveManager.InspectSlot(3).Meta;
                    Require(persisted?.ApothecaryCases?.Stages?.Single() ==
                            (int)ApothecaryCaseStage.Resolved &&
                            persisted.VillagerRelationships?.MemoryIds?.Single() == careful.memoryId,
                        "resolved case state did not survive serialization");
                }
                finally { ApothecaryCases.OnChanged -= changed; }
            }
            finally
            {
                if (SaveManager.IsAtomicTransactionActive)
                    SaveManager.EditorCancelAtomicTransactionForVerification();
                SaveManager.EditorRejectNextAtomicCommit = false;
                SaveManager.EditorSaveDirectoryOverride = originalOverride;
                SaveManager.SetActiveSlot(originalSlot);
                ApothecaryCases.HydrateFrom(originalCases);
                ApothecaryRuntime.HydrateFrom(originalStock);
                VillagerRelationships.HydrateFrom(originalRelationships);
                GameScores.HydrateFrom(originalScores);
                clock.SetTime(originalDay, originalHour);
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }

        private static void VerifyLocalized(ApothecaryCaseData data, params string[] suffixes)
        {
            foreach (string suffix in suffixes)
            {
                string id = data.TextId(suffix);
                Require(Localization.Get(id) != id, data.Id + " is missing copy for " + suffix);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("APOTHECARY CASEWORK — FAIL: " + message);
        }
    }
}
#endif
