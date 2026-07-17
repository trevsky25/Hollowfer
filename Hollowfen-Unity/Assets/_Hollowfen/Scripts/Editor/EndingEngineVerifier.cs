#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hollowfen.Data;
using Hollowfen.Quests;
using Hollowfen.Save;
using UnityEditor;

namespace Hollowfen.EditorTools
{
    /// <summary>State-mutating verification harness. Back up the save directory before running.</summary>
    public static class EndingEngineVerifier
    {
        public static string RunAll()
        {
            var endings = AssetDatabase.FindAssets("t:EndingData", new[] { "Assets/_Hollowfen/Data/Endings" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<EndingData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(ending => ending != null)
                .OrderBy(ending => ending.Id)
                .ToArray();
            Require(endings.Length == 4, "expected four ending assets");

            int originalSlot = SaveManager.ActiveSlot;
            SaveManager.SetActiveSlot(3);
            try
            {
                VerifyGates(endings);
                foreach (var ending in endings) VerifyResolution(endings, ending);
                VerifyAtomicRecovery();
            }
            finally
            {
                SaveManager.SetActiveSlot(originalSlot);
            }

            return "ENDING ENGINE — PASS: fallback + threshold gates, all 4 exclusive commits, achievements, card isolation, disk round-trip, second-choice refusal, atomic backup recovery";
        }

        private static void VerifyGates(EndingData[] endings)
        {
            var fallback = endings.Single(ending => ending.Id == "lordly_patronage");
            GameScores.HydrateFrom(new SaveSlotMeta { GameFlagIds = new[] { "final_choice_available" } });
            foreach (var ending in endings)
                Require(EndingResolver.Evaluate(ending).IsEligible == (ending == fallback),
                    "only patronage should be eligible at the fallback state: " + ending.Id);

            var free = endings.Single(ending => ending.Id == "free_hollow");
            var freeMeta = QualifyingMeta(free);
            freeMeta.VillageHope = free.MinimumVillageHope - 1;
            GameScores.HydrateFrom(freeMeta);
            Require(!EndingResolver.Evaluate(free).IsEligible, "Free Hollow gate accepted Hope below threshold");
            freeMeta.VillageHope++;
            GameScores.HydrateFrom(freeMeta);
            Require(EndingResolver.Evaluate(free).IsEligible, "Free Hollow rejected its exact Hope threshold");

            var capital = endings.Single(ending => ending.Id == "capital");
            var capitalMeta = QualifyingMeta(capital);
            capitalMeta.RelationshipValues[0] = capital.MinimumRelationshipValues[0] - 1;
            GameScores.HydrateFrom(capitalMeta);
            Require(!EndingResolver.Evaluate(capital).IsEligible, "Capital gate accepted Theo below threshold");
            capitalMeta.RelationshipValues[0]++;
            GameScores.HydrateFrom(capitalMeta);
            Require(EndingResolver.Evaluate(capital).IsEligible, "Capital rejected its exact Theo threshold");

            var witch = endings.Single(ending => ending.Id == "witch_path");
            var witchMeta = QualifyingMeta(witch);
            witchMeta.Knowledge = witch.MinimumKnowledge - 1;
            GameScores.HydrateFrom(witchMeta);
            Require(!EndingResolver.Evaluate(witch).IsEligible, "Witch path accepted Knowledge below threshold");
            witchMeta.Knowledge++;
            GameScores.HydrateFrom(witchMeta);
            Require(EndingResolver.Evaluate(witch).IsEligible, "Witch path rejected its exact Knowledge threshold");
        }

        private static void VerifyResolution(EndingData[] endings, EndingData chosen)
        {
            GameScores.HydrateFrom(QualifyingMeta(chosen));
            QuestManager.ResetForSlotSwitch();
            QuestManager.HydrateFrom(Array.Empty<string>(), Array.Empty<string>());

            int endingAchievementCount = 0;
            Action<string> achievement = id =>
            {
                if (id == chosen.AchievementId) endingAchievementCount++;
            };
            GameEvents.OnAchievementTrigger += achievement;
            try
            {
                Require(EndingResolver.TryResolve(chosen, out var failure),
                    chosen.Id + " failed to resolve: " + failure);
                Require(GameScores.HasFlag("game_complete"), chosen.Id + " did not set game_complete");
                Require(GameScores.HasFlag(chosen.EndingFlagId), chosen.Id + " did not set its ending flag");
                Require(endings.Count(ending => GameScores.HasFlag(ending.EndingFlagId)) == 1,
                    chosen.Id + " committed more than one ending flag");
                Require(QuestManager.IsStoryCardUnlocked(chosen.StoryCard.Id),
                    chosen.Id + " did not unlock its story card");
                Require(endings.Where(ending => ending != chosen)
                        .All(ending => !QuestManager.IsStoryCardUnlocked(ending.StoryCard.Id)),
                    chosen.Id + " unlocked another ending's story card");
                Require(endingAchievementCount == 1, chosen.Id + " ending achievement did not fire exactly once");
                Require(!EndingResolver.TryResolve(endings.First(ending => ending != chosen), out _),
                    chosen.Id + " allowed a second ending commit");
                Require(endingAchievementCount == 1, chosen.Id + " achievement fired again after rejected commit");

                var disk = SaveManager.GetSlotMeta(3);
                Require(disk != null, chosen.Id + " wrote no save");
                Require(disk.CurrentQuestId == "game_complete" && disk.CurrentAct == 4,
                    chosen.Id + " did not persist the completed slot identity");
                Require(disk.GameFlagIds != null && disk.GameFlagIds.Contains("game_complete") &&
                        disk.GameFlagIds.Contains(chosen.EndingFlagId),
                    chosen.Id + " flags did not round-trip to disk");
                Require(disk.UnlockedStoryCardIds != null && disk.UnlockedStoryCardIds.Contains(chosen.StoryCard.Id),
                    chosen.Id + " story card did not round-trip to disk");

                SaveCoordinator.LoadSlot(3);
                Require(GameScores.HasFlag(chosen.EndingFlagId) && GameScores.HasFlag("game_complete"),
                    chosen.Id + " failed hydration after save reload");
                Require(QuestManager.IsStoryCardUnlocked(chosen.StoryCard.Id),
                    chosen.Id + " story card failed hydration after save reload");
            }
            finally
            {
                GameEvents.OnAchievementTrigger -= achievement;
            }
        }

        private static void VerifyAtomicRecovery()
        {
            var first = new SaveSlotMeta { CurrentQuest = "atomic-a", CurrentQuestId = "atomic-a", TimestampUnix = 1 };
            var second = new SaveSlotMeta { CurrentQuest = "atomic-b", CurrentQuestId = "atomic-b", TimestampUnix = 2 };
            SaveManager.WriteSlot(3, first);
            SaveManager.WriteSlot(3, second);
            Require(File.Exists(SaveManager.SlotPath(3) + ".bak"), "atomic replacement produced no backup");

            File.WriteAllText(SaveManager.SlotPath(3), "{ deliberately interrupted json");
            var recovered = SaveManager.GetSlotMeta(3);
            Require(recovered != null && recovered.CurrentQuestId == "atomic-a",
                "corrupt primary did not recover the previous complete snapshot");
        }

        private static SaveSlotMeta QualifyingMeta(EndingData ending)
        {
            return new SaveSlotMeta
            {
                VillageHope = ending.MinimumVillageHope,
                Knowledge = ending.MinimumKnowledge,
                GameFlagIds = ending.RequiredFlagIds != null ? ending.RequiredFlagIds.ToArray() : Array.Empty<string>(),
                RelationshipNpcIds = ending.RelationshipNpcIds != null ? ending.RelationshipNpcIds.ToArray() : Array.Empty<string>(),
                RelationshipValues = ending.MinimumRelationshipValues != null ? ending.MinimumRelationshipValues.ToArray() : Array.Empty<int>(),
                CompletedQuestIds = Array.Empty<string>(),
                UnlockedStoryCardIds = Array.Empty<string>(),
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[EndingEngineVerifier] " + message);
        }
    }
}
#endif
