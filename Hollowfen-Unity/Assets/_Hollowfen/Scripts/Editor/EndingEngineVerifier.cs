#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hollowfen.Data;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using Hollowfen.Save;
using UnityEditor;
using UnityEngine;

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
            VerifySceneBootstrap(endings);

            int originalSlot = SaveManager.ActiveSlot;
            SaveManager.SetActiveSlot(3);
            try
            {
                VerifyGates(endings);
                VerifyRecoverableDecision();
                VerifyQuestCheckpointOrdering();
                foreach (var ending in endings) VerifyResolution(endings, ending);
                VerifyAtomicRecovery();
            }
            finally
            {
                SaveManager.SetActiveSlot(originalSlot);
            }

            return "ENDING ENGINE — PASS: four scene-wired endings, fallback + threshold gates, settled quest checkpoints, post-quest fork recovery, resumable presentation for all 4 endings, achievements, card isolation, disk round-trip, acknowledgement, second-choice refusal, atomic backup recovery";
        }

        private static void VerifySceneBootstrap(EndingData[] endings)
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<QuestBootstrap>(FindObjectsInactive.Include);
            Require(bootstrap != null, "loaded gameplay scene has no QuestBootstrap");

            var serialized = new SerializedObject(bootstrap);
            var references = serialized.FindProperty("_endings");
            Require(references != null && references.arraySize == endings.Length,
                "QuestBootstrap is not wired to all four endings");

            var expected = new HashSet<EndingData>(endings);
            for (int i = 0; i < references.arraySize; i++)
            {
                var ending = references.GetArrayElementAtIndex(i).objectReferenceValue as EndingData;
                Require(ending != null && expected.Remove(ending),
                    "QuestBootstrap contains a null or duplicate ending reference");
            }
            Require(expected.Count == 0, "QuestBootstrap is missing a canonical ending reference");
        }

        private static void VerifyQuestCheckpointOrdering()
        {
            var act1Final = AssetDatabase.LoadAssetAtPath<QuestData>(
                "Assets/_Hollowfen/Data/Quests/Quest_Act1_07_MeetAlmy.asset");
            var act2First = AssetDatabase.LoadAssetAtPath<QuestData>(
                "Assets/_Hollowfen/Data/Quests/Quest_Act2_08_AlmyTeach.asset");
            var terminal = AssetDatabase.LoadAssetAtPath<QuestData>(
                "Assets/_Hollowfen/Data/Quests/Quest_Act4_26_MeetAldric.asset");
            Require(act1Final != null && act2First != null && terminal != null,
                "checkpoint quest assets are missing");

            SaveManager.WriteSlot(3, new SaveSlotMeta());
            GameScores.HydrateFrom(new SaveSlotMeta());
            QuestManager.ResetForSlotSwitch();
            QuestManager.HydrateFrom(Array.Empty<string>(), Array.Empty<string>());

            QuestData observedActive = null;
            Action<QuestData> checkpoint = _ =>
            {
                observedActive = QuestManager.ActiveQuest;
                SaveCoordinator.SaveAll();
            };
            QuestManager.QuestCompleted += checkpoint;
            try
            {
                QuestManager.StartQuest(act1Final);
                QuestManager.CompleteQuest(act1Final.Id);
                Require(observedActive == act2First,
                    "QuestCompleted listeners still observe the completed Act-I quest");
                var boundaryDisk = SaveManager.GetSlotMeta(3);
                Require(boundaryDisk != null && boundaryDisk.CurrentQuestId == act2First.Id &&
                        boundaryDisk.CurrentAct == 2,
                    "Act-I to Act-II checkpoint did not persist the next quest identity");
            }
            finally
            {
                QuestManager.QuestCompleted -= checkpoint;
            }

            SaveManager.WriteSlot(3, new SaveSlotMeta());
            GameScores.HydrateFrom(new SaveSlotMeta());
            QuestManager.ResetForSlotSwitch();
            QuestManager.HydrateFrom(Array.Empty<string>(), Array.Empty<string>());
            checkpoint = _ => SaveCoordinator.SaveAll();
            QuestManager.QuestCompleted += checkpoint;
            try
            {
                QuestManager.StartQuest(terminal);
                QuestManager.CompleteQuest(terminal.Id);
                var terminalDisk = SaveManager.GetSlotMeta(3);
                Require(terminalDisk != null && terminalDisk.CurrentQuestId == "final_choice_available" &&
                        terminalDisk.CurrentAct == 4,
                    "terminal checkpoint is not labelled as the pending final decision");
            }
            finally
            {
                QuestManager.QuestCompleted -= checkpoint;
            }
        }

        private static void VerifyRecoverableDecision()
        {
            var aldric = AssetDatabase.LoadAssetAtPath<NPCData>(
                "Assets/_Hollowfen/Data/NPCs/NPC_Aldric.asset");
            var meeting = AssetDatabase.LoadAssetAtPath<Dialogue.DialogueData>(
                "Assets/_Hollowfen/Data/Dialogue/Dialogue_Act4_MeetAldric.asset");
            Require(aldric != null && meeting != null, "Aldric recovery assets are missing");

            var pending = new SaveSlotMeta
            {
                CurrentQuestId = "meetAldric",
                CurrentAct = 4,
                CompletedQuestIds = new[] { "meetAldric" },
                UnlockedStoryCardIds = Array.Empty<string>(),
                GameFlagIds = new[] { "aldric_meeting_started", "final_choice_available" },
            };
            SaveManager.WriteSlot(3, pending);
            SaveCoordinator.LoadSlot(3);
            Require(aldric.PickDialog() == meeting,
                "reloaded post-quest state cannot reopen Aldric's ending fork");

            pending.GameFlagIds = new[]
            {
                "aldric_meeting_started", "final_choice_available", "ending_lordly_patronage", "game_complete"
            };
            GameScores.HydrateFrom(pending);
            QuestManager.HydrateFrom(pending.CompletedQuestIds, pending.UnlockedStoryCardIds);
            Require(aldric.PickDialog() == null,
                "Aldric recovery fork remains interactable after game completion");
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
                Require(EndingResolver.TryGetPendingPresentation(endings, out var pending) && pending == chosen,
                    chosen.Id + " cannot recover its interrupted presentation after reload");
                Require(EndingResolver.ReconcileCommittedEnding(pending),
                    chosen.Id + " could not reconcile its committed presentation");
                Require(endingAchievementCount == 2,
                    chosen.Id + " recovery did not idempotently re-request its achievement");
                Require(EndingResolver.MarkPresentationSeen(),
                    chosen.Id + " could not persist presentation acknowledgement");
                Require(!EndingResolver.TryGetPendingPresentation(endings, out _),
                    chosen.Id + " still replays after acknowledgement");
                var acknowledged = SaveManager.GetSlotMeta(3);
                Require(acknowledged != null && acknowledged.GameFlagIds != null &&
                        acknowledged.GameFlagIds.Contains(EndingResolver.PresentationSeenFlag),
                    chosen.Id + " presentation acknowledgement did not reach disk");
            }
            finally
            {
                GameEvents.OnAchievementTrigger -= achievement;
            }
        }

        private static void VerifyAtomicRecovery()
        {
            var first = new SaveSlotMeta { CurrentQuestId = "atomic-a", TimestampUnix = 1 };
            var second = new SaveSlotMeta { CurrentQuestId = "atomic-b", TimestampUnix = 2 };
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
