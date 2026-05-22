using System;
using Hollowfen.Data;
using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.Dialogue
{
    // Matches the data shape documented in docs/dialog-system.md. Consecutive same-speaker lines
    // are MERGED into a single entry with "\n\n" between sentences (don't make the player advance
    // twice for one speaker). The `isCloseup` flag marks emotional climax beats for the future
    // cinematic camera pass.
    [Serializable]
    public struct DialogueLine
    {
        public string speaker;
        [TextArea(2, 6)] public string text;
        public bool isCloseup;
    }

    [CreateAssetMenu(fileName = "Dialogue_New", menuName = "Hollowfen/Dialogue/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private DialogueLine[] _lines;

        [Header("Outcome (fires when the dialog finishes)")]
        [SerializeField] private StoryCardData _unlockStoryCard;
        [SerializeField] private QuestData _completeQuest;
        [SerializeField] private string _giveItemId;
        [SerializeField] private DialogueData _nextDialog;

        public string Id => _id;
        public DialogueLine[] Lines => _lines;
        public StoryCardData UnlockStoryCard => _unlockStoryCard;
        public QuestData CompleteQuest => _completeQuest;
        public string GiveItemId => _giveItemId;
        public DialogueData NextDialog => _nextDialog;
    }
}
