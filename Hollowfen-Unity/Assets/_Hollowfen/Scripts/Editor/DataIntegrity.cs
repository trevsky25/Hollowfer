using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using Hollowfen.Map;
using Hollowfen.NPCs;
using Hollowfen.Quests;
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
        { "prompt.inspect.verb", "prompt.npc.talk", "prompt.plant.verb", "prompt.examine.verb", "prompt.door.unlock", "growbed.name" };

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
            var loc = LocalizationTable(issues);

            CheckQuests(issues, quests, loc);
            CheckQuestChain(issues, quests);
            CheckDialogues(issues, dialogues, quests);
            CheckNpcs(issues, npcs, loc);
            CheckScoreHooks(issues, quests, mushrooms);
            CheckDatabases(issues);
            CheckLocations(issues, locations, loc);
            CheckPromptIds(issues, loc);
            CheckBuildSettings(issues);

            issues.Add(New(Severity.Info, "coverage", "(project)",
                $"checked {quests.Count} quests, {dialogues.Count} dialogues, {npcs.Count} NPCs, {locations.Count} locations, {mushrooms.Count} mushrooms"));
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

            foreach (var d in dialogues)
            {
                var path = Path(d);
                if (d.Lines == null || d.Lines.Length == 0)
                {
                    issues.Add(New(Severity.Error, "dialogue-lines", path, "no lines"));
                    continue;
                }
                foreach (var line in d.Lines)
                {
                    if (string.IsNullOrWhiteSpace(line.speaker) || string.IsNullOrWhiteSpace(line.text))
                        issues.Add(New(Severity.Error, "dialogue-lines", path, "line with empty speaker or text"));
                    else if (speakerColors != null && !speakerColors.ContainsKey(line.speaker))
                        issues.Add(New(Severity.Warn, "dialogue-speaker", path,
                            $"speaker '{line.speaker}' not in DialogueScreen.SpeakerColors (renders default ink)"));
                }

                CheckRelationshipArrays(issues, path, d.RelationshipNpcIds, d.RelationshipDeltas);

                if (d.SetFlagIds != null && d.SetFlagIds.Any(string.IsNullOrWhiteSpace))
                    issues.Add(New(Severity.Error, "dialogue-flags", path, "empty string in SetFlagIds"));

                if (d.CompleteQuest != null && !questSet.Contains(d.CompleteQuest))
                    issues.Add(New(Severity.Warn, "dialogue-quest", path,
                        $"CompleteQuest '{d.CompleteQuest.Id}' is outside {DataRoot}/Quests"));

                if (d.Choices != null && d.Choices.Length > 0)
                {
                    if (d.Choices.Length > 4)
                        issues.Add(New(Severity.Error, "dialogue-choices", path, $"{d.Choices.Length} choices — the input scheme supports max 4"));
                    for (int i = 0; i < d.Choices.Length; i++)
                        if (string.IsNullOrWhiteSpace(d.Choices[i].text))
                            issues.Add(New(Severity.Error, "dialogue-choices", path, $"choice {i} has empty text"));
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

        private static void CheckDatabases(List<Issue> issues)
        {
            foreach (var db in LoadAll<StoryCardDatabase>())
                CheckDbArray(issues, db, "_cards", (StoryCardData c) => c.Id);
            foreach (var db in LoadAll<MushroomFieldGuideDatabase>())
                CheckDbArray(issues, db, "_entries", (MushroomFieldGuideData m) => m.Id);
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
        }

        private static void CheckBuildSettings(List<Issue> issues)
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || !scenes[0].path.EndsWith("Scene_MainMenu.unity") || !scenes[0].enabled)
                issues.Add(New(Severity.Error, "build-settings", "(build settings)", "index 0 must be an enabled Scene_MainMenu (boot scene)"));
            if (!scenes.Any(s => s.enabled && s.path.EndsWith("Scene_Hollowfen.unity")))
                issues.Add(New(Severity.Error, "build-settings", "(build settings)", "Scene_Hollowfen missing or disabled"));
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
