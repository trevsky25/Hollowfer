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
        [Tooltip("Dialog fires only if the purse holds at least this much copper (Voss's 144c). 0 = ignore.")]
        public int requiresCoinsCopper;
        [Tooltip("Dialog fires only while the forage basket is non-empty (Marra's sale loop).")]
        public bool requiresBasketNonEmpty;
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
        public DialogueData PickDialog()
        {
            if (_dialogueEntries != null)
            {
                for (int i = 0; i < _dialogueEntries.Length; i++)
                {
                    var e = _dialogueEntries[i];
                    if (e.dialog == null) continue;
                    if (e.requiresQuestCompleted != null && !QuestManager.IsCompleted(e.requiresQuestCompleted.Id)) continue;
                    if (e.activeQuest != null && !QuestManager.IsActive(e.activeQuest.Id)) continue;
                    if (!string.IsNullOrEmpty(e.requiresFlagId) && !GameScores.HasFlag(e.requiresFlagId)) continue;
                    if (e.requiresCoinsCopper > 0 && Items.CoinPurse.TotalCopper < e.requiresCoinsCopper) continue;
                    if (e.requiresBasketNonEmpty && Foraging.InventoryRuntime.TotalCount <= 0) continue;
                    return e.dialog;
                }
            }
            return _repeatDialog;
        }
    }
}
