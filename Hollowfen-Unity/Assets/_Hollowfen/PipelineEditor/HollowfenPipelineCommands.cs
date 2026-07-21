#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor.Commands.Observability;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowfen.Pipeline
{
    /// <summary>
    /// First-class native Pipeline commands that wrap Hollowfen's existing production policy.
    /// This assembly is Editor-only and deliberately reaches game/editor types through a narrow
    /// reflection allowlist so the rest of Hollowfen never depends on the experimental package.
    /// </summary>
    public static class HollowfenPipelineCommands
    {
        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";
        private const string IsolationSessionKey = "Hollowfen.Pipeline.OwnedSaveIsolation";
        private const string SaveManagerType = "Hollowfen.Save.SaveManager";

        private static readonly VerifierSpec[] Verifiers =
        {
            new VerifierSpec("active-ui", "Hollowfen.EditorTools.ProductionUIVerifier",
                "VerifyActiveForAutomation", true, false, false),
            new VerifierSpec("apothecary-casework", "Hollowfen.EditorTools.ApothecaryCaseworkVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("apothecary-preparation", "Hollowfen.EditorTools.ApothecaryPreparationVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("bram-character", "Hollowfen.EditorTools.BramCharacterVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("bridge-restoration", "Hollowfen.EditorTools.BridgeRestorationVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("day-night-lighting", "Hollowfen.EditorTools.DayNightLightingVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("ending-engine", "Hollowfen.EditorTools.EndingEngineVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("gameplay-audio", "Hollowfen.EditorTools.GameplayAudioVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("gameplay-foundation", "Hollowfen.EditorTools.GameplayFoundationVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("inventory-transactions", "Hollowfen.EditorTools.InventoryTransactionVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("living-village-encounters", "Hollowfen.EditorTools.LivingVillageEncounterVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("mill-door-progression", "Hollowfen.EditorTools.MillDoorProgressionVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("music-playlist", "Hollowfen.EditorTools.MusicPlaylistVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("narrative-copy", "Hollowfen.EditorTools.NarrativeCopyVerifier",
                "RunAll", false, false, false),
            new VerifierSpec("npc-schedules", "Hollowfen.EditorTools.NPCScheduleVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("production-balance", "Hollowfen.EditorTools.ProductionBalanceVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("relationships", "Hollowfen.EditorTools.RelationshipSystemVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("restoration", "Hollowfen.EditorTools.RestorationVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("save-integrity", "Hollowfen.EditorTools.SaveIntegrityVerifier",
                "RunAll", false, false, false),
            new VerifierSpec("story-world-alignment", "Hollowfen.EditorTools.StoryWorldAlignmentVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("village-requests", "Hollowfen.EditorTools.VillageRequestVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("village-restoration-expansion",
                "Hollowfen.EditorTools.VillageRestorationExpansionVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("weather", "Hollowfen.EditorTools.WeatherSystemVerifier",
                "RunAll", true, true, true),
            new VerifierSpec("world-feedback", "Hollowfen.EditorTools.WorldFeedbackVerifier",
                "RunAll", true, true, true),
        };

        [CliCommand("hollowfen_health",
            "Read Hollowfen Editor, scene, console, package, and save-isolation health.")]
        public static object Health()
        {
            Scene active = SceneManager.GetActiveScene();
            Scene[] openScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .ToArray();
            IReadOnlyList<ConsoleLogEntryDto> logs = ConsoleLogBuffer.Snapshot();
            Type saveManager = FindType(SaveManagerType);
            string saveOverride = ReadStaticProperty(saveManager, "EditorSaveDirectoryOverride") as string;
            object activeSlotValue = ReadStaticProperty(saveManager, "ActiveSlot");
            int? activeSlot = activeSlotValue is int slot ? slot : null;
            string ownedIsolation = SessionState.GetString(IsolationSessionKey, string.Empty);

            UnityEditor.PackageManager.PackageInfo[] packages =
                UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            string pipelineVersion = packages.FirstOrDefault(package =>
                package.name == "com.unity.pipeline")?.version;
            string coplayVersion = packages.FirstOrDefault(package =>
                package.name == "com.coplaydev.unity-mcp")?.version;

            return new
            {
                status = ReadyStatus(),
                unityVersion = Application.unityVersion,
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                playMode = EditorApplication.isPaused ? "paused" :
                    EditorApplication.isPlaying ? "playing" : "stopped",
                compiling = EditorApplication.isCompiling,
                activeScene = new
                {
                    name = active.name,
                    path = active.path,
                    dirty = active.isDirty,
                },
                openSceneCount = openScenes.Length,
                dirtyScenes = openScenes.Where(scene => scene.isDirty)
                    .Select(scene => scene.path).ToArray(),
                console = new
                {
                    errors = logs.Count(log => IsError(log.Type)),
                    warnings = logs.Count(log => log.Type == "Warning"),
                    captured = logs.Count,
                },
                saves = new
                {
                    activeSlot,
                    overrideActive = !string.IsNullOrWhiteSpace(saveOverride),
                    ownedIsolation = IsSamePath(saveOverride, ownedIsolation),
                    isolationId = IsolationId(ownedIsolation),
                },
                packages = new
                {
                    pipeline = pipelineVersion,
                    coplay = coplayVersion,
                },
            };
        }

        [CliCommand("hollowfen_preflight",
            "Run Hollowfen's exact audit-build technical preflight and data-integrity report.")]
        public static object Preflight()
        {
            RequireStoppedAndReady("preflight");
            string audit = Convert.ToString(InvokeStatic(
                "Hollowfen.EditorTools.ProductionBuildGate",
                "ValidateAuditPreflightForAutomation"));
            string integrity = Convert.ToString(InvokeStatic(
                "Hollowfen.EditorTools.DataIntegrity",
                "RunAllAsReport"));
            string integritySummary = FirstLine(integrity);

            if (!audit.StartsWith("AUDIT PREFLIGHT — PASS", StringComparison.Ordinal) ||
                integritySummary != "DATA INTEGRITY — ERRORS=0 WARNINGS=0")
                throw new InvalidOperationException(
                    $"Hollowfen preflight rejected: {audit} · {integritySummary}");

            return new
            {
                status = "passed",
                audit,
                integritySummary,
                integrityReport = integrity,
            };
        }

        [CliCommand("hollowfen_verifier_catalog",
            "List the allowlisted Hollowfen verifiers and their required Editor state.")]
        public static object VerifierCatalog() => new
        {
            count = Verifiers.Length,
            verifiers = Verifiers.Select(spec => spec.Describe()).ToArray(),
        };

        [CliCommand("hollowfen_run_verifier",
            "Run one allowlisted Hollowfen verifier. Mutating verification requires confirm=true; use dry_run to validate state.")]
        public static object RunVerifier(
            [CliArg("name", "Allowlisted verifier name from hollowfen_verifier_catalog.", Required = true)]
            string name,
            [CliArg("confirm", "Acknowledge verifier state mutation and run it.")]
            bool confirm = false,
            [CliArg("dry_run", "Validate the verifier and Editor state without invoking it.")]
            bool dryRun = false)
        {
            VerifierSpec spec = Verifiers.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            if (spec == null)
                throw new ArgumentException(
                    $"Unknown Hollowfen verifier '{name}'. Use hollowfen_verifier_catalog.", nameof(name));

            string[] blockers = VerifierBlockers(spec).ToArray();
            if (dryRun)
            {
                return new
                {
                    status = "dry_run",
                    ready = blockers.Length == 0,
                    verifier = spec.Describe(),
                    blockers,
                };
            }

            if (!confirm)
                throw new ArgumentException(
                    "Refusing to run a state-mutating verifier without confirm=true. Use dry_run=true first.");
            if (blockers.Length > 0)
                throw new InvalidOperationException(
                    $"Verifier '{spec.Name}' is not ready: {string.Join("; ", blockers)}");

            object result = InvokeStatic(spec.TypeName, spec.MethodName);
            string report = result as string;
            if (string.IsNullOrWhiteSpace(report))
                throw new InvalidOperationException(
                    $"Verifier '{spec.Name}' did not return a synchronous report; refusing to infer success.");
            string summary = FirstLine(report);
            if (summary.IndexOf("FAIL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                summary.IndexOf("PASS", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException(
                    $"Verifier '{spec.Name}' rejected the run: {report}");

            return new
            {
                status = "passed",
                verifier = spec.Name,
                report,
            };
        }

        [CliCommand("hollowfen_begin_save_isolation",
            "Create and arm a Hollowfen-owned temporary save directory. Requires confirm=true.")]
        public static object BeginSaveIsolation(
            [CliArg("confirm", "Create and arm the isolated save directory.")]
            bool confirm = false,
            [CliArg("dry_run", "Validate without creating a directory or changing SaveManager.")]
            bool dryRun = false)
        {
            RequireStoppedAndReady("save isolation");
            Type saveManager = RequiredType(SaveManagerType);
            string currentOverride = ReadStaticProperty(saveManager, "EditorSaveDirectoryOverride") as string;
            string existingOwned = SessionState.GetString(IsolationSessionKey, string.Empty);

            if (!string.IsNullOrWhiteSpace(currentOverride) && !IsSamePath(currentOverride, existingOwned))
                throw new InvalidOperationException(
                    "SaveManager already has an override that this command does not own; refusing to replace it.");
            if (!string.IsNullOrWhiteSpace(existingOwned))
            {
                if (!IsUnderRoot(existingOwned, IsolationRoot()))
                    throw new InvalidOperationException(
                        "Recorded save isolation escaped its owned root; refusing to reuse it.");
                if (Directory.Exists(existingOwned) && IsSamePath(currentOverride, existingOwned))
                    return new
                    {
                        status = "already_active",
                        isolationId = IsolationId(existingOwned),
                    };
                if (Directory.Exists(existingOwned))
                    throw new InvalidOperationException(
                        "A recorded isolation directory is no longer active; run hollowfen_end_save_isolation before starting another.");
            }

            if (dryRun)
                return new
                {
                    status = "dry_run",
                    wouldCreate = "Library/HollowfenPipeline/isolated-saves/<id>",
                };
            if (!confirm)
                throw new ArgumentException(
                    "Refusing to create save isolation without confirm=true. Use dry_run=true first.");

            string id = Guid.NewGuid().ToString("N");
            string path = Path.Combine(IsolationRoot(), id);
            Directory.CreateDirectory(path);
            try
            {
                WriteStaticProperty(saveManager, "EditorSaveDirectoryOverride", path);
                InvokeStatic(saveManager, "EditorArmSaveDirectoryOverrideForNextPlay", path);
                SessionState.SetString(IsolationSessionKey, path);
            }
            catch (Exception setupError)
            {
                var cleanupErrors = new List<string>();
                try
                {
                    string failedOverride = ReadStaticProperty(saveManager,
                        "EditorSaveDirectoryOverride") as string;
                    if (IsSamePath(failedOverride, path))
                        InvokeStatic(saveManager, "EditorClearSaveDirectoryOverride");
                }
                catch (Exception cleanupError)
                {
                    cleanupErrors.Add("override cleanup: " + cleanupError.Message);
                }

                try
                {
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                }
                catch (Exception cleanupError)
                {
                    cleanupErrors.Add("directory cleanup: " + cleanupError.Message);
                }

                if (cleanupErrors.Count > 0)
                {
                    SessionState.SetString(IsolationSessionKey, path);
                    throw new InvalidOperationException(
                        "Save isolation setup failed and retained ownership for cleanup: " +
                        string.Join("; ", cleanupErrors), setupError);
                }
                throw;
            }

            return new
            {
                status = "armed",
                isolationId = id,
                location = $"Library/HollowfenPipeline/isolated-saves/{id}",
            };
        }

        [CliCommand("hollowfen_end_save_isolation",
            "Clear and delete only the Hollowfen-owned temporary save directory. Requires confirm=true.")]
        public static object EndSaveIsolation(
            [CliArg("confirm", "Clear SaveManager and delete the owned temporary directory.")]
            bool confirm = false,
            [CliArg("dry_run", "Validate the owned directory without clearing or deleting it.")]
            bool dryRun = false)
        {
            RequireStoppedAndReady("save isolation cleanup");
            string owned = SessionState.GetString(IsolationSessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(owned))
                return new { status = "already_clear" };
            if (!IsUnderRoot(owned, IsolationRoot()))
                throw new InvalidOperationException(
                    "Recorded save isolation escaped its owned root; refusing cleanup.");

            Type saveManager = RequiredType(SaveManagerType);
            string currentOverride = ReadStaticProperty(saveManager, "EditorSaveDirectoryOverride") as string;
            if (!string.IsNullOrWhiteSpace(currentOverride) && !IsSamePath(currentOverride, owned))
                throw new InvalidOperationException(
                    "SaveManager points at a different override; refusing to clear or delete either directory.");

            if (dryRun)
                return new
                {
                    status = "dry_run",
                    isolationId = IsolationId(owned),
                    exists = Directory.Exists(owned),
                };
            if (!confirm)
                throw new ArgumentException(
                    "Refusing save isolation cleanup without confirm=true. Use dry_run=true first.");

            InvokeStatic(saveManager, "EditorClearSaveDirectoryOverride");
            if (Directory.Exists(owned)) Directory.Delete(owned, true);
            SessionState.EraseString(IsolationSessionKey);
            return new { status = "cleared", isolationId = IsolationId(owned) };
        }

        [CliCommand("hollowfen_world_audit",
            "Read a compact active-scene audit for objects, render cost, colliders, missing scripts, and materials.")]
        public static object WorldAudit(
            [CliArg("include_inactive", "Include inactive GameObjects in counts and findings.")]
            bool includeInactive = true,
            [CliArg("max_findings", "Maximum paths returned for each finding type (1-200).")]
            int maxFindings = 50)
        {
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("Scripts are compiling; wait before auditing the scene.");

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("No valid active scene is loaded.");

            int limit = Mathf.Clamp(maxFindings, 1, 200);
            GameObject[] objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .Where(gameObject => includeInactive || gameObject.activeInHierarchy)
                .Distinct()
                .ToArray();

            Renderer[] renderers = objects.SelectMany(gameObject =>
                    gameObject.GetComponents<Renderer>())
                .Where(renderer => renderer != null && (includeInactive || renderer.enabled))
                .ToArray();
            Collider[] colliderComponents = objects.SelectMany(gameObject =>
                    gameObject.GetComponents<Collider>())
                .Where(collider => collider != null)
                .ToArray();
            Collider[] enabledColliders = colliderComponents
                .Where(collider => collider.enabled)
                .ToArray();

            string[] negativeScaleColliders = enabledColliders
                .Where(collider => HasNegativeDeterminant(collider.transform.lossyScale))
                .Select(collider => HierarchyPath(collider.transform))
                .Distinct().OrderBy(path => path).ToArray();
            string[] disabledNegativeScaleColliders = colliderComponents
                .Where(collider => !collider.enabled &&
                    HasNegativeDeterminant(collider.transform.lossyScale))
                .Select(collider => HierarchyPath(collider.transform))
                .Distinct().OrderBy(path => path).ToArray();
            string[] zeroScaleColliders = enabledColliders
                .Where(collider => HasZeroScale(collider.transform.lossyScale))
                .Select(collider => HierarchyPath(collider.transform))
                .Distinct().OrderBy(path => path).ToArray();
            string[] missingScripts = objects
                .Where(gameObject => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
                .Select(gameObject => HierarchyPath(gameObject.transform))
                .OrderBy(path => path).ToArray();

            long triangles = objects.Sum(AuthoredTriangleCount);
            int uniqueMaterials = renderers.SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Select(material => material.GetInstanceID())
                .Distinct().Count();

            return new
            {
                status = "complete",
                scene = new { name = scene.name, path = scene.path, dirty = scene.isDirty },
                includeInactive,
                counts = new
                {
                    gameObjects = objects.Length,
                    activeGameObjects = objects.Count(gameObject => gameObject.activeInHierarchy),
                    renderers = renderers.Length,
                    colliderComponents = colliderComponents.Length,
                    enabledColliders = enabledColliders.Length,
                    disabledColliders = colliderComponents.Length - enabledColliders.Length,
                    lights = objects.Sum(gameObject => gameObject.GetComponents<Light>().Length),
                    particleSystems = objects.Sum(gameObject => gameObject.GetComponents<ParticleSystem>().Length),
                    uniqueMaterials,
                    authoredMeshTriangles = triangles,
                    missingScripts = missingScripts.Length,
                    negativeScaleColliders = negativeScaleColliders.Length,
                    disabledNegativeScaleColliders = disabledNegativeScaleColliders.Length,
                    zeroScaleColliders = zeroScaleColliders.Length,
                },
                findings = new
                {
                    missingScripts = missingScripts.Take(limit).ToArray(),
                    negativeScaleColliders = negativeScaleColliders.Take(limit).ToArray(),
                    disabledNegativeScaleColliders = disabledNegativeScaleColliders.Take(limit).ToArray(),
                    zeroScaleColliders = zeroScaleColliders.Take(limit).ToArray(),
                    truncated = new
                    {
                        missingScripts = missingScripts.Length > limit,
                        negativeScaleColliders = negativeScaleColliders.Length > limit,
                        disabledNegativeScaleColliders = disabledNegativeScaleColliders.Length > limit,
                        zeroScaleColliders = zeroScaleColliders.Length > limit,
                    },
                },
                interpretation = "Loaded-scene hierarchy totals; disabled collider findings are tracked separately and are not active physics. Not visible-frame or standalone-player performance.",
            };
        }

        private static IEnumerable<string> VerifierBlockers(VerifierSpec spec)
        {
            if (EditorApplication.isCompiling) yield return "scripts are compiling";
            if (spec.RequiresPlayMode && !EditorApplication.isPlaying)
                yield return "enter Play Mode";
            if (!spec.RequiresPlayMode && EditorApplication.isPlaying)
                yield return "exit Play Mode";
            if (spec.RequiresGameplayScene && SceneManager.GetActiveScene().path != GameplayScene)
                yield return "load Scene_Hollowfen";
            if (spec.RequiresIsolatedSave && !OwnedSaveIsolationActive())
                yield return "arm Hollowfen-owned save isolation before entering Play Mode";
            Type verifierType = FindType(spec.TypeName);
            if (verifierType == null)
            {
                yield return $"verifier type '{spec.TypeName}' is unavailable";
                yield break;
            }

            MethodInfo verifierMethod = verifierType.GetMethod(spec.MethodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (verifierMethod == null)
                yield return $"verifier method '{spec.TypeName}.{spec.MethodName}' is unavailable";
            else if (verifierMethod.GetParameters().Length != 0 ||
                     verifierMethod.ReturnType != typeof(string))
                yield return $"verifier method '{spec.TypeName}.{spec.MethodName}' does not expose a synchronous string report";
        }

        private static string ReadyStatus()
        {
            if (EditorApplication.isCompiling) return "compiling";
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "busy";
            return "ready";
        }

        private static void RequireStoppedAndReady(string operation)
        {
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException($"Cannot run {operation} while scripts compile.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException($"Cannot run {operation} while the Editor is in Play Mode.");
            if (Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt).Any(scene => scene.isDirty))
                throw new InvalidOperationException($"Cannot run {operation} with a dirty open scene.");
        }

        private static object InvokeStatic(string typeName, string methodName, params object[] arguments) =>
            InvokeStatic(RequiredType(typeName), methodName, arguments);

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) throw new MissingMethodException(type.FullName, methodName);
            try
            {
                return method.Invoke(null, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw new InvalidOperationException(exception.InnerException.Message, exception.InnerException);
            }
        }

        private static Type RequiredType(string fullName) =>
            FindType(fullName) ?? throw new TypeLoadException($"Required Hollowfen type '{fullName}' is unavailable.");

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        private static object ReadStaticProperty(Type type, string propertyName) =>
            type?.GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);

        private static void WriteStaticProperty(Type type, string propertyName, object value)
        {
            PropertyInfo property = type.GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property == null || !property.CanWrite)
                throw new MissingMemberException(type.FullName, propertyName);
            property.SetValue(null, value);
        }

        private static bool OwnedSaveIsolationActive()
        {
            string owned = SessionState.GetString(IsolationSessionKey, string.Empty);
            string current = ReadStaticProperty(FindType(SaveManagerType),
                "EditorSaveDirectoryOverride") as string;
            return Directory.Exists(owned) && IsSamePath(owned, current) && IsUnderRoot(owned, IsolationRoot());
        }

        private static string IsolationRoot() => Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "Library", "HollowfenPipeline", "isolated-saves"));

        private static bool IsUnderRoot(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                                    Path.DirectorySeparatorChar;
            string normalizedPath = Path.GetFullPath(path);
            return normalizedPath.StartsWith(normalizedRoot, StringComparison.Ordinal);
        }

        private static bool IsSamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
            return string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.Ordinal);
        }

        private static string IsolationId(string path) =>
            string.IsNullOrWhiteSpace(path) ? null : new DirectoryInfo(path).Name;

        private static string FirstLine(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            int newline = value.IndexOfAny(new[] { '\r', '\n' });
            return newline < 0 ? value : value.Substring(0, newline);
        }

        private static bool IsError(string type) =>
            type == "Error" || type == "Exception" || type == "Assert";

        private static bool HasNegativeDeterminant(Vector3 scale) =>
            scale.x * scale.y * scale.z < 0f;

        private static bool HasZeroScale(Vector3 scale) =>
            Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f) ||
            Mathf.Approximately(scale.z, 0f);

        private static long AuthoredTriangleCount(GameObject gameObject)
        {
            long triangles = 0;
            foreach (MeshFilter filter in gameObject.GetComponents<MeshFilter>())
                triangles += MeshTriangleCount(filter.sharedMesh);
            foreach (SkinnedMeshRenderer renderer in gameObject.GetComponents<SkinnedMeshRenderer>())
                triangles += MeshTriangleCount(renderer.sharedMesh);
            return triangles;
        }

        private static long MeshTriangleCount(Mesh mesh)
        {
            if (mesh == null) return 0;
            long indices = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                if (mesh.GetTopology(subMesh) == MeshTopology.Triangles)
                    indices += (long)mesh.GetIndexCount(subMesh);
            return indices / 3;
        }

        private static string HierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent)
                names.Push(current.name);
            return "/" + string.Join("/", names);
        }

        private sealed class VerifierSpec
        {
            public VerifierSpec(string name, string typeName, string methodName,
                bool requiresPlayMode, bool requiresGameplayScene, bool requiresIsolatedSave)
            {
                Name = name;
                TypeName = typeName;
                MethodName = methodName;
                RequiresPlayMode = requiresPlayMode;
                RequiresGameplayScene = requiresGameplayScene;
                RequiresIsolatedSave = requiresIsolatedSave;
            }

            public string Name { get; }
            public string TypeName { get; }
            public string MethodName { get; }
            public bool RequiresPlayMode { get; }
            public bool RequiresGameplayScene { get; }
            public bool RequiresIsolatedSave { get; }

            public object Describe() => new
            {
                name = Name,
                requiresPlayMode = RequiresPlayMode,
                requiresGameplayScene = RequiresGameplayScene,
                requiresOwnedSaveIsolation = RequiresIsolatedSave,
            };
        }
    }
}
#endif
