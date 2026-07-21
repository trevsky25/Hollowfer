using System;
using System.Collections.Generic;
using Hollowfen.GameTime;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.Apothecary
{
    public enum ApothecaryCaseStage
    {
        Unstarted = 0,
        Investigating = 1,
        AwaitingFollowUp = 2,
        Resolved = 3,
    }

    public enum ApothecaryCaseActionResult
    {
        Completed,
        InvalidCase,
        Locked,
        WrongStage,
        InvestigationIncomplete,
        PreparationMissing,
        FollowUpNotDue,
        SaveUnavailable,
    }

    /// <summary>Durable investigation and follow-up state for the apothecary case ledger.</summary>
    public static class ApothecaryCases
    {
        public readonly struct CaseRecord
        {
            public CaseRecord(ApothecaryCaseStage stage, int startedDay, int evidenceMask,
                int interviewMask, string decisionId, int followUpDay, int resolvedDay)
            {
                Stage = stage;
                StartedDay = startedDay;
                EvidenceMask = evidenceMask;
                InterviewMask = interviewMask;
                DecisionId = decisionId ?? string.Empty;
                FollowUpDay = followUpDay;
                ResolvedDay = resolvedDay;
            }

            public ApothecaryCaseStage Stage { get; }
            public int StartedDay { get; }
            public int EvidenceMask { get; }
            public int InterviewMask { get; }
            public string DecisionId { get; }
            public int FollowUpDay { get; }
            public int ResolvedDay { get; }
        }

        private static readonly Dictionary<string, CaseRecord> Records =
            new Dictionary<string, CaseRecord>(StringComparer.Ordinal);
        private static bool _hydrated;

        public static event Action OnChanged;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            Records.Clear();
            _hydrated = false;
            OnChanged = null;
        }

        public static CaseRecord Get(ApothecaryCaseData data)
        {
            EnsureHydrated();
            if (data == null || string.IsNullOrWhiteSpace(data.Id)) return default;
            return Records.TryGetValue(data.Id, out var record) ? record : default;
        }

        public static bool IsUnlocked(ApothecaryCaseData data)
        {
            if (data == null || !data.HasValidStructure) return false;
            if (!string.IsNullOrWhiteSpace(data.RequiredFlagId) &&
                !GameScores.HasFlag(data.RequiredFlagId)) return false;
            if (data.RequiresResolvedCase == null) return true;
            CaseRecord prior = Get(data.RequiresResolvedCase);
            return prior.Stage == ApothecaryCaseStage.Resolved &&
                   CurrentDay >= prior.ResolvedDay + data.UnlockDelayDays;
        }

        public static bool IsInvestigationComplete(ApothecaryCaseData data)
        {
            if (data == null) return false;
            CaseRecord record = Get(data);
            return record.Stage == ApothecaryCaseStage.Investigating &&
                   (record.EvidenceMask & RequiredMask(data.Clues?.Length ?? 0)) ==
                   RequiredMask(data.Clues?.Length ?? 0) &&
                   (record.InterviewMask & RequiredMask(data.Interviews?.Length ?? 0)) ==
                   RequiredMask(data.Interviews?.Length ?? 0);
        }

        public static bool HasObserved(ApothecaryCaseData data, int index) =>
            index >= 0 && index < 30 && (Get(data).EvidenceMask & 1 << index) != 0;

        public static bool HasInterviewed(ApothecaryCaseData data, int index) =>
            index >= 0 && index < 30 && (Get(data).InterviewMask & 1 << index) != 0;

        public static ApothecaryCaseDecision? ChosenDecision(ApothecaryCaseData data)
        {
            if (data == null) return null;
            string id = Get(data).DecisionId;
            if (string.IsNullOrWhiteSpace(id) || data.Decisions == null) return null;
            foreach (var decision in data.Decisions)
                if (string.Equals(decision.id, id, StringComparison.Ordinal)) return decision;
            return null;
        }

        public static ApothecaryCaseActionResult Begin(ApothecaryCaseData data)
        {
            if (data == null || !data.HasValidStructure) return ApothecaryCaseActionResult.InvalidCase;
            if (Get(data).Stage != ApothecaryCaseStage.Unstarted)
                return ApothecaryCaseActionResult.WrongStage;
            if (!IsUnlocked(data)) return ApothecaryCaseActionResult.Locked;

            ApothecaryCaseSnapshot casesBefore = ToSnapshot();
            var scoresBefore = new SaveSlotMeta();
            GameScores.WriteTo(scoresBefore);
            if (!SaveManager.TryBeginAtomicTransaction(out _))
                return ApothecaryCaseActionResult.SaveUnavailable;
            try
            {
                Records[data.Id] = new CaseRecord(ApothecaryCaseStage.Investigating,
                    CurrentDay, 0, 0, string.Empty, 0, 0);
                GameScores.SetFlag(data.ActiveFlagId);
                GameScores.SetFlag("apothecary_casework_started");
                SaveManager.PublishAfterAtomicCommit(PublishChanged);
                SaveCoordinator.SaveAllWithPlayer();
                if (SaveManager.TryCommitAtomicTransaction(out _))
                    return ApothecaryCaseActionResult.Completed;
                Restore(casesBefore, null, scoresBefore, null);
                return ApothecaryCaseActionResult.SaveUnavailable;
            }
            catch (Exception exception)
            {
                Restore(casesBefore, null, scoresBefore, null);
                Debug.LogWarning("[ApothecaryCases] Could not begin case: " + exception.Message);
                return ApothecaryCaseActionResult.SaveUnavailable;
            }
        }

        public static bool Observe(ApothecaryCaseData data, int index)
        {
            if (data == null || index < 0 || index >= (data.Clues?.Length ?? 0) || index >= 30)
                return false;
            CaseRecord record = Get(data);
            if (record.Stage != ApothecaryCaseStage.Investigating) return false;
            int mask = record.EvidenceMask | 1 << index;
            if (mask == record.EvidenceMask) return false;
            return CommitInvestigationProgress(data, new CaseRecord(record.Stage,
                record.StartedDay, mask, record.InterviewMask, record.DecisionId,
                record.FollowUpDay, record.ResolvedDay));
        }

        public static bool Interview(ApothecaryCaseData data, int index)
        {
            if (data == null || index < 0 || index >= (data.Interviews?.Length ?? 0) || index >= 30)
                return false;
            CaseRecord record = Get(data);
            if (record.Stage != ApothecaryCaseStage.Investigating) return false;
            int mask = record.InterviewMask | 1 << index;
            if (mask == record.InterviewMask) return false;
            return CommitInvestigationProgress(data, new CaseRecord(record.Stage,
                record.StartedDay, record.EvidenceMask, mask, record.DecisionId,
                record.FollowUpDay, record.ResolvedDay));
        }

        public static ApothecaryCaseActionResult Decide(ApothecaryCaseData data, string decisionId)
        {
            if (data == null || !data.HasValidStructure) return ApothecaryCaseActionResult.InvalidCase;
            CaseRecord record = Get(data);
            if (record.Stage != ApothecaryCaseStage.Investigating)
                return ApothecaryCaseActionResult.WrongStage;
            if (!IsInvestigationComplete(data))
                return ApothecaryCaseActionResult.InvestigationIncomplete;
            if (!TryDecision(data, decisionId, out var decision))
                return ApothecaryCaseActionResult.InvalidCase;
            string productId = decision.preparation.ResultId;
            if (!ApothecaryRuntime.CanConsumeProducts(new[] { productId }, new[] { 1 }))
                return ApothecaryCaseActionResult.PreparationMissing;

            ApothecaryCaseSnapshot casesBefore = ToSnapshot();
            ApothecarySnapshot stockBefore = ApothecaryRuntime.ToSnapshot();
            var scoresBefore = new SaveSlotMeta();
            GameScores.WriteTo(scoresBefore);
            if (!SaveManager.TryBeginAtomicTransaction(out _))
                return ApothecaryCaseActionResult.SaveUnavailable;
            try
            {
                if (!ApothecaryRuntime.TryConsumeProductsForTransaction(
                        new[] { productId }, new[] { 1 }))
                {
                    Restore(casesBefore, stockBefore, scoresBefore, null);
                    return ApothecaryCaseActionResult.PreparationMissing;
                }
                int due = CurrentDay + Mathf.Max(1, decision.followUpDays);
                Records[data.Id] = new CaseRecord(ApothecaryCaseStage.AwaitingFollowUp,
                    record.StartedDay, record.EvidenceMask, record.InterviewMask,
                    decision.id, due, 0);
                GameScores.SetFlag(data.TreatedFlagId);
                GameScores.SetFlag("apothecary_case_choice_" + data.Id + "_" + decision.id);
                SaveManager.PublishAfterAtomicCommit(PublishChanged);
                SaveCoordinator.SaveAllWithPlayer();
                if (SaveManager.TryCommitAtomicTransaction(out _))
                    return ApothecaryCaseActionResult.Completed;
                Restore(casesBefore, stockBefore, scoresBefore, null);
                return ApothecaryCaseActionResult.SaveUnavailable;
            }
            catch (Exception exception)
            {
                Restore(casesBefore, stockBefore, scoresBefore, null);
                Debug.LogWarning("[ApothecaryCases] Treatment commit failed: " + exception.Message);
                return ApothecaryCaseActionResult.SaveUnavailable;
            }
        }

        public static ApothecaryCaseActionResult Resolve(ApothecaryCaseData data)
        {
            if (data == null) return ApothecaryCaseActionResult.InvalidCase;
            CaseRecord record = Get(data);
            if (record.Stage != ApothecaryCaseStage.AwaitingFollowUp)
                return ApothecaryCaseActionResult.WrongStage;
            if (CurrentDay < record.FollowUpDay) return ApothecaryCaseActionResult.FollowUpNotDue;
            if (!TryDecision(data, record.DecisionId, out var decision))
                return ApothecaryCaseActionResult.InvalidCase;

            ApothecaryCaseSnapshot casesBefore = ToSnapshot();
            var scoresBefore = new SaveSlotMeta();
            GameScores.WriteTo(scoresBefore);
            VillagerRelationshipSnapshot relationshipsBefore = VillagerRelationships.ToSnapshot();
            if (!SaveManager.TryBeginAtomicTransaction(out _))
                return ApothecaryCaseActionResult.SaveUnavailable;
            try
            {
                Records[data.Id] = new CaseRecord(ApothecaryCaseStage.Resolved,
                    record.StartedDay, record.EvidenceMask, record.InterviewMask,
                    record.DecisionId, record.FollowUpDay, CurrentDay);
                GameScores.SetFlag(data.ResolvedFlagId);
                GameScores.SetFlag("apothecary_case_grade_" + data.Id + "_" +
                                   decision.grade.ToString().ToLowerInvariant());
                GameScores.AddVillageHope(decision.villageHope);
                GameScores.AddKnowledge(decision.knowledge);
                GameScores.AddRelationship(data.PatientNpcId, decision.relationshipDelta);
                VillagerRelationships.Remember(data.PatientNpcId, decision.memoryId, CurrentDay);
                VillagerRelationships.AddBond(data.PatientNpcId, data.MentorNpcId,
                    decision.mentorBondDelta);
                if (AllAuthoredCasesResolved() &&
                    GameScores.SetFlag("apothecary_casework_complete"))
                {
                    GameScores.SetFlag("apothecary_care_record_trusted");
                    GameScores.AddVillageHope(2);
                }
                SaveManager.PublishAfterAtomicCommit(PublishChanged);
                SaveCoordinator.SaveAllWithPlayer();
                if (SaveManager.TryCommitAtomicTransaction(out _))
                    return ApothecaryCaseActionResult.Completed;
                Restore(casesBefore, null, scoresBefore, relationshipsBefore);
                return ApothecaryCaseActionResult.SaveUnavailable;
            }
            catch (Exception exception)
            {
                Restore(casesBefore, null, scoresBefore, relationshipsBefore);
                Debug.LogWarning("[ApothecaryCases] Follow-up commit failed: " + exception.Message);
                return ApothecaryCaseActionResult.SaveUnavailable;
            }
        }

        public static void HydrateFrom(ApothecaryCaseSnapshot snapshot)
        {
            Records.Clear();
            if (snapshot != null)
            {
                int count = SharedLength(snapshot.Ids, snapshot.Stages, snapshot.StartedDays,
                    snapshot.EvidenceMasks, snapshot.InterviewMasks, snapshot.DecisionIds,
                    snapshot.FollowUpDays, snapshot.ResolvedDays);
                for (int i = 0; i < count; i++)
                {
                    string id = snapshot.Ids[i]?.Trim();
                    if (string.IsNullOrEmpty(id)) continue;
                    var stage = (ApothecaryCaseStage)Mathf.Clamp(snapshot.Stages[i], 0, 3);
                    Records[id] = new CaseRecord(stage, Mathf.Max(0, snapshot.StartedDays[i]),
                        Mathf.Max(0, snapshot.EvidenceMasks[i]), Mathf.Max(0, snapshot.InterviewMasks[i]),
                        snapshot.DecisionIds[i], Mathf.Max(0, snapshot.FollowUpDays[i]),
                        Mathf.Max(0, snapshot.ResolvedDays[i]));
                }
            }
            _hydrated = true;
            PublishChanged();
        }

        public static ApothecaryCaseSnapshot ToSnapshot()
        {
            EnsureHydrated();
            var snapshot = new ApothecaryCaseSnapshot
            {
                Ids = new string[Records.Count],
                Stages = new int[Records.Count],
                StartedDays = new int[Records.Count],
                EvidenceMasks = new int[Records.Count],
                InterviewMasks = new int[Records.Count],
                DecisionIds = new string[Records.Count],
                FollowUpDays = new int[Records.Count],
                ResolvedDays = new int[Records.Count],
            };
            int index = 0;
            foreach (var pair in Records)
            {
                snapshot.Ids[index] = pair.Key;
                snapshot.Stages[index] = (int)pair.Value.Stage;
                snapshot.StartedDays[index] = pair.Value.StartedDay;
                snapshot.EvidenceMasks[index] = pair.Value.EvidenceMask;
                snapshot.InterviewMasks[index] = pair.Value.InterviewMask;
                snapshot.DecisionIds[index] = pair.Value.DecisionId;
                snapshot.FollowUpDays[index] = pair.Value.FollowUpDay;
                snapshot.ResolvedDays[index] = pair.Value.ResolvedDay;
                index++;
            }
            return snapshot;
        }

        private static int CurrentDay => TimeManager.Instance != null
            ? Mathf.Max(1, TimeManager.Instance.Day)
            : Mathf.Max(1, SaveManager.GetSlotMeta(SaveManager.ActiveSlot)?.GameDay ?? 1);

        private static int RequiredMask(int count) => count <= 0 ? 0 : (1 << Mathf.Min(30, count)) - 1;

        private static bool TryDecision(ApothecaryCaseData data, string id,
            out ApothecaryCaseDecision decision)
        {
            if (data != null && data.Decisions != null)
                foreach (var candidate in data.Decisions)
                    if (string.Equals(candidate.id, id, StringComparison.Ordinal))
                    {
                        decision = candidate;
                        return true;
                    }
            decision = default;
            return false;
        }

        private static bool AllAuthoredCasesResolved()
        {
            ApothecaryCaseDatabase database = ApothecaryCaseDatabase.Load();
            if (database == null || database.Cases.Count == 0) return false;
            foreach (ApothecaryCaseData entry in database.Cases)
                if (entry == null || Get(entry).Stage != ApothecaryCaseStage.Resolved) return false;
            return true;
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try { HydrateFrom(SaveManager.GetSlotMeta(SaveManager.ActiveSlot)?.ApothecaryCases); }
            catch (Exception exception)
            {
                Debug.LogWarning("[ApothecaryCases] Hydration failed: " + exception.Message);
            }
        }

        private static bool CommitInvestigationProgress(ApothecaryCaseData data,
            CaseRecord updated)
        {
            ApothecaryCaseSnapshot casesBefore = ToSnapshot();
            if (!SaveManager.TryBeginAtomicTransaction(out _)) return false;
            try
            {
                Records[data.Id] = updated;
                SaveManager.PublishAfterAtomicCommit(PublishChanged);
                SaveCoordinator.SaveAllWithPlayer();
                if (SaveManager.TryCommitAtomicTransaction(out _)) return true;
                Restore(casesBefore, null, null, null);
                return false;
            }
            catch (Exception exception)
            {
                Restore(casesBefore, null, null, null);
                Debug.LogWarning("[ApothecaryCases] Investigation save failed: " +
                                 exception.Message);
                return false;
            }
        }

        private static void Restore(ApothecaryCaseSnapshot cases, ApothecarySnapshot stock,
            SaveSlotMeta scores, VillagerRelationshipSnapshot relationships)
        {
            HydrateFrom(cases);
            if (stock != null) ApothecaryRuntime.HydrateFrom(stock);
            if (scores != null) GameScores.HydrateFrom(scores);
            if (relationships != null) VillagerRelationships.HydrateFrom(relationships);
            SaveManager.CancelAtomicTransaction();
        }

        private static void PublishChanged()
        {
            var handlers = OnChanged;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                Action callback = handler;
                SaveManager.PublishAfterAtomicCommit(callback);
            }
        }

        private static int SharedLength(params Array[] arrays)
        {
            int count = int.MaxValue;
            foreach (Array array in arrays) count = Mathf.Min(count, array?.Length ?? 0);
            return Mathf.Max(0, count == int.MaxValue ? 0 : count);
        }
    }
}
