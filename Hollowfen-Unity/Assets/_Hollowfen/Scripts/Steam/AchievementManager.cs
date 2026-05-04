using UnityEngine;

namespace Hollowfen.Steam
{
    // Stub. Real Steamworks SDK wiring happens in a later session.
    // For now: subscribe to GameEvents.OnAchievementTrigger and log.
    public static class AchievementManager
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            GameEvents.OnAchievementTrigger -= OnAchievement;
            GameEvents.OnAchievementTrigger += OnAchievement;
        }

        private static void OnAchievement(string achievementId)
        {
            Debug.Log($"[Achievement] {achievementId}");
        }
    }
}
