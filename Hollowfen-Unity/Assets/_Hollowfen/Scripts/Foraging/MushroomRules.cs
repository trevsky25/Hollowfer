using Hollowfen.Data;
using Hollowfen.Quests;

namespace Hollowfen.Foraging
{
    /// <summary>
    /// One policy boundary for mushroom progression. Scene nodes, cultivation UI, buyers,
    /// and future recipes all ask the same data-authored rules instead of duplicating flags.
    /// </summary>
    public static class MushroomRules
    {
        public static bool CanHarvest(MushroomFieldGuideData species)
        {
            if (species == null) return false;
            return string.IsNullOrEmpty(species.RequiredForageFlagId) ||
                   GameScores.HasFlag(species.RequiredForageFlagId);
        }

        public static bool CanCultivate(MushroomFieldGuideData species)
        {
            if (species == null || !species.Cultivable) return false;
            if (!QuestManager.IsCompleted("almyTeach") &&
                !(QuestManager.IsActive("almyTeach") && GameScores.HasFlag("act2_started")))
                return false;
            if (!MushroomDiscovery.IsDiscovered(species.Id)) return false;
            return string.IsNullOrEmpty(species.CultivationUnlockFlagId) ||
                   GameScores.HasFlag(species.CultivationUnlockFlagId);
        }

        public static int SaleValue(MushroomFieldGuideData species, MushroomBuyer buyer) =>
            species != null ? species.ValueFor(buyer) : 0;
    }
}
