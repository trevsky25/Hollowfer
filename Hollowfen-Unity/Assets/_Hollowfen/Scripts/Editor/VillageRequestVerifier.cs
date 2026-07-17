#if UNITY_EDITOR
using System;
using System.Linq;
using Hollowfen.Foraging;
using Hollowfen.GameTime;
using Hollowfen.Items;
using Hollowfen.Quests;
using Hollowfen.Requests;
using Hollowfen.Save;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.EditorTools
{
    /// <summary>State-mutating Play Mode verifier. The automation wrapper owns save backup/restore.</summary>
    public static class VillageRequestVerifier
    {
        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            int originalSlot = SaveManager.ActiveSlot;
            SaveManager.SetActiveSlot(3);
            try
            {
                var clock = TimeManager.Instance;
                Require(clock != null, "TimeManager is missing from the gameplay scene");
                var requests = LoadRequests();
                Require(requests.Length == 10, "expected ten authored village requests");
                VerifyWorldSource();

                QuestManager.ResetForSlotSwitch();
                QuestManager.HydrateFrom(new[] { "firstSale" }, Array.Empty<string>());
                GameScores.HydrateFrom(new SaveSlotMeta
                {
                    GameFlagIds = new[]
                    {
                        "theo_met", "apprentice_system_unlocked", "foraging_knife_unlocked",
                    },
                });
                CoinPurse.HydrateFrom(0);
                InventoryRuntime.HydrateFrom(null);
                VillageRequests.HydrateFrom(null);
                clock.SetTime(20, 10f);

                var marraDay20 = RequireCurrent("marra");
                var eddaDay20 = RequireCurrent("edda");
                var theoDay20 = RequireCurrent("theo");
                Require(VillageRequests.CurrentForNpc("marra") == marraDay20,
                    "same-day selection is not deterministic");
                Require(!marraDay20.OneShot && !eddaDay20.OneShot && !theoDay20.OneShot,
                    "ordinary rotations selected a story request");

                VillageRequests.Track(marraDay20);
                var tracked = VillageRequests.ToSnapshot();
                VillageRequests.HydrateFrom(tracked);
                Require(VillageRequests.TrackedRequest == marraDay20,
                    "tracked request failed its in-memory save round-trip");

                FillBasket(marraDay20);
                int relationshipBefore = GameScores.GetRelationship("marra");
                var first = VillageRequests.Complete(marraDay20);
                Require(first.Success && first.Copper == marraDay20.RewardCopper && first.FirstCompletion,
                    "first recurring Marra delivery returned the wrong result");
                Require(CoinPurse.TotalCopper == marraDay20.RewardCopper,
                    "recurring request did not pay its authored copper reward");
                Require(GameScores.GetRelationship("marra") == relationshipBefore +
                        marraDay20.FirstCompletionRelationshipDelta,
                    "first-completion relationship reward was not applied exactly once");
                Require(BasketIsEmptyFor(marraDay20), "delivery did not consume its full basket atomically");
                Require(VillageRequests.CurrentForNpc("marra") == null,
                    "Marra offered a second ordinary order on the same day");
                Require(!VillageRequests.Complete(marraDay20).Success,
                    "a claimed request could be completed twice");

                VillageRequests.Track(theoDay20);
                clock.SetTime(21, 8f);
                Require(VillageRequests.TrackedRequest == null, "daily tracker did not expire at dawn");
                var marraDay21 = RequireCurrent("marra");
                Require(marraDay21.Id != marraDay20.Id, "Marra's order did not rotate on the next day");

                // Three days later the same authored job returns, but its relationship reward does not.
                clock.SetTime(23, 8f);
                var marraDay23 = RequireCurrent("marra");
                Require(marraDay23.Id == marraDay20.Id, "three-request rotation did not cycle deterministically");
                FillBasket(marraDay23);
                int relationshipAfterFirst = GameScores.GetRelationship("marra");
                var repeat = VillageRequests.Complete(marraDay23);
                Require(repeat.Success && !repeat.FirstCompletion,
                    "returning authored order did not use repeat-completion behavior");
                Require(GameScores.GetRelationship("marra") == relationshipAfterFirst,
                    "repeat work farmed a first-completion relationship reward");

                RequireCurrent("edda");
                RequireCurrent("theo");
                VerifyRequestCard(clock);

                // The festival outranks an already-claimed Marra workday and persists as story state.
                var festival = requests.Single(request => request.Id == "festival_four_dishes");
                var festivalQuest = festival.CompleteQuest;
                Require(festivalQuest != null, "festival request has no quest completion");
                QuestManager.StartQuest(festivalQuest);
                GameScores.SetFlag("festival_gathering_active");
                Require(VillageRequests.CurrentForNpc("marra") == festival,
                    "active festival gathering did not outrank rotating kitchen work");
                FillBasket(festival);
                var story = VillageRequests.Complete(festival);
                Require(story.Success && story.Copper == 0,
                    "festival delivery did not commit as story work");
                Require(QuestManager.IsCompleted("festivalHosted") &&
                        QuestManager.ActiveQuest != null && QuestManager.ActiveQuest.Id == "aldricLetter",
                    "festival delivery did not complete and advance the canonical quest chain");
                Require(GameScores.HasFlag("festival_prepared") && GameScores.HasFlag("festival_hosted"),
                    "festival consequences were not committed with the delivery");
                Require(BasketIsEmptyFor(festival), "festival did not consume all four dishes' ingredients");
                Require(VillageRequests.CurrentForNpc("marra") == null,
                    "completed one-shot festival request remained available");

                var disk = SaveManager.GetSlotMeta(3);
                Require(disk?.VillageRequests?.CompletedOneShotIds != null &&
                        disk.VillageRequests.CompletedOneShotIds.Contains(festival.Id),
                    "completed gathering did not reach the save file");
                VillageRequests.HydrateFrom(null);
                VillageRequests.HydrateFrom(disk.VillageRequests);
                Require(VillageRequests.ToSnapshot().CompletedOneShotIds.Contains(festival.Id),
                    "completed gathering failed save hydration");

                return "VILLAGE REQUESTS — PASS: 9 deterministic dawn-rotating NPC orders, one-per-NPC daily claims, atomic basket/payment saves, first-only relationship rewards, tracker expiry, controller request card, medicinal/trader availability, and four-dish festival quest handoff";
            }
            finally
            {
                SaveManager.SetActiveSlot(originalSlot);
            }
        }

        private static VillageRequestData[] LoadRequests() =>
            AssetDatabase.FindAssets("t:VillageRequestData", new[] { "Assets/_Hollowfen/Data/Requests" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<VillageRequestData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(request => request != null)
                .OrderBy(request => request.Id)
                .ToArray();

        private static VillageRequestData RequireCurrent(string npc)
        {
            var request = VillageRequests.CurrentForNpc(npc);
            Require(request != null, npc + " has no eligible rotating request");
            return request;
        }

        private static void FillBasket(VillageRequestData request)
        {
            var ids = new string[request.RequirementCount];
            var counts = new int[request.RequirementCount];
            for (int i = 0; i < request.RequirementCount; i++)
            {
                ids[i] = request.RequiredSpecies[i].Id;
                counts[i] = request.RequiredCountAt(i);
            }
            InventoryRuntime.HydrateFrom(new InventorySnapshot { Ids = ids, Counts = counts });
            Require(VillageRequests.CanDeliver(request), "freshly filled basket is not deliverable");
        }

        private static bool BasketIsEmptyFor(VillageRequestData request)
        {
            for (int i = 0; i < request.RequirementCount; i++)
                if (InventoryRuntime.GetCount(request.RequiredSpecies[i]) != 0) return false;
            return true;
        }

        private static void VerifyRequestCard(TimeManager clock)
        {
            clock.SetTime(24, 9f);
            var request = RequireCurrent("theo");
            var screen = VillageRequestScreen.Ensure();
            screen.Open(request, "Theo", null, null);
            Require(screen.IsOpen, "request card did not open");
            Require(screen.GetComponentsInChildren<Button>(true).Length >= 3,
                "request card is missing controller-selectable actions");
            Require(Mathf.Approximately(Time.timeScale, 0f), "request card did not pause world time");
            screen.Close();
            Require(!screen.IsOpen && Time.timeScale > 0f, "request card did not restore gameplay state");
        }

        private static void VerifyWorldSource()
        {
            var wild = UnityEngine.Object.FindObjectsByType<MushroomNode>(FindObjectsInactive.Include)
                .Where(node => !node.IsCultivated)
                .ToArray();
            Require(wild.Any(node => node.Data != null && node.Data.Id == "lacewig" &&
                                     node.NodeId.StartsWith("wild.lacewig.", StringComparison.Ordinal)),
                "festival Lacewig has no stable wild source");
            Require(wild.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count() == wild.Length,
                "wild node ids are not unique after adding the festival source");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[VillageRequestVerifier] " + message);
        }
    }
}
#endif
