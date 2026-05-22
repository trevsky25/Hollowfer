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
                    return e.dialog;
                }
            }
            return _repeatDialog;
        }
    }
}
