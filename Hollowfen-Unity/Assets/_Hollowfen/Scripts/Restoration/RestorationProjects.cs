using System;
using System.Collections.Generic;
using Hollowfen.GameTime;
using Hollowfen.Items;
using Hollowfen.Quests;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.Restoration
{
    /// <summary>
    /// Save-backed restoration progression. Explicit project state is monotonic, while authored
    /// quest/flag rules migrate legacy saves upward and keep story-owned outcomes authoritative.
    /// </summary>
    public static class RestorationProjects
    {
        public enum ContributionResult
        {
            Funded,
            AlreadyFunded,
            NotAvailable,
            InsufficientCoins,
            SaveUnavailable,
            Invalid,
        }

        private sealed class Record
        {
            public RestorationStage Stage;
            public int StartedDay;
            public int ChangedDay;
        }

        private static readonly Dictionary<string, Record> Records =
            new Dictionary<string, Record>(StringComparer.Ordinal);
        private static RestorationProjectDatabase _database;
        private static bool _hydrated;
        private static bool _bindingsReady;
        private static bool _syncing;
        private static bool _hydrating;

        public static event Action<string, RestorationStage> OnStageChanged;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            Records.Clear();
            _database = null;
            _hydrated = false;
            _bindingsReady = false;
            _syncing = false;
            _hydrating = false;
            OnStageChanged = null;
        }

        public static RestorationProjectData Resolve(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId)) return null;
            foreach (var project in Projects())
                if (project != null && string.Equals(project.Id, projectId, StringComparison.Ordinal)) return project;
            return null;
        }

        public static IReadOnlyList<RestorationProjectData> AllProjects
        {
            get
            {
                EnsureHydrated();
                return Projects();
            }
        }

        public static RestorationStage GetStage(RestorationProjectData project)
        {
            if (project == null) return RestorationStage.Unavailable;
            EnsureHydrated();
            EnsureBindings();
            Reconcile(project, true);
            return Records.TryGetValue(project.Id, out var record)
                ? record.Stage
                : RestorationStage.Unavailable;
        }

        public static int StartedDay(RestorationProjectData project)
        {
            GetStage(project);
            return project != null && Records.TryGetValue(project.Id, out var record) ? record.StartedDay : 0;
        }

        public static int ChangedDay(RestorationProjectData project)
        {
            GetStage(project);
            return project != null && Records.TryGetValue(project.Id, out var record) ? record.ChangedDay : 0;
        }

        public static bool IsMilestoneComplete(RestorationMilestone milestone)
        {
            EnsureHydrated();
            switch (milestone.Condition)
            {
                case RestorationCondition.ActiveQuest:
                    return QuestManager.IsActive(milestone.ValueId);
                case RestorationCondition.CompletedQuest:
                    return QuestManager.IsCompleted(milestone.ValueId);
                case RestorationCondition.FlagSet:
                    return GameScores.HasFlag(milestone.ValueId);
                case RestorationCondition.ProjectStage:
                    var project = Resolve(milestone.ValueId);
                    return project != null && GetStage(project) >= milestone.RequiredStage;
                default:
                    return false;
            }
        }

        /// <summary>Future projects use this after an atomic contribution or authored work beat.</summary>
        public static bool Advance(RestorationProjectData project, RestorationStage nextStage)
        {
            if (project == null || string.IsNullOrWhiteSpace(project.Id)) return false;
            EnsureHydrated();
            EnsureBindings();
            return SetStage(project.Id, nextStage, true);
        }

        /// <summary>
        /// Pays for one authored supply line and commits coins, flags, and restoration state as
        /// one verified journal snapshot. A failed disk commit restores every runtime store.
        /// </summary>
        public static ContributionResult Contribute(RestorationProjectData project,
            RestorationContribution contribution)
        {
            if (project == null || string.IsNullOrWhiteSpace(contribution.FundedFlagId) ||
                contribution.CostCopper <= 0) return ContributionResult.Invalid;
            if (GameScores.HasFlag(contribution.FundedFlagId)) return ContributionResult.AlreadyFunded;
            if (GetStage(project) < contribution.AvailableFromStage)
                return ContributionResult.NotAvailable;
            if (CoinPurse.TotalCopper < contribution.CostCopper)
                return ContributionResult.InsufficientCoins;

            int rollbackCopper = CoinPurse.TotalCopper;
            var rollbackLedger = CoinPurse.ToLedgerSnapshot();
            var rollbackScores = new SaveSlotMeta();
            GameScores.WriteTo(rollbackScores);
            var rollbackRestoration = ToSnapshot();

            if (!SaveManager.TryBeginAtomicTransaction(out _))
                return ContributionResult.SaveUnavailable;

            try
            {
                if (!CoinPurse.TrySpend(contribution.CostCopper, "purse.transaction.restoration"))
                {
                    SaveManager.CancelAtomicTransaction();
                    return ContributionResult.InsufficientCoins;
                }

                GameScores.SetFlag(contribution.FundedFlagId);
                if (AllContributionsFunded(project) &&
                    !string.IsNullOrWhiteSpace(project.ContributionsCompleteFlagId))
                    GameScores.SetFlag(project.ContributionsCompleteFlagId);

                Reconcile(project, true);
                SaveCoordinator.SaveAllWithPlayer();
                if (!SaveManager.TryCommitAtomicTransaction(out _))
                {
                    RestoreContributionState(rollbackCopper, rollbackLedger,
                        rollbackScores, rollbackRestoration);
                    return ContributionResult.SaveUnavailable;
                }
                return ContributionResult.Funded;
            }
            catch (Exception exception)
            {
                if (SaveManager.IsAtomicTransactionActive)
                    RestoreContributionState(rollbackCopper, rollbackLedger,
                        rollbackScores, rollbackRestoration);
                Debug.LogWarning("[RestorationProjects] Contribution failed: " + exception.Message);
                return ContributionResult.SaveUnavailable;
            }
        }

        public static bool IsContributionFunded(RestorationContribution contribution) =>
            !string.IsNullOrWhiteSpace(contribution.FundedFlagId) &&
            GameScores.HasFlag(contribution.FundedFlagId);

        public static bool AllContributionsFunded(RestorationProjectData project)
        {
            if (project == null || project.Contributions == null || project.Contributions.Length == 0)
                return false;
            foreach (var contribution in project.Contributions)
                if (!IsContributionFunded(contribution)) return false;
            return true;
        }

        /// <summary>
        /// Commits a restored place's first real use, permanent benefit flags, score rewards,
        /// and Occupied stage as one journal revision. Trigger re-entry is idempotent.
        /// </summary>
        public static bool CompleteFirstUse(RestorationProjectData project, string completionFlagId,
            IEnumerable<string> consequenceFlagIds, RestorationStage requiredStage,
            RestorationStage completedStage)
        {
            if (project == null || string.IsNullOrWhiteSpace(completionFlagId) ||
                GetStage(project) < requiredStage || GameScores.HasFlag(completionFlagId)) return false;

            var rollbackScores = new SaveSlotMeta();
            GameScores.WriteTo(rollbackScores);
            var rollbackRestoration = ToSnapshot();
            if (!SaveManager.TryBeginAtomicTransaction(out _)) return false;

            try
            {
                GameScores.SetFlag(completionFlagId);
                if (consequenceFlagIds != null)
                    foreach (string flag in consequenceFlagIds)
                        if (!string.IsNullOrWhiteSpace(flag)) GameScores.SetFlag(flag);
                if (project.VillageHopeReward > 0)
                    GameScores.AddVillageHope(project.VillageHopeReward);
                if (project.KnowledgeReward > 0)
                    GameScores.AddKnowledge(project.KnowledgeReward);

                Advance(project, completedStage);
                SaveCoordinator.SaveAllWithPlayer();
                if (SaveManager.TryCommitAtomicTransaction(out _)) return true;

                RestoreFirstUseState(rollbackScores, rollbackRestoration);
                return false;
            }
            catch (Exception exception)
            {
                if (SaveManager.IsAtomicTransactionActive)
                    RestoreFirstUseState(rollbackScores, rollbackRestoration);
                Debug.LogWarning("[RestorationProjects] First use failed: " + exception.Message);
                return false;
            }
        }

        public static void HydrateFrom(RestorationSnapshot snapshot)
        {
            _hydrating = true;
            try
            {
                Records.Clear();
                if (snapshot != null)
                {
                    int count = Mathf.Min(snapshot.ProjectIds?.Length ?? 0,
                        Mathf.Min(snapshot.Stages?.Length ?? 0,
                            Mathf.Min(snapshot.StartedDays?.Length ?? 0, snapshot.ChangedDays?.Length ?? 0)));
                    for (int i = 0; i < count; i++)
                    {
                        string id = snapshot.ProjectIds[i];
                        if (string.IsNullOrWhiteSpace(id)) continue;
                        Records[id] = new Record
                        {
                            Stage = (RestorationStage)Mathf.Clamp(snapshot.Stages[i],
                                (int)RestorationStage.Unavailable, (int)RestorationStage.Occupied),
                            StartedDay = Mathf.Max(0, snapshot.StartedDays[i]),
                            ChangedDay = Mathf.Max(0, snapshot.ChangedDays[i]),
                        };
                    }
                }
                _hydrated = true;
                EnsureBindings();
                foreach (var project in Projects())
                    if (project != null) Reconcile(project, false);
            }
            finally
            {
                _hydrating = false;
            }
            NotifyHydratedStages();
        }

        public static RestorationSnapshot ToSnapshot()
        {
            EnsureHydrated();
            var ids = new List<string>(Records.Keys);
            ids.Sort(StringComparer.Ordinal);
            var snapshot = new RestorationSnapshot
            {
                ProjectIds = ids.ToArray(),
                Stages = new int[ids.Count],
                StartedDays = new int[ids.Count],
                ChangedDays = new int[ids.Count],
            };
            for (int i = 0; i < ids.Count; i++)
            {
                var record = Records[ids[i]];
                snapshot.Stages[i] = (int)record.Stage;
                snapshot.StartedDays[i] = record.StartedDay;
                snapshot.ChangedDays[i] = record.ChangedDay;
            }
            return snapshot;
        }

        public static void ReconcileAll()
        {
            if (_syncing) return;
            EnsureHydrated();
            _syncing = true;
            try
            {
                foreach (var project in Projects())
                    if (project != null) Reconcile(project, true);
            }
            finally
            {
                _syncing = false;
            }
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try
            {
                HydrateFrom(SaveManager.GetSlotMeta(SaveManager.ActiveSlot)?.RestorationProjects);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[RestorationProjects] Hydration failed: " + exception.Message);
            }
        }

        private static void EnsureBindings()
        {
            if (_bindingsReady) return;
            _bindingsReady = true;
            GameScores.OnChanged -= ReconcileAll;
            GameScores.OnChanged += ReconcileAll;
            QuestManager.QuestStarted -= HandleQuestChanged;
            QuestManager.QuestStarted += HandleQuestChanged;
            QuestManager.QuestCompleted -= HandleQuestChanged;
            QuestManager.QuestCompleted += HandleQuestChanged;
            TimeManager.OnDayChanged -= HandleDayChanged;
            TimeManager.OnDayChanged += HandleDayChanged;
        }

        private static void HandleQuestChanged(QuestData _) => ReconcileAll();
        private static void HandleDayChanged(int _) => ReconcileAll();

        private static void Reconcile(RestorationProjectData project, bool persist)
        {
            if (project == null || string.IsNullOrWhiteSpace(project.Id)) return;
            RestorationStage derived = RestorationStage.Unavailable;
            if (project.StageRules != null)
            {
                foreach (var rule in project.StageRules)
                {
                    bool satisfied;
                    switch (rule.Condition)
                    {
                        case RestorationCondition.ActiveQuest:
                            satisfied = QuestManager.IsActive(rule.ValueId);
                            break;
                        case RestorationCondition.CompletedQuest:
                            satisfied = QuestManager.IsCompleted(rule.ValueId);
                            break;
                        case RestorationCondition.FlagSet:
                            satisfied = GameScores.HasFlag(rule.ValueId);
                            break;
                        case RestorationCondition.ProjectStage:
                            satisfied = false;
                            break;
                        default:
                            satisfied = false;
                            break;
                    }
                    if (satisfied && rule.Stage > derived) derived = rule.Stage;
                }
            }
            SetStage(project.Id, derived, persist);
        }

        private static bool SetStage(string projectId, RestorationStage stage, bool persist)
        {
            if (!Records.TryGetValue(projectId, out var record))
            {
                if (stage == RestorationStage.Unavailable) return false;
                record = new Record();
                Records.Add(projectId, record);
            }
            if (stage <= record.Stage) return false;

            int day = CurrentDay();
            record.Stage = stage;
            if (record.StartedDay <= 0) record.StartedDay = day;
            record.ChangedDay = day;
            if (persist) Persist();
            if (!_hydrating)
            {
                var handlers = OnStageChanged;
                if (handlers != null)
                    foreach (Action<string, RestorationStage> handler in handlers.GetInvocationList())
                    {
                        var callback = handler;
                        SaveManager.PublishAfterAtomicCommit(() => callback(projectId, stage));
                    }
            }
            return true;
        }

        private static void RestoreContributionState(int copper, CoinLedgerSnapshot ledger,
            SaveSlotMeta scores, RestorationSnapshot restoration)
        {
            try
            {
                CoinPurse.HydrateFrom(copper, ledger);
                GameScores.HydrateFrom(scores);
                HydrateFrom(restoration);
            }
            finally
            {
                SaveManager.CancelAtomicTransaction();
            }
        }

        private static void RestoreFirstUseState(SaveSlotMeta scores, RestorationSnapshot restoration)
        {
            try
            {
                GameScores.HydrateFrom(scores);
                HydrateFrom(restoration);
            }
            finally
            {
                SaveManager.CancelAtomicTransaction();
            }
        }

        private static RestorationProjectData[] Projects()
        {
            if (_database == null) _database = Resources.Load<RestorationProjectDatabase>("RestorationProjectDatabase");
            return _database != null && _database.Projects != null
                ? _database.Projects
                : Array.Empty<RestorationProjectData>();
        }

        private static void NotifyHydratedStages()
        {
            foreach (var project in Projects())
            {
                if (project == null || string.IsNullOrWhiteSpace(project.Id)) continue;
                var stage = Records.TryGetValue(project.Id, out var record)
                    ? record.Stage
                    : RestorationStage.Unavailable;
                var handlers = OnStageChanged;
                if (handlers == null) continue;
                foreach (Action<string, RestorationStage> handler in handlers.GetInvocationList())
                {
                    var callback = handler;
                    string projectId = project.Id;
                    SaveManager.PublishAfterAtomicCommit(() => callback(projectId, stage));
                }
            }
        }

        private static int CurrentDay()
        {
            if (TimeManager.Instance != null) return TimeManager.Instance.Day;
            var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
            return meta != null && meta.GameDay > 0 ? meta.GameDay : 1;
        }

        private static void Persist()
        {
            try { SaveManager.AutoSaveRestorationProjects(ToSnapshot()); }
            catch (Exception exception) { Debug.LogWarning("[RestorationProjects] Autosave failed: " + exception.Message); }
        }
    }
}
