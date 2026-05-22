using UnityEngine;

namespace Hollowfen.Quests
{
    // One-shot scene bootstrapper. On Start(), if no quest is active and the configured initial
    // quest isn't already completed, start it. Used to kick off Act I when Scene_Hollowfen loads.
    public class QuestBootstrap : MonoBehaviour
    {
        [SerializeField] private QuestData _initialQuest;
        [SerializeField] private float _startDelaySeconds = 0.25f;

        private void Start()
        {
            if (_initialQuest == null) return;
            if (QuestManager.ActiveQuest != null) return;
            if (QuestManager.IsCompleted(_initialQuest.Id)) return;
            QuestManager.StartQuest(_initialQuest);
        }
    }
}
