using Hollowfen.Data;
using Hollowfen.Dialogue;
using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.Requests
{
    public enum VillageRequestKind
    {
        Kitchen = 0,
        Medicine = 1,
        Market = 2,
        Gathering = 3,
    }

    [CreateAssetMenu(fileName = "Request_New", menuName = "Hollowfen/Requests/Village Request")]
    public sealed class VillageRequestData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _npcId;
        [SerializeField] private VillageRequestKind _kind;
        [SerializeField] private string _titleId;
        [SerializeField] private string _descriptionId;
        [SerializeField] private string _requesterLineId;
        [SerializeField] private Sprite _heroImage;

        [Header("Delivery")]
        [SerializeField] private MushroomFieldGuideData[] _requiredSpecies;
        [SerializeField] private int[] _requiredCounts;
        [SerializeField, Min(0)] private int _rewardCopper;
        [SerializeField, Tooltip("Applied only the first time this authored request is completed.")]
        private int _firstCompletionRelationshipDelta;
        [SerializeField, Tooltip("Applied only the first time this authored request is completed.")]
        private int _firstCompletionKnowledgeDelta;

        [Header("Availability")]
        [SerializeField] private string[] _requiredFlagIds;
        [SerializeField] private string[] _requiredCompletedQuestIds;
        [SerializeField, Tooltip("When populated, this request is available only while that quest is active.")]
        private string _activeQuestId;
        [SerializeField, Tooltip("One-shot story request; recurring requests rotate by game day.")]
        private bool _oneShot;

        [Header("On delivery")]
        [SerializeField] private string[] _completionFlagIds;
        [SerializeField, Tooltip("Optional story quest completed atomically with delivery.")]
        private QuestData _completeQuest;
        [SerializeField, Tooltip("Optional authored dialogue presented after the delivery has safely committed.")]
        private DialogueData _completionDialogue;

        public string Id => _id;
        public string NpcId => _npcId;
        public VillageRequestKind Kind => _kind;
        public string TitleId => _titleId;
        public string DescriptionId => _descriptionId;
        public string RequesterLineId => _requesterLineId;
        public Sprite HeroImage => _heroImage;
        public MushroomFieldGuideData[] RequiredSpecies => _requiredSpecies;
        public int[] RequiredCounts => _requiredCounts;
        public int RewardCopper => Mathf.Max(0, _rewardCopper);
        public int FirstCompletionRelationshipDelta => _firstCompletionRelationshipDelta;
        public int FirstCompletionKnowledgeDelta => _firstCompletionKnowledgeDelta;
        public string[] RequiredFlagIds => _requiredFlagIds;
        public string[] RequiredCompletedQuestIds => _requiredCompletedQuestIds;
        public string ActiveQuestId => _activeQuestId;
        public bool OneShot => _oneShot;
        public string[] CompletionFlagIds => _completionFlagIds;
        public QuestData CompleteQuest => _completeQuest;
        public DialogueData CompletionDialogue => _completionDialogue;

        public int RequirementCount => Mathf.Min(_requiredSpecies?.Length ?? 0, _requiredCounts?.Length ?? 0);

        public int RequiredCountAt(int index) =>
            index >= 0 && index < RequirementCount ? Mathf.Max(1, _requiredCounts[index]) : 0;
    }
}
