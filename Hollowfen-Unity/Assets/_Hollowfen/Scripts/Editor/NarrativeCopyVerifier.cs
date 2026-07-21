#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using Hollowfen.Map;
using Hollowfen.Quests;
using Hollowfen.Requests;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Player-facing copy lint for dialogue, quests, requests, locations, and story beats.</summary>
    public static class NarrativeCopyVerifier
    {
        private static readonly Regex SpacingError = new Regex(
            @" {2,}|\s+[,.!?;:]", RegexOptions.Compiled);
        private static readonly Regex CommonTypo = new Regex(
            @"\b(teh|alot|recieve|occured|seperate|definately|wierd|jounral|apothocary|mushrom|identifiy|becuase)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        [MenuItem("Hollowfen/Verify/Narrative Copy Quality")]
        private static void RunMenu() => Debug.Log(RunAll());

        public static string RunAll()
        {
            var issues = new List<string>();
            DialogueData[] dialogues = Assets<DialogueData>("Assets/_Hollowfen/Data/Dialogue");
            QuestData[] quests = Assets<QuestData>("Assets/_Hollowfen/Data/Quests");
            VillageRequestData[] requests = Assets<VillageRequestData>("Assets/_Hollowfen/Data/Requests");
            LocationData[] locations = Assets<LocationData>("Assets/_Hollowfen/Data/Locations");
            StoryMomentData[] moments = Assets<StoryMomentData>("Assets/_Hollowfen/Data/StoryMoments");

            foreach (DialogueData dialogue in dialogues)
            {
                DialogueLine[] lines = dialogue.Lines ?? Array.Empty<DialogueLine>();
                for (int i = 0; i < lines.Length; i++)
                {
                    Check(issues, dialogue.Id + " line " + (i + 1), lines[i].speaker, 32);
                    Check(issues, dialogue.Id + " line " + (i + 1), lines[i].text, 480);
                    if (i > 0 && string.Equals(lines[i - 1].speaker, lines[i].speaker,
                            StringComparison.OrdinalIgnoreCase) &&
                        (lines[i - 1].voiceClip == null || lines[i].voiceClip == null))
                        issues.Add(dialogue.Id + " has an unvoiced same-speaker split; merge it " +
                                   "or author both beats as intentional voiced cadence");
                }

                DialogueChoice[] choices = dialogue.Choices ?? Array.Empty<DialogueChoice>();
                if (choices.Length > 4) issues.Add(dialogue.Id + " exposes more than four choices");
                for (int i = 0; i < choices.Length; i++)
                    Check(issues, dialogue.Id + " choice " + (i + 1), choices[i].text, 110);
            }

            foreach (QuestData quest in quests)
            {
                Check(issues, quest.Id + " title", Localization.Get(quest.DisplayNameId), 56);
                Check(issues, quest.Id + " objective", Localization.Get(quest.ObjectiveTextId), 220);
            }

            foreach (VillageRequestData request in requests)
            {
                Check(issues, request.Id + " title", Localization.Get(request.TitleId), 56);
                Check(issues, request.Id + " description", Localization.Get(request.DescriptionId), 220);
                Check(issues, request.Id + " requester line", Localization.Get(request.RequesterLineId), 220);
            }

            foreach (LocationData location in locations)
            {
                Check(issues, location.Id + " name", Localization.Get(location.DisplayNameId), 56);
                Check(issues, location.Id + " description",
                    Localization.Get(location.ShortDescriptionId), 140);
            }

            foreach (StoryMomentData moment in moments)
            {
                string[] captions = moment.ResolveCaptions() ?? Array.Empty<string>();
                for (int i = 0; i < captions.Length; i++)
                    Check(issues, moment.name + " caption " + (i + 1), captions[i], 260);
                if (moment.HasPageText)
                    Check(issues, moment.name + " page text", moment.PageText, 1200);
            }

            if (issues.Count > 0)
                throw new InvalidOperationException("NARRATIVE COPY — FAIL (" + issues.Count + ")\n" +
                                                    string.Join("\n", issues.Take(30)));
            return "NARRATIVE COPY — PASS: " + dialogues.Length + " dialogues, " + quests.Length +
                   " quest cards, " + requests.Length + " village requests, " + locations.Length +
                   " location toasts, and " + moments.Length +
                   " cinematic moments are clean, bounded, and advance-efficient.";
        }

        private static void Check(ICollection<string> issues, string owner, string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                issues.Add(owner + " is blank");
                return;
            }
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
                issues.Add(owner + " has leading or trailing whitespace");
            if (SpacingError.IsMatch(value.Replace("\n\n", "\n")))
                issues.Add(owner + " has malformed spacing");
            if (CommonTypo.IsMatch(value)) issues.Add(owner + " contains a common typo");
            if (value.Length > maxLength)
                issues.Add(owner + " is " + value.Length + " characters (limit " + maxLength + ")");
        }

        private static T[] Assets<T>(string root) where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { root })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .ToArray();
    }
}
#endif
