using System;
using Hollowfen.Dialogue;
using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.NPCs
{
    // ScriptableObject describing a single NPC + which dialog they have available based on
    // the player's quest progression. Mirrors the per-NPC picker pattern from
    // docs/dialog-system.md (e.g. pickDialogForBram): walk the entries in order, first match
    // wins. Fallback to _repeatDialog if nothing matches (idle "hello" lines after their arc).
    [Serializable]
    public struct NPCDialogueEntry
    {
        [Tooltip("Dialog fires when this quest is active. Leave null to match any active quest.")]
        public QuestData activeQuest;
        [Tooltip("Dialog fires only AFTER this quest is completed (chained progression). Leave null to ignore.")]
        public QuestData requiresQuestCompleted;
        [Tooltip("Dialog fires only while this game flag is set (e.g. knife_ready). Empty = ignore.")]
        public string requiresFlagId;
        [Tooltip("Dialog is unavailable while this game flag is set (e.g. game_complete). Empty = ignore.")]
        public string blockedByFlagId;
        [Tooltip("Optional personal moment only: do not replace dialogue owned by the active story objective.")]
        public bool requiresNoActiveQuest;
        [Tooltip("Optional spatial beat: the NPC must currently occupy this exact authored schedule slot.")]
        public string requiresScheduleSlotLabel;
        [Tooltip("Optional paired beat: the named partner must also be present in their required schedule slot.")]
        public string requiresPartnerNpcId;
        [Tooltip("Exact schedule slot required of the paired NPC. Empty uses this entry's own slot label.")]
        public string requiresPartnerScheduleSlotLabel;
        [Tooltip("This NPC must remember the named moment. Empty = ignore.")]
        public string requiresMemoryId;
        [Tooltip("This NPC must not yet remember the named moment. Empty = ignore.")]
        public string blockedByMemoryId;
        [Tooltip("Optional personal favor chain id. Empty = ignore stage limits.")]
        public string favorId;
        [Min(0)] public int minimumFavorStage;
        [Tooltip("0 means no upper bound; otherwise this is inclusive.")]
        [Min(0)] public int maximumFavorStage;
        public bool usesMinimumRelationship;
        public int minimumRelationship;
        public bool usesMaximumRelationship;
        public int maximumRelationship;
        [Tooltip("Dialog fires only if the purse holds at least this much copper (Voss's 144c). 0 = ignore.")]
        public int requiresCoinsCopper;
        [Tooltip("Dialog fires only while the forage basket is non-empty (Marra's sale loop).")]
        public bool requiresBasketNonEmpty;
        [Tooltip("Dialog fires only while the basket holds at least one of this species (Marra's Brightspore tonic). Null = ignore.")]
        public Data.MushroomFieldGuideData requiresForage;
        public DialogueData dialog;
    }

    [CreateAssetMenu(fileName = "NPC_New", menuName = "Hollowfen/NPCs/NPC Data")]
    public class NPCData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameId;
        [SerializeField] private NPCDialogueEntry[] _dialogueEntries;
        [SerializeField, Tooltip("Fallback dialog when no entry matches.")]
        private DialogueData _repeatDialog;

        public string Id => _id;
        public string DisplayNameId => _displayNameId;
        public DialogueData RepeatDialog => _repeatDialog;

        // Walk the entries in author order, return the first whose conditions all pass.
        public DialogueData PickDialog(string currentScheduleSlot = null)
        {
            // Quest ownership is semantic, not an importer-order convention. Personal moments
            // may be prepended later without ever masking the NPC's current story objective.
            var active = PickEntryDialog(true, currentScheduleSlot);
            if (active != null) return active;
            var entry = PickEntryDialog(false, currentScheduleSlot);
            return entry != null ? entry : _repeatDialog;
        }

        // Used by NPCInteractable to keep authored story beats ahead of ambient village work.
        // A story-linked request may explicitly take over its own active quest after its intro.
        public DialogueData PickActiveQuestDialog(string currentScheduleSlot = null) =>
            PickEntryDialog(true, currentScheduleSlot);

        private DialogueData PickEntryDialog(bool activeQuestEntriesOnly, string currentScheduleSlot)
        {
            if (_dialogueEntries != null)
            {
                for (int i = 0; i < _dialogueEntries.Length; i++)
                {
                    var e = _dialogueEntries[i];
                    if (e.dialog == null) continue;
                    if (activeQuestEntriesOnly && e.activeQuest == null) continue;
                    if (e.requiresQuestCompleted != null && !QuestManager.IsCompleted(e.requiresQuestCompleted.Id)) continue;
                    if (e.activeQuest != null && !QuestManager.IsActive(e.activeQuest.Id)) continue;
                    if (!string.IsNullOrEmpty(e.requiresFlagId) && !GameScores.HasFlag(e.requiresFlagId)) continue;
                    if (!string.IsNullOrEmpty(e.blockedByFlagId) && GameScores.HasFlag(e.blockedByFlagId)) continue;
                    if (e.requiresNoActiveQuest && QuestManager.ActiveQuest != null) continue;
                    if (!string.IsNullOrEmpty(e.requiresScheduleSlotLabel) &&
                        !string.Equals(e.requiresScheduleSlotLabel, currentScheduleSlot,
                            StringComparison.Ordinal)) continue;
                    if (!string.IsNullOrEmpty(e.requiresPartnerNpcId))
                    {
                        NPCSchedule partner = NPCSchedule.ForNpcId(e.requiresPartnerNpcId);
                        string requiredPartnerSlot = string.IsNullOrEmpty(
                            e.requiresPartnerScheduleSlotLabel)
                                ? e.requiresScheduleSlotLabel
                                : e.requiresPartnerScheduleSlotLabel;
                        if (partner == null || string.IsNullOrEmpty(requiredPartnerSlot) ||
                            !string.Equals(partner.CurrentSlotLabel, requiredPartnerSlot,
                                StringComparison.Ordinal)) continue;
                    }
                    if (!string.IsNullOrEmpty(e.requiresMemoryId) &&
                        !VillagerRelationships.HasMemory(_id, e.requiresMemoryId)) continue;
                    if (!string.IsNullOrEmpty(e.blockedByMemoryId) &&
                        VillagerRelationships.HasMemory(_id, e.blockedByMemoryId)) continue;
                    if (!string.IsNullOrEmpty(e.favorId))
                    {
                        int stage = VillagerRelationships.FavorStage(e.favorId);
                        if (stage < Mathf.Max(0, e.minimumFavorStage)) continue;
                        if (e.maximumFavorStage > 0 && stage > e.maximumFavorStage) continue;
                    }
                    int relationship = GameScores.GetRelationship(_id);
                    if (e.usesMinimumRelationship && relationship < e.minimumRelationship) continue;
                    if (e.usesMaximumRelationship && relationship > e.maximumRelationship) continue;
                    if (e.requiresCoinsCopper > 0 && Items.CoinPurse.TotalCopper < e.requiresCoinsCopper) continue;
                    if (e.requiresBasketNonEmpty && Foraging.InventoryRuntime.TotalCount <= 0) continue;
                    if (e.requiresForage != null && Foraging.InventoryRuntime.GetCount(e.requiresForage) <= 0) continue;
                    return e.dialog;
                }
            }
            return null;
        }
    }
}
