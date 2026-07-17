using Hollowfen.Data;
using Hollowfen.Map;
using UnityEngine;

namespace Hollowfen.Quests
{
    // A single mission entry. Mirrors the rows in docs/story.md's Act mission tables (e.g. `arrive`,
    // `speakBram`, `firstForage`). Quests chain via _nextQuest; completing one auto-starts the next.
    // Optional waypoint hint sets the player's compass to the named POI when this quest is active.
    [CreateAssetMenu(fileName = "Quest_New", menuName = "Hollowfen/Quests/Quest Data")]
    public class QuestData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameId;
        [SerializeField] private string _objectiveTextId;
        [SerializeField, Range(1, 4)] private int _act = 1;
        [SerializeField] private int _order = 1;

        [Header("On Complete")]
        [SerializeField, Tooltip("Story card unlocked when this quest completes (optional).")]
        private StoryCardData _unlockStoryCardOnComplete;
        [SerializeField, Tooltip("Optional authored hero presentation. Null uses the compact story-card completion treatment.")]
        private StoryMomentData _storyMoment;
        [SerializeField, Tooltip("Quest auto-started when this one completes (optional).")]
        private QuestData _nextQuest;

        [Header("Hints")]
        [SerializeField, Tooltip("Location suggested as waypoint while this quest is active (optional).")]
        private LocationData _waypointLocation;

        [Header("Score deltas applied on completion (story.md tables)")]
        [SerializeField] private int _villageHopeDelta;
        [SerializeField] private int _knowledgeDelta;
        [SerializeField, Tooltip("NPC ids, parallel with the deltas array (e.g. bram, marra).")]
        private string[] _relationshipNpcIds;
        [SerializeField] private int[] _relationshipDeltas;

        public string Id => _id;
        public string DisplayNameId => _displayNameId;
        public string ObjectiveTextId => _objectiveTextId;
        public int Act => _act;
        public int Order => _order;
        public StoryCardData UnlockStoryCardOnComplete => _unlockStoryCardOnComplete;
        public StoryMomentData StoryMoment => _storyMoment;
        public QuestData NextQuest => _nextQuest;
        public LocationData WaypointLocation => _waypointLocation;
        public int VillageHopeDelta => _villageHopeDelta;
        public int KnowledgeDelta => _knowledgeDelta;
        public string[] RelationshipNpcIds => _relationshipNpcIds;
        public int[] RelationshipDeltas => _relationshipDeltas;
    }
}
