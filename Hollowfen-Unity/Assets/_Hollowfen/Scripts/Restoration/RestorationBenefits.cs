using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.Restoration
{
    /// <summary>
    /// Small, deterministic gameplay improvements earned by putting restored places back into
    /// use. Flags remain the durable authority, so benefits need no additional save payload.
    /// </summary>
    public static class RestorationBenefits
    {
        public const int BaseCuttingStrokes = 6;

        public static int CuttingStrokes => GameScores.HasFlag("forge_in_use") ? 5 : BaseCuttingStrokes;

        public static float CultivationHoursMultiplier =>
            GameScores.HasFlag("chapel_garden_in_use") ? 0.75f : 1f;

        public static int DailyRequestBonusCopper =>
            GameScores.HasFlag("crooked_pintle_in_use") ? 2 : 0;

        public static int WildRespawnDays(int authoredDays) =>
            Mathf.Max(1, authoredDays - (GameScores.HasFlag("witch_cottage_in_use") ? 1 : 0));

        public static int CultivationYieldBonus =>
            GameScores.HasFlag("tobin_workshop_in_use") ? 1 : 0;
    }
}
