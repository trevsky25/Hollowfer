#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Single entry point for auditable desktop builds. Manual player builds are treated as
    /// release builds by default; only BuildAudit explicitly relaxes the unresolved store
    /// identity checks. Technical, scene, option, package, and output checks always run.
    /// </summary>
    public static class ProductionBuildGate
    {
        private static readonly string[] ExpectedScenes =
        {
            "Assets/_Hollowfen/Scenes/Scene_MainMenu.unity",
            "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity",
        };

        private static readonly string[] PlayerDenylist =
        {
            "MCPForUnity",
            "glTFast",
            "Unity.VisualScripting",
            "Unity.Multiplayer.Center",
            "Newtonsoft.Json",
        };

        private const BuildOptions ForbiddenOptions =
            BuildOptions.Development |
            BuildOptions.AllowDebugging |
            BuildOptions.ConnectWithProfiler;

        private static int _auditBuildDepth;

        internal static bool IsAuditBuild => _auditBuildDepth > 0;

        [MenuItem("Hollowfen/Production/Validate Release Build")]
        public static void ValidateReleaseMenu()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            Validate(target, BuildOptions.None, requireShippingIdentity: true);
            Debug.Log($"[ProductionBuild] RELEASE PREFLIGHT — PASS ({target}).");
        }

        [MenuItem("Hollowfen/Production/Validate Release Build", true)]
        private static bool ValidateReleaseMenuEnabled() =>
            !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Hollowfen/Production/Build Audit macOS...")]
        private static void BuildAuditMacMenu()
        {
            string output = EditorUtility.SaveFilePanel(
                "Build Hollowfen Audit Player",
                string.Empty,
                "Hollowfen-Audit.app",
                "app");
            if (string.IsNullOrWhiteSpace(output)) return;
            BuildAudit(output, BuildTarget.StandaloneOSX);
        }

        /// <summary>
        /// Creates a non-development audit player. This skips only the shipping identity
        /// requirement and is intentionally named so it cannot be mistaken for a release.
        /// </summary>
        public static BuildReport BuildAudit(string outputPath, BuildTarget target = BuildTarget.StandaloneOSX)
        {
            return Build(outputPath, target, requireShippingIdentity: false);
        }

        /// <summary>Creates a release player after every production check passes.</summary>
        public static BuildReport BuildRelease(string outputPath, BuildTarget target)
        {
            return Build(outputPath, target, requireShippingIdentity: true);
        }

        /// <summary>
        /// Unity batch-mode entry point. Required: -hollowfenOutput. Optional:
        /// -hollowfenTarget StandaloneOSX|StandaloneWindows64 (defaults to active target).
        /// </summary>
        public static void BuildReleaseFromCommandLine()
        {
            string output = ReadCommandLineValue("-hollowfenOutput", required: true);
            BuildTarget target = ReadCommandLineTarget();
            BuildRelease(output, target);
        }

        /// <summary>
        /// Audit counterpart for CI/local technical builds. It accepts the same command-line
        /// arguments as BuildReleaseFromCommandLine and skips only placeholder identity checks.
        /// </summary>
        public static void BuildAuditFromCommandLine()
        {
            string output = ReadCommandLineValue("-hollowfenOutput", required: true);
            BuildTarget target = ReadCommandLineTarget();
            BuildAudit(output, target);
        }

        internal static void Validate(
            BuildTarget target,
            BuildOptions options,
            bool requireShippingIdentity)
        {
            var problems = new List<string>();

            ValidateEditorState(problems);
            ValidateTarget(target, problems);
            ValidateOptions(options, problems);
            ValidateScenes(problems);
            if (requireShippingIdentity) ValidateShippingIdentity(problems);
            ValidatePlayerAssemblies(problems);
            ValidatePluginImporters(target, problems);

            if (problems.Count > 0)
                throw new BuildFailedException(
                    "[ProductionBuild] Preflight failed:\n - " + string.Join("\n - ", problems));
        }

        internal static void InspectBuildReport(BuildReport report)
        {
            if (report == null)
                throw new BuildFailedException("[ProductionBuild] Unity returned no BuildReport.");

            string[] deniedFiles = report.GetFiles()
                .Select(file => file.path)
                .Where(path => ContainsDeniedToken(path, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (deniedFiles.Length > 0)
                throw new BuildFailedException(
                    "[ProductionBuild] Denied package/plugin files reached the player:\n - " +
                    string.Join("\n - ", deniedFiles));
        }

        private static BuildReport Build(
            string outputPath,
            BuildTarget target,
            bool requireShippingIdentity)
        {
            string normalizedOutput = ValidateOutputPath(outputPath, target);
            Validate(target, BuildOptions.None, requireShippingIdentity);

            string parent = Path.GetDirectoryName(normalizedOutput);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

            var playerOptions = new BuildPlayerOptions
            {
                scenes = ExpectedScenes.ToArray(),
                locationPathName = normalizedOutput,
                target = target,
                options = BuildOptions.None,
            };

            BuildReport report;
            using (new AuditBuildScope(enabled: !requireShippingIdentity))
                report = BuildPipeline.BuildPlayer(playerOptions);

            if (report == null || report.summary.result != BuildResult.Succeeded)
            {
                string result = report == null ? "no report" : report.summary.result.ToString();
                int errors = report == null ? 0 : report.summary.totalErrors;
                throw new BuildFailedException(
                    $"[ProductionBuild] Player build failed ({result}, errors={errors}).");
            }

            // The postprocessor performs this check for every build. Repeat it here so the
            // contract remains explicit even if Unity changes postprocessor invocation order.
            InspectBuildReport(report);
            Debug.Log(
                $"[ProductionBuild] {(requireShippingIdentity ? "RELEASE" : "AUDIT")} BUILD — PASS: " +
                $"{target}, {report.summary.totalSize} bytes, {normalizedOutput}");
            return report;
        }

        private static void ValidateEditorState(List<string> problems)
        {
            if (EditorApplication.isCompiling)
                problems.Add("scripts are still compiling; wait for compilation to finish");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                problems.Add("the Editor is in or entering Play Mode; stop Play Mode before building");
        }

        private static void ValidateTarget(BuildTarget target, List<string> problems)
        {
            if (target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneWindows64)
            {
                problems.Add(
                    $"unsupported target {target}; only StandaloneOSX and StandaloneWindows64 are shipping targets");
                return;
            }

            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target)) return;

            if (target == BuildTarget.StandaloneWindows64)
                problems.Add(
                    "Windows Standalone support is unavailable. Install the Windows Build Support module for Unity 6000.4.4f1 in Unity Hub");
            else
                problems.Add(
                    "macOS Standalone support is unavailable. Install the Mac Build Support module for Unity 6000.4.4f1 in Unity Hub");
        }

        private static void ValidateOptions(BuildOptions options, List<string> problems)
        {
            BuildOptions forbidden = options & ForbiddenOptions;
            if (forbidden != BuildOptions.None)
                problems.Add(
                    $"development/debug/profiler build options are forbidden for production players: {forbidden}");
        }

        private static void ValidateScenes(List<string> problems)
        {
            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (!enabledScenes.SequenceEqual(ExpectedScenes, StringComparer.Ordinal))
            {
                string expected = string.Join(", ", ExpectedScenes);
                string actual = enabledScenes.Length == 0
                    ? "(none)"
                    : string.Join(", ", enabledScenes);
                problems.Add($"enabled scenes must be exactly [{expected}] in that order; found [{actual}]");
            }

            foreach (string path in ExpectedScenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    problems.Add($"required scene asset is missing: {path}");
            }
        }

        private static void ValidateShippingIdentity(List<string> problems)
        {
            string company = PlayerSettings.companyName?.Trim() ?? string.Empty;
            string product = PlayerSettings.productName?.Trim() ?? string.Empty;
            string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone)?.Trim() ?? string.Empty;
            string version = PlayerSettings.bundleVersion?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(company) ||
                string.Equals(company, "DefaultCompany", StringComparison.OrdinalIgnoreCase))
                problems.Add("Company Name is still the Unity placeholder 'DefaultCompany'");

            if (string.IsNullOrEmpty(product) ||
                string.Equals(product, "Hollowfen-Unity", StringComparison.OrdinalIgnoreCase))
                problems.Add("Product Name is still the project placeholder 'Hollowfen-Unity'");

            if (string.IsNullOrEmpty(identifier) ||
                identifier.IndexOf("Unity-Technologies", StringComparison.OrdinalIgnoreCase) >= 0 ||
                identifier.IndexOf("unity.template", StringComparison.OrdinalIgnoreCase) >= 0)
                problems.Add($"Standalone Application Identifier is still a Unity template value ('{identifier}')");

            if (string.IsNullOrEmpty(version) ||
                string.Equals(version, "0.1.0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(version, "0.0.0", StringComparison.OrdinalIgnoreCase))
                problems.Add($"Bundle Version is still a pre-release placeholder ('{version}')");
        }

        private static void ValidatePlayerAssemblies(List<string> problems)
        {
            foreach (UnityEditor.Compilation.Assembly assembly in
                     CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                IEnumerable<string> identifiers = new[] { assembly.name, assembly.outputPath }
                    .Concat(assembly.compiledAssemblyReferences ?? Array.Empty<string>());
                string denied = identifiers.FirstOrDefault(value => ContainsDeniedToken(value, out _));
                if (!string.IsNullOrEmpty(denied))
                    problems.Add($"denied player assembly/reference: {assembly.name} -> {denied}");
            }
        }

        private static void ValidatePluginImporters(BuildTarget target, List<string> problems)
        {
            foreach (PluginImporter importer in PluginImporter.GetAllImporters())
            {
                if (importer == null || !ContainsDeniedToken(importer.assetPath, out string token)) continue;
                if (!importer.GetCompatibleWithPlatform(target)) continue;
                problems.Add(
                    $"denied plugin '{token}' is compatible with {target}: {importer.assetPath}");
            }
        }

        private static bool ContainsDeniedToken(string value, out string token)
        {
            foreach (string candidate in PlayerDenylist)
            {
                if (!string.IsNullOrEmpty(value) &&
                    value.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    token = candidate;
                    return true;
                }
            }

            token = string.Empty;
            return false;
        }

        private static string ValidateOutputPath(string outputPath, BuildTarget target)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new BuildFailedException(
                    "[ProductionBuild] Output path is required (-hollowfenOutput in batch mode).");

            string fullPath = Path.GetFullPath(outputPath.Trim());
            string expectedExtension = target == BuildTarget.StandaloneWindows64 ? ".exe" : ".app";
            if (!fullPath.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException(
                    $"[ProductionBuild] {target} output must end in '{expectedExtension}': {fullPath}");

            if (File.Exists(fullPath) || Directory.Exists(fullPath))
                throw new BuildFailedException(
                    $"[ProductionBuild] Refusing to overwrite an existing build output: {fullPath}");

            return fullPath;
        }

        private static BuildTarget ReadCommandLineTarget()
        {
            string value = ReadCommandLineValue("-hollowfenTarget", required: false);
            if (string.IsNullOrWhiteSpace(value)) return EditorUserBuildSettings.activeBuildTarget;

            if (string.Equals(value, "StandaloneOSX", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "macOS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "osx", StringComparison.OrdinalIgnoreCase))
                return BuildTarget.StandaloneOSX;
            if (string.Equals(value, "StandaloneWindows64", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Windows", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "win64", StringComparison.OrdinalIgnoreCase))
                return BuildTarget.StandaloneWindows64;

            throw new BuildFailedException(
                $"[ProductionBuild] Unknown -hollowfenTarget '{value}'. Use StandaloneOSX or StandaloneWindows64.");
        }

        private static string ReadCommandLineValue(string name, bool required)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring(name.Length + 1);
                if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) continue;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    return args[i + 1];
                break;
            }

            if (required)
                throw new BuildFailedException($"[ProductionBuild] Missing required command-line argument {name}.");
            return null;
        }

        private sealed class AuditBuildScope : IDisposable
        {
            private readonly bool _enabled;

            public AuditBuildScope(bool enabled)
            {
                _enabled = enabled;
                if (_enabled) _auditBuildDepth++;
            }

            public void Dispose()
            {
                if (_enabled) _auditBuildDepth--;
            }
        }
    }

    /// <summary>Applies production checks to both scripted and manual player builds.</summary>
    public sealed class ProductionBuildPreprocessor : IPreprocessBuildWithReport
    {
        // Run before ShippingPhysicsBuildGate (-1000); both gates remain independent.
        public int callbackOrder => -2000;

        public void OnPreprocessBuild(BuildReport report)
        {
            ProductionBuildGate.Validate(
                report.summary.platform,
                report.summary.options,
                requireShippingIdentity: !ProductionBuildGate.IsAuditBuild);
        }
    }

    /// <summary>Confirms excluded tooling did not leak into the completed player.</summary>
    public sealed class ProductionBuildPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 10000;

        public void OnPostprocessBuild(BuildReport report)
        {
            ProductionBuildGate.InspectBuildReport(report);
        }
    }
}
#endif
