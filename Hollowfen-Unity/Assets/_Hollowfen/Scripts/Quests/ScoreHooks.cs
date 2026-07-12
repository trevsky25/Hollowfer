using System.Collections.Generic;
using Hollowfen.Foraging;
using Hollowfen.Items;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Wires the story.md flag schema and the Knowledge meter to existing game events, so
    // content batches don't have to hand-set flags everywhere. Lives on the _Narration host.
    public class ScoreHooks : MonoBehaviour
    {
        // questId -> flags set on completion (story.md Act tables)
        private static readonly Dictionary<string, string[]> QuestFlags = new Dictionary<string, string[]>
        {
            { "arrive",      new[] { "act1_started" } },
            { "speakBram",   new[] { "mill_key_received" } },
            { "searchMill",  new[] { "mill_entered" } },
            { "findJournal", new[] { "journal_found" } },
            { "firstForage", new[] { "first_safe_harvest_complete", "goldfoot_sample_harvested" } },
            { "firstSale",   new[] { "first_sale_complete", "basic_selling_unlocked", "basic_cooking_unlocked" } },
            { "meetAlmy",    new[] { "almy_met", "act1_complete" } },
            // Act II (act2_started / joren_met / voss_first_visit_seen are set by the dialogues themselves)
            { "almyTeach",   new[] { "cultivation_unlocked", "wood_ear_log_planted" } },
            { "forgeKnife",  new[] { "foraging_knife_unlocked" } },
            { "firstTax",    new[] { "wenmar_tax_paid", "voss_notices_wren" } },
            // theo_met is set by Theo's first-sale dialogue; brightspore_tonic_made by Marra's.
            { "theoTrade",       new[] { "theo_trade_unlocked" } },
            { "edsGrandfather",  new[] { "edda_grandfather_recovering" } },
            // cottages_reopened_1/2 come from Pell's funding dialogue + the dawn scheduler;
            // calden_visiting stages Calden at the mill; chapel_garden_locked from the gate.
            { "meetHollin",      new[] { "hollin_met" } },
            { "cottagesReopen",  new[] { "calden_visiting" } },
            // hollin_at_mill stages her at the mill for Act III's opening scene.
            { "caldenWarning",   new[] { "act2_complete", "hollin_at_mill" } },
            // Act III (wendlight_known comes from discovery via SpeciesFlags below)
            { "hollinReveals",     new[] { "act3_started", "hollin_lineage_revealed", "sable_named" } },
            { "findWitchCottage",  new[] { "witch_cottage_found", "sable_seedbook_recovered", "tier4_mushrooms_unlocked" } },
            { "wendlightFound",    new[] { "wend_old_course_mapped", "aldric_logging_evidence_found" } },
            // Act III B close: Voss delivers Aldric's sealed letter; act3_complete gates Act IV.
            { "aldricLetter",      new[] { "aldric_letter_received", "voss_humanized", "act3_complete" } },
        };

        // species id -> known-flag (bible names diverge from camelCase ids)
        private static readonly Dictionary<string, string> SpeciesFlags = new Dictionary<string, string>
        {
            { "fieldCap",    "field_cap_known" },
            { "woodEar",     "wood_ear_known" },
            { "pinecrest",   "pinecrest_known" },
            { "goldfoot",    "goldfoot_partial_known" },
            { "brightspore", "brightspore_known" },
            { "wendlight",   "wendlight_known" },
        };

        private void OnEnable()
        {
            QuestManager.QuestCompleted += HandleQuestCompleted;
            QuestManager.StoryCardUnlocked += HandleCardUnlocked;
            MushroomDiscovery.OnDiscovered += HandleDiscovered;
        }

        private void OnDisable()
        {
            QuestManager.QuestCompleted -= HandleQuestCompleted;
            QuestManager.StoryCardUnlocked -= HandleCardUnlocked;
            MushroomDiscovery.OnDiscovered -= HandleDiscovered;
        }

        private void HandleQuestCompleted(QuestData quest)
        {
            if (quest == null) return;
            if (QuestFlags.TryGetValue(quest.Id, out var flags))
                foreach (var f in flags) GameScores.SetFlag(f);
        }

        private void HandleCardUnlocked(string cardId)
        {
            if (cardId == "homecoming") GameScores.SetFlag("homecoming_seen");
        }

        private void HandleDiscovered(string speciesId)
        {
            GameScores.AddKnowledge(1);
            if (SpeciesFlags.TryGetValue(speciesId, out var flag)) GameScores.SetFlag(flag);
        }
    }
}
