#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using Hollowfen.NPCs;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Authors paired, place-bound village conversations. The dialogue is available only while
    /// both participants' schedules have brought them to the same restored location.
    /// </summary>
    public static class LivingVillageEncounterImporter
    {
        private const string Root = "Assets/_Hollowfen/Data/Dialogue";
        private const string LegacyRoot = Root + "/LivingVillage";

        private sealed class Encounter
        {
            public string Id;
            public string ScheduleLabel;
            public string RequiredFlag;
            public string CompletedFlag;
            public string FirstNpcId;
            public string SecondNpcId;
            public string FirstMemoryId;
            public string SecondMemoryId;
            public int Hope;
            public int Knowledge;
            public int Minutes;
            public DialogueLine[] Lines;
        }

        [MenuItem("Hollowfen/Living Village/Build Paired Encounters")]
        public static void BuildAll()
        {
            EnsureFolder(Root);
            foreach (Encounter encounter in Encounters()) Build(encounter);
            NPCScheduleImporter.ApplyAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LivingVillage] Built four restored-place encounters with paired staging, " +
                      "weather-aware arrival, durable memories, and NPC-to-NPC bonds.");
        }

        private static void Build(Encounter encounter)
        {
            string assetName = encounter.Id.Replace('.', '_');
            string path = Root + "/Dialogue_" + assetName + ".asset";
            DialogueData dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (dialogue == null)
            {
                string legacyPath = LegacyRoot + "/Dialogue_" + assetName + ".asset";
                if (AssetDatabase.LoadAssetAtPath<DialogueData>(legacyPath) != null)
                {
                    string error = AssetDatabase.MoveAsset(legacyPath, path);
                    if (!string.IsNullOrEmpty(error))
                        throw new InvalidOperationException("Could not migrate encounter dialogue: " + error);
                    dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
                }
            }
            if (dialogue == null)
            {
                dialogue = ScriptableObject.CreateInstance<DialogueData>();
                AssetDatabase.CreateAsset(dialogue, path);
            }

            DialogueLine[] lines = PreserveVoiceClips(dialogue.Lines, encounter.Lines);
            Set(dialogue, "_id", encounter.Id);
            Set(dialogue, "_lines", lines);
            Set(dialogue, "_mushroomHandoff", default(DialogueMushroomHandoffCue));
            Set<object>(dialogue, "_unlockStoryCard", null);
            Set<object>(dialogue, "_completeQuest", null);
            Set(dialogue, "_giveItemId", "");
            Set(dialogue, "_grantCoinsCopper", 0);
            Set(dialogue, "_spendsCoinsCopper", 0);
            Set(dialogue, "_sellsForageBasket", false);
            Set(dialogue, "_basketCopperPerItem", 0);
            Set(dialogue, "_basketBuyer", MushroomBuyer.None);
            Set<object>(dialogue, "_grantForage", null);
            Set(dialogue, "_grantForageCount", 1);
            Set<object>(dialogue, "_consumeForage", null);
            Set(dialogue, "_consumeForageCount", 1);
            Set(dialogue, "_setFlagIds", new[] { encounter.CompletedFlag });
            Set(dialogue, "_villageHopeDelta", encounter.Hope);
            Set(dialogue, "_knowledgeDelta", encounter.Knowledge);
            Set(dialogue, "_relationshipNpcIds", new[]
            {
                encounter.FirstNpcId, encounter.SecondNpcId,
            });
            Set(dialogue, "_relationshipDeltas", new[] { 1, 1 });
            Set(dialogue, "_memoryOutcomes", new[]
            {
                new DialogueMemoryOutcome
                {
                    npcId = encounter.FirstNpcId,
                    memoryId = encounter.FirstMemoryId,
                },
                new DialogueMemoryOutcome
                {
                    npcId = encounter.SecondNpcId,
                    memoryId = encounter.SecondMemoryId,
                },
            });
            Set(dialogue, "_bondOutcomes", new[]
            {
                new DialogueBondOutcome
                {
                    firstNpcId = encounter.FirstNpcId,
                    secondNpcId = encounter.SecondNpcId,
                    delta = 2,
                },
            });
            Set(dialogue, "_favorOutcomes", Array.Empty<DialogueFavorOutcome>());
            Set(dialogue, "_advanceMinutes", encounter.Minutes);
            Set(dialogue, "_atomicSocialOutcomes", true);
            Set<object>(dialogue, "_transitionMoment", null);
            Set<object>(dialogue, "_nextDialog", null);
            Set(dialogue, "_choices", Array.Empty<DialogueChoice>());
            EditorUtility.SetDirty(dialogue);

            var firstEntry = new NPCDialogueEntry
            {
                requiresFlagId = encounter.RequiredFlag,
                blockedByFlagId = encounter.CompletedFlag,
                requiresScheduleSlotLabel = encounter.ScheduleLabel,
                requiresPartnerNpcId = encounter.SecondNpcId,
                requiresPartnerScheduleSlotLabel = encounter.ScheduleLabel,
                dialog = dialogue,
            };
            var secondEntry = firstEntry;
            secondEntry.requiresPartnerNpcId = encounter.FirstNpcId;
            UpsertNpcEntry(encounter.FirstNpcId, firstEntry, encounter.Id);
            UpsertNpcEntry(encounter.SecondNpcId, secondEntry, encounter.Id);
        }

        private static void UpsertNpcEntry(string npcId, NPCDialogueEntry entry, string dialogueId)
        {
            NPCData npc = FindNpc(npcId);
            FieldInfo field = typeof(NPCData).GetField("_dialogueEntries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var existing = field?.GetValue(npc) as NPCDialogueEntry[] ?? Array.Empty<NPCDialogueEntry>();
            var merged = new List<NPCDialogueEntry> { entry };
            merged.AddRange(existing.Where(candidate => candidate.dialog == null ||
                !string.Equals(candidate.dialog.Id, dialogueId, StringComparison.Ordinal)));
            field?.SetValue(npc, merged.ToArray());
            EditorUtility.SetDirty(npc);
        }

        private static DialogueLine[] PreserveVoiceClips(DialogueLine[] prior, DialogueLine[] authored)
        {
            prior = prior ?? Array.Empty<DialogueLine>();
            for (int i = 0; i < authored.Length; i++)
            {
                if (i >= prior.Length || prior[i].voiceClip == null ||
                    !string.Equals(prior[i].speaker, authored[i].speaker, StringComparison.Ordinal) ||
                    !string.Equals(prior[i].text, authored[i].text, StringComparison.Ordinal)) continue;
                authored[i].voiceClip = prior[i].voiceClip;
            }
            return authored;
        }

        private static Encounter[] Encounters() => new[]
        {
            new Encounter
            {
                Id = "encounter.apothecary_rain_ledger",
                ScheduleLabel = "Rain ledger at Tobin's apothecary",
                RequiredFlag = "apothecary_story_complete",
                CompletedFlag = "encounter.apothecary_rain_seen",
                FirstNpcId = "bram",
                SecondNpcId = "edda",
                FirstMemoryId = "bram.shared_rain_ledger",
                SecondMemoryId = "edda.shared_rain_ledger",
                Knowledge = 1,
                Minutes = 20,
                Lines = Lines(
                    L("Bram", "Tobin marked rain days in the shelf ledger. Not the weather—the people who came in pretending they were only wet."),
                    L("Edda", "Because cold pride sounds very much like silence."),
                    L("Wren", "Then we write both: coat soaked, breakfast missed, joke attempted."),
                    L("Bram", "Cruel accuracy."),
                    L("Edda", "Useful accuracy. Keep that line, Wren.", true)),
            },
            new Encounter
            {
                Id = "encounter.forge_ledger",
                ScheduleLabel = "Settling the forge ledger",
                RequiredFlag = "forge_in_use",
                CompletedFlag = "encounter.forge_ledger_seen",
                FirstNpcId = "joren",
                SecondNpcId = "pell",
                FirstMemoryId = "joren.settled_forge_ledger",
                SecondMemoryId = "pell.settled_forge_ledger",
                Hope = 1,
                Minutes = 25,
                Lines = Lines(
                    L("Pell", "You have written 'hinges' where the ledger asks for public benefit."),
                    L("Joren", "Doors that open are a public benefit."),
                    L("Wren", "Write 'doors opened,' then keep the hinges in the margin."),
                    L("Pell", "Imprecise, but defensible."),
                    L("Joren", "That is the warmest thing he has ever said about my work.", true)),
            },
            new Encounter
            {
                Id = "encounter.garden_seed_exchange",
                ScheduleLabel = "Trading seed at the chapel garden",
                RequiredFlag = "chapel_garden_in_use",
                CompletedFlag = "encounter.garden_seed_seen",
                FirstNpcId = "almy",
                SecondNpcId = "marra",
                FirstMemoryId = "almy.traded_chapel_seed",
                SecondMemoryId = "marra.traded_chapel_seed",
                Knowledge = 1,
                Minutes = 25,
                Lines = Lines(
                    L("Marra", "I need leaves that survive a stewpot, not another plant that looks meaningful beside a wall."),
                    L("Almy", "Meaning keeps poorly in broth. Take the sorrel seed."),
                    L("Wren", "And the sheltered row? It stays damp after the morning mist."),
                    L("Almy", "Good. Plant half there and let Marra compare the flavor."),
                    L("Marra", "A garden trial with lunch at the end. At last, scholarship improves.", true)),
            },
            new Encounter
            {
                Id = "encounter.pintle_last_table",
                ScheduleLabel = "Setting the Pintle's last table",
                RequiredFlag = "crooked_pintle_in_use",
                CompletedFlag = "encounter.pintle_last_table_seen",
                FirstNpcId = "bram",
                SecondNpcId = "marra",
                FirstMemoryId = "bram.set_last_pintle_table",
                SecondMemoryId = "marra.set_last_pintle_table",
                Hope = 1,
                Minutes = 30,
                Lines = Lines(
                    L("Bram", "One bowl left. In the old days Tobin would arrive just late enough to claim it was an accident."),
                    L("Marra", "In the old days I always made one bowl too many."),
                    L("Wren", "Then set three places tonight."),
                    L("Bram", "For whom?"),
                    L("Marra", "For whoever comes through the door needing not to explain themselves.", true)),
            },
        };

        private static DialogueLine[] Lines(params DialogueLine[] lines) => lines;

        private static DialogueLine L(string speaker, string text, bool closeup = false) =>
            new DialogueLine { speaker = speaker, text = text, isCloseup = closeup };

        private static NPCData FindNpc(string id) => AssetDatabase.FindAssets("t:NPCData",
                new[] { "Assets/_Hollowfen/Data/NPCs" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<NPCData>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(asset => asset != null && asset.Id == id);

        private static void Set<T>(DialogueData target, string fieldName, T value)
        {
            FieldInfo field = typeof(DialogueData).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(typeof(DialogueData).Name, fieldName);
            field.SetValue(target, value);
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string part in path.Substring("Assets/".Length)
                         .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
#endif
