using UnityEngine;

namespace Hollowfen.Quests
{
    // Drop on a GameObject with a trigger collider. When the player tagged "Player" enters,
    // if the quest matched in _completesQuestIfActive is currently active, complete it.
    // Optional: also start a quest if no quest is currently active and this is the first one
    // (Mission 1 "arrive" use case — entering the village square auto-starts Act I).
    [RequireComponent(typeof(Collider))]
    public class QuestTrigger : MonoBehaviour
    {
        [SerializeField] private string _playerTag = "Player";
        [SerializeField, Tooltip("Quest started when player enters IF no quest is active. Leave null to disable auto-start.")]
        private QuestData _autoStartIfNoneActive;
        [SerializeField, Tooltip("Quest completed when player enters AND it's the currently active quest.")]
        private QuestData _completesQuestIfActive;
        [SerializeField] private bool _fireOnce = true;

        private bool _fired;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_fired && _fireOnce) return;
            if (!other.CompareTag(_playerTag)) return;

            // Auto-start
            if (_autoStartIfNoneActive != null
                && QuestManager.ActiveQuest == null
                && !QuestManager.IsCompleted(_autoStartIfNoneActive.Id))
            {
                QuestManager.StartQuest(_autoStartIfNoneActive);
            }

            // Complete-on-enter
            if (_completesQuestIfActive != null
                && QuestManager.IsActive(_completesQuestIfActive.Id))
            {
                QuestManager.CompleteQuest(_completesQuestIfActive.Id);
                _fired = true;
            }
        }
    }
}
