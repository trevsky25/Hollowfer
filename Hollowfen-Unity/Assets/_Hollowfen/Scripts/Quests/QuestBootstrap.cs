using UnityEngine;

namespace Hollowfen.Quests
{
    // One-shot scene bootstrapper. On Start(), if no quest is active, walk the quest chain
    // from the configured initial quest to the first uncompleted entry and start it — so a
    // session resumed from the autosave picks up mid-chain instead of restarting Act I.
    public class QuestBootstrap : MonoBehaviour
    {
        [SerializeField] private QuestData _initialQuest;
        [SerializeField] private float _startDelaySeconds = 0.25f;

        private void Start()
        {
            if (_initialQuest == null) return;
            if (QuestManager.ActiveQuest != null) return;

            var quest = _initialQuest;
            int guard = 0;
            while (quest != null && QuestManager.IsCompleted(quest.Id) && guard++ < 64)
                quest = quest.NextQuest;

            if (quest != null) QuestManager.StartQuest(quest);
        }
    }
}
