using System.Linq;
using Hollowfen.Data;
using Hollowfen.Items;
using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.Foraging
{
    public enum MushroomJournalPageState
    {
        Locked,
        ReferenceAvailable,
        Studied,
        FieldVerified,
    }

    /// <summary>
    /// The journal is Wren's knowledge boundary. It exposes reference pages as the story opens
    /// new foraging tiers, records which pages she has studied, and awards Knowledge only when a
    /// live specimen is correctly identified. Discovery and harvesting are intentionally separate:
    /// naming a mushroom never puts it in the basket.
    /// </summary>
    public static class MushroomKnowledge
    {
        private const string JournalItemId = "item.fathers_journal";
        private const string StudiedPrefix = "mushroom_studied_";
        private const string IdentifiedPrefix = "mushroom_identified_";

        public static bool HasJournal => KeyItems.Has(JournalItemId) ||
                                         QuestManager.IsCompleted("findJournal") ||
                                         GameScores.HasFlag("journal_found");

        public static bool CanReadPage(MushroomFieldGuideData species)
        {
            if (species == null) return false;
            if (MushroomDiscovery.IsDiscovered(species.Id)) return true;
            if (!HasJournal) return false;
            return string.IsNullOrEmpty(species.RequiredForageFlagId) ||
                   GameScores.HasFlag(species.RequiredForageFlagId);
        }

        public static bool HasStudied(MushroomFieldGuideData species) =>
            species != null && GameScores.HasFlag(StudiedPrefix + species.Id);

        /// <summary>
        /// Persistent proof that Wren completed this species' live three-stage field test.
        /// Journal/story discovery deliberately does not satisfy this gate.
        /// </summary>
        public static bool IsFieldIdentified(MushroomFieldGuideData species) =>
            species != null && GameScores.HasFlag(IdentifiedPrefix + species.Id);

        public static MushroomJournalPageState PageState(MushroomFieldGuideData species)
        {
            if (species == null || !CanReadPage(species)) return MushroomJournalPageState.Locked;
            if (IsFieldIdentified(species)) return MushroomJournalPageState.FieldVerified;
            return HasStudied(species)
                ? MushroomJournalPageState.Studied
                : MushroomJournalPageState.ReferenceAvailable;
        }

        public static void StudyPage(MushroomFieldGuideData species)
        {
            if (CanReadPage(species)) GameScores.SetFlag(StudiedPrefix + species.Id);
        }

        public static bool CanCompare(MushroomFieldGuideData species) =>
            species != null && HasJournal && CanReadPage(species) && HasStudied(species);

        public static bool RecordIdentification(MushroomFieldGuideData species) =>
            RecordIdentificationInternal(species, null);

        public static bool RecordIdentification(MushroomFieldGuideData species,
            Vector3 worldPosition) => RecordIdentificationInternal(species, worldPosition);

        private static bool RecordIdentificationInternal(MushroomFieldGuideData species,
            Vector3? worldPosition)
        {
            if (!CanCompare(species)) return false;
            StudyPage(species);
            GameScores.SetFlag(IdentifiedPrefix + species.Id);
            MushroomFieldNotes.RecordFirstVerification(species, worldPosition);
            MushroomDiscovery.MarkDiscovered(species.Id);
            // ScoreHooks owns the canonical +1 Knowledge response to OnDiscovered. Keeping the
            // award there also covers story-taught species without double-counting field tests.
            int count = MushroomDiscovery.All.Count();
            if (count >= 5) GameEvents.TriggerAchievement("ACH_FIELD_GUIDE_5");
            if (count >= 21) GameEvents.TriggerAchievement("ACH_FIELD_GUIDE_COMPLETE");
            // A story can expose a page before this test, so MarkDiscovered may be idempotent.
            // The field-identification flag, rather than discovery's return value, is success.
            return IsFieldIdentified(species);
        }
    }
}
