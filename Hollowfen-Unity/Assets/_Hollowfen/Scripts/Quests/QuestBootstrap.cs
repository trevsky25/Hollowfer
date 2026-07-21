using System.Collections;
using Hollowfen.Data;
using Hollowfen.UI;
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
        [SerializeField, Tooltip("The four canonical endings, used to resume an interrupted finale after load.")]
        private EndingData[] _endings;

        private void Start()
        {
            if (_initialQuest != null && QuestManager.ActiveQuest == null)
            {
                var quest = _initialQuest;
                int guard = 0;
                while (quest != null && QuestManager.IsCompleted(quest.Id) && guard++ < 64)
                    quest = quest.NextQuest;

                if (quest != null) QuestManager.StartQuest(quest);
            }

            if (EndingResolver.TryGetPendingPresentation(_endings, out _))
                StartCoroutine(ResumeCommittedEnding());
        }

        private IEnumerator ResumeCommittedEnding()
        {
            // Let scene UI, dialogue, and loading overlays settle before presentation takes
            // input/time ownership. Re-check after the delay in case the slot changed meanwhile.
            if (_startDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(_startDelaySeconds);
            else
                yield return null;

            if (EndingResolver.TryGetPendingPresentation(_endings, out var ending))
                EndingDirector.Ensure().ResumeCommitted(ending);
        }
    }
}
