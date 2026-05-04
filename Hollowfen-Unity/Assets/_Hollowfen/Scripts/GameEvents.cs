using System;

namespace Hollowfen
{
    public static class GameEvents
    {
        public static event Action<string> OnAchievementTrigger;

        public static void TriggerAchievement(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId)) return;
            OnAchievementTrigger?.Invoke(achievementId);
        }
    }
}
