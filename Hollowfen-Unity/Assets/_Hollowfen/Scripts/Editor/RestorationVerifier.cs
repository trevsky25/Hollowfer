#if UNITY_EDITOR
using System;
using System.Linq;
using Hollowfen.GameTime;
using Hollowfen.Quests;
using Hollowfen.Restoration;
using Hollowfen.Save;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>State-mutating Play Mode coverage; automation must supply an isolated save directory.</summary>
    public static class RestorationVerifier
    {
        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            Require(!string.IsNullOrWhiteSpace(SaveManager.EditorSaveDirectoryOverride),
                "set SaveManager.EditorSaveDirectoryOverride to an isolated directory first");

            SaveCoordinator.StartNewGame(3);
            var project = Resources.Load<RestorationProjectDatabase>("RestorationProjectDatabase")?
                .Projects?.FirstOrDefault(item => item != null && item.Id == "cottages");
            Require(project != null, "cottages project is missing from the runtime database");
            VerifyAuthoredWorld(project);
            VerifyMonotonicRoundTrip(project);
            VerifyDawnReveal(project);
            VerifyLegacyMigration(project);
            VerifyNormalization();
            return "LIVING RESTORATION — PASS: 2 staged sites + village board, monotonic project stages, " +
                   "legacy flag migration, normalized parallel arrays, and full save/load round-trip";
        }

        private static void VerifyAuthoredWorld(RestorationProjectData project)
        {
            var sites = UnityEngine.Object.FindObjectsByType<RestorationSite>(FindObjectsInactive.Include);
            var boards = UnityEngine.Object.FindObjectsByType<RestorationBoard>(FindObjectsInactive.Include);
            var reveals = UnityEngine.Object.FindObjectsByType<RestorationRevealDirector>(FindObjectsInactive.Include);
            var projectReveals = reveals.Where(reveal => reveal.Project == project).ToArray();
            Require(sites.Count(site => site.Project == project) == 2,
                "cottages need exactly two authored restoration sites");
            Require(boards.Length == 1, "the village needs exactly one restoration board");
            Require(projectReveals.Length == 1 && projectReveals[0].FocusTarget != null,
                "the cottages need exactly one authored dawn reveal and focus target");
            Require(Vector3.Distance(projectReveals[0].FocusTarget.position,
                        new Vector3(213.7f, 39.6f, 305.5f)) < .2f &&
                    Vector3.Dot(projectReveals[0].FrameDirection.normalized,
                        new Vector3(-.44f, 0f, -.90f).normalized) > .99f,
                "the dawn reveal must frame the real cottage front from its clear southwestern approach");
            Require(sites.All(site => site.GetComponent<Collider>() != null &&
                                      site.gameObject.layer == LayerMask.NameToLayer("Foraging")),
                "a restoration site is missing its Foraging interaction trigger");
            Require(sites.SelectMany(site => site.GetComponentsInChildren<ParticleSystem>(true)).Count() >= 2,
                "each restored cottage needs authored chimney smoke");
            Require(sites.SelectMany(site => site.GetComponentsInChildren<Hollowfen.GameTime.NightLight>(true)).Count() >= 2,
                "each restored cottage needs an evening window light");
        }

        private static void VerifyDawnReveal(RestorationProjectData project)
        {
            var clock = TimeManager.Instance;
            var reveal = UnityEngine.Object.FindObjectsByType<RestorationRevealDirector>(
                    FindObjectsInactive.Include)
                .FirstOrDefault(candidate => candidate.Project == project);
            Require(clock != null && reveal != null, "the clock or restoration reveal is missing");
            int originalDay = clock.Day;
            float originalHour = clock.Hour;
            clock.SetTime(originalDay, 19f);
            GameScores.HydrateFrom(new SaveSlotMeta
            {
                GameFlagIds = new[] { "shutters_funded", "cottages_reopened_1" },
            });
            RestorationProjects.HydrateFrom(null);
            Require(RestorationProjects.GetStage(project) == RestorationStage.WorkUnderway,
                "dawn reveal fixture did not begin at WorkUnderway");

            int queuedBefore = reveal.QueuedCount;
            clock.AdvanceTo(originalDay + 1, 7f, true);
            Require(GameScores.HasFlag("cottages_reopened_2") &&
                    RestorationProjects.GetStage(project) == RestorationStage.Restored,
                "the overnight cottage promotion did not restore the second home");
            Require(reveal.QueuedCount == queuedBefore + 1 && reveal.IsPending &&
                    reveal.LastPromotionDay == originalDay + 1,
                "the real dawn promotion did not queue exactly one deferred reveal");
            reveal.CancelPending();
            int queuedAfterPromotion = reveal.QueuedCount;
            RestorationProjects.HydrateFrom(RestorationProjects.ToSnapshot());
            Require(reveal.QueuedCount == queuedAfterPromotion,
                "save hydration replayed the one-time dawn reveal");
            clock.SetTime(originalDay, originalHour);
        }

        private static void VerifyMonotonicRoundTrip(RestorationProjectData project)
        {
            RestorationProjects.HydrateFrom(null);
            Require(RestorationProjects.Advance(project, RestorationStage.Surveyed),
                "project did not advance to Surveyed");
            Require(RestorationProjects.Advance(project, RestorationStage.WorkUnderway),
                "project did not advance to WorkUnderway");
            Require(!RestorationProjects.Advance(project, RestorationStage.SuppliesCommitted) &&
                    RestorationProjects.GetStage(project) == RestorationStage.WorkUnderway,
                "project regressed to an earlier stage");
            var northLane = UnityEngine.Object.FindObjectsByType<RestorationSite>(FindObjectsInactive.Include)
                .FirstOrDefault(site => site.name == "RestorationSite_NorthLane");
            Require(northLane != null &&
                    northLane.transform.Find("WorkUnderway")?.gameObject.activeSelf == true &&
                    northLane.transform.Find("Surveyed")?.gameObject.activeSelf == false,
                "WorkUnderway did not switch the North Lane world presentation");

            SaveCoordinator.SaveAll();
            var disk = SaveManager.InspectSlot(3);
            Require(disk.CanLoad && SnapshotStage(disk.Meta.RestorationProjects, project.Id) ==
                    (int)RestorationStage.WorkUnderway,
                "WorkUnderway did not reach the save envelope");

            RestorationProjects.HydrateFrom(null);
            Require(SaveCoordinator.TryLoadSlot(3, out _) &&
                    RestorationProjects.GetStage(project) == RestorationStage.WorkUnderway,
                "restoration stage did not hydrate after a slot reload");
            Require(northLane.transform.Find("WorkUnderway")?.gameObject.activeSelf == true,
                "loaded WorkUnderway state did not restore its world presentation");
        }

        private static void VerifyLegacyMigration(RestorationProjectData project)
        {
            GameScores.HydrateFrom(new SaveSlotMeta
            {
                GameFlagIds = new[] { "shutters_funded", "cottages_reopened_1" },
            });
            RestorationProjects.HydrateFrom(null);
            Require(RestorationProjects.GetStage(project) == RestorationStage.WorkUnderway,
                "legacy first-cottage flags did not migrate to WorkUnderway");

            GameScores.SetFlag("cottages_reopened_2");
            Require(RestorationProjects.GetStage(project) == RestorationStage.Restored,
                "legacy second-cottage flag did not migrate to Restored");
            Require(RestorationProjects.Advance(project, RestorationStage.Occupied),
                "completed cottage record did not advance to Occupied");
            SaveCoordinator.SaveAll();
            RestorationProjects.HydrateFrom(null);
            Require(SaveCoordinator.TryLoadSlot(3, out _) &&
                    RestorationProjects.GetStage(project) == RestorationStage.Occupied,
                "Occupied did not survive a full save/load round-trip");
            var sites = UnityEngine.Object.FindObjectsByType<RestorationSite>(FindObjectsInactive.Include);
            Require(sites.Where(site => site.Project == project).All(site =>
                    site.transform.Find("Occupied")?.gameObject.activeSelf == true),
                "loaded Occupied state did not restore the occupied cottage presentation");
            var board = UnityEngine.Object.FindAnyObjectByType<RestorationBoard>(FindObjectsInactive.Include);
            Require(board != null && board.transform.Find("BoardVisual")?.gameObject.activeSelf == true,
                "loaded Occupied state did not restore the village board unlock");
        }

        private static void VerifyNormalization()
        {
            var meta = SaveManager.GetSlotMeta(3);
            Require(meta != null, "normalization fixture has no loadable save");
            meta.RestorationProjects = new RestorationSnapshot
            {
                ProjectIds = new[] { "cottages", "orphan" },
                Stages = new[] { 99, -7 },
                StartedDays = new[] { -3 },
                ChangedDays = new[] { -4, 12, 13 },
            };
            SaveManager.WriteSlot(3, meta);
            var normalized = SaveManager.InspectSlot(3).Meta.RestorationProjects;
            Require(normalized.ProjectIds.Length == 1 && normalized.Stages.Length == 1 &&
                    normalized.Stages[0] == (int)RestorationStage.Occupied &&
                    normalized.StartedDays[0] == 0 && normalized.ChangedDays[0] == 0,
                "restoration parallel arrays or values were not normalized");
        }

        private static int SnapshotStage(RestorationSnapshot snapshot, string projectId)
        {
            if (snapshot == null) return -1;
            int count = Mathf.Min(snapshot.ProjectIds?.Length ?? 0, snapshot.Stages?.Length ?? 0);
            for (int i = 0; i < count; i++)
                if (snapshot.ProjectIds[i] == projectId) return snapshot.Stages[i];
            return -1;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[RestorationVerifier] " + message);
        }
    }
}
#endif
