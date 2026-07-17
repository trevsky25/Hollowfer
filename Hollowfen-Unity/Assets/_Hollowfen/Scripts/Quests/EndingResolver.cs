using System.Collections.Generic;
using Hollowfen.Data;
using Hollowfen.Save;

namespace Hollowfen.Quests
{
    public readonly struct EndingEligibility
    {
        public EndingEligibility(bool isEligible, string reason)
        {
            IsEligible = isEligible;
            Reason = reason;
        }

        public bool IsEligible { get; }
        public string Reason { get; }
    }

    /// <summary>
    /// Pure eligibility evaluation plus the one-way, exclusive ending commit. The UI asks
    /// Evaluate as often as it likes; only TryResolve mutates state.
    /// </summary>
    public static class EndingResolver
    {
        public static readonly string[] CanonicalEndingFlags =
        {
            "ending_free_hollow",
            "ending_lordly_patronage",
            "ending_capital",
            "ending_witch_path",
        };

        public static EndingEligibility Evaluate(EndingData ending)
        {
            if (ending == null)
                return new EndingEligibility(false, "This path is not configured.");

            if (GameScores.HasFlag("game_complete"))
                return new EndingEligibility(false, "Wren has already chosen Hollowfen's future.");

            if (GameScores.VillageHope < ending.MinimumVillageHope)
                return new EndingEligibility(false, ending.LockedHint);
            if (GameScores.Knowledge < ending.MinimumKnowledge)
                return new EndingEligibility(false, ending.LockedHint);

            var flags = ending.RequiredFlagIds;
            if (flags != null)
            {
                foreach (var flag in flags)
                    if (!GameScores.HasFlag(flag))
                        return new EndingEligibility(false, ending.LockedHint);
            }

            var npcIds = ending.RelationshipNpcIds;
            var minimums = ending.MinimumRelationshipValues;
            int count = npcIds == null || minimums == null ? 0 : System.Math.Min(npcIds.Length, minimums.Length);
            for (int i = 0; i < count; i++)
                if (GameScores.GetRelationship(npcIds[i]) < minimums[i])
                    return new EndingEligibility(false, ending.LockedHint);

            return new EndingEligibility(true, ending.ChoiceContext);
        }

        public static bool TryResolve(EndingData ending, out string failureReason)
        {
            var eligibility = Evaluate(ending);
            if (!eligibility.IsEligible)
            {
                failureReason = eligibility.Reason;
                return false;
            }

            var flags = new List<string>();
            if (ending.ConsequenceFlagIds != null)
                flags.AddRange(ending.ConsequenceFlagIds);

            if (!GameScores.TryCompleteEnding(ending.EndingFlagId, flags))
            {
                failureReason = "Another ending has already been committed.";
                return false;
            }

            if (ending.StoryCard != null)
                QuestManager.UnlockStoryCard(ending.StoryCard.Id);
            if (!string.IsNullOrWhiteSpace(ending.AchievementId))
                GameEvents.TriggerAchievement(ending.AchievementId);

            // Full snapshot after the targeted score/card autosaves: player transform, clock,
            // inventory, and the exclusive ending state all land in the same final save image.
            SaveCoordinator.SaveAllWithPlayer();
            failureReason = null;
            return true;
        }

        public static string CompletedEndingFlag()
        {
            foreach (var flag in CanonicalEndingFlags)
                if (GameScores.HasFlag(flag)) return flag;
            return null;
        }
    }
}
