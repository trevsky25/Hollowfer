#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hollowfen.Apothecary;
using Hollowfen.Cultivation;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using Hollowfen.Foraging;
using Hollowfen.Items;
using Hollowfen.Map;
using Hollowfen.Quests;
using Hollowfen.Requests;
using Hollowfen.Restoration;
using Hollowfen.Save;
using Hollowfen.Settings;
using Hollowfen.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Editor-only deterministic staging and measurement for Batch 125's full-game UI audit.
    /// The Pipeline runner owns production preflight, save isolation, scene routing, frame settling,
    /// screenshots, and cleanup. This class only stages authored runtime presentations and reports
    /// their live text/layout state; it never creates a Player-runtime automation surface.
    /// </summary>
    public static class FullUIAuditHarness
    {
        public const int Width = 1280;
        public const int Height = 800;

        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";
        private const string ScaleKey = "accessibility.interfaceScale";
        private const string MotionKey = "accessibility.reducedMotion";
        private const string CaptionKey = "accessibility.captionBacking";

        private static readonly List<GameObject> AuditObjects = new List<GameObject>();
        private static string _route = "(none)";

        [Serializable]
        private sealed class PreferenceSnapshot
        {
            public bool hasScale;
            public bool hasMotion;
            public bool hasCaption;
            public int scale;
            public int motion;
            public int caption;
        }

        [Serializable]
        private sealed class PresentationReport
        {
            public string route;
            public string scene;
            public string profile;
            public string productionVerifier;
            public string selected;
            public int rootCanvasCount;
            public int visibleTextCount;
            public int paragraphCount;
            public float minimumTextPixels = float.MaxValue;
            public float minimumParagraphPixels = float.MaxValue;
            public List<string> textBelow14Pixels = new List<string>();
            public List<string> paragraphsBelow14Pixels = new List<string>();
            public List<string> clippedText = new List<string>();
            public List<string> offscreenText = new List<string>();
        }

        public static string ConfigureGameView() => VisualBaselineHarness.ConfigureGameView();

        public static string CapturePreferenceSnapshot()
        {
            return JsonUtility.ToJson(new PreferenceSnapshot
            {
                hasScale = PlayerPrefs.HasKey(ScaleKey),
                hasMotion = PlayerPrefs.HasKey(MotionKey),
                hasCaption = PlayerPrefs.HasKey(CaptionKey),
                scale = PlayerPrefs.GetInt(ScaleKey, 0),
                motion = PlayerPrefs.GetInt(MotionKey, 0),
                caption = PlayerPrefs.GetInt(CaptionKey, 0),
            });
        }

        public static string ApplyProfile(int scaleIndex)
        {
            RequirePlayMode();
            if (scaleIndex != 0 && scaleIndex != 2)
                throw new ArgumentOutOfRangeException(nameof(scaleIndex),
                    "The audit supports the production 100% and maximum 115% profiles.");
            GameSettings.InterfaceScaleIndex = scaleIndex;
            GameSettings.ReducedMotion = scaleIndex == 2;
            GameSettings.CaptionBacking = scaleIndex == 2;
            AccessibilityPresentationPolicy.RequestRefresh();
            return scaleIndex == 2 ? "115%-reduced-motion-caption-backing" : "100%-standard";
        }

        public static string RestorePreferenceSnapshot(string json)
        {
            PreferenceSnapshot snapshot = JsonUtility.FromJson<PreferenceSnapshot>(json);
            if (snapshot == null) throw new ArgumentException("Invalid preference snapshot.", nameof(json));
            RestoreIntPreference(ScaleKey, snapshot.hasScale, snapshot.scale,
                value => GameSettings.InterfaceScaleIndex = value);
            RestoreBoolPreference(MotionKey, snapshot.hasMotion, snapshot.motion,
                value => GameSettings.ReducedMotion = value);
            RestoreBoolPreference(CaptionKey, snapshot.hasCaption, snapshot.caption,
                value => GameSettings.CaptionBacking = value);
            AccessibilityPresentationPolicy.RequestRefresh();
            return "Accessibility preferences restored.";
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

            string[] speciesIds = fieldGuide.Entries.Where(entry => entry != null)
                .Select(entry => entry.Id).ToArray();
            var flags = new List<string>
            {
                "journal_found", "apothecary_story_complete", "tobin_workshop_complete",
            };
            foreach (PreparationRecipeData recipe in LoadAssets<PreparationRecipeData>())
                if (recipe != null && !string.IsNullOrWhiteSpace(recipe.RequiredFlagId))
                    flags.Add(recipe.RequiredFlagId);
            foreach (MushroomFieldGuideData species in fieldGuide.Entries.Where(entry => entry != null))
            {
                flags.Add("mushroom_studied_" + species.Id);
                flags.Add("mushroom_identified_" + species.Id);
                if (!string.IsNullOrWhiteSpace(species.RequiredForageFlagId))
                    flags.Add(species.RequiredForageFlagId);
            }
            GameScores.HydrateFrom(new SaveSlotMeta { GameFlagIds = flags.Distinct().ToArray() });
            QuestManager.HydrateFrom(Array.Empty<string>(), story.Cards
                .Where(card => card != null).Select(card => card.Id).ToArray());
            MushroomDiscovery.HydrateFrom(speciesIds);
            InventoryRuntime.HydrateFrom(new InventorySnapshot
            {
                Ids = speciesIds,
                Counts = speciesIds.Select(_ => 9).ToArray(),
            });
            CoinPurse.HydrateFrom(347, new CoinLedgerSnapshot
            {
                AmountsCopper = new[] { 96, -24, 63, -12 },
                BalancesAfterCopper = new[] { 320, 296, 359, 347 },
                ReasonIds = new[]
                {
                    "purse.transaction.request", "purse.transaction.spent",
                    "purse.transaction.earned", "purse.transaction.spent",
                },
            });
            ApothecaryRuntime.HydrateFrom(new ApothecarySnapshot
            {
                ProductIds = LoadAssets<PreparationRecipeData>()
                    .Where(recipe => recipe != null).Select(recipe => recipe.ResultId).ToArray(),
                ProductCounts = LoadAssets<PreparationRecipeData>()
                    .Where(recipe => recipe != null).Select(_ => 3).ToArray(),
                CraftedRecipeIds = LoadAssets<PreparationRecipeData>()
                    .Where(recipe => recipe != null).Select(recipe => recipe.Id).ToArray(),
            });
            return $"Reference UI progression staged in isolated memory: {story.Count} story cards, " +
                   $"{speciesIds.Length} species, {LoadAssets<PreparationRecipeData>().Length} preparations.";
        }

        public static string PrepareRoute(string route)
        {
            RequirePlayMode();
            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentException("Route is required.", nameof(route));
            ClosePresentations();
            _route = route;

            switch (route)
            {
                case "main-menu": OpenScreen("main-menu"); break;
                case "save-slots": OpenScreen("save-slot"); break;
                case "loading": OpenScreen("loading"); break;
                case "settings-audio": OpenSettings(SettingsScreen.Tab.Audio); break;
                case "settings-graphics": OpenSettings(SettingsScreen.Tab.Graphics); break;
                case "settings-controls": OpenSettings(SettingsScreen.Tab.Controls); break;
                case "settings-accessibility": OpenSettings(SettingsScreen.Tab.Accessibility); break;
                case "settings-credits": OpenSettings(SettingsScreen.Tab.Credits); break;
                case "confirm-modal":
                    ConfirmModal.Instance.Configure("Leave Hollowfen?",
                        "Your journal is safe. Return to the mist only when you are ready.",
                        () => { }, null);
                    OpenScreen("confirm-modal");
                    break;
                case "story-index": OpenScreen("story"); break;
                case "story-detail":
                    StoryCardData storyCard = LoadAssets<StoryCardData>()
                        .Where(item => item != null)
                        .OrderByDescending(item => JournalText.StoryNote(item)?.Length ?? 0)
                        .FirstOrDefault() ?? throw new InvalidOperationException(
                            "No story card is available.");
                    FindLive<StoryDetailScreen>().SetCard(storyCard,
                        AssetDatabase.LoadAssetAtPath<StoryCardDatabase>(
                            "Assets/_Hollowfen/Data/StoryCards/StoryCardDatabase.asset"));
                    OpenScreen("story-detail");
                    break;
                case "field-guide": OpenScreen("field-guide"); break;
                case "mushroom-detail":
                    MushroomFieldGuideDatabase fieldGuide = Resources.Load<MushroomFieldGuideDatabase>(
                        "MushroomFieldGuideDatabase");
                    MushroomFieldGuideData mushroom = fieldGuide.Entries
                        .Where(item => item != null)
                        .OrderByDescending(item => JournalText.MushroomDescription(item)?.Length ?? 0)
                        .First();
                    FindLive<MushroomDetailScreen>().SetEntry(mushroom, fieldGuide);
                    OpenScreen("mushroom-detail");
                    break;
                case "people-archive": OpenScreen("wren"); break;
                case "purse": PurseScreen.OpenFromMenu(); break;
                case "restoration-ledger": OpenRestorationLedger(); break;
                case "ending-credits": OpenEndingCredits(); break;
                case "gameplay-hud": StageGameplayHud(false); break;
                case "request-tracker": StageGameplayHud(true); break;
                case "pause": OpenPause(); break;
                case "inventory": OpenInventory(); break;
                case "inspect-known": OpenInspect(true, false); break;
                case "identify-study": OpenInspect(false, false); break;
                case "identify-quiz": OpenInspect(false, true); break;
                case "map": OpenMap(); break;
                case "dialogue-line": OpenDialogue(false); break;
                case "dialogue-choices": OpenDialogue(true); break;
                case "village-request": OpenVillageRequest(); break;
                case "cultivation": OpenCultivation(); break;
                case "apothecary-preparation": OpenApothecaryPreparation(); break;
                case "apothecary-case-unstarted": OpenApothecaryCase(ApothecaryCaseStage.Unstarted); break;
                case "apothecary-case-investigation": OpenApothecaryCase(ApothecaryCaseStage.Investigating); break;
                case "apothecary-case-decisions": OpenApothecaryCaseDecisions(); break;
                case "apothecary-case-followup": OpenApothecaryCase(ApothecaryCaseStage.AwaitingFollowUp); break;
                case "apothecary-case-resolved": OpenApothecaryCase(ApothecaryCaseStage.Resolved); break;
                case "intro-guide": OpenIntroGuide(); break;
                case "story-card-toast": OpenStoryCardToast(); break;
                case "key-item-toast": OpenKeyItemToast(); break;
                case "region-arrival-toast": OpenRegionArrivalToast(); break;
                case "restoration-toast": OpenRestorationToast(); break;
                case "narration": OpenNarration(false); break;
                case "narration-cinematic": OpenNarration(true); break;
                case "cutting-hud": OpenCuttingHud(); break;
                default: throw new ArgumentException("Unknown full UI audit route: " + route);
            }
            return "Staged UI audit route '" + route + "'.";
        }

        public static string FinalizeRoute()
        {
            RequirePlayMode();
            Canvas.ForceUpdateCanvases();
            if (string.Equals(_route, "main-menu", StringComparison.Ordinal))
            {
                MenuCinematics cinematics = FindLive<MenuCinematics>();
                if (cinematics != null) Invoke(cinematics, "FinishRevealState");
            }
            Canvas.ForceUpdateCanvases();
            return "Finalized UI audit route '" + _route + "'.";
        }

        public static string PresentationState()
        {
            UIManager manager = UIManager.Instance;
            string top = manager != null && manager.TopScreen != null
                ? manager.TopScreen.ScreenId
                : "(standalone)";
            bool transitioning = manager != null && manager.IsTransitioning;
            string target = VisualBaselineHarness.PresentationState().Split('|')[2];
            return _route + "|" + top + "|" + transitioning + "|" + target +
                   "|frame=" + Time.frameCount;
        }

        public static string InspectAndCapture(string route, string profile, string relativePath)
        {
            RequirePlayMode();
            if (!string.Equals(route, _route, StringComparison.Ordinal))
                throw new InvalidOperationException("Requested capture does not match the staged route.");

            string productionReport = ProductionUIVerifier.VerifyActiveForAutomation();
            PresentationReport report = InspectVisibleText(route, profile, productionReport);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string requested = (relativePath ?? string.Empty).Replace('\\', '/');
            if (!requested.StartsWith("Docs/screenshots/batch-125-ui-audit/", StringComparison.Ordinal) ||
                !requested.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Batch 125 captures must stay below their owned screenshot folder.");
            string output = Path.GetFullPath(Path.Combine(projectRoot, requested));
            string root = Path.GetFullPath(Path.Combine(projectRoot,
                "Docs/screenshots/batch-125-ui-audit")) + Path.DirectorySeparatorChar;
            if (!output.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidOperationException("Capture path escapes the Batch 125 evidence root.");
            if (File.Exists(output))
                throw new IOException("Refusing to overwrite UI audit evidence: " + requested);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            ScreenCapture.CaptureScreenshot(output);
            return JsonUtility.ToJson(report, true);
        }

        private static PresentationReport InspectVisibleText(string route, string profile,
            string productionReport)
        {
            Canvas.ForceUpdateCanvases();
            var report = new PresentationReport
            {
                route = route,
                scene = SceneManager.GetActiveScene().path,
                profile = profile,
                productionVerifier = productionReport,
                selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                    ? HierarchyPath(EventSystem.current.currentSelectedGameObject.transform)
                    : "(none)",
                rootCanvasCount = Resources.FindObjectsOfTypeAll<Canvas>().Count(canvas =>
                    IsLive(canvas) && canvas.enabled && canvas.gameObject.activeInHierarchy &&
                    canvas.isRootCanvas),
            };

            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (!IsVisible(text)) continue;
                text.ForceMeshUpdate();
                report.visibleTextCount++;
                bool paragraph = text.text != null && text.text.Length >= 60 &&
                                 text.textWrappingMode != TextWrappingModes.NoWrap;
                if (paragraph) report.paragraphCount++;
                Canvas canvas = text.GetComponentInParent<Canvas>();
                float pixels = text.fontSize * (canvas != null && canvas.rootCanvas != null
                    ? canvas.rootCanvas.scaleFactor
                    : 1f);
                report.minimumTextPixels = Mathf.Min(report.minimumTextPixels, pixels);
                if (paragraph) report.minimumParagraphPixels =
                    Mathf.Min(report.minimumParagraphPixels, pixels);
                string finding = pixels.ToString("0.0") + "px · " + HierarchyPath(text.transform) +
                                 " · “" + Compact(text.text) + "”";
                if (pixels < 14f && !IsDecorativeMicrocopy(text))
                    report.textBelow14Pixels.Add(finding);
                if (paragraph && pixels < 14f)
                    report.paragraphsBelow14Pixels.Add(finding);
                if (text.isTextOverflowing && IsClippingOverflowMode(text.overflowMode))
                    report.clippedText.Add(HierarchyPath(text.transform) + " · “" +
                                           Compact(text.text) + "”");
                if (IsOffscreen(text.rectTransform, canvas))
                    report.offscreenText.Add(HierarchyPath(text.transform) + " · “" +
                                             Compact(text.text) + "”");
            }
            if (report.visibleTextCount == 0) report.minimumTextPixels = 0f;
            if (report.paragraphCount == 0) report.minimumParagraphPixels = 0f;
            return report;
        }

        private static void OpenScreen(string screenId)
        {
            UIManager manager = UIManager.Instance ??
                throw new InvalidOperationException("UIManager is unavailable.");
            manager.OpenScreen(screenId);
        }

        private static void OpenSettings(SettingsScreen.Tab tab)
        {
            SettingsScreen.NextOpenTab = tab;
            OpenScreen("settings");
        }

        private static void OpenPause()
        {
            UIManager manager = UIManager.Instance ??
                throw new InvalidOperationException("UIManager is unavailable.");
            Invoke(manager, "EnsurePauseInstance");
            manager.OpenScreen("pause");
        }

        private static void OpenInventory()
        {
            InventoryScreen screen = FindLive<InventoryScreen>() ??
                throw new InvalidOperationException("InventoryScreen is unavailable.");
            screen.Open();
        }

        private static void OpenMap()
        {
            MapScreen screen = FindLive<MapScreen>() ??
                throw new InvalidOperationException("MapScreen is unavailable.");
            screen.Open();
        }

        private static void OpenInspect(bool identified, bool quiz)
        {
            MushroomNode node = Resources.FindObjectsOfTypeAll<MushroomNode>()
                .Where(candidate => IsLive(candidate) && candidate.Data != null)
                .OrderByDescending(candidate => JournalText.MushroomDescription(candidate.Data)?.Length ?? 0)
                .FirstOrDefault() ?? throw new InvalidOperationException("No authored mushroom node is available.");
            MushroomFieldGuideData species = node.Data;
            var flags = new List<string> { "journal_found" };
            if (identified) flags.Add("mushroom_identified_" + species.Id);
            else flags.Add("mushroom_studied_" + species.Id);
            if (!string.IsNullOrWhiteSpace(species.RequiredForageFlagId))
                flags.Add(species.RequiredForageFlagId);
            GameScores.HydrateFrom(new SaveSlotMeta { GameFlagIds = flags.ToArray() });
            MushroomDiscovery.HydrateFrom(new[] { species.Id });
            InspectScreen.Open(node);
            if (quiz)
            {
                InspectScreen screen = InspectScreen.Instance ??
                    throw new InvalidOperationException("InspectScreen did not open.");
                Invoke(screen, "BeginJournalComparison");
            }
        }

        private static void OpenDialogue(bool choices)
        {
            DialogueScreen screen = DialogueScreen.Instance ??
                throw new InvalidOperationException("DialogueScreen is unavailable.");
            DialogueData[] all = LoadAssets<DialogueData>();
            DialogueData data = choices
                ? all.Where(item => item != null && (item.Choices?.Length ?? 0) > 0)
                    .OrderByDescending(item => item.Choices.Length).ThenByDescending(item =>
                        item.Choices.Sum(choice => choice.text?.Length ?? 0)).FirstOrDefault()
                : all.Where(item => item != null && (item.Lines?.Length ?? 0) > 0)
                    .OrderByDescending(item => item.Lines[0].text?.Length ?? 0).FirstOrDefault();
            if (data == null) throw new InvalidOperationException("No suitable dialogue asset is available.");
            screen.Open(data);
            if (choices)
            {
                Invoke(screen, "SkipTypewriter");
                Invoke(screen, "BeginChoices", data.Choices);
            }
        }

        private static void OpenVillageRequest()
        {
            VillageRequestData request = LoadAssets<VillageRequestData>()
                .Where(item => item != null)
                .OrderByDescending(item => Localization.Get(item.DescriptionId)?.Length ?? 0)
                .FirstOrDefault() ?? throw new InvalidOperationException("No village request is available.");
            VillageRequestScreen.Ensure().Open(request, "Marra Willowfen", null, null);
        }

        private static void OpenCultivation()
        {
            GrowBed bed = FindLive<GrowBed>() ??
                throw new InvalidOperationException("No grow bed is available.");
            CultivationScreen.Ensure().Open(bed);
        }

        private static void OpenApothecaryPreparation()
        {
            PrepareReferenceProgression();
            ApothecaryStation station = FindLive<ApothecaryStation>() ??
                throw new InvalidOperationException("ApothecaryStation is unavailable.");
            ApothecaryScreen.Ensure().Open(station);
        }

        private static void OpenApothecaryCase(ApothecaryCaseStage stage)
        {
            ApothecaryCaseData data = ApothecaryCaseDatabase.Load().Cases[0];
            ApothecaryCaseDecision careful = data.Decisions.First(decision =>
                decision.grade == ApothecaryCaseGrade.Careful);
            int evidence = stage == ApothecaryCaseStage.Investigating ? 3 :
                stage >= ApothecaryCaseStage.AwaitingFollowUp ? 3 : 0;
            int interviews = stage == ApothecaryCaseStage.Investigating ? 1 :
                stage >= ApothecaryCaseStage.AwaitingFollowUp ? 3 : 0;
            ApothecaryCases.HydrateFrom(stage == ApothecaryCaseStage.Unstarted
                ? null
                : new ApothecaryCaseSnapshot
                {
                    Ids = new[] { data.Id },
                    Stages = new[] { (int)stage },
                    StartedDays = new[] { 4 },
                    EvidenceMasks = new[] { evidence },
                    InterviewMasks = new[] { interviews },
                    DecisionIds = new[] { stage >= ApothecaryCaseStage.AwaitingFollowUp
                        ? careful.id : string.Empty },
                    FollowUpDays = new[] { 5 },
                    ResolvedDays = new[] { stage == ApothecaryCaseStage.Resolved ? 6 : 0 },
                });
            GameScores.HydrateFrom(new SaveSlotMeta
            {
                GameDay = 6,
                GameFlagIds = new[] { "apothecary_story_complete" },
            });
            OpenCaseLedger();
        }

        private static void OpenApothecaryCaseDecisions()
        {
            ApothecaryCaseData data = ApothecaryCaseDatabase.Load().Cases[0];
            ApothecaryCases.HydrateFrom(new ApothecaryCaseSnapshot
            {
                Ids = new[] { data.Id },
                Stages = new[] { (int)ApothecaryCaseStage.Investigating },
                StartedDays = new[] { 4 },
                EvidenceMasks = new[] { 3 },
                InterviewMasks = new[] { 3 },
                DecisionIds = new[] { string.Empty },
                FollowUpDays = new[] { 0 },
                ResolvedDays = new[] { 0 },
            });
            GameScores.HydrateFrom(new SaveSlotMeta
            {
                GameDay = 4,
                GameFlagIds = new[] { "apothecary_story_complete" },
            });
            ApothecaryRuntime.HydrateFrom(new ApothecarySnapshot
            {
                ProductIds = data.Decisions.Select(decision => decision.preparation.ResultId).ToArray(),
                ProductCounts = data.Decisions.Select(_ => 3).ToArray(),
                CraftedRecipeIds = data.Decisions.Select(decision => decision.preparation.Id).ToArray(),
            });
            OpenCaseLedger();
        }

        private static void OpenCaseLedger()
        {
            ApothecaryCaseLedgerStation station = FindLive<ApothecaryCaseLedgerStation>() ??
                throw new InvalidOperationException("Apothecary case ledger station is unavailable.");
            ApothecaryCaseScreen.Ensure().Open(station);
        }

        private static void StageGameplayHud(bool requestTracker)
        {
            QuestData quest = LoadAssets<QuestData>().Where(item => item != null)
                .OrderByDescending(item => Localization.Get(item.ObjectiveTextId)?.Length ?? 0)
                .FirstOrDefault() ?? throw new InvalidOperationException("No quest is available.");
            QuestManager.HydrateFrom(Array.Empty<string>(), Array.Empty<string>());
            QuestManager.StartQuest(quest);
            if (requestTracker)
            {
                VillageRequestData request = LoadAssets<VillageRequestData>()
                    .Where(item => item != null)
                    .OrderByDescending(item => Localization.Get(item.DescriptionId)?.Length ?? 0)
                    .FirstOrDefault();
                if (request != null) VillageRequests.Track(request);
                VillageRequestTrackerHUD.Ensure();
            }
            ForceGameplayHudVisible();
        }

        private static void OpenRestorationLedger()
        {
            RestorationProjectData project = LoadAssets<RestorationProjectData>()
                .Where(item => item != null)
                .OrderByDescending(item => Localization.Get(item.SummaryId)?.Length ?? 0)
                .FirstOrDefault() ?? throw new InvalidOperationException("No restoration project is available.");
            RestorationLedgerScreen.OpenProject(project);
        }

        private static void OpenEndingCredits()
        {
            EndingData ending = LoadAssets<EndingData>().FirstOrDefault(item => item != null) ??
                throw new InvalidOperationException("No ending is available.");
            if (!EndingCreditsScreen.Show(ending, null))
                throw new InvalidOperationException("Ending credits could not open.");
        }

        private static void OpenIntroGuide()
        {
            IntroGuide guide = FindLive<IntroGuide>() ??
                throw new InvalidOperationException("IntroGuide is unavailable.");
            guide.ShowOnce();
        }

        private static void OpenStoryCardToast()
        {
            StoryCardToast toast = StoryCardToast.Instance ?? FindLive<StoryCardToast>() ??
                throw new InvalidOperationException("StoryCardToast is unavailable.");
            StoryCardData card = LoadAssets<StoryCardData>().Where(item => item != null)
                .OrderByDescending(item => JournalText.StorySubtitle(item)?.Length ?? 0)
                .FirstOrDefault() ?? throw new InvalidOperationException("No story card is available.");
            Invoke(toast, "BuildIfNeeded");
            GetField<RectTransform>(toast, "_card").gameObject.SetActive(true);
            Invoke(toast, "PopulateCard", card);
            Invoke(toast, "SetX", -28f);
        }

        private static void OpenKeyItemToast()
        {
            KeyItemToast toast = FindLive<KeyItemToast>() ??
                throw new InvalidOperationException("KeyItemToast is unavailable.");
            Invoke(toast, "BuildIfNeeded");
            GetField<RectTransform>(toast, "_card").gameObject.SetActive(true);
            GetField<TMP_Text>(toast, "_eyebrow").text = Localization.Get("toast.received").ToUpperInvariant();
            GetField<TMP_Text>(toast, "_title").text = Localization.Get("item.fathers_journal.name");
            Invoke(toast, "SetX", -28f);
        }

        private static void OpenRegionArrivalToast()
        {
            RegionArrivalToast toast = FindLive<RegionArrivalToast>() ??
                throw new InvalidOperationException("RegionArrivalToast is unavailable.");
            toast.PreviewRegion("old_wood", true);
        }

        private static void OpenRestorationToast()
        {
            RestorationProjectData project = LoadAssets<RestorationProjectData>()
                .Where(item => item != null)
                .OrderByDescending(item => Localization.Get(item.BenefitId)?.Length ?? 0)
                .FirstOrDefault() ?? throw new InvalidOperationException("No restoration project is available.");
            RestorationCompletionToast.Show(project);
        }

        private static void OpenNarration(bool cinematic)
        {
            NarrationOverlay overlay = NarrationOverlay.Instance ??
                throw new InvalidOperationException("NarrationOverlay is unavailable.");
            string[] captions =
            {
                "The fen kept every promise in the shape of a path, and every warning in the water.",
                "Wren closed the journal only after the village lights answered one another.",
            };
            if (!cinematic) overlay.Show(captions);
            else
            {
                StoryCardData card = LoadAssets<StoryCardData>().FirstOrDefault(item =>
                    item != null && item.Image != null);
                overlay.ShowCinematic(captions, null, card != null ? card.Image : null);
            }
        }

        private static void OpenCuttingHud()
        {
            GameObject host = new GameObject("_FullUIAuditCuttingHUD");
            AuditObjects.Add(host);
            Type hudType = typeof(InventoryScreen).Assembly.GetType(
                "Hollowfen.Foraging.ForageCuttingHUD") ??
                throw new MissingMemberException("Hollowfen.Foraging.ForageCuttingHUD");
            Component hud = host.AddComponent(hudType);
            Invoke(hud, "Build", "Velvet Shank", true);
            Invoke(hud, "UpdateInput", new Vector2(.45f, -.2f),
                new Vector2(.7f, .15f), .78f, true);
            Invoke(hud, "SetProgress", .62f, 4, 7);
        }

        private static void ClosePresentations()
        {
            foreach (GameObject auditObject in AuditObjects)
                if (auditObject != null) UnityEngine.Object.Destroy(auditObject);
            AuditObjects.Clear();
            if (ApothecaryScreen.Instance != null) ApothecaryScreen.Instance.Close();
            if (ApothecaryCaseScreen.Instance != null) ApothecaryCaseScreen.Instance.Close();
            if (CultivationScreen.Instance != null) CultivationScreen.Instance.Close();
            if (VillageRequestScreen.Instance != null) VillageRequestScreen.Instance.Close();
            if (InventoryScreen.Instance != null) InventoryScreen.Instance.Close();
            if (MapScreenExists(out MapScreen map)) map.Close();
            if (DialogueScreen.Instance != null) DialogueScreen.Instance.Close();
            if (InspectScreen.Instance != null && InspectScreen.Instance.IsOpen)
                Invoke(InspectScreen.Instance, "Hide");
            if (NarrationOverlay.Instance != null && NarrationOverlay.Instance.IsShowing)
                NarrationOverlay.Instance.SkipAll();
            IntroGuide guide = FindLive<IntroGuide>();
            if (guide != null)
            {
                guide.StopAllCoroutines();
                Canvas guideCanvas = GetField<Canvas>(guide, "_canvas");
                if (guideCanvas != null) guideCanvas.gameObject.SetActive(false);
                SetFieldValue(guide, "_running", null);
                SetFieldValue(guide, "_closing", false);
                Invoke(guide, "ReleasePresentation");
            }
            StoryCardToast storyToast = StoryCardToast.Instance ?? FindLive<StoryCardToast>();
            if (storyToast != null)
            {
                storyToast.StopAllCoroutines();
                Invoke(storyToast, "SetX", 10000f);
                RectTransform card = GetField<RectTransform>(storyToast, "_card");
                if (card != null) card.gameObject.SetActive(false);
            }
            KeyItemToast keyToast = FindLive<KeyItemToast>();
            if (keyToast != null)
            {
                keyToast.StopAllCoroutines();
                Invoke(keyToast, "SetX", 10000f);
                RectTransform card = GetField<RectTransform>(keyToast, "_card");
                if (card != null) card.gameObject.SetActive(false);
            }
            RestorationCompletionToast restorationToast = FindLive<RestorationCompletionToast>();
            if (restorationToast != null)
            {
                restorationToast.StopAllCoroutines();
                CanvasGroup toastGroup = GetField<CanvasGroup>(restorationToast, "_group");
                if (toastGroup != null) toastGroup.alpha = 0f;
                SetFieldValue(restorationToast, "_routine", null);
            }
            RegionArrivalToast region = FindLive<RegionArrivalToast>();
            if (region != null) region.HideImmediate();
            UIManager.Instance?.CloseAll();
            Time.timeScale = 1f;
        }

        private static void ForceGameplayHudVisible()
        {
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (!IsLive(canvas) || !canvas.gameObject.activeInHierarchy) continue;
                if (canvas.sortingOrder > 20) continue;
                canvas.enabled = true;
                CanvasGroup group = canvas.GetComponent<CanvasGroup>();
                if (group != null) group.alpha = 1f;
            }
        }

        private static bool MapScreenExists(out MapScreen screen)
        {
            screen = FindLive<MapScreen>();
            return screen != null && screen.IsOpen;
        }

        private static T[] LoadAssets<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name,
                    new[] { "Assets/_Hollowfen" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                .ToArray();
        }

        private static T FindLive<T>() where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(IsLive);
        }

        private static object Invoke(object target, string method, params object[] arguments)
        {
            MethodInfo info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingMethodException(target.GetType().FullName, method);
            return info.Invoke(target, arguments);
        }

        private static T GetField<T>(object target, string field) where T : class
        {
            FieldInfo info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingFieldException(target.GetType().FullName, field);
            return info.GetValue(target) as T;
        }

        private static void SetFieldValue(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingFieldException(target.GetType().FullName, field);
            info.SetValue(target, value);
        }

        private static bool IsClippingOverflowMode(TextOverflowModes mode) =>
            mode == TextOverflowModes.Truncate || mode == TextOverflowModes.Ellipsis ||
            mode == TextOverflowModes.Masking;

        private static bool IsLive(Component component) =>
            component != null && component.gameObject.scene.IsValid();

        private static bool IsVisible(TMP_Text text)
        {
            if (!IsLive(text) || !text.enabled || !text.gameObject.activeInHierarchy ||
                text.color.a <= .01f || string.IsNullOrWhiteSpace(text.text)) return false;
            float alpha = text.color.a;
            foreach (CanvasGroup group in text.GetComponentsInParent<CanvasGroup>(true))
                alpha *= group.alpha;
            Canvas canvas = text.GetComponentInParent<Canvas>();
            return alpha > .03f && canvas != null && canvas.enabled;
        }

        private static bool IsDecorativeMicrocopy(TMP_Text text)
        {
            string name = text.name.ToLowerInvariant();
            return name.Contains("edition") || name.Contains("counter") ||
                   name.Contains("credit") || name.Contains("mark");
        }

        private static bool IsOffscreen(RectTransform rect, Canvas canvas)
        {
            if (rect == null || canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                return false;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    corners[i]);
                if (screen.Contains(point)) return false;
            }
            return true;
        }

        private static string Compact(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            value = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return value.Length <= 90 ? value : value.Substring(0, 87) + "…";
        }

        private static string HierarchyPath(Transform transform)
        {
            var parts = new List<string>();
            for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
                parts.Add(cursor.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static void RestoreIntPreference(string key, bool existed, int value,
            Action<int> setter)
        {
            if (existed) setter(value);
            else
            {
                PlayerPrefs.DeleteKey(key);
                setter(0);
                PlayerPrefs.DeleteKey(key);
            }
        }

        private static void RestoreBoolPreference(string key, bool existed, int value,
            Action<bool> setter)
        {
            if (existed) setter(value == 1);
            else
            {
                PlayerPrefs.DeleteKey(key);
                setter(false);
                PlayerPrefs.DeleteKey(key);
            }
        }

        private static void RequirePlayMode()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Full UI audit staging requires Play Mode.");
        }
    }
}
#endif
