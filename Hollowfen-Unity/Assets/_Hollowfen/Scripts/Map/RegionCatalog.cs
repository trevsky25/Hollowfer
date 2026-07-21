namespace Hollowfen.Map
{
    /// <summary>
    /// Canonical presentation metadata for authored world regions. Region ids remain the small,
    /// stable keys stored on RegionTrigger and LocationData; player-facing copy stays localized.
    /// </summary>
    public static class RegionCatalog
    {
        public readonly struct Entry
        {
            public readonly string Id;
            public readonly string NameId;
            public readonly string SubtitleId;
            public readonly string FallbackName;
            public readonly string FallbackSubtitle;

            public Entry(string id, string nameId, string subtitleId, string fallbackName,
                string fallbackSubtitle)
            {
                Id = id;
                NameId = nameId;
                SubtitleId = subtitleId;
                FallbackName = fallbackName;
                FallbackSubtitle = fallbackSubtitle;
            }
        }

        private static readonly Entry[] Entries =
        {
            new Entry("village", "region.village.name", "region.village.subtitle",
                "Hollowfen Village", "Hearthlight, rain, and familiar roads."),
            new Entry("wend", "region.wend.name", "region.wend.subtitle",
                "The Wend", "The old watercourse through wet earth and stone."),
            new Entry("old_wood", "region.old_wood.name", "region.old_wood.subtitle",
                "The Old Wood", "Where the footpath thins beneath the canopy."),
            new Entry("manor", "region.manor.name", "region.manor.subtitle",
                "Aldric's Manor", "High walls above the failing village."),
        };

        public static int Count => Entries.Length;

        public static bool TryGet(string id, out Entry entry)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < Entries.Length; i++)
                {
                    if (Entries[i].Id != id) continue;
                    entry = Entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public static bool IsKnown(string id) => TryGet(id, out _);

        public static string DisplayName(string id)
        {
            return TryGet(id, out var entry)
                ? Localization.Get(entry.NameId, entry.FallbackName)
                : string.IsNullOrEmpty(id) ? "—" : id;
        }

        public static string Subtitle(string id)
        {
            return TryGet(id, out var entry)
                ? Localization.Get(entry.SubtitleId, entry.FallbackSubtitle)
                : "";
        }
    }
}
