namespace Hollowfen.Save
{
    /// <summary>
    /// Owns the stable identity rendered by save-slot rows. New journals persist only an ID;
    /// the historical CurrentQuest text is a read-only compatibility fallback for id-less saves.
    /// </summary>
    public static class SaveQuestIdentity
    {
        public const string GameCompleteId = "game_complete";
        public const string FinalChoiceAvailableId = "final_choice_available";
        public const string ActOneCompleteId = "act1_complete";

        public static string QuestNameKey(string questId) =>
            string.IsNullOrWhiteSpace(questId) ? string.Empty : "quest." + questId.Trim() + ".name";

        public static void Set(SaveSlotMeta meta, string questId)
        {
            if (meta == null) return;
            meta.CurrentQuestId = string.IsNullOrWhiteSpace(questId) ? string.Empty : questId.Trim();
            meta.CurrentQuest = null;
        }

        public static void PrepareForWrite(SaveSlotMeta meta)
        {
            if (meta == null) return;
            meta.CurrentQuestId = string.IsNullOrWhiteSpace(meta.CurrentQuestId)
                ? string.Empty
                : meta.CurrentQuestId.Trim();

            // Keep the legacy cache only while no stable ID exists. This lets old journals
            // remain readable, while the first authoritative full save retires their text.
            if (!string.IsNullOrEmpty(meta.CurrentQuestId))
                meta.CurrentQuest = null;
            else if (!string.IsNullOrWhiteSpace(meta.CurrentQuest))
                meta.CurrentQuest = meta.CurrentQuest.Trim();
            else
                meta.CurrentQuest = null;
        }

        public static string ResolveDisplayName(SaveSlotMeta meta)
        {
            if (meta == null) return "—";

            string questId = string.IsNullOrWhiteSpace(meta.CurrentQuestId)
                ? string.Empty
                : meta.CurrentQuestId.Trim();
            switch (questId)
            {
                case GameCompleteId:
                    return Localization.Get("ending.save.complete");
                case FinalChoiceAvailableId:
                    return Localization.Get("ending.save.choose");
                case ActOneCompleteId:
                    return Localization.Get("save.act1_complete");
            }

            if (!string.IsNullOrEmpty(questId))
            {
                string key = QuestNameKey(questId);
                return Localization.TryGet(key, out string localized)
                    ? localized
                    : Localization.Get("save.quest.unknown");
            }

            // Schema-zero journals predate CurrentQuestId. Their cached text is presentation
            // only and disappears after gameplay establishes an authoritative ID and saves.
            return string.IsNullOrWhiteSpace(meta.CurrentQuest) ? "—" : meta.CurrentQuest.Trim();
        }
    }
}
