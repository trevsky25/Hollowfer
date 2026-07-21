using System;
using System.Collections.Generic;
using System.Linq;
using Hollowfen.Apothecary;
using Hollowfen.Foraging;
using Hollowfen.GameTime;
using Hollowfen.Items;
using Hollowfen.Map;
using Hollowfen.Quests;
using Hollowfen.Restoration;
using Hollowfen.Save;
using Hollowfen.Weather;
using UnityEngine;

namespace Hollowfen.Requests
{
    /// <summary>
    /// Save-backed village request scheduler. Story requests persist until delivered; ordinary
    /// NPC orders rotate deterministically at dawn and may be claimed once per NPC per game day.
    /// </summary>
    public static class VillageRequests
    {
        public readonly struct CompletionResult
        {
            public CompletionResult(bool success, int copper, bool firstCompletion, string failure)
            {
                Success = success;
                Copper = copper;
                FirstCompletion = firstCompletion;
                Failure = failure;
            }

            public bool Success { get; }
            public int Copper { get; }
            public bool FirstCompletion { get; }
            public string Failure { get; }
        }

        private sealed class DailyCompletion
        {
            public string RequestId;
            public int Day;
        }

        private sealed class DeliveryRollbackState
        {
            internal InventorySnapshot Inventory;
            internal ApothecarySnapshot Apothecary;
            internal int Copper;
            internal CoinLedgerSnapshot CoinLedger;
            internal SaveSlotMeta Scores;
            internal VillageRequestSnapshot Requests;
            internal string[] CompletedQuestIds;
            internal string[] StoryCardIds;
            internal QuestData ActiveQuest;
            internal LocationMarker Waypoint;
        }

        private static readonly HashSet<string> CompletedOneShots = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, DailyCompletion> DailyByNpc =
            new Dictionary<string, DailyCompletion>(StringComparer.Ordinal);
        private static VillageRequestDatabase _database;
        private static bool _hydrated;
        private static string _trackedRequestId;
        private static int _trackedDay;

        public static event Action OnChanged;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            CompletedOneShots.Clear();
            DailyByNpc.Clear();
            _database = null;
            _hydrated = false;
            _trackedRequestId = null;
            _trackedDay = 0;
            OnChanged = null;
            TimeManager.OnDayChanged -= HandleDayChanged;
            TimeManager.OnDayChanged += HandleDayChanged;
        }

        public static VillageRequestData CurrentForNpc(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId)) return null;
            EnsureHydrated();
            var requests = DatabaseRequests();
            int day = CurrentDay();

            // Authored story deliveries always outrank rotating work and never expire at dawn.
            foreach (var request in requests)
            {
                if (request == null || !request.OneShot || request.NpcId != npcId) continue;
                if (CompletedOneShots.Contains(request.Id) || !IsEligible(request)) continue;
                return request;
            }

            // One ordinary delivery per NPC per day, even if another story outcome changes
            // the eligible request pool after the basket was claimed.
            if (DailyByNpc.TryGetValue(npcId, out var claimed) && claimed.Day == day)
                return null;

            var eligible = new List<VillageRequestData>();
            foreach (var request in requests)
            {
                if (request == null || request.OneShot || request.NpcId != npcId || !IsEligible(request)) continue;
                eligible.Add(request);
            }
            eligible.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            if (eligible.Count == 0) return null;

            int index = PositiveModulo(day - 1 + StableHash(npcId), eligible.Count);
            return eligible[index];
        }

        public static VillageRequestData TrackedRequest
        {
            get
            {
                EnsureHydrated();
                var request = Resolve(_trackedRequestId);
                if (request == null) return null;
                if (!request.OneShot && _trackedDay != CurrentDay()) return null;
                var current = CurrentForNpc(request.NpcId);
                return current != null && current.Id == request.Id ? request : null;
            }
        }

        public static void Track(VillageRequestData request)
        {
            if (request == null) return;
            EnsureHydrated();
            int day = request.OneShot ? 0 : CurrentDay();
            if (_trackedRequestId == request.Id && _trackedDay == day) return;
            _trackedRequestId = request.Id;
            _trackedDay = day;
            Persist();
            PublishChanged();
        }

        public static bool CanDeliver(VillageRequestData request)
        {
            if (request == null || request.TotalRequirementCount <= 0) return false;
            for (int i = 0; i < request.RequirementCount; i++)
            {
                var species = request.RequiredSpecies[i];
                if (species == null || InventoryRuntime.GetCount(species) < request.RequiredCountAt(i)) return false;
            }
            for (int i = 0; i < request.PreparationRequirementCount; i++)
            {
                var recipe = request.RequiredPreparations[i];
                if (recipe == null || ApothecaryRuntime.ProductCount(recipe.ResultId) <
                    request.RequiredPreparationCountAt(i)) return false;
            }
            return true;
        }

        public static string RequirementProgress(VillageRequestData request)
        {
            if (request == null) return string.Empty;
            var parts = new List<string>();
            for (int i = 0; i < request.RequirementCount; i++)
            {
                var species = request.RequiredSpecies[i];
                if (species == null) continue;
                int need = request.RequiredCountAt(i);
                parts.Add(Hollowfen.UI.JournalText.MushroomName(species) + " " +
                    Mathf.Min(InventoryRuntime.GetCount(species), need) + "/" + need);
            }
            for (int i = 0; i < request.PreparationRequirementCount; i++)
            {
                var recipe = request.RequiredPreparations[i];
                if (recipe == null) continue;
                int need = request.RequiredPreparationCountAt(i);
                parts.Add(Localization.Get(recipe.ResultNameId) + " " +
                    Mathf.Min(ApothecaryRuntime.ProductCount(recipe.ResultId), need) + "/" + need);
            }
            return string.Join(Localization.Get("format.inline_separator"), parts);
        }

        public static int RewardFor(VillageRequestData request)
        {
            if (request == null) return 0;
            int reward = request.RewardCopper;
            if (request.OneShot) return reward;
            reward += RestorationBenefits.DailyRequestBonusCopper;
            if (request.WetWeatherBonusCopper > 0 && WeatherSystem.IsWetAtCurrentTime())
                reward += request.WetWeatherBonusCopper;
            return reward;
        }

        public static CompletionResult Complete(VillageRequestData request)
        {
            EnsureHydrated();
            if (request == null) return new CompletionResult(false, 0, false, "Missing request data.");
            var current = CurrentForNpc(request.NpcId);
            if (current == null || current.Id != request.Id)
                return new CompletionResult(false, 0, false, "That request is no longer active.");
            if (!CanDeliver(request))
                return new CompletionResult(false, 0, false, "The delivery is still missing an item.");

            var rollback = CaptureDeliveryState();
            if (!SaveManager.TryBeginAtomicTransaction(out _))
                return new CompletionResult(false, 0, false,
                    "Delivery could not be saved. Nothing was consumed.");

            bool first = false;
            try
            {
                // Batch removal publishes only to staged runtime state while the transaction is
                // active. All targeted autosaves and outward events remain suppressed until the
                // final full-slot snapshot is the verified higher-revision recovery winner.
                if (request.RequirementCount > 0 &&
                    !InventoryRuntime.TryRemoveBatch(request.RequiredSpecies, request.RequiredCounts,
                        out var removalFailure))
                {
                    SaveManager.CancelAtomicTransaction();
                    switch (removalFailure)
                    {
                        case InventoryRuntime.BatchRemovalFailure.InvalidBatch:
                            return new CompletionResult(false, 0, false,
                                "That request has invalid ingredient data.");
                        case InventoryRuntime.BatchRemovalFailure.PersistenceUnavailable:
                            return new CompletionResult(false, 0, false,
                                "Delivery could not be saved. Nothing was consumed.");
                        default:
                            return new CompletionResult(false, 0, false,
                                "The basket changed before delivery.");
                    }
                }

                if (request.PreparationRequirementCount > 0 &&
                    !ApothecaryRuntime.TryConsumeProductsForTransaction(
                        RequiredProductIds(request), request.RequiredPreparationCounts))
                {
                    RestoreDeliveryStateAndCancel(rollback);
                    return new CompletionResult(false, 0, false,
                        "The workshop shelf changed before delivery.");
                }

                int day = CurrentDay();
                if (request.OneShot) CompletedOneShots.Add(request.Id);
                // A story gathering also occupies that NPC's delivery rhythm for the day; Marra
                // should not ask for another shopping list while the festival bowls are still out.
                DailyByNpc[request.NpcId] = new DailyCompletion { RequestId = request.Id, Day = day };

                first = !GameScores.HasFlag(FirstCompletionFlag(request.Id));
                if (first)
                {
                    GameScores.SetFlag(FirstCompletionFlag(request.Id));
                    if (request.FirstCompletionRelationshipDelta != 0)
                        GameScores.AddRelationship(request.NpcId, request.FirstCompletionRelationshipDelta);
                    if (request.FirstCompletionKnowledgeDelta != 0)
                        GameScores.AddKnowledge(request.FirstCompletionKnowledgeDelta);
                }

                int rewardCopper = RewardFor(request);
                if (rewardCopper > 0)
                    CoinPurse.Add(rewardCopper, "purse.transaction.delivery");
                if (request.CompletionFlagIds != null)
                    foreach (var flag in request.CompletionFlagIds)
                        if (!string.IsNullOrWhiteSpace(flag)) GameScores.SetFlag(flag);

                _trackedRequestId = null;
                _trackedDay = 0;
                Persist();

                // Quest state and score deltas are staged before the disk commit. Quest/card/UI
                // events and the Steam-style achievement fan-out remain queued until it succeeds.
                if (request.CompleteQuest != null && QuestManager.IsActive(request.CompleteQuest.Id))
                    QuestManager.CompleteQuest(request.CompleteQuest.Id);

                SaveCoordinator.SaveAllWithPlayer();
                if (!SaveManager.TryCommitAtomicTransaction(out _))
                {
                    RestoreDeliveryStateAndCancel(rollback);
                    return new CompletionResult(false, 0, false,
                        "Delivery could not be saved. Nothing was consumed.");
                }

                PublishChanged();
                return new CompletionResult(true, rewardCopper, first, null);
            }
            catch (Exception exception)
            {
                if (SaveManager.IsAtomicTransactionActive) RestoreDeliveryStateAndCancel(rollback);
                Debug.LogWarning("[VillageRequests] Delivery transaction failed: " + exception.Message);
                return new CompletionResult(false, 0, false,
                    "Delivery could not be saved. Nothing was consumed.");
            }
        }

        public static VillageRequestData Resolve(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            foreach (var request in DatabaseRequests())
                if (request != null && request.Id == id) return request;
            return null;
        }

        public static void HydrateFrom(VillageRequestSnapshot snapshot)
        {
            CompletedOneShots.Clear();
            DailyByNpc.Clear();
            if (snapshot != null)
            {
                if (snapshot.CompletedOneShotIds != null)
                    foreach (var id in snapshot.CompletedOneShotIds)
                        if (!string.IsNullOrWhiteSpace(id)) CompletedOneShots.Add(id);

                int count = Mathf.Min(snapshot.DailyNpcIds?.Length ?? 0,
                    Mathf.Min(snapshot.DailyRequestIds?.Length ?? 0, snapshot.DailyDays?.Length ?? 0));
                for (int i = 0; i < count; i++)
                {
                    if (string.IsNullOrWhiteSpace(snapshot.DailyNpcIds[i]) ||
                        string.IsNullOrWhiteSpace(snapshot.DailyRequestIds[i])) continue;
                    DailyByNpc[snapshot.DailyNpcIds[i]] = new DailyCompletion
                    {
                        RequestId = snapshot.DailyRequestIds[i],
                        Day = Mathf.Max(1, snapshot.DailyDays[i]),
                    };
                }
                _trackedRequestId = snapshot.TrackedRequestId;
                _trackedDay = snapshot.TrackedDay;
            }
            else
            {
                _trackedRequestId = null;
                _trackedDay = 0;
            }
            _hydrated = true;
            EnsureDaySubscription();
            PublishChanged();
        }

        public static VillageRequestSnapshot ToSnapshot()
        {
            EnsureHydrated();
            var oneShots = new List<string>(CompletedOneShots);
            oneShots.Sort(StringComparer.Ordinal);
            var npcIds = new List<string>(DailyByNpc.Keys);
            npcIds.Sort(StringComparer.Ordinal);
            var snapshot = new VillageRequestSnapshot
            {
                CompletedOneShotIds = oneShots.ToArray(),
                DailyNpcIds = npcIds.ToArray(),
                DailyRequestIds = new string[npcIds.Count],
                DailyDays = new int[npcIds.Count],
                TrackedRequestId = _trackedRequestId,
                TrackedDay = _trackedDay,
            };
            for (int i = 0; i < npcIds.Count; i++)
            {
                var completion = DailyByNpc[npcIds[i]];
                snapshot.DailyRequestIds[i] = completion.RequestId;
                snapshot.DailyDays[i] = completion.Day;
            }
            return snapshot;
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            EnsureDaySubscription();
            try
            {
                HydrateFrom(SaveManager.GetSlotMeta(SaveManager.ActiveSlot)?.VillageRequests);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VillageRequests] Hydration failed: " + e.Message);
            }
        }

        private static void EnsureDaySubscription()
        {
            TimeManager.OnDayChanged -= HandleDayChanged;
            TimeManager.OnDayChanged += HandleDayChanged;
        }

        private static void HandleDayChanged(int day)
        {
            if (!string.IsNullOrEmpty(_trackedRequestId) && _trackedDay > 0 && _trackedDay != day)
            {
                _trackedRequestId = null;
                _trackedDay = 0;
                Persist();
            }
            PublishChanged();
        }

        private static bool IsEligible(VillageRequestData request)
        {
            if (request == null || request.TotalRequirementCount <= 0 ||
                request.TotalRequirementCount > 4) return false;
            if (!string.IsNullOrWhiteSpace(request.ActiveQuestId) && !QuestManager.IsActive(request.ActiveQuestId))
                return false;
            if (request.RequiredFlagIds != null)
                foreach (var flag in request.RequiredFlagIds)
                    if (!string.IsNullOrWhiteSpace(flag) && !GameScores.HasFlag(flag)) return false;
            if (request.RequiredCompletedQuestIds != null)
                foreach (var quest in request.RequiredCompletedQuestIds)
                    if (!string.IsNullOrWhiteSpace(quest) && !QuestManager.IsCompleted(quest)) return false;
            return true;
        }

        private static VillageRequestData[] DatabaseRequests()
        {
            if (_database == null) _database = Resources.Load<VillageRequestDatabase>("VillageRequestDatabase");
            return _database != null && _database.Requests != null
                ? _database.Requests
                : Array.Empty<VillageRequestData>();
        }

        private static void Persist()
        {
            try { SaveManager.AutoSaveVillageRequests(ToSnapshot()); }
            catch (Exception e) { Debug.LogWarning("[VillageRequests] Autosave failed: " + e.Message); }
        }

        private static int CurrentDay()
        {
            if (TimeManager.Instance != null) return TimeManager.Instance.Day;
            var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
            return meta != null && meta.GameDay > 0 ? meta.GameDay : 1;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 16777619;
                return (int)(hash & 0x7fffffff);
            }
        }

        private static int PositiveModulo(int value, int divisor) => (value % divisor + divisor) % divisor;
        private static string FirstCompletionFlag(string requestId) => "village_request_first_" + requestId;

        private static DeliveryRollbackState CaptureDeliveryState()
        {
            // Force lazy quest hydration before copying its collections.
            QuestManager.IsCompleted("__village_delivery_snapshot__");
            var scores = new SaveSlotMeta();
            GameScores.WriteTo(scores);
            return new DeliveryRollbackState
            {
                Inventory = InventoryRuntime.ToSnapshot(),
                Apothecary = ApothecaryRuntime.ToSnapshot(),
                Copper = CoinPurse.TotalCopper,
                CoinLedger = CoinPurse.ToLedgerSnapshot(),
                Scores = scores,
                Requests = ToSnapshot(),
                CompletedQuestIds = QuestManager.CompletedQuestIds.ToArray(),
                StoryCardIds = QuestManager.UnlockedStoryCardIds.ToArray(),
                ActiveQuest = QuestManager.ActiveQuest,
                Waypoint = LocationRegistry.ActiveWaypoint,
            };
        }

        private static void RestoreDeliveryStateAndCancel(DeliveryRollbackState state)
        {
            try
            {
                InventoryRuntime.HydrateFrom(state?.Inventory);
                ApothecaryRuntime.HydrateFrom(state?.Apothecary);
                CoinPurse.HydrateFrom(state?.Copper ?? 0, state?.CoinLedger);
                GameScores.HydrateFrom(state?.Scores);
                HydrateFrom(state?.Requests);

                QuestManager.ResetForSlotSwitch();
                QuestManager.HydrateFrom(state?.CompletedQuestIds, state?.StoryCardIds);
                if (state?.ActiveQuest != null) QuestManager.StartQuest(state.ActiveQuest);

                if (state?.Waypoint != null) LocationRegistry.SetWaypoint(state.Waypoint);
                else LocationRegistry.ClearWaypoint();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                // Rollback notifications were queued under the still-active transaction; discard
                // both those and the original completion publications together.
                SaveManager.CancelAtomicTransaction();
            }
        }

        private static void PublishChanged()
        {
            var handlers = OnChanged;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                var callback = handler;
                SaveManager.PublishAfterAtomicCommit(callback);
            }
        }

        private static string[] RequiredProductIds(VillageRequestData request)
        {
            int count = request?.PreparationRequirementCount ?? 0;
            var ids = new string[count];
            for (int i = 0; i < count; i++)
                ids[i] = request.RequiredPreparations[i] != null
                    ? request.RequiredPreparations[i].ResultId
                    : null;
            return ids;
        }
    }
}
