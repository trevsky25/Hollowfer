#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hollowfen.Apothecary;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using Hollowfen.NPCs;
using Hollowfen.Restoration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Idempotent authoring for Tobin's six-case village-care chapter.</summary>
    public static class ApothecaryCaseworkImporter
    {
        private const string CaseRoot = "Assets/_Hollowfen/Data/Apothecary/Cases";
        private const string DialogueRoot = "Assets/_Hollowfen/Data/Dialogue";
        private const string DatabasePath = "Assets/_Hollowfen/Resources/ApothecaryCaseDatabase.asset";
        private const string PrefabPath =
            "Assets/_Hollowfen/Art/Apothecary/PF_TobinApothecaryBuilding.prefab";

        private sealed class Spec
        {
            public string Id;
            public string PatientNpcId;
            public string ProfilePath;
            public string[] Clues;
            public string[] Interviews;
            public string CarefulRecipe;
            public string SupportiveRecipe;
            public string MistakenRecipe;
            public string[] Intake;
            public string[] FollowUp;
        }

        private static readonly Spec[] Specs =
        {
            new Spec
            {
                Id = "bram_rain_shiver", PatientNpcId = "bram",
                ProfilePath = "Assets/_Hollowfen/Data/Characters/People/Character_old-bram.asset",
                Clues = new[] { "sleeves", "hands" }, Interviews = new[] { "onset", "meal" },
                CarefulRecipe = "goldfoot_broth", SupportiveRecipe = "brightspore_tonic",
                MistakenRecipe = "field_ink",
                Intake = new[]
                {
                    "Bram|It is a rain-shiver, nothing more. The sort a dry chair usually argues out of a man.",
                    "Wren|Then we will listen before we argue with it.",
                    "Edda|Good. Begin with what the weather changed, not what you expect to find.",
                },
                FollowUp = new[]
                {
                    "Bram|Whatever the ledger calls it, I slept warm and woke sounding like myself again.",
                    "Wren|I wrote down what helped—and what nearly distracted me.",
                },
            },
            new Spec
            {
                Id = "pell_fading_ledger", PatientNpcId = "pell",
                ProfilePath = "Assets/_Hollowfen/Data/Characters/People/Character_elder-pell.asset",
                Clues = new[] { "page", "eyes" }, Interviews = new[] { "daylight", "pattern" },
                CarefulRecipe = "field_ink", SupportiveRecipe = "goldfoot_broth",
                MistakenRecipe = "brightspore_tonic",
                Intake = new[]
                {
                    "Pell|The north-lane figures blur every evening. I would prefer an answer that does not begin with my age.",
                    "Wren|Then I will examine the ledger as carefully as its keeper.",
                    "Edda|A case is sometimes the person. Sometimes it is the thing they have trusted too long.",
                },
                FollowUp = new[]
                {
                    "Pell|The figures have stopped wandering. It appears the ledger required care more urgently than I did.",
                    "Wren|That distinction is staying in the casebook.",
                },
            },
            new Spec
            {
                Id = "joren_hammer_echo", PatientNpcId = "joren",
                ProfilePath = "Assets/_Hollowfen/Data/Characters/People/Character_joren.asset",
                Clues = new[] { "grip", "soot" }, Interviews = new[] { "rhythm", "rest" },
                CarefulRecipe = "brightspore_tonic", SupportiveRecipe = "goldfoot_broth",
                MistakenRecipe = "field_ink",
                Intake = new[]
                {
                    "Joren|My hand keeps the hammer's rhythm after the hammer is down. Makes fine work feel one beat late.",
                    "Wren|Show me what changes when you loosen your grip.",
                    "Edda|And ask what the forge has demanded this week that it did not demand before.",
                },
                FollowUp = new[]
                {
                    "Joren|The false beat has quieted. I changed my grip as well as taking what you made.",
                    "Wren|Good. The preparation was only one part of the answer.",
                },
            },
            new Spec
            {
                Id = "marra_cellar_bloom", PatientNpcId = "marra",
                ProfilePath = "Assets/_Hollowfen/Data/Characters/People/Character_marra.asset",
                Clues = new[] { "jar", "cloth" }, Interviews = new[] { "shelf", "weather" },
                CarefulRecipe = "field_ink", SupportiveRecipe = "brightspore_tonic",
                MistakenRecipe = "goldfoot_broth",
                Intake = new[]
                {
                    "Marra|Three pantry jars have taken the same pale bloom. I will not feed a guess to the village.",
                    "Wren|We will mark the affected shelf and trace what all three jars shared.",
                    "Edda|Care begins before anyone swallows a remedy. Sometimes it begins with refusing a risky meal.",
                },
                FollowUp = new[]
                {
                    "Marra|The marked jars are gone, the dry shelf is holding, and no one had to learn the lesson at supper.",
                    "Wren|Prevention belongs in the ledger too.",
                },
            },
            new Spec
            {
                Id = "almy_brightspore_sleep", PatientNpcId = "almy",
                ProfilePath = "Assets/_Hollowfen/Data/Characters/People/Character_sister-almy.asset",
                Clues = new[] { "pollen", "pulse" }, Interviews = new[] { "dream", "exposure" },
                CarefulRecipe = "brightspore_tonic", SupportiveRecipe = "goldfoot_broth",
                MistakenRecipe = "field_ink",
                Intake = new[]
                {
                    "Almy|The Brightspore beds keep following me into sleep. I wake as though the chapel lamps are still burning.",
                    "Wren|How long were you among the fruiting trays yesterday?",
                    "Edda|Treat the pattern with respect. Familiar work can still change its worker.",
                },
                FollowUp = new[]
                {
                    "Almy|Last night was dark, ordinary, and wonderfully empty. I have shortened the late tending rounds.",
                    "Wren|Then both changes belong beside the result.",
                },
            },
            new Spec
            {
                Id = "theo_road_cold", PatientNpcId = "theo",
                ProfilePath = "Assets/_Hollowfen/Data/Characters/People/Character_theo.asset",
                Clues = new[] { "cloak", "voice" }, Interviews = new[] { "crossing", "camp" },
                CarefulRecipe = "goldfoot_broth", SupportiveRecipe = "brightspore_tonic",
                MistakenRecipe = "field_ink",
                Intake = new[]
                {
                    "Theo|The west road put its cold straight through the wagon boards. Even my sales pitch has started shivering.",
                    "Wren|Tell me where you stopped, and when the wet reached your clothes.",
                    "Edda|Road stories grow in the telling. Keep his details separate from his performance.",
                },
                FollowUp = new[]
                {
                    "Theo|Voice restored, cloak drying properly, and only the usual amount of theatrical suffering remains.",
                    "Wren|I will record that as a careful recovery.",
                },
            },
        };

        [MenuItem("Hollowfen/Apothecary/Build Patient Casework")]
        public static void BuildMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            EnsureFolder(CaseRoot);
            var recipes = LoadRecipes();
            var authored = new List<ApothecaryCaseData>();
            ApothecaryCaseData previous = null;
            foreach (Spec spec in Specs)
            {
                DialogueData intake = UpsertDialogue(spec, false);
                DialogueData followUp = UpsertDialogue(spec, true);
                ApothecaryCaseData data = UpsertCase(spec, previous, recipes, intake, followUp);
                authored.Add(data);
                previous = data;
            }
            UpsertDatabase(authored);
            WireNpcDialogues(authored);
            InstallLedgerInteraction();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (EditorSceneManager.GetActiveScene().isDirty)
                EditorSceneManager.SaveOpenScenes();
            return "APOTHECARY CASEWORK — BUILT: six sequential village-care cases, " +
                   "evidence/interview decisions, delayed follow-ups, patient dialogue, and an authored ledger";
        }

        private static Dictionary<string, PreparationRecipeData> LoadRecipes()
        {
            return AssetDatabase.FindAssets("t:PreparationRecipeData",
                    new[] { "Assets/_Hollowfen/Data/Apothecary" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<PreparationRecipeData>)
                .Where(recipe => recipe != null)
                .ToDictionary(recipe => recipe.Id, StringComparer.Ordinal);
        }

        private static ApothecaryCaseData UpsertCase(Spec spec, ApothecaryCaseData previous,
            IReadOnlyDictionary<string, PreparationRecipeData> recipes, DialogueData intake,
            DialogueData followUp)
        {
            string path = CaseRoot + "/Case_" + ToPascal(spec.Id) + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<ApothecaryCaseData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ApothecaryCaseData>();
                AssetDatabase.CreateAsset(data, path);
            }
            Set(data, "_id", spec.Id);
            Set(data, "_patientNpcId", spec.PatientNpcId);
            Set(data, "_patientProfile", Required<CharacterProfileData>(spec.ProfilePath));
            Set(data, "_mentorNpcId", "edda");
            Set(data, "_requiredFlagId", "apothecary_story_complete");
            Set(data, "_requiresResolvedCase", previous);
            Set(data, "_unlockDelayDays", previous == null ? 0 : 1);
            Set(data, "_clues", spec.Clues.Select(id => new ApothecaryCaseClue { id = id }).ToArray());
            Set(data, "_interviews", spec.Interviews.Select(id =>
                new ApothecaryCaseInterview { id = id }).ToArray());
            Set(data, "_decisions", new[]
            {
                Decision(spec, "careful", recipes[spec.CarefulRecipe], ApothecaryCaseGrade.Careful),
                Decision(spec, "supportive", recipes[spec.SupportiveRecipe], ApothecaryCaseGrade.Supportive),
                Decision(spec, "mistaken", recipes[spec.MistakenRecipe], ApothecaryCaseGrade.Mistaken),
            });
            Set(data, "_intakeDialogue", intake);
            Set(data, "_followUpDialogue", followUp);
            EditorUtility.SetDirty(data);
            return data;
        }

        private static ApothecaryCaseDecision Decision(Spec spec, string id,
            PreparationRecipeData recipe, ApothecaryCaseGrade grade)
        {
            return new ApothecaryCaseDecision
            {
                id = id,
                preparation = recipe,
                grade = grade,
                followUpDays = grade == ApothecaryCaseGrade.Careful ? 1 : 2,
                villageHope = grade == ApothecaryCaseGrade.Careful ? 1 : 0,
                knowledge = 1,
                relationshipDelta = grade == ApothecaryCaseGrade.Careful ? 2 :
                    grade == ApothecaryCaseGrade.Supportive ? 1 : 0,
                mentorBondDelta = grade == ApothecaryCaseGrade.Careful ? 2 : 1,
                memoryId = "case." + spec.Id + "." + id,
            };
        }

        private static DialogueData UpsertDialogue(Spec spec, bool followUp)
        {
            string suffix = followUp ? "FollowUp" : "Intake";
            string path = DialogueRoot + "/Dialogue_ApothecaryCase_" + ToPascal(spec.Id) +
                          "_" + suffix + ".asset";
            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (dialogue == null)
            {
                dialogue = ScriptableObject.CreateInstance<DialogueData>();
                AssetDatabase.CreateAsset(dialogue, path);
            }
            string[] source = followUp ? spec.FollowUp : spec.Intake;
            DialogueLine[] priorLines = dialogue.Lines ?? Array.Empty<DialogueLine>();
            var lines = source.Select((row, index) =>
            {
                int split = row.IndexOf('|');
                string speaker = row.Substring(0, split);
                string text = row.Substring(split + 1);
                AudioClip preservedVoice = index < priorLines.Length &&
                    priorLines[index].speaker == speaker && priorLines[index].text == text
                        ? priorLines[index].voiceClip
                        : null;
                return new DialogueLine
                {
                    speaker = speaker,
                    text = text,
                    isCloseup = followUp,
                    voiceClip = preservedVoice,
                };
            }).ToArray();
            Set(dialogue, "_id", "apothecary_case_" + spec.Id + "_" +
                                 (followUp ? "followup" : "intake"));
            Set(dialogue, "_lines", lines);
            Set(dialogue, "_setFlagIds", new[]
            {
                "apothecary_case_" + (followUp ? "followup_seen_" : "intake_") + spec.Id,
            });
            ResetDialogueOutcomes(dialogue);
            EditorUtility.SetDirty(dialogue);
            return dialogue;
        }

        private static void ResetDialogueOutcomes(DialogueData dialogue)
        {
            Set(dialogue, "_mushroomHandoff", default(DialogueMushroomHandoffCue));
            Set(dialogue, "_unlockStoryCard", null);
            Set(dialogue, "_completeQuest", null);
            Set(dialogue, "_giveItemId", "");
            Set(dialogue, "_grantCoinsCopper", 0);
            Set(dialogue, "_spendsCoinsCopper", 0);
            Set(dialogue, "_sellsForageBasket", false);
            Set(dialogue, "_basketCopperPerItem", 0);
            Set(dialogue, "_grantForage", null);
            Set(dialogue, "_grantForageCount", 0);
            Set(dialogue, "_consumeForage", null);
            Set(dialogue, "_consumeForageCount", 0);
            Set(dialogue, "_villageHopeDelta", 0);
            Set(dialogue, "_knowledgeDelta", 0);
            Set(dialogue, "_relationshipNpcIds", Array.Empty<string>());
            Set(dialogue, "_relationshipDeltas", Array.Empty<int>());
            Set(dialogue, "_memoryOutcomes", Array.Empty<DialogueMemoryOutcome>());
            Set(dialogue, "_bondOutcomes", Array.Empty<DialogueBondOutcome>());
            Set(dialogue, "_favorOutcomes", Array.Empty<DialogueFavorOutcome>());
            Set(dialogue, "_advanceMinutes", 0);
            Set(dialogue, "_transitionMoment", null);
            Set(dialogue, "_nextDialog", null);
            Set(dialogue, "_choices", Array.Empty<DialogueChoice>());
        }

        private static void UpsertDatabase(IReadOnlyList<ApothecaryCaseData> cases)
        {
            var database = AssetDatabase.LoadAssetAtPath<ApothecaryCaseDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<ApothecaryCaseDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }
            Set(database, "_cases", cases.ToArray());
            EditorUtility.SetDirty(database);
        }

        private static void WireNpcDialogues(IReadOnlyList<ApothecaryCaseData> cases)
        {
            foreach (IGrouping<string, ApothecaryCaseData> group in cases.GroupBy(data => data.PatientNpcId))
            {
                NPCData npc = FindNpc(group.Key);
                FieldInfo field = typeof(NPCData).GetField("_dialogueEntries",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var existing = field?.GetValue(npc) as NPCDialogueEntry[] ?? Array.Empty<NPCDialogueEntry>();
                var merged = new List<NPCDialogueEntry>();
                foreach (ApothecaryCaseData data in group)
                {
                    merged.Add(new NPCDialogueEntry
                    {
                        requiresFlagId = data.ResolvedFlagId,
                        blockedByFlagId = "apothecary_case_followup_seen_" + data.Id,
                        requiresNoActiveQuest = true,
                        dialog = data.FollowUpDialogue,
                    });
                    merged.Add(new NPCDialogueEntry
                    {
                        requiresFlagId = data.ActiveFlagId,
                        blockedByFlagId = "apothecary_case_intake_" + data.Id,
                        requiresNoActiveQuest = true,
                        dialog = data.IntakeDialogue,
                    });
                }
                merged.AddRange(existing.Where(entry => entry.dialog == null ||
                    !entry.dialog.Id.StartsWith("apothecary_case_", StringComparison.Ordinal)));
                field?.SetValue(npc, merged.ToArray());
                EditorUtility.SetDirty(npc);
            }
        }

        private static void InstallLedgerInteraction()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform old = contents.transform.Find("CaseLedgerInteraction");
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                Transform book = contents.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(candidate => candidate.name == "Open_Book");
                if (book == null) throw new InvalidOperationException("Purchased open ledger prop is missing.");

                var interaction = new GameObject("CaseLedgerInteraction");
                interaction.transform.SetParent(contents.transform, false);
                interaction.transform.localPosition = contents.transform.InverseTransformPoint(book.position) +
                                                      Vector3.up * .18f;
                interaction.transform.localRotation = Quaternion.identity;
                int layer = LayerMask.NameToLayer("Foraging");
                if (layer >= 0) interaction.layer = layer;
                var collider = interaction.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.center = new Vector3(0f, .65f, 0f);
                collider.size = new Vector3(2.2f, 1.7f, 2.2f);
                var station = interaction.AddComponent<ApothecaryCaseLedgerStation>();
                Set(station, "_restorationProjectId", "tobin_workshop");
                Set(station, "_requiredStage", RestorationStage.Occupied);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static NPCData FindNpc(string id)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:NPCData",
                         new[] { "Assets/_Hollowfen/Data/NPCs" }))
            {
                NPCData npc = AssetDatabase.LoadAssetAtPath<NPCData>(AssetDatabase.GUIDToAssetPath(guid));
                if (npc != null && string.Equals(npc.Id, id, StringComparison.Ordinal)) return npc;
            }
            throw new InvalidOperationException("NPC '" + id + "' is missing.");
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required asset is missing: " + path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string segment in path.Substring("Assets/".Length).Split('/'))
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }

        private static string ToPascal(string id) => string.Concat(id.Split('_')
            .Where(part => part.Length > 0)
            .Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));

        private static void Set(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
            field.SetValue(target, value);
        }
    }
}
#endif
