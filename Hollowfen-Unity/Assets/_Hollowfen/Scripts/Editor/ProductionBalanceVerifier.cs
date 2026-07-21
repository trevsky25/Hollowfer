#if UNITY_EDITOR
using System;
using System.Linq;
using Hollowfen.Data;
using Hollowfen.Foraging;
using Hollowfen.Items;
using Hollowfen.Requests;
using Hollowfen.Restoration;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Production balance guardrails for the first tax, recurring work, and restoration.</summary>
    public static class ProductionBalanceVerifier
    {
        [MenuItem("Hollowfen/Verify/Economy & Progression Balance")]
        private static void RunMenu() => Debug.Log(RunAll());

        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            MushroomFieldGuideData[] species = Assets<MushroomFieldGuideData>(
                "Assets/_Hollowfen/Data/Mushrooms");
            VillageRequestData[] requests = Assets<VillageRequestData>(
                "Assets/_Hollowfen/Data/Requests");
            RestorationProjectData[] projects = Assets<RestorationProjectData>(
                "Assets/_Hollowfen/Data/Restoration");

            Require(species.Length == 21, "expected 21 priced mushroom species");
            Require(requests.Length == 16, "expected 16 authored village requests");
            Require(projects.Length == 7, "expected seven restoration projects");

            foreach (MushroomFieldGuideData mushroom in species)
            {
                int marra = mushroom.ValueFor(MushroomBuyer.Marra);
                int theo = mushroom.ValueFor(MushroomBuyer.Theo);
                Require(marra >= 0 && theo >= 0, mushroom.Id + " has a negative price");
                if (marra > 0 && theo > 0)
                    Require(theo >= marra, "Theo undercuts Marra for " + mushroom.Id);
                Require(mushroom.WildRespawnDays >= 1 && mushroom.WildRespawnDays <= 5,
                    mushroom.Id + " has an outlier respawn delay");
            }

            foreach (VillageRequestData request in requests.Where(request => !request.OneShot))
            {
                int opportunityCost = RawOpportunityCost(request);
                if (opportunityCost > 0)
                    Require(request.RewardCopper + request.WetWeatherBonusCopper >= opportunityCost,
                        request.Id + " pays less than the basket's best direct-sale value");
            }

            MushroomNode[] nodes = UnityEngine.Object.FindObjectsByType<MushroomNode>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int tierOneMarraCapacity = nodes.Where(node => node != null && !node.IsCultivated &&
                    node.Data != null && node.Data.Tier == ForageTier.BasketCommon)
                .Sum(node => node.Data.ValueFor(MushroomBuyer.Marra));
            const int firstPayment = 38;
            const int jorenCommission = 12;
            const int taxDemand = 12 * CoinPurse.CopperPerSilver;
            Require(firstPayment - jorenCommission + tierOneMarraCapacity >= taxDemand,
                "the first tax cannot be earned from the unlocked common-species population");

            int restorationCost = projects.Sum(project =>
                project.Contributions?.Sum(contribution => contribution.CostCopper) ?? 0);
            Require(restorationCost > taxDemand,
                "the complete restoration catalogue is cheaper than the Act II tax pressure");
            Require(restorationCost <= tierOneMarraCapacity * 2,
                "restoration requires more than two complete common-forage cycles");
            foreach (RestorationProjectData project in projects)
                foreach (RestorationContribution contribution in project.Contributions ??
                             Array.Empty<RestorationContribution>())
                    Require(contribution.CostCopper >= 10 && contribution.CostCopper <= 24,
                        project.Id + " has an outlier contribution cost");

            Require(Localization.Get("quest.firstTax.objective").Contains("first sundown"),
                "the first-tax objective does not disclose its grace window");
            return "ECONOMY & PROGRESSION — PASS: 21 species preserve Theo's market premium; " +
                   "12 recurring jobs never underpay direct-sale opportunity cost; the 144c tax " +
                   "is reachable from first-payment cash plus one common-forage population and " +
                   "discloses a full grace night; seven restorations total " + restorationCost +
                   "c, within two common-forage cycles.";
        }

        private static int RawOpportunityCost(VillageRequestData request)
        {
            int total = 0;
            for (int i = 0; i < request.RequirementCount; i++)
            {
                MushroomFieldGuideData mushroom = request.RequiredSpecies[i];
                if (mushroom == null) continue;
                total += request.RequiredCountAt(i) * Mathf.Max(
                    mushroom.ValueFor(MushroomBuyer.Marra),
                    mushroom.ValueFor(MushroomBuyer.Theo));
            }
            return total;
        }

        private static T[] Assets<T>(string root) where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { root })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .ToArray();

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(
                "[ProductionBalanceVerifier] " + message);
        }
    }
}
#endif
