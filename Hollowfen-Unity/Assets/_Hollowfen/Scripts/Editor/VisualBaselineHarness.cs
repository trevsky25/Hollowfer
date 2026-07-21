#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Hollowfen.Data;
using Hollowfen.Foraging;
using Hollowfen.Quests;
using Hollowfen.UI;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Editor-only staging helpers for the Pipeline-driven visual/performance baseline. The durable
    /// runner lives in tools/agent/capture_visual_baseline.py; this class keeps Unity-specific state
    /// changes narrow, reversible, and callable through Pipeline's Roslyn eval surface.
    /// </summary>
    public static class VisualBaselineHarness
    {
        public const int Width = 1280;
        public const int Height = 800;

        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";
        private const string GameViewLabel = "Steam Deck (1280x800)";

        private static readonly string[] CapturableScreens =
        {
            "main-menu", "save-slot", "settings", "story", "story-detail",
            "field-guide", "mushroom-detail", "wren",
        };

        [MenuItem("Hollowfen/Production/Configure 1280x800 Game View")]
        public static void ConfigureGameViewMenu() => Debug.Log(ConfigureGameView());

        public static string ConfigureGameView()
        {
            Assembly editorAssembly = typeof(Editor).Assembly;
            Type sizesType = RequiredType(editorAssembly, "UnityEditor.GameViewSizes");
            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = RequiredProperty(singletonType, "instance", BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            object group = RequiredProperty(sizesType, "currentGroup", BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic).GetValue(sizes);

            Type groupType = group.GetType();
            MethodInfo getSize = RequiredMethod(groupType, "GetGameViewSize");
            int builtInCount = (int)RequiredMethod(groupType, "GetBuiltinCount").Invoke(group, null);
            int customCount = (int)RequiredMethod(groupType, "GetCustomCount").Invoke(group, null);
            int selectedIndex = -1;

            for (int index = 0; index < customCount; index++)
            {
                object candidate = getSize.Invoke(group, new object[] { builtInCount + index });
                Type candidateType = candidate.GetType();
                int width = (int)RequiredProperty(candidateType, "width", BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic).GetValue(candidate);
                int height = (int)RequiredProperty(candidateType, "height", BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic).GetValue(candidate);
                if (width == Width && height == Height)
                {
                    selectedIndex = builtInCount + index;
                    break;
                }
            }

            if (selectedIndex < 0)
            {
                Type sizeType = RequiredType(editorAssembly, "UnityEditor.GameViewSize");
                Type sizeKindType = RequiredType(editorAssembly, "UnityEditor.GameViewSizeType");
                object fixedResolution = Enum.Parse(sizeKindType, "FixedResolution");
                object size = Activator.CreateInstance(sizeType,
                    new[] { fixedResolution, (object)Width, Height, GameViewLabel });
                RequiredMethod(groupType, "AddCustomSize").Invoke(group, new[] { size });
                selectedIndex = builtInCount + customCount;
            }

            Type gameViewType = RequiredType(editorAssembly, "UnityEditor.GameView");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            RequiredProperty(gameViewType, "selectedSizeIndex", BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic).SetValue(gameView, selectedIndex);
            gameView.Repaint();

            Vector2Int target = GetGameViewTargetSize(gameViewType, gameView);
            if (target.x != Width || target.y != Height)
                throw new InvalidOperationException(
                    $"Game View target is {target.x}x{target.y}; expected {Width}x{Height}.");
            return $"Game View ready: {target.x}x{target.y} ({GameViewLabel}).";
        }

        public static string PrepareReferenceProgression()
        {
            RequirePlayMode();

            StoryCardDatabase story = AssetDatabase.LoadAssetAtPath<StoryCardDatabase>(
                "Assets/_Hollowfen/Data/StoryCards/StoryCardDatabase.asset");
            MushroomFieldGuideDatabase fieldGuide = Resources.Load<MushroomFieldGuideDatabase>(
                "MushroomFieldGuideDatabase");
            if (story == null || fieldGuide == null)
                throw new InvalidOperationException("Reference databases are unavailable.");

            QuestManager.HydrateFrom(Array.Empty<string>(), story.Cards
                .Where(card => card != null)
                .Select(card => card.Id)
                .ToArray());
            MushroomDiscovery.HydrateFrom(fieldGuide.Entries
                .Where(entry => entry != null)
                .Select(entry => entry.Id)
                .ToArray());

            return $"Reference progression staged in memory: {story.Count} story cards, " +
                   $"{fieldGuide.Count} field-guide entries; no save write.";
        }

        public static string PrepareScreen(string screenId)
        {
            RequirePlayMode();
            if (!CapturableScreens.Contains(screenId, StringComparer.Ordinal))
                throw new ArgumentException($"Unsupported baseline screen '{screenId}'.", nameof(screenId));
            if (screenId == "story-detail" || screenId == "mushroom-detail")
                throw new InvalidOperationException(
                    $"Open '{screenId}' through OpenSelectedDetail so its content is assigned first.");

            UIManager manager = UIManager.Instance;
            if (manager == null) throw new InvalidOperationException("UIManager is unavailable.");
            if (manager.IsTransitioning)
                throw new InvalidOperationException("UIManager is still transitioning.");

            manager.CloseAll();
            manager.OpenScreen(screenId);
            return $"Opening baseline screen '{screenId}'.";
        }

        public static string OpenSelectedDetail(string sourceScreenId, string detailScreenId)
        {
            RequirePlayMode();
            bool validPair = sourceScreenId == "story" && detailScreenId == "story-detail" ||
                             sourceScreenId == "field-guide" && detailScreenId == "mushroom-detail";
            if (!validPair) throw new ArgumentException("Unsupported baseline detail pair.");

            UIManager manager = UIManager.Instance;
            if (manager == null || manager.IsTransitioning || manager.TopScreen == null ||
                manager.TopScreen.ScreenId != sourceScreenId)
                throw new InvalidOperationException($"'{sourceScreenId}' is not settled and active.");

            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            Button button = selected != null ? selected.GetComponent<Button>() : null;
            if (button == null || !button.IsInteractable())
                throw new InvalidOperationException(
                    $"'{sourceScreenId}' has no interactable selected content row.");

            button.onClick.Invoke();
            return $"Opening baseline detail '{detailScreenId}' from '{selected.name}'.";
        }

        public static string PresentationState()
        {
            UIManager manager = UIManager.Instance;
            string top = manager != null && manager.TopScreen != null
                ? manager.TopScreen.ScreenId
                : "(none)";
            bool transitioning = manager != null && manager.IsTransitioning;
            Assembly editorAssembly = typeof(Editor).Assembly;
            Type gameViewType = RequiredType(editorAssembly, "UnityEditor.GameView");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            Vector2Int target = GetGameViewTargetSize(gameViewType, gameView);
            return $"{top}|{transitioning}|{target.x}x{target.y}|frame={Time.frameCount}";
        }

        public static string CaptureCurrentScreen(string relativePath)
        {
            RequirePlayMode();
            UIManager manager = UIManager.Instance;
            if (manager == null || manager.TopScreen == null || manager.IsTransitioning)
                throw new InvalidOperationException("No settled UI presentation is active.");

            string report = ProductionUIVerifier.VerifyActiveForAutomation();
            if (!report.StartsWith("PASS", StringComparison.Ordinal))
                throw new InvalidOperationException(report);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string requested = (relativePath ?? string.Empty).Replace('\\', '/');
            if (!requested.StartsWith("Docs/screenshots/", StringComparison.Ordinal) ||
                !requested.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "Baseline captures must be PNGs below Docs/screenshots/.", nameof(relativePath));

            string output = Path.GetFullPath(Path.Combine(projectRoot, requested));
            string captureRoot = Path.GetFullPath(Path.Combine(projectRoot, "Docs/screenshots")) +
                                 Path.DirectorySeparatorChar;
            if (!output.StartsWith(captureRoot, StringComparison.Ordinal))
                throw new InvalidOperationException("Capture path escapes Docs/screenshots/.");
            if (File.Exists(output))
                throw new IOException($"Refusing to overwrite existing baseline capture: {requested}");

            string directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(output);
            return $"Queued {manager.TopScreen.ScreenId}: {requested}\n{report}";
        }

        public static string PlacePlayerForBaseline(float x, float z, float yaw)
        {
            RequirePlayMode();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != GameplayScene)
                throw new InvalidOperationException("The gameplay scene is not active.");

            UIManager.Instance?.CloseAll();
            ThirdPersonController player = UnityEngine.Object.FindFirstObjectByType<ThirdPersonController>();
            if (player == null) throw new InvalidOperationException("ThirdPersonController is unavailable.");

            Terrain terrain = Terrain.activeTerrain;
            float y = player.transform.position.y;
            if (terrain != null)
                y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y + 0.15f;

            CharacterController controller = player.GetComponent<CharacterController>();
            bool restoreController = controller != null && controller.enabled;
            if (restoreController) controller.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
            if (restoreController) controller.enabled = true;

            Physics.SyncTransforms();
            return $"Player staged at ({x:0.0}, {y:0.0}, {z:0.0}), yaw {yaw:0}.";
        }

        public static string MeasureEditorStepTimings(int frames)
        {
            RequirePlayMode();
            if (frames < 3 || frames > 300)
                throw new ArgumentOutOfRangeException(nameof(frames), "Use 3–300 timed frames.");

            var milliseconds = new double[frames];
            double tickToMilliseconds = 1000d / System.Diagnostics.Stopwatch.Frequency;
            for (int index = 0; index < frames; index++)
            {
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                EditorApplication.Step();
                long finished = System.Diagnostics.Stopwatch.GetTimestamp();
                milliseconds[index] = (finished - started) * tickToMilliseconds;
            }

            return JsonUtility.ToJson(new EditorStepTimingResult
            {
                frames = frames,
                milliseconds = milliseconds,
            });
        }

        private static Vector2Int GetGameViewTargetSize(Type gameViewType, EditorWindow gameView)
        {
            object size = RequiredProperty(gameViewType, "targetRenderSize", BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic).GetValue(gameView);
            Vector2 value = (Vector2)size;
            return new Vector2Int(Mathf.RoundToInt(value.x), Mathf.RoundToInt(value.y));
        }

        private static void RequirePlayMode()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Visual baseline staging requires Play Mode.");
        }

        private static Type RequiredType(Assembly assembly, string name) =>
            assembly.GetType(name) ?? throw new MissingMemberException(name);

        private static PropertyInfo RequiredProperty(Type type, string name, BindingFlags flags) =>
            type.GetProperty(name, flags) ?? throw new MissingMemberException(type.FullName, name);

        private static MethodInfo RequiredMethod(Type type, string name) =>
            type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
            throw new MissingMemberException(type.FullName, name);

        [Serializable]
        private sealed class EditorStepTimingResult
        {
            public int frames;
            public double[] milliseconds;
        }
    }
}
#endif
