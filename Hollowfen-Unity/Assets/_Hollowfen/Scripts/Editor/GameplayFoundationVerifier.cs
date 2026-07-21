#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Hollowfen.Cultivation;
using Hollowfen.Data;
using Hollowfen.Foraging;
using Hollowfen.GameTime;
using Hollowfen.Items;
using Hollowfen.Quests;
using Hollowfen.Save;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// State-mutating Play Mode verifier for the repeatable game-loop foundation. It refuses to
    /// run until automation supplies an isolated EditorSaveDirectoryOverride.
    /// </summary>
    public static class GameplayFoundationVerifier
    {
        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            var species = LoadSpecies();
            int originalSlot = SaveManager.ActiveSlot;
            Require(!string.IsNullOrWhiteSpace(SaveManager.EditorSaveDirectoryOverride),
                "set SaveManager.EditorSaveDirectoryOverride to an isolated directory first");
            SaveCoordinator.StartNewGame(3);
            try
            {
                int wildNodes = VerifyAuthoredWorld(species);
                VerifyIdentificationGate(species);
                VerifyBuyerEconomy(species);
                VerifyForageLifecycle();
                VerifyCultivation(species);
                VerifyClockBoundaries();

                int cultivable = species.Count(entry => entry.Cultivable);
                return $"GAMEPLAY FOUNDATION — PASS: {wildNodes} stable wild nodes + rest point, 21 persistent field-identification harvest gates, non-mutating buyer quotes, species-aware refusals/copper + purse-ledger persistence, forage save/respawn round-trip, " +
                       cultivable + " data-authored cultivation recipes, and ordered sundown/dawn clock boundaries";
            }
            finally
            {
                SaveManager.SetActiveSlot(originalSlot);
            }
        }

        private static MushroomFieldGuideData[] LoadSpecies()
        {
            var all = AssetDatabase.FindAssets("t:MushroomFieldGuideData", new[] { "Assets/_Hollowfen/Data/Mushrooms" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<MushroomFieldGuideData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(entry => entry != null)
                .OrderBy(entry => entry.Id)
                .ToArray();
            Require(all.Length == 21, "expected 21 mushroom gameplay profiles");
            return all;
        }

        private static int VerifyAuthoredWorld(MushroomFieldGuideData[] species)
        {
            var wild = UnityEngine.Object.FindObjectsByType<MushroomNode>(FindObjectsInactive.Include)
                .Where(node => !node.IsCultivated)
                .ToArray();
            Require(wild.Length >= 21, "expected at least one authored wild node per mushroom species");
            Require(wild.All(node => node.Data != null && !string.IsNullOrWhiteSpace(node.NodeId)),
                "a wild mushroom is missing its species or stable node id");
            var authored = wild.Where(node =>
                !node.NodeId.StartsWith("wild.generated.", StringComparison.Ordinal)).ToArray();
            Require(authored.All(node => node.NodeId.StartsWith(
                    "wild." + node.Data.Id + ".", StringComparison.Ordinal)),
                "a wild mushroom node id does not match its species");
            Require(wild.Where(node => node.NodeId.StartsWith("wild.generated.", StringComparison.Ordinal))
                    .All(node => node.NodeId.Split('.').Length >= 4),
                "a generated ecology node lacks its stable habitat/index id");
            Require(wild.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count() == wild.Length,
                "wild mushroom node ids are not unique");
            var profileIds = new HashSet<string>(species.Select(entry => entry.Id), StringComparer.Ordinal);
            Require(wild.All(node => profileIds.Contains(node.Data.Id)),
                "a world node references a species without a gameplay profile");

            var rest = UnityEngine.Object.FindObjectsByType<RestSpot>(FindObjectsInactive.Include);
            Require(rest.Length == 1, "expected exactly one mill-hearth rest point");
            return wild.Length;
        }

        private static void VerifyIdentificationGate(MushroomFieldGuideData[] species)
        {
            string[] storyFlags = species.Select(entry => entry.RequiredForageFlagId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .Append("journal_found")
                .ToArray();
            MushroomDiscovery.HydrateFrom(species.Select(entry => entry.Id).ToArray());
            GameScores.HydrateFrom(new SaveSlotMeta { GameFlagIds = storyFlags });

            Require(species.All(entry => !MushroomRules.CanHarvest(entry)),
                "a journal-discovered species can be harvested without its field test");
            string[] commonIds = { "fieldMushroom", "woodEar", "pinecrest" };
            Require(commonIds.All(id => species.Any(entry => entry.Id == id)),
                "the common-species identification coverage set drifted");
            Require(species.Where(entry => commonIds.Contains(entry.Id))
                    .All(entry => !MushroomRules.CanHarvest(entry)),
                "Field Mushroom, Wood Ear, or Pinecrest bypassed field identification");

            string[] verifiedFlags = storyFlags.Concat(species.Select(entry =>
                    "mushroom_identified_" + entry.Id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            GameScores.HydrateFrom(new SaveSlotMeta { GameFlagIds = verifiedFlags });
            Require(species.All(MushroomRules.CanHarvest),
                "a field-identified species with its story lesson unlocked remains unharvestable");
        }

        private static void VerifyBuyerEconomy(MushroomFieldGuideData[] species)
        {
            var sellable = species.First(entry => entry.ValueFor(MushroomBuyer.Marra) > 0 &&
                                                  entry.ValueFor(MushroomBuyer.Theo) > 0);
            var refused = species.First(entry => entry.Edibility == Edibility.Deadly);

            InventoryRuntime.HydrateFrom(new InventorySnapshot
            {
                Ids = new[] { sellable.Id, refused.Id },
                Counts = new[] { 2, 1 },
            });
            var marraQuote = InventoryRuntime.QuoteFor(MushroomBuyer.Marra);
            Require(marraQuote.SoldCount == 2 && marraQuote.RefusedCount == 1 &&
                    marraQuote.Copper == sellable.ValueFor(MushroomBuyer.Marra) * 2,
                "Marra's purse quote does not match her buyer policy");
            Require(InventoryRuntime.GetCount(sellable.Id) == 2 && InventoryRuntime.GetCount(refused.Id) == 1,
                "reading a purse quote changed the basket");
            var marra = InventoryRuntime.SellTo(MushroomBuyer.Marra);
            Require(marra.SoldCount == 2 && marra.RefusedCount == 1,
                "Marra did not sell/refuse the expected basket contents");
            Require(marra.Copper == sellable.ValueFor(MushroomBuyer.Marra) * 2,
                "Marra's data-authored copper total is wrong");
            Require(InventoryRuntime.GetCount(sellable.Id) == 0 && InventoryRuntime.GetCount(refused.Id) == 1,
                "buyer sale removed a refused specimen or retained a sold one");

            CoinPurse.HydrateFrom(0);
            CoinPurse.Add(marra.Copper, "purse.transaction.marra_sale");
            Require(CoinPurse.RecentTransactions.Count == 1 &&
                    CoinPurse.RecentTransactions[0].AmountCopper == marra.Copper &&
                    CoinPurse.RecentTransactions[0].ReasonId == "purse.transaction.marra_sale",
                "Marra's sale did not enter the purse ledger");

            InventoryRuntime.HydrateFrom(new InventorySnapshot
            {
                Ids = new[] { sellable.Id, refused.Id },
                Counts = new[] { 3, 1 },
            });
            var theo = InventoryRuntime.SellTo(MushroomBuyer.Theo);
            Require(theo.SoldCount == 3 && theo.RefusedCount == 1,
                "Theo did not sell/refuse the expected basket contents");
            Require(theo.Copper == sellable.ValueFor(MushroomBuyer.Theo) * 3,
                "Theo's data-authored copper total is wrong");
            CoinPurse.Add(theo.Copper, "purse.transaction.theo_sale");
            Require(CoinPurse.RecentTransactions.Count == 2 &&
                    CoinPurse.RecentTransactions[0].ReasonId == "purse.transaction.theo_sale" &&
                    CoinPurse.RecentTransactions[0].BalanceAfterCopper == marra.Copper + theo.Copper,
                "Theo's sale did not enter the purse ledger with the running balance");

            var saved = SaveManager.GetSlotMeta(3);
            var disk = saved?.Inventory;
            Require(disk != null && disk.Ids != null && disk.Ids.Contains(refused.Id),
                "species-aware sale did not persist the refused basket item");
            Require(saved.CoinLedger != null && saved.CoinLedger.AmountsCopper != null &&
                    saved.CoinLedger.AmountsCopper.Length == 2 &&
                    saved.CoinLedger.ReasonIds[0] == "purse.transaction.theo_sale",
                "purse transaction history did not persist to the save slot");
        }

        private static void VerifyForageLifecycle()
        {
            const string testNode = "verify.forage.lifecycle";
            ForageNodeStates.HydrateFrom(null);
            ForageNodeStates.MarkHarvested(testNode, 7);
            Require(!ForageNodeStates.IsAvailable(testNode, 7, 2), "a freshly cut node immediately respawned");
            Require(!ForageNodeStates.IsAvailable(testNode, 8, 2), "a two-day node respawned one dawn early");
            Require(ForageNodeStates.IsAvailable(testNode, 9, 2), "a node did not respawn on its authored dawn");

            var snapshot = ForageNodeStates.ToSnapshot();
            ForageNodeStates.HydrateFrom(snapshot);
            Require(!ForageNodeStates.IsAvailable(testNode, 8, 2), "forage state failed in-memory round-trip");
            var disk = SaveManager.GetSlotMeta(3)?.ForageNodes;
            Require(disk != null && disk.Ids != null && disk.Ids.Contains(testNode),
                "forage harvest did not round-trip to the save slot");
        }

        private static void VerifyCultivation(MushroomFieldGuideData[] species)
        {
            var crops = species.Where(entry => entry.Cultivable).ToArray();
            Require(crops.Length >= 3, "expected at least three cultivation recipes");
            Require(crops.All(entry => entry.WorldPrefab != null && entry.CultivationHours > 0f &&
                                       entry.CultivationYield > 0),
                "a cultivation recipe is missing a prefab, growth time, or yield");

            var unlockFlags = crops.Select(entry => entry.CultivationUnlockFlagId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            GameScores.HydrateFrom(new SaveSlotMeta { GameFlagIds = unlockFlags });
            QuestManager.ResetForSlotSwitch();
            QuestManager.HydrateFrom(new[] { "almyTeach" }, Array.Empty<string>());
            MushroomDiscovery.HydrateFrom(crops.Select(entry => entry.Id).ToArray());
            InventoryRuntime.HydrateFrom(new InventorySnapshot
            {
                Ids = crops.Select(entry => entry.Id).ToArray(),
                Counts = crops.Select(_ => 1).ToArray(),
            });
            Require(crops.All(MushroomRules.CanCultivate), "an unlocked authored recipe is still rejected");
            Require(CultivationScreen.HasPlantableSpecies(), "recipe picker cannot see an eligible basket crop");

            GrowBeds.HydrateFrom(null);
            var beds = UnityEngine.Object.FindObjectsByType<GrowBed>(FindObjectsInactive.Include);
            Require(beds.Length > 0, "no grow bed is present in the gameplay scene");
            var planted = crops.First(entry => entry.Id == "woodEar");
            beds[0].Plant(planted);
            var bedState = GrowBeds.ToSnapshot();
            Require(bedState.SpeciesIds != null && bedState.SpeciesIds.Contains(planted.Id),
                "grow bed did not persist the selected recipe species");
            Require(InventoryRuntime.GetCount(planted.Id) == 0,
                "planting did not consume exactly one spawn specimen");
        }

        private static void VerifyClockBoundaries()
        {
            var clock = TimeManager.Instance;
            Require(clock != null, "TimeManager is missing in gameplay");
            clock.SetTime(40, 18f);
            var events = new List<string>();
            Action sundown = () => events.Add("sundown");
            Action<int> dawn = day => events.Add("day:" + day);
            TimeManager.OnSundown += sundown;
            TimeManager.OnDayChanged += dawn;
            try
            {
                clock.AdvanceTo(41, 7f, false);
                Require(events.SequenceEqual(new[] { "sundown", "day:41" }),
                    "rest advancement did not emit sundown then dawn exactly once");
                clock.AdvanceTo(41, 7f, false);
                Require(events.Count == 2, "zero-duration rest emitted duplicate clock events");
                Require(clock.Day == 41 && Mathf.Approximately(clock.Hour, 7f),
                    "clock did not land on the requested dawn");
            }
            finally
            {
                TimeManager.OnSundown -= sundown;
                TimeManager.OnDayChanged -= dawn;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[GameplayFoundationVerifier] " + message);
        }
    }
}
#endif
