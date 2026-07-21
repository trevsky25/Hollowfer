using System;
using Hollowfen.Save;

namespace Hollowfen
{
    public static class GameEvents
    {
        public static event Action<string> OnAchievementTrigger;

        public static void TriggerAchievement(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId)) return;
            var handlers = OnAchievementTrigger;
            if (handlers == null) return;
            foreach (Action<string> handler in handlers.GetInvocationList())
            {
                var callback = handler;
                SaveManager.PublishAfterAtomicCommit(() => callback(achievementId));
            }
        }
    }
}
