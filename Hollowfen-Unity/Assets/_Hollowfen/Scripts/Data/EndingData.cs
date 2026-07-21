using Hollowfen.Dialogue;
using UnityEngine;

namespace Hollowfen.Data
{
    [CreateAssetMenu(fileName = "Ending_New", menuName = "Hollowfen/Story/Ending")]
    public sealed class EndingData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id;
        [SerializeField] private string _endingFlagId;
        [SerializeField] private string _choiceText;
        [SerializeField, TextArea(2, 4)] private string _choiceContext;
        [SerializeField, TextArea(2, 4)] private string _lockedHint;

        [Header("Eligibility")]
        [SerializeField, Min(0)] private int _minimumVillageHope;
        [SerializeField, Min(0)] private int _minimumKnowledge;
        [SerializeField] private string[] _requiredFlagIds;
        [SerializeField] private string[] _relationshipNpcIds;
        [SerializeField] private int[] _minimumRelationshipValues;

        [Header("Resolution")]
        [SerializeField] private DialogueData _resolutionDialogue;
        [SerializeField] private StoryCardData _storyCard;
        [SerializeField, TextArea(2, 8)] private string[] _epilogueCaptions;
        [SerializeField] private string[] _consequenceFlagIds;
        [SerializeField] private string _achievementId;

        public string Id => _id;
        public string EndingFlagId => _endingFlagId;
        public string ChoiceText => _choiceText;
        public string ChoiceContext => _choiceContext;
        public string LockedHint => _lockedHint;
        public int MinimumVillageHope => _minimumVillageHope;
        public int MinimumKnowledge => _minimumKnowledge;
        public string[] RequiredFlagIds => _requiredFlagIds;
        public string[] RelationshipNpcIds => _relationshipNpcIds;
        public int[] MinimumRelationshipValues => _minimumRelationshipValues;
        public DialogueData ResolutionDialogue => _resolutionDialogue;
        public StoryCardData StoryCard => _storyCard;
        public string[] EpilogueCaptions => _epilogueCaptions;
        public string[] ConsequenceFlagIds => _consequenceFlagIds;
        public string AchievementId => _achievementId;
    }
}
