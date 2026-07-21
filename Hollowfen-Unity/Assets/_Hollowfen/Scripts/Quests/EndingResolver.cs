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
        public const string PresentationSeenFlag = "ending_presentation_seen";

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
                return new EndingEligibility(false, Localization.Get("ending.error.not_configured"));

            if (GameScores.HasFlag("game_complete"))
                return new EndingEligibility(false, Localization.Get("ending.error.already_chosen"));

            if (GameScores.VillageHope < ending.MinimumVillageHope)
                return new EndingEligibility(false, Hollowfen.UI.JournalText.EndingLockedHint(ending));
            if (GameScores.Knowledge < ending.MinimumKnowledge)
                return new EndingEligibility(false, Hollowfen.UI.JournalText.EndingLockedHint(ending));

            var flags = ending.RequiredFlagIds;
            if (flags != null)
            {
                foreach (var flag in flags)
                    if (!GameScores.HasFlag(flag))
                        return new EndingEligibility(false, Hollowfen.UI.JournalText.EndingLockedHint(ending));
            }

            var npcIds = ending.RelationshipNpcIds;
            var minimums = ending.MinimumRelationshipValues;
            int count = npcIds == null || minimums == null ? 0 : System.Math.Min(npcIds.Length, minimums.Length);
            for (int i = 0; i < count; i++)
                if (GameScores.GetRelationship(npcIds[i]) < minimums[i])
                    return new EndingEligibility(false, Hollowfen.UI.JournalText.EndingLockedHint(ending));

            return new EndingEligibility(true, Hollowfen.UI.JournalText.EndingContext(ending));
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
                failureReason = Localization.Get("ending.error.already_committed");
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

        /// <summary>
        /// Finds the one committed ending whose presentation still needs to be shown. The
        /// committed score state is intentionally durable before the cinematic begins, so a
        /// process interruption replays the presentation instead of losing it.
        /// </summary>
        public static bool TryGetPendingPresentation(IEnumerable<EndingData> endings, out EndingData ending)
        {
            ending = null;
            if (!GameScores.HasFlag("game_complete") || GameScores.HasFlag(PresentationSeenFlag))
                return false;

            string committedFlag = CompletedEndingFlag();
            if (string.IsNullOrEmpty(committedFlag) || endings == null) return false;
            foreach (var candidate in endings)
            {
                if (candidate != null && candidate.EndingFlagId == committedFlag)
                {
                    ending = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Repairs idempotent rewards before replaying an interrupted presentation.</summary>
        public static bool ReconcileCommittedEnding(EndingData ending)
        {
            if (ending == null || !GameScores.HasFlag("game_complete") ||
                !GameScores.HasFlag(ending.EndingFlagId)) return false;

            if (ending.StoryCard != null)
                QuestManager.UnlockStoryCard(ending.StoryCard.Id);

            // Steam achievement unlocks are idempotent. Re-requesting the committed ending's
            // unlock closes the crash window between score/card persistence and the API call.
            if (!string.IsNullOrWhiteSpace(ending.AchievementId))
                GameEvents.TriggerAchievement(ending.AchievementId);

            SaveCoordinator.SaveAllWithPlayer();
            return true;
        }

        /// <summary>Durably acknowledges that the player deliberately dismissed the finale.</summary>
        public static bool MarkPresentationSeen()
        {
            if (!GameScores.HasFlag("game_complete") || string.IsNullOrEmpty(CompletedEndingFlag()))
                return false;

            GameScores.SetFlag(PresentationSeenFlag);
            SaveCoordinator.SaveAllWithPlayer();
            return GameScores.HasFlag(PresentationSeenFlag);
        }

        public static string CompletedEndingFlag()
        {
            foreach (var flag in CanonicalEndingFlags)
                if (GameScores.HasFlag(flag)) return flag;
            return null;
        }
    }
}
