using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using Hollowfen.Foraging;
using Hollowfen.Map;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using Hollowfen.Requests;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Asset-layer data integrity checks. These are the project's "EditMode tests":
    /// they live in an editor utility rather than a Unity Test Framework assembly
    /// because game code compiles into Assembly-CSharp (coupled to StarterAssets /
    /// Magic Pig sources with no asmdefs) and test assemblies cannot reference it.
    ///
    /// Run: menu "Hollowfen/Data Integrity Report", over the MCP bridge via
    /// RunAllAsReport(), or batchmode -executeMethod ...DataIntegrity.RunCLI.
    /// Manifest of what each check proves: Docs/tests.md.
    /// </summary>
    public static class DataIntegrity
    {
        private const string DataRoot = "Assets/_Hollowfen/Data";

        // Relationship-score keys are bible cast ids; an NPC asset is NOT required
        // (GameScores keys by string), but an id outside this set is a typo.
        private static readonly HashSet<string> CanonNpcIds = new HashSet<string>
        { "bram", "marra", "edda", "almy", "joren", "voss", "theo", "hollin", "calden", "aldric", "pell" };

        // Localization ids consumed by live code paths. Localization.Get returns the
        // raw id on a miss, so a missing entry never errors at runtime — only here.
        private static readonly string[] ConsumedPromptIds =
        {
            "prompt.inspect.verb", "prompt.npc.talk", "prompt.request.view", "prompt.request.deliver",
            "prompt.plant.verb", "prompt.examine.verb",
            "prompt.door.unlock", "growbed.name", "inspect.locked.knowledge", "prompt.rest.verb",
            "rest.mill_hearth.name", "rest.confirm.dusk.title", "rest.confirm.dusk.body",
            "rest.confirm.dawn.title", "rest.confirm.dawn.body", "rest.transition.dusk", "rest.transition.dawn",
            "cultivation.eyebrow", "cultivation.title", "cultivation.recipe.details", "cultivation.cancel"
        };

        private static readonly string[] ConsumedJournalIds =
        {
            "journal.eyebrow", "journal.close", "journal.back", "journal.previous", "journal.next", "journal.page",
            "journal.hint.index", "journal.hint.reader", "journal.hint.specimen", "journal.hint.wren",
            "journal.story.title", "journal.story.counter", "journal.story.cards", "journal.story.locked",
            "journal.story.locked_title", "journal.story.locked_body", "journal.story.annotations",
            "journal.story.hide_annotations", "journal.story.no_note",
            "journal.field.title", "journal.field.counter", "journal.field.unknown", "journal.field.unknown_label",
            "journal.field.missing_photo", "journal.field.model_pending", "journal.field.model_caption",
            "journal.field.model_badge", "journal.field.photo_heading", "journal.field.features", "journal.field.note", "journal.field.habitat",
            "journal.field.season", "journal.field.lookalikes", "journal.field.photo_credit",
            "journal.wren.title", "journal.wren.background", "journal.wren.perspective", "journal.wren.carries", "journal.wren.studies",
            "journal.wren.age", "journal.wren.home", "journal.wren.work", "journal.wren.keepsake",
            "journal.wren.model_badge", "journal.wren.model_caption", "journal.wren.model_pending",
            "journal.wren.plate.study", "journal.wren.plate.front", "journal.wren.plate.back",
            "journal.wren.plate.three_quarter", "journal.wren.plate.knife"
        };

        private static readonly string[] ConsumedEndingIds =
        {
            "ending.choice.speaker", "ending.save.complete", "ending.credits.eyebrow",
            "ending.credits.saved", "ending.credits.heading", "ending.credits.return",
            "ending.credits.remain", "ending.credits.hint"
        };

        private static readonly string[] ConsumedRequestIds =
        {
            "request.kind.kitchen", "request.kind.medicine", "request.kind.market", "request.kind.gathering",
            "request.requirements", "request.reward.copper", "request.reward.story", "request.deliver",
            "request.missing", "request.talk_instead", "request.leave", "request.continue",
            "request.completed.eyebrow", "request.completed.title", "request.completed.body",
            "request.completed.story_body", "request.completed.first", "request.completed.repeat",
            "request.tracker.story", "request.tracker.daily"
        };

        public enum Severity { Error, Warn, Info }

        public class Issue
        {
            public Severity Severity;
            public string Category;
            public string Asset;
            public string Message;
        }

        // ---------- entry points ----------

        [MenuItem("Hollowfen/Data Integrity Report")]
        public static void RunMenu() => Debug.Log(RunAllAsReport());

        /// <summary>Batchmode gate: exit 2 on errors, 0 otherwise.</summary>
        public static void RunCLI()
        {
            var issues = RunAll();
            Debug.Log(Format(issues));
            EditorApplication.Exit(issues.Any(i => i.Severity == Severity.Error) ? 2 : 0);
        }

        public static string RunAllAsReport() => Format(RunAll());

        public static List<Issue> RunAll()
        {
            var issues = new List<Issue>();
            var quests = LoadAll<QuestData>();
            var dialogues = LoadAll<DialogueData>();
            var npcs = LoadAll<NPCData>();
            var locations = LoadAll<LocationData>();
            var mushrooms = LoadAll<MushroomFieldGuideData>();
            var stories = LoadAll<StoryCardData>();
            var storyMoments = LoadAll<StoryMomentData>();
            var characters = LoadAll<CharacterProfileData>();
            var endings = LoadAll<EndingData>();
            var requests = LoadAll<VillageRequestData>();
            var loc = LocalizationTable(issues);

            CheckQuests(issues, quests, loc);
            CheckQuestChain(issues, quests);
            CheckDialogues(issues, dialogues, quests);
            CheckNpcs(issues, npcs, loc);
            CheckScoreHooks(issues, quests, mushrooms);
            CheckDatabases(issues);
            CheckJournalContent(issues, stories, mushrooms, characters);
            CheckStoryMoments(issues, quests, dialogues, stories, storyMoments, loc);
            CheckEndings(issues, endings, dialogues);
            CheckVillageRequests(issues, requests, quests, loc);
            CheckLocations(issues, locations, loc);
            CheckPromptIds(issues, loc);
            CheckBuildSettings(issues);

            issues.Add(New(Severity.Info, "coverage", "(project)",
                $"checked {quests.Count} quests, {dialogues.Count} dialogues, {npcs.Count} NPCs, {locations.Count} locations, {mushrooms.Count} mushrooms, {stories.Count} story cards, {storyMoments.Count} story moments, {characters.Count} character profiles, {endings.Count} endings, {requests.Count} village requests"));
            return issues;
        }

        // ---------- checks ----------

        private static void CheckQuests(List<Issue> issues, List<QuestData> quests, Dictionary<string, string> loc)
        {
            var seen = new HashSet<string>();
            foreach (var q in quests)
            {
                var path = Path(q);
                if (string.IsNullOrWhiteSpace(q.Id))
                    issues.Add(New(Severity.Error, "quest-id", path, "empty _id"));
                else if (!seen.Add(q.Id))
                    issues.Add(New(Severity.Error, "quest-id", path, $"duplicate quest id '{q.Id}'"));

                RequireLoc(issues, loc, q.DisplayNameId, path, "DisplayNameId (rendered by QuestHUD)");
                RequireLoc(issues, loc, q.ObjectiveTextId, path, "ObjectiveTextId (rendered by QuestHUD)");
                CheckRelationshipArrays(issues, path, q.RelationshipNpcIds, q.RelationshipDeltas);
            }
        }

        private static void CheckQuestChain(List<Issue> issues, List<QuestData> quests)
        {
            var referenced = new HashSet<QuestData>(quests.Select(q => q.NextQuest).Where(n => n != null));
            var roots = quests.Where(q => !referenced.Contains(q)).ToList();
            if (roots.Count != 1)
                issues.Add(New(Severity.Warn, "quest-chain", "(chain)",
                    $"expected exactly 1 chain root, found {roots.Count}: {string.Join(", ", roots.Select(r => r.Id))}"));

            var reachable = new HashSet<QuestData>();
            foreach (var root in roots)
            {
                var pathSeen = new HashSet<QuestData>();
                var cur = root;
                var hops = 0;
                while (cur != null && hops++ < 128)
                {
                    if (!pathSeen.Add(cur))
                    {
                        issues.Add(New(Severity.Error, "quest-chain", Path(cur), $"NextQuest cycle at '{cur.Id}'"));
                        break;
                    }
                    reachable.Add(cur);
                    cur = cur.NextQuest;
                }
            }
            foreach (var q in quests.Where(q => !reachable.Contains(q)))
                issues.Add(New(Severity.Warn, "quest-chain", Path(q), $"'{q.Id}' unreachable from the chain root"));
        }

        private static void CheckDialogues(List<Issue> issues, List<DialogueData> dialogues, List<QuestData> quests)
        {
            var speakerColors = StaticDict<string, Color>(typeof(DialogueScreen), "SpeakerColors", issues);
            var questSet = new HashSet<QuestData>(quests);
            foreach (var problem in DialogueVoiceoverImporter.ManifestProblems(dialogues))
                issues.Add(New(Severity.Error, "dialogue-voice-manifest",
                    DialogueVoiceoverImporter.ManifestPath, problem));

            foreach (var d in dialogues)
            {
                var path = Path(d);
                if (d.Lines == null || d.Lines.Length == 0)
                {
                    issues.Add(New(Severity.Error, "dialogue-lines", path, "no lines"));
                    continue;
                }
                for (int lineIndex = 0; lineIndex < d.Lines.Length; lineIndex++)
                {
                    var line = d.Lines[lineIndex];
                    if (string.IsNullOrWhiteSpace(line.speaker) || string.IsNullOrWhiteSpace(line.text))
                        issues.Add(New(Severity.Error, "dialogue-lines", path, "line with empty speaker or text"));
                    else if (speakerColors != null && !speakerColors.ContainsKey(line.speaker))
                        issues.Add(New(Severity.Warn, "dialogue-speaker", path,
                            $"speaker '{line.speaker}' not in DialogueScreen.SpeakerColors (renders default ink)"));

                    // ClipPath deliberately rejects an empty speaker; keep corrupt-content
                    // reporting inside DataIntegrity instead of letting that guard abort the report.
                    if (string.IsNullOrWhiteSpace(line.speaker)) continue;

                    string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
                    string expectedVoicePath = DialogueVoiceoverImporter.ClipPath(assetName, lineIndex, line.speaker);
                    if (line.voiceClip == null)
                    {
                        issues.Add(New(Severity.Error, "dialogue-voice", path,
                            $"line {lineIndex} ({line.speaker}) has no voice clip; expected {expectedVoicePath}"));
                    }
                    else
                    {
                        string actualVoicePath = AssetDatabase.GetAssetPath(line.voiceClip);
                        if (!string.Equals(actualVoicePath, expectedVoicePath, System.StringComparison.Ordinal))
                            issues.Add(New(Severity.Error, "dialogue-voice", path,
                                $"line {lineIndex} ({line.speaker}) is wired to {actualVoicePath}; expected {expectedVoicePath}"));
                        if (line.voiceClip.channels != 1 || line.voiceClip.frequency != 24000)
                            issues.Add(New(Severity.Error, "dialogue-voice-format", actualVoicePath,
                                $"expected 24kHz mono spoken-word source; found {line.voiceClip.frequency}Hz / {line.voiceClip.channels}ch"));
                    }
                }

                CheckRelationshipArrays(issues, path, d.RelationshipNpcIds, d.RelationshipDeltas);

                if (d.SellsForageBasket && d.BasketCopperPerItem > 0 && d.BasketBuyer == MushroomBuyer.None)
                    issues.Add(New(Severity.Error, "mushroom-economy", path,
                        "repeat basket sale has a flat price but no species-aware buyer"));

                if (d.SetFlagIds != null && d.SetFlagIds.Any(string.IsNullOrWhiteSpace))
                    issues.Add(New(Severity.Error, "dialogue-flags", path, "empty string in SetFlagIds"));

                if (d.CompleteQuest != null && !questSet.Contains(d.CompleteQuest))
                    issues.Add(New(Severity.Warn, "dialogue-quest", path,
                        $"CompleteQuest '{d.CompleteQuest.Id}' is outside {DataRoot}/Quests"));

                if (d.TransitionMoment != null && d.Id.IndexOf("repeat", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    issues.Add(New(Severity.Error, "story-moment-repeat", path,
                        "repeat dialogue may not replay a one-time story moment"));

                if (d.Choices != null && d.Choices.Length > 0)
                {
                    if (d.Choices.Length > 4)
                        issues.Add(New(Severity.Error, "dialogue-choices", path, $"{d.Choices.Length} choices — the input scheme supports max 4"));
                    for (int i = 0; i < d.Choices.Length; i++)
                    {
                        if (string.IsNullOrWhiteSpace(d.Choices[i].text))
                            issues.Add(New(Severity.Error, "dialogue-choices", path, $"choice {i} has empty text"));
                        if (d.Choices[i].next != null && d.Choices[i].ending != null)
                            issues.Add(New(Severity.Error, "dialogue-choices", path, $"choice {i} has both a dialogue branch and an ending"));
                        if (d.Choices[i].ending != null && !string.IsNullOrWhiteSpace(d.Choices[i].setsFlagId))
                            issues.Add(New(Severity.Error, "dialogue-choices", path, $"ending choice {i} also sets a loose flag; consequences belong on EndingData"));
                    }
                    if (d.NextDialog != null)
                        issues.Add(New(Severity.Warn, "dialogue-choices", path, "has both NextDialog and Choices — NextDialog is ignored at runtime"));
                }

                // Chains + choice branches form a graph; a cycle traps the player at timeScale 0.
                if (HasDialogueCycle(d))
                    issues.Add(New(Severity.Error, "dialogue-chain", path, "cycle via NextDialog/Choices"));
            }
        }

        private static bool HasDialogueCycle(DialogueData root)
        {
            return CycleDfs(root, new HashSet<DialogueData>(), 0);
        }

        private static void CheckEndings(List<Issue> issues, List<EndingData> endings, List<DialogueData> dialogues)
        {
            var canonicalFlags = new HashSet<string>(EndingResolver.CanonicalEndingFlags);
            if (endings.Count != canonicalFlags.Count)
                issues.Add(New(Severity.Error, "ending-count", EndingRootLabel(),
                    $"expected {canonicalFlags.Count} canonical ending assets, found {endings.Count}"));

            var ids = new HashSet<string>();
            var flags = new HashSet<string>();
            var cards = new HashSet<StoryCardData>();
            foreach (var ending in endings)
            {
                var path = Path(ending);
                if (string.IsNullOrWhiteSpace(ending.Id))
                    issues.Add(New(Severity.Error, "ending-id", path, "empty ending id"));
                else if (!ids.Add(ending.Id))
                    issues.Add(New(Severity.Error, "ending-id", path, $"duplicate ending id '{ending.Id}'"));

                if (!canonicalFlags.Contains(ending.EndingFlagId))
                    issues.Add(New(Severity.Error, "ending-flag", path, $"'{ending.EndingFlagId}' is not a canonical ending flag"));
                else if (!flags.Add(ending.EndingFlagId))
                    issues.Add(New(Severity.Error, "ending-flag", path, $"duplicate ending flag '{ending.EndingFlagId}'"));

                if (string.IsNullOrWhiteSpace(ending.ChoiceText) || string.IsNullOrWhiteSpace(ending.ChoiceContext) ||
                    string.IsNullOrWhiteSpace(ending.LockedHint))
                    issues.Add(New(Severity.Error, "ending-content", path, "choice text, context, and locked hint are required"));
                if (ending.ResolutionDialogue == null)
                    issues.Add(New(Severity.Error, "ending-content", path, "missing final resolution dialogue"));
                else if (!dialogues.Contains(ending.ResolutionDialogue))
                    issues.Add(New(Severity.Error, "ending-content", path, "resolution dialogue is outside the canonical data root"));
                if (ending.StoryCard == null)
                    issues.Add(New(Severity.Error, "ending-content", path, "missing ending story card"));
                else
                {
                    if (!cards.Add(ending.StoryCard))
                        issues.Add(New(Severity.Error, "ending-content", path, "story card is shared by more than one ending"));
                    if (ending.StoryCard.Image == null)
                        issues.Add(New(Severity.Error, "ending-content", path, "ending story card has no epilogue art"));
                    if (ending.StoryCard.UnlockAt != -1)
                        issues.Add(New(Severity.Error, "ending-unlock", Path(ending.StoryCard),
                            "ending cards must use _unlockAt -1 and unlock only after the exclusive choice"));
                }
                if (ending.EpilogueCaptions == null || ending.EpilogueCaptions.Length < 2 ||
                    ending.EpilogueCaptions.Any(string.IsNullOrWhiteSpace))
                    issues.Add(New(Severity.Error, "ending-content", path, "epilogue needs at least two non-empty caption beats"));
                if (string.IsNullOrWhiteSpace(ending.AchievementId))
                    issues.Add(New(Severity.Error, "ending-content", path, "missing ending achievement id"));
                if (ending.RequiredFlagIds == null || !ending.RequiredFlagIds.Contains("final_choice_available"))
                    issues.Add(New(Severity.Error, "ending-gate", path, "ending must require final_choice_available"));
                if (ending.RequiredFlagIds != null && ending.RequiredFlagIds.Any(string.IsNullOrWhiteSpace))
                    issues.Add(New(Severity.Error, "ending-gate", path, "required flags contain an empty id"));
                if (ending.ConsequenceFlagIds != null && ending.ConsequenceFlagIds.Any(string.IsNullOrWhiteSpace))
                    issues.Add(New(Severity.Error, "ending-consequence", path, "consequence flags contain an empty id"));
                CheckRelationshipArrays(issues, path, ending.RelationshipNpcIds, ending.MinimumRelationshipValues);
            }

            foreach (var flag in canonicalFlags.Where(flag => !flags.Contains(flag)))
                issues.Add(New(Severity.Error, "ending-flag", EndingRootLabel(), $"missing canonical ending flag '{flag}'"));

            var meeting = dialogues.FirstOrDefault(d => d.Id == "act4.meet.aldric");
            if (meeting == null)
            {
                issues.Add(New(Severity.Error, "ending-choice", EndingRootLabel(), "missing act4.meet.aldric dialogue"));
                return;
            }
            if (meeting.Choices == null || meeting.Choices.Length != canonicalFlags.Count)
            {
                issues.Add(New(Severity.Error, "ending-choice", Path(meeting),
                    $"final meeting must expose exactly {canonicalFlags.Count} ending choices"));
                return;
            }
            var wired = new HashSet<EndingData>();
            for (int i = 0; i < meeting.Choices.Length; i++)
            {
                var choice = meeting.Choices[i];
                if (choice.ending == null)
                    issues.Add(New(Severity.Error, "ending-choice", Path(meeting), $"choice {i} has no EndingData"));
                else if (!wired.Add(choice.ending))
                    issues.Add(New(Severity.Error, "ending-choice", Path(meeting), $"choice {i} repeats ending '{choice.ending.Id}'"));
                if (choice.next != null)
                    issues.Add(New(Severity.Error, "ending-choice", Path(meeting), $"choice {i} branches to dialogue instead of resolving its ending"));
            }
            foreach (var ending in endings.Where(ending => !wired.Contains(ending)))
                issues.Add(New(Severity.Error, "ending-choice", Path(meeting), $"ending '{ending.Id}' is not wired into the decision"));
        }

        private static string EndingRootLabel() => DataRoot + "/Endings";

        private static bool CycleDfs(DialogueData node, HashSet<DialogueData> onPath, int depth)
        {
            if (node == null || depth > 64) return false;
            if (!onPath.Add(node)) return true;
            bool cycle = CycleDfs(node.NextDialog, onPath, depth + 1);
            if (!cycle && node.Choices != null)
                foreach (var c in node.Choices)
                    if (CycleDfs(c.next, onPath, depth + 1)) { cycle = true; break; }
            onPath.Remove(node);
            return cycle;
        }

        private static void CheckNpcs(List<Issue> issues, List<NPCData> npcs, Dictionary<string, string> loc)
        {
            var entriesField = typeof(NPCData).GetField("_dialogueEntries", BindingFlags.NonPublic | BindingFlags.Instance);
            var seen = new HashSet<string>();
            foreach (var n in npcs)
            {
                var path = Path(n);
                if (string.IsNullOrWhiteSpace(n.Id))
                    issues.Add(New(Severity.Error, "npc-id", path, "empty _id"));
                else
                {
                    if (!seen.Add(n.Id)) issues.Add(New(Severity.Error, "npc-id", path, $"duplicate npc id '{n.Id}'"));
                    if (n.Id != n.Id.ToLowerInvariant()) issues.Add(New(Severity.Error, "npc-id", path, $"id '{n.Id}' must be lowercase"));
                    if (!CanonNpcIds.Contains(n.Id)) issues.Add(New(Severity.Warn, "npc-id", path, $"id '{n.Id}' not in the bible cast list"));
                }

                RequireLoc(issues, loc, n.DisplayNameId, path, "DisplayNameId (rendered by the interaction prompt)");

                var entries = entriesField?.GetValue(n) as NPCDialogueEntry[];
                if (entries == null || entries.Length == 0)
                {
                    if (n.RepeatDialog == null)
                        issues.Add(New(Severity.Warn, "npc-entries", path, "no entries and no repeat dialog — NPC can never talk"));
                    continue;
                }
                for (int i = 0; i < entries.Length; i++)
                {
                    var e = entries[i];
                    if (e.dialog == null)
                        issues.Add(New(Severity.Warn, "npc-entries", path, $"entry {i} has no dialog (silently skipped at runtime)"));

                    // PickDialog is first-match-wins: an unconditional entry above others
                    // permanently shadows everything after it.
                    var unconditional = e.activeQuest == null && e.requiresQuestCompleted == null
                        && string.IsNullOrEmpty(e.requiresFlagId) && e.requiresCoinsCopper <= 0 && !e.requiresBasketNonEmpty;
                    if (unconditional && e.dialog != null && i < entries.Length - 1)
                        issues.Add(New(Severity.Error, "npc-entries", path,
                            $"entry {i} is unconditional — entries {i + 1}..{entries.Length - 1} are unreachable"));
                }
                if (n.RepeatDialog == null)
                    issues.Add(New(Severity.Info, "npc-entries", path, "no repeat dialog — NPC goes silent outside entry conditions (intentional for Voss-style windows)"));
            }
        }

        private static void CheckScoreHooks(List<Issue> issues, List<QuestData> quests, List<MushroomFieldGuideData> mushrooms)
        {
            var questIds = new HashSet<string>(quests.Select(q => q.Id));
            var mushroomIds = new HashSet<string>(mushrooms.Select(m => m.Id));

            var questFlags = StaticDict<string, string[]>(typeof(ScoreHooks), "QuestFlags", issues);
            if (questFlags != null)
                foreach (var key in questFlags.Keys.Where(k => !questIds.Contains(k)))
                    issues.Add(New(Severity.Error, "scorehooks", "ScoreHooks.cs", $"QuestFlags key '{key}' matches no quest asset id"));

            var speciesFlags = StaticDict<string, string>(typeof(ScoreHooks), "SpeciesFlags", issues);
            if (speciesFlags != null)
                foreach (var key in speciesFlags.Keys.Where(k => !mushroomIds.Contains(k)))
                    issues.Add(New(Severity.Error, "scorehooks", "ScoreHooks.cs", $"SpeciesFlags key '{key}' matches no mushroom asset id"));
        }

        private static void CheckVillageRequests(List<Issue> issues, List<VillageRequestData> requests,
            List<QuestData> quests, Dictionary<string, string> loc)
        {
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            var questIds = new HashSet<string>(quests.Select(quest => quest.Id), System.StringComparer.Ordinal);
            foreach (var request in requests)
            {
                string path = Path(request);
                if (string.IsNullOrWhiteSpace(request.Id))
                    issues.Add(New(Severity.Error, "village-request", path, "empty request id"));
                else if (!seen.Add(request.Id))
                    issues.Add(New(Severity.Error, "village-request", path, $"duplicate request id '{request.Id}'"));
                if (!CanonNpcIds.Contains(request.NpcId))
                    issues.Add(New(Severity.Error, "village-request", path, $"unknown requester NPC id '{request.NpcId}'"));

                RequireLoc(issues, loc, request.TitleId, path, "request TitleId");
                RequireLoc(issues, loc, request.DescriptionId, path, "request DescriptionId");
                RequireLoc(issues, loc, request.RequesterLineId, path, "request RequesterLineId");
                if (request.HeroImage == null)
                    issues.Add(New(Severity.Error, "village-request", path, "missing illustrated story-card image"));

                int speciesLength = request.RequiredSpecies?.Length ?? 0;
                int countLength = request.RequiredCounts?.Length ?? 0;
                if (speciesLength != countLength)
                    issues.Add(New(Severity.Error, "village-request", path,
                        $"parallel requirement mismatch: {speciesLength} species vs {countLength} counts"));
                if (request.RequirementCount < 1 || request.RequirementCount > 4)
                    issues.Add(New(Severity.Error, "village-request", path,
                        $"request card supports 1–4 requirements; found {request.RequirementCount}"));
                var speciesIds = new HashSet<string>(System.StringComparer.Ordinal);
                for (int i = 0; i < request.RequirementCount; i++)
                {
                    var species = request.RequiredSpecies[i];
                    if (species == null)
                    {
                        issues.Add(New(Severity.Error, "village-request", path, $"requirement {i} has no species"));
                        continue;
                    }
                    if (!speciesIds.Add(species.Id))
                        issues.Add(New(Severity.Error, "village-request", path, $"species '{species.Id}' appears twice"));
                    if (request.RequiredCounts[i] <= 0)
                        issues.Add(New(Severity.Error, "village-request", path, $"requirement {i} count must be positive"));
                    if (species.Edibility == Edibility.Deadly || species.Edibility == Edibility.Psychoactive)
                        issues.Add(New(Severity.Error, "village-request", path,
                            $"unsafe species '{species.Id}' cannot appear in a village delivery"));
                }

                if (request.RequiredFlagIds != null && request.RequiredFlagIds.Any(string.IsNullOrWhiteSpace))
                    issues.Add(New(Severity.Error, "village-request", path, "required flags contain an empty id"));
                if (request.CompletionFlagIds != null && request.CompletionFlagIds.Any(string.IsNullOrWhiteSpace))
                    issues.Add(New(Severity.Error, "village-request", path, "completion flags contain an empty id"));
                if (request.RequiredCompletedQuestIds != null)
                {
                    foreach (string id in request.RequiredCompletedQuestIds)
                    {
                        if (string.IsNullOrWhiteSpace(id))
                            issues.Add(New(Severity.Error, "village-request", path, "required quests contain an empty id"));
                        else if (!questIds.Contains(id))
                            issues.Add(New(Severity.Error, "village-request", path, $"required quest '{id}' does not exist"));
                    }
                }
                if (!string.IsNullOrWhiteSpace(request.ActiveQuestId) && !questIds.Contains(request.ActiveQuestId))
                    issues.Add(New(Severity.Error, "village-request", path,
                        $"active quest gate '{request.ActiveQuestId}' does not exist"));

                if (request.OneShot)
                {
                    if (request.CompleteQuest == null || request.CompletionDialogue == null)
                        issues.Add(New(Severity.Error, "village-request", path,
                            "story request needs an atomic quest completion and follow-up dialogue"));
                    else if (request.CompleteQuest.Id != request.ActiveQuestId)
                        issues.Add(New(Severity.Error, "village-request", path,
                            "story request CompleteQuest must match its active quest gate"));
                }
                else
                {
                    if (request.RewardCopper <= 0)
                        issues.Add(New(Severity.Error, "village-request", path, "recurring work needs a copper reward"));
                    if (request.CompleteQuest != null || request.CompletionDialogue != null)
                        issues.Add(New(Severity.Error, "village-request", path,
                            "recurring work may not own one-shot story outcomes"));
                }
            }

            foreach (string npc in new[] { "marra", "edda", "theo" })
            {
                int count = requests.Count(request => !request.OneShot && request.NpcId == npc);
                if (count < 3)
                    issues.Add(New(Severity.Error, "village-request", RequestRootLabel(),
                        $"{npc} needs at least three rotating requests; found {count}"));
            }
            if (requests.Count(request => request.OneShot && request.Kind == VillageRequestKind.Gathering) != 1)
                issues.Add(New(Severity.Error, "village-request", RequestRootLabel(),
                    "expected exactly one one-shot village gathering"));

            var database = AssetDatabase.LoadAssetAtPath<VillageRequestDatabase>(
                "Assets/_Hollowfen/Resources/VillageRequestDatabase.asset");
            if (database == null)
                issues.Add(New(Severity.Error, "village-request", "Assets/_Hollowfen/Resources",
                    "missing VillageRequestDatabase runtime resource"));
            else
            {
                CheckDbArray(issues, database, "_requests", (VillageRequestData request) => request.Id);
                var databaseSet = new HashSet<VillageRequestData>(database.Requests ?? System.Array.Empty<VillageRequestData>());
                foreach (var request in requests.Where(request => !databaseSet.Contains(request)))
                    issues.Add(New(Severity.Error, "village-request", Path(database),
                        $"request '{request.Id}' is missing from the runtime database"));
                if ((database.Requests?.Length ?? 0) != requests.Count)
                    issues.Add(New(Severity.Error, "village-request", Path(database),
                        $"database has {database.Requests?.Length ?? 0} rows for {requests.Count} authored requests"));
            }
        }

        private static string RequestRootLabel() => DataRoot + "/Requests";

        private static void CheckDatabases(List<Issue> issues)
        {
            foreach (var db in LoadAll<StoryCardDatabase>())
            {
                CheckDbArray(issues, db, "_cards", (StoryCardData c) => c.Id);
                if (db.Count != 30)
                    issues.Add(New(Severity.Error, "journal-database", Path(db), $"expected 30 canonical story cards, found {db.Count}"));
            }
            foreach (var db in LoadAll<MushroomFieldGuideDatabase>())
            {
                CheckDbArray(issues, db, "_entries", (MushroomFieldGuideData m) => m.Id);
                if (db.Count != 21)
                    issues.Add(New(Severity.Error, "journal-database", Path(db), $"expected 21 canonical mushroom entries, found {db.Count}"));
            }
        }

        private static void CheckJournalContent(
            List<Issue> issues,
            List<StoryCardData> stories,
            List<MushroomFieldGuideData> mushrooms,
            List<CharacterProfileData> characters)
        {
            foreach (var card in stories)
            {
                var path = Path(card);
                RequireJournalField(issues, card.Id, path, "story id");
                RequireJournalField(issues, card.DisplayNameId, path, "story DisplayNameId");
                RequireJournalField(issues, card.DescriptionId, path, "story DescriptionId");
                RequireJournalField(issues, card.Title, path, "story title fallback");
                RequireJournalField(issues, card.Body, path, "story body fallback");
                if (card.Image == null)
                    issues.Add(New(Severity.Error, "journal-art", path, "story card has no fullscreen image"));
            }

            foreach (var entry in mushrooms)
            {
                var path = Path(entry);
                RequireJournalField(issues, entry.Id, path, "mushroom id");
                RequireJournalField(issues, entry.DisplayNameId, path, "mushroom DisplayNameId");
                RequireJournalField(issues, entry.DescriptionId, path, "mushroom DescriptionId");
                RequireJournalField(issues, entry.CommonName, path, "mushroom name fallback");
                RequireJournalField(issues, entry.Description, path, "mushroom description fallback");
                if (entry.Photo == null)
                    issues.Add(New(Severity.Info, "journal-art", path, "no photo — Field Guide intentionally renders the missing-sketch state"));

                if ((entry.Edibility == Edibility.Deadly || entry.Edibility == Edibility.Psychoactive) &&
                    (entry.ValueFor(MushroomBuyer.Marra) > 0 || entry.ValueFor(MushroomBuyer.Theo) > 0))
                    issues.Add(New(Severity.Error, "mushroom-economy", path,
                        "deadly/psychoactive species must be refused by both food buyers"));
                if ((entry.Edibility == Edibility.Edible || entry.Edibility == Edibility.Medicinal) &&
                    entry.ValueFor(MushroomBuyer.Marra) <= 0 && entry.ValueFor(MushroomBuyer.Theo) <= 0)
                    issues.Add(New(Severity.Error, "mushroom-economy", path,
                        "safe species has no authored buyer value"));
                if (entry.Tier != ForageTier.BasketCommon && string.IsNullOrWhiteSpace(entry.RequiredForageFlagId))
                    issues.Add(New(Severity.Error, "mushroom-progression", path,
                        $"tier {(int)entry.Tier} species has no forage-unlock flag"));
                if (entry.Cultivable && entry.WorldPrefab == null)
                    issues.Add(New(Severity.Error, "mushroom-cultivation", path,
                        "cultivable species has no world prefab for its crop"));

                if (MushroomModelImporter.HasModelForSpecies(entry.Id))
                {
                    if (entry.WorldPrefab == null)
                        issues.Add(New(Severity.Error, "mushroom-model", path, "delivered species has no gameplay WorldPrefab"));
                    else
                    {
                        var node = entry.WorldPrefab.GetComponent<MushroomNode>();
                        if (node == null)
                            issues.Add(New(Severity.Error, "mushroom-model", path, "WorldPrefab has no MushroomNode"));
                        else if (node.Data != entry)
                            issues.Add(New(Severity.Error, "mushroom-model", path, "WorldPrefab MushroomNode points at a different species"));
                    }
                    if (entry.JournalPreviewPrefab == null)
                        issues.Add(New(Severity.Error, "journal-model", path, "delivered species has no dedicated journal preview prefab"));
                    float expectedExposure = MushroomModelImporter.JournalExposureForSpecies(entry.Id);
                    if (Mathf.Abs(entry.JournalExposure - expectedExposure) > 0.001f)
                        issues.Add(New(Severity.Error, "journal-model", path, $"journal exposure {entry.JournalExposure:F2} does not match manifest {expectedExposure:F2}"));
                }
                else if (entry.Id == "aldermark")
                {
                    issues.Add(New(Severity.Info, "mushroom-model", path, "Aldermark awaits its canon Maitake model"));
                }
            }
            int journalModels = mushrooms.Count(entry => entry.JournalPreviewPrefab != null);
            int cultivable = mushrooms.Count(entry => entry.Cultivable);
            if (cultivable < 3)
                issues.Add(New(Severity.Error, "mushroom-cultivation", "MushroomFieldGuideDatabase",
                    $"expected at least three authored cultivation recipes, found {cultivable}"));
            else
                issues.Add(New(Severity.Info, "mushroom-cultivation", "MushroomFieldGuideDatabase",
                    $"{cultivable} species have data-authored cultivation recipes"));
            int expectedJournalModels = mushrooms.Count(entry => MushroomModelImporter.HasModelForSpecies(entry.Id));
            if (journalModels != expectedJournalModels)
                issues.Add(New(Severity.Error, "journal-model", "MushroomFieldGuideDatabase", $"expected {expectedJournalModels} delivered-species journal models, found {journalModels}"));
            else
                issues.Add(New(Severity.Info, "journal-model", "MushroomFieldGuideDatabase", $"{journalModels} species have dedicated 3D journal models"));

            foreach (var profile in characters)
            {
                var path = Path(profile);
                RequireJournalField(issues, profile.Id, path, "character id");
                RequireJournalField(issues, profile.DisplayNameId, path, "character DisplayNameId");
                RequireJournalField(issues, profile.DescriptionId, path, "character DescriptionId");
                if (profile.HeroPortrait == null)
                    issues.Add(New(Severity.Error, "journal-art", path, "character profile has no hero portrait"));
                if (profile.Id == "wren-tobin" && (profile.StudySheet == null || profile.FigureFront == null ||
                    profile.FigureBack == null || profile.FigureThreeQuarter == null || profile.KnifePlate == null))
                    issues.Add(New(Severity.Error, "journal-art", path, "Wren profile is missing one or more field-study plates"));
                if (profile.Id == "wren-tobin")
                {
                    if (profile.JournalModelPrefab == null)
                        issues.Add(New(Severity.Error, "journal-model", path, "Wren profile has no journal model prefab"));
                    else
                    {
                        GameObject preview = profile.JournalModelPrefab;
                        var skinned = preview.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        long triangles = 0;
                        foreach (SkinnedMeshRenderer renderer in skinned)
                        {
                            Mesh mesh = renderer.sharedMesh;
                            if (mesh == null) continue;
                            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                                triangles += (long)mesh.GetIndexCount(submesh) / 3L;
                        }
                        if (skinned.Length == 0)
                            issues.Add(New(Severity.Error, "journal-model", path, "Wren journal prefab has no skinned renderer"));
                        if (triangles > WrenJournalModelImporter.TriangleBudget)
                            issues.Add(New(Severity.Error, "journal-model", path, $"Wren journal model has {triangles:N0} triangles; budget is {WrenJournalModelImporter.TriangleBudget:N0}"));
                        if (preview.GetComponentsInChildren<Collider>(true).Length > 0)
                            issues.Add(New(Severity.Error, "journal-model", path, "Wren journal prefab must remain collider-free"));
                        if (preview.GetComponentsInChildren<MonoBehaviour>(true).Length > 0)
                            issues.Add(New(Severity.Error, "journal-model", path, "Wren journal prefab must remain visual-only (no MonoBehaviours)"));
                        Animator animator = preview.GetComponentInChildren<Animator>(true);
                        if (animator == null || animator.avatar == null || !animator.avatar.isValid)
                            issues.Add(New(Severity.Error, "journal-model", path, "Wren journal prefab has no valid humanoid avatar"));
                        else
                            issues.Add(New(Severity.Info, "journal-model", path, $"Wren journal study: {triangles:N0} triangles across {skinned.Length} skinned renderer(s)"));
                    }
                    if (profile.JournalIdleClip == null)
                        issues.Add(New(Severity.Error, "journal-model", path, "Wren profile has no journal idle clip"));
                    if (profile.JournalExposure < 0f || profile.JournalExposure > 0.4f)
                        issues.Add(New(Severity.Error, "journal-model", path, "Wren journal exposure is outside the supported 0..0.4 range"));
                }
            }
        }

        private static void CheckStoryMoments(
            List<Issue> issues,
            List<QuestData> quests,
            List<DialogueData> dialogues,
            List<StoryCardData> stories,
            List<StoryMomentData> moments,
            Dictionary<string, string> loc)
        {
            var cardsByQuest = stories
                .Where(card => !string.IsNullOrWhiteSpace(card.QuestId))
                .GroupBy(card => card.QuestId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var momentSet = new HashSet<StoryMomentData>(moments);
            var seenMomentIds = new HashSet<string>();

            // All 26 numbered objectives receive at least the compact card treatment. A quest opts
            // into a richer authored beat by referencing StoryMomentData; unlock and presentation
            // remain separate so previews never mutate journal progression.
            foreach (var quest in quests)
            {
                var path = Path(quest);
                if (!cardsByQuest.TryGetValue(quest.Id, out var matchingCards) || matchingCards.Count != 1)
                {
                    int count = matchingCards != null ? matchingCards.Count : 0;
                    issues.Add(New(Severity.Error, "story-moment-coverage", path,
                        $"quest '{quest.Id}' needs exactly one StoryCardData match; found {count}"));
                    continue;
                }

                var card = matchingCards[0];
                bool questUnlock = quest.UnlockStoryCardOnComplete == card;
                int dialogueUnlocks = dialogues.Count(dialogue => dialogue.UnlockStoryCard == card);
                if (!questUnlock && dialogueUnlocks == 0)
                    issues.Add(New(Severity.Error, "story-moment-unlock", path,
                        $"story card '{card.Id}' is never unlocked by the quest or its dialogue"));
                else if (questUnlock && dialogueUnlocks > 0)
                    issues.Add(New(Severity.Info, "story-moment-unlock", path,
                        $"story card '{card.Id}' has idempotent quest + dialogue unlock routes"));

                if (quest.StoryMoment == null) continue;
                if (!momentSet.Contains(quest.StoryMoment))
                    issues.Add(New(Severity.Error, "story-moment-reference", path,
                        "StoryMoment is outside the canonical StoryMoments data folder"));
                if (quest.StoryMoment.StoryCard != card)
                    issues.Add(New(Severity.Error, "story-moment-card", path,
                        $"StoryMoment card must be the quest's canonical '{card.Id}' card"));
            }

            foreach (var moment in moments)
            {
                var path = Path(moment);
                if (string.IsNullOrWhiteSpace(moment.Id))
                    issues.Add(New(Severity.Error, "story-moment-id", path, "empty story moment id"));
                else if (!seenMomentIds.Add(moment.Id))
                    issues.Add(New(Severity.Error, "story-moment-id", path, $"duplicate id '{moment.Id}'"));

                if (moment.StoryCard == null)
                    issues.Add(New(Severity.Error, "story-moment-card", path, "missing canonical StoryCardData"));

                var captions = moment.ResolveCaptions();
                if (captions.Length == 0)
                    issues.Add(New(Severity.Error, "story-moment-caption", path, "no resolved caption beats"));

                if (moment.CaptionIds != null)
                {
                    for (int i = 0; i < moment.CaptionIds.Length; i++)
                    {
                        string id = moment.CaptionIds[i];
                        bool hasFallback = moment.CaptionFallbacks != null && i < moment.CaptionFallbacks.Length &&
                            !string.IsNullOrWhiteSpace(moment.CaptionFallbacks[i]);
                        if (string.IsNullOrWhiteSpace(id) && !hasFallback)
                            issues.Add(New(Severity.Error, "story-moment-caption", path,
                                $"caption source {i} has neither localization id nor fallback"));
                        else if (!string.IsNullOrWhiteSpace(id) && (loc == null || !loc.ContainsKey(id)) && !hasFallback)
                            issues.Add(New(Severity.Error, "story-moment-localization", path,
                                $"caption id '{id}' is missing and has no English fallback"));
                    }
                }

                if (moment.VoiceClips != null && moment.VoiceClips.Length > 0 && moment.VoiceClips.Length != captions.Length)
                    issues.Add(New(Severity.Error, "story-moment-voice", path,
                        $"{moment.VoiceClips.Length} voice slots for {captions.Length} caption beats"));

                var images = moment.ResolveImages();
                if (images.Length == 0 || images.Any(image => image == null))
                    issues.Add(New(Severity.Error, "story-moment-art", path, "missing painted image"));
                if (moment.BeatImages != null && moment.BeatImages.Length > 0)
                {
                    if (moment.BeatImages.Length != captions.Length)
                        issues.Add(New(Severity.Error, "story-moment-art", path,
                            $"{moment.BeatImages.Length} image mappings for {captions.Length} caption beats"));
                    if (moment.BeatImages.Any(index => index < 0 || index >= images.Length))
                        issues.Add(New(Severity.Error, "story-moment-art", path, "caption maps to an unavailable image"));
                }

                var owningQuests = quests.Where(quest => quest.StoryMoment == moment).ToList();
                if (owningQuests.Count != 1)
                    issues.Add(New(Severity.Error, "story-moment-owner", path,
                        $"expected one owning quest; found {owningQuests.Count}"));

                int dialogueRefs = dialogues.Count(dialogue => dialogue.TransitionMoment == moment);
                if (moment.Trigger == StoryMomentTrigger.DialogueTransition && dialogueRefs != 1)
                    issues.Add(New(Severity.Error, "story-moment-trigger", path,
                        $"dialogue transition moment needs exactly one dialogue reference; found {dialogueRefs}"));
                if (moment.Trigger == StoryMomentTrigger.ManualInteraction && dialogueRefs != 0)
                    issues.Add(New(Severity.Error, "story-moment-trigger", path,
                        "manual interaction moment is also referenced by dialogue"));
            }
        }

        private static void CheckDbArray<T>(List<Issue> issues, ScriptableObject db, string fieldName, System.Func<T, string> id)
            where T : ScriptableObject
        {
            var path = Path(db);
            var arr = db.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(db) as T[];
            if (arr == null)
            {
                issues.Add(New(Severity.Error, "database", path, $"missing array field {fieldName}"));
                return;
            }
            var seen = new HashSet<string>();
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == null)
                    issues.Add(New(Severity.Error, "database", path, $"{fieldName}[{i}] is null"));
                else if (!seen.Add(id(arr[i])))
                    issues.Add(New(Severity.Error, "database", path, $"duplicate id '{id(arr[i])}' at {fieldName}[{i}]"));
            }
        }

        private static void CheckLocations(List<Issue> issues, List<LocationData> locations, Dictionary<string, string> loc)
        {
            foreach (var l in locations)
            {
                RequireLoc(issues, loc, l.DisplayNameId, Path(l), "DisplayNameId (rendered on map chips + side card)");
                RequireLoc(issues, loc, l.ShortDescriptionId, Path(l), "ShortDescriptionId (rendered on map side card)");
            }
        }

        private static void CheckPromptIds(List<Issue> issues, Dictionary<string, string> loc)
        {
            if (loc == null) return;
            foreach (var id in ConsumedPromptIds.Where(id => !loc.ContainsKey(id)))
                issues.Add(New(Severity.Error, "localization", "Localization.cs", $"consumed id '{id}' missing from _table"));
            foreach (var id in ConsumedJournalIds.Where(id => !loc.ContainsKey(id)))
                issues.Add(New(Severity.Error, "localization", "Localization.cs", $"journal id '{id}' missing from _table"));
            foreach (var id in ConsumedEndingIds.Where(id => !loc.ContainsKey(id)))
                issues.Add(New(Severity.Error, "localization", "Localization.cs", $"ending id '{id}' missing from _table"));
            foreach (var id in ConsumedRequestIds.Where(id => !loc.ContainsKey(id)))
                issues.Add(New(Severity.Error, "localization", "Localization.cs", $"request id '{id}' missing from _table"));
        }

        private static void CheckBuildSettings(List<Issue> issues)
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || !scenes[0].path.EndsWith("Scene_MainMenu.unity") || !scenes[0].enabled)
                issues.Add(New(Severity.Error, "build-settings", "(build settings)", "index 0 must be an enabled Scene_MainMenu (boot scene)"));
            if (!scenes.Any(s => s.enabled && s.path.EndsWith("Scene_Hollowfen.unity")))
                issues.Add(New(Severity.Error, "build-settings", "(build settings)", "Scene_Hollowfen missing or disabled"));
            if (LayerMask.NameToLayer("JournalPreview") < 0)
                issues.Add(New(Severity.Error, "build-settings", "TagManager.asset", "JournalPreview layer is required by Field Guide model cameras"));
        }

        // ---------- helpers ----------

        private static void CheckRelationshipArrays(List<Issue> issues, string path, string[] ids, int[] deltas)
        {
            var idLen = ids?.Length ?? 0;
            var deltaLen = deltas?.Length ?? 0;
            if (idLen != deltaLen)
                issues.Add(New(Severity.Error, "relationships", path,
                    $"parallel array mismatch: {idLen} npc ids vs {deltaLen} deltas (extras silently ignored)"));
            if (ids == null) return;
            foreach (var id in ids.Where(id => !CanonNpcIds.Contains(id)))
                issues.Add(New(Severity.Error, "relationships", path, $"relationship id '{id}' is not a bible cast id"));
        }

        private static void RequireLoc(List<Issue> issues, Dictionary<string, string> loc, string id, string path, string what)
        {
            if (loc == null) return;
            if (string.IsNullOrWhiteSpace(id))
                issues.Add(New(Severity.Error, "localization", path, $"empty {what}"));
            else if (!loc.ContainsKey(id))
                issues.Add(New(Severity.Error, "localization", path, $"{what} '{id}' missing from Localization._table (renders the raw id)"));
        }

        private static void RequireJournalField(List<Issue> issues, string value, string path, string what)
        {
            if (string.IsNullOrWhiteSpace(value))
                issues.Add(New(Severity.Error, "journal-content", path, $"empty {what}"));
        }

        private static Dictionary<string, string> LocalizationTable(List<Issue> issues) =>
            StaticDict<string, string>(typeof(Localization), "_table", issues);

        private static Dictionary<TK, TV> StaticDict<TK, TV>(System.Type type, string field, List<Issue> issues)
        {
            var f = type.GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            var value = f?.GetValue(null) as Dictionary<TK, TV>;
            if (value == null)
                issues.Add(New(Severity.Error, "reflection", type.Name,
                    $"could not read {type.Name}.{field} — checker needs updating if the field moved"));
            return value;
        }

        private static List<T> LoadAll<T>() where T : ScriptableObject =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataRoot })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null).ToList();

        private static string Path(Object o) => AssetDatabase.GetAssetPath(o);

        private static Issue New(Severity s, string category, string asset, string message) =>
            new Issue { Severity = s, Category = category, Asset = asset, Message = message };

        private static string Format(List<Issue> issues)
        {
            var errors = issues.Count(i => i.Severity == Severity.Error);
            var warns = issues.Count(i => i.Severity == Severity.Warn);
            var sb = new StringBuilder();
            sb.AppendLine($"DATA INTEGRITY — ERRORS={errors} WARNINGS={warns}");
            foreach (var i in issues.OrderBy(i => i.Severity).ThenBy(i => i.Category))
                sb.AppendLine($"{i.Severity.ToString().ToUpperInvariant(),-5} [{i.Category}] {i.Asset} — {i.Message}");
            return sb.ToString();
        }
    }
}
