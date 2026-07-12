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
        [SerializeField, Tooltip("Coins granted on finish, in total copper (12c = 1s). 0 = none.")]
        private int _grantCoinsCopper;
        [SerializeField, Tooltip("Coins SPENT on finish, in total copper (Joren's commission, Voss's tax). Best-effort TrySpend.")]
        private int _spendsCoinsCopper;
        [SerializeField, Tooltip("Empties Wren's mushroom basket on finish (firstSale: Marra takes the lot for the pot).")]
        private bool _sellsForageBasket;
        [SerializeField, Tooltip("With Sells Forage Basket: copper paid PER mushroom in the basket (repeatable Marra sale loop).")]
        private int _basketCopperPerItem;
        [SerializeField, Tooltip("Forage granted on finish (Almy's Wood Ear spawn plugs). Null = none.")]
        private MushroomFieldGuideData _grantForage;
        [SerializeField] private int _grantForageCount = 1;
        [SerializeField, Tooltip("Forage CONSUMED from the basket on finish (Marra's Brightspore tonic). Best-effort remove; gate availability with the NPC entry's Requires Forage.")]
        private MushroomFieldGuideData _consumeForage;
        [SerializeField] private int _consumeForageCount = 1;
        [SerializeField, Tooltip("Game flags set on finish (story.md flag tables, e.g. voss_first_visit_seen).")]
        private string[] _setFlagIds;

        [Header("Score deltas (story.md relationship tables)")]
        [SerializeField] private int _villageHopeDelta;
        [SerializeField] private int _knowledgeDelta;
        [SerializeField, Tooltip("NPC ids, parallel with the deltas array (e.g. bram, marra).")]
        private string[] _relationshipNpcIds;
        [SerializeField] private int[] _relationshipDeltas;

        [SerializeField] private DialogueData _nextDialog;

        public string Id => _id;
        public DialogueLine[] Lines => _lines;
        public StoryCardData UnlockStoryCard => _unlockStoryCard;
        public QuestData CompleteQuest => _completeQuest;
        public string GiveItemId => _giveItemId;
        public int GrantCoinsCopper => _grantCoinsCopper;
        public int SpendsCoinsCopper => _spendsCoinsCopper;
        public bool SellsForageBasket => _sellsForageBasket;
        public int BasketCopperPerItem => _basketCopperPerItem;
        public MushroomFieldGuideData GrantForage => _grantForage;
        public int GrantForageCount => _grantForageCount;
        public MushroomFieldGuideData ConsumeForage => _consumeForage;
        public int ConsumeForageCount => _consumeForageCount;
        public string[] SetFlagIds => _setFlagIds;
        public int VillageHopeDelta => _villageHopeDelta;
        public int KnowledgeDelta => _knowledgeDelta;
        public string[] RelationshipNpcIds => _relationshipNpcIds;
        public int[] RelationshipDeltas => _relationshipDeltas;
        public DialogueData NextDialog => _nextDialog;
    }
}
