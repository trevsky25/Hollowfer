using Hollowfen.Data;

namespace Hollowfen.UI
{
    public static class JournalText
    {
        public static string StoryAct(StoryCardData card) => Field("story", card != null ? card.Id : null, "act", card != null ? card.Act : "");
        public static string StoryScene(StoryCardData card) => Field("story", card != null ? card.Id : null, "scene", card != null ? card.Scene : "");
        public static string StoryTitle(StoryCardData card) => card == null ? "" : Localization.Get(card.DisplayNameId, card.Title);
        public static string StorySubtitle(StoryCardData card) => Field("story", card != null ? card.Id : null, "subtitle", card != null ? card.Subtitle : "");
        public static string StoryBody(StoryCardData card) => card == null ? "" : Localization.Get(card.DescriptionId, card.Body);
        public static string StoryNote(StoryCardData card) => Field("story", card != null ? card.Id : null, "wren_note", card != null ? card.WrenNote : "");
        public static string StoryBeat(StoryCardData card, int index) => Field("story", card != null ? card.Id : null, "beat." + index, card != null && card.Beats != null && index >= 0 && index < card.Beats.Length ? card.Beats[index] : "");

        public static string MushroomName(MushroomFieldGuideData entry) => entry == null ? "" : Localization.Get(entry.DisplayNameId, entry.CommonName);
        public static string MushroomDescription(MushroomFieldGuideData entry) => entry == null ? "" : Localization.Get(entry.DescriptionId, entry.Description);
        public static string MushroomLatin(MushroomFieldGuideData entry) => Field("mushroom", entry != null ? entry.Id : null, "latin", entry != null ? entry.LatinName : "");
        public static string MushroomEdibility(MushroomFieldGuideData entry) => Field("mushroom", entry != null ? entry.Id : null, "edibility", entry != null ? entry.EdibilityLabel : "");
        public static string MushroomHabitat(MushroomFieldGuideData entry) => Field("mushroom", entry != null ? entry.Id : null, "habitat", entry != null ? entry.Habitat : "");
        public static string MushroomSeason(MushroomFieldGuideData entry) => Field("mushroom", entry != null ? entry.Id : null, "season", entry != null ? entry.Season : "");
        public static string MushroomLookalikes(MushroomFieldGuideData entry) => Field("mushroom", entry != null ? entry.Id : null, "lookalikes", entry != null ? entry.Lookalikes : "");
        public static string MushroomNotes(MushroomFieldGuideData entry) => Field("mushroom", entry != null ? entry.Id : null, "notes", entry != null ? entry.Notes : "");
        public static string MushroomCredit(MushroomFieldGuideData entry) => Field("mushroom", entry != null ? entry.Id : null, "photo_credit", entry != null ? entry.PhotoCredit : "");
        public static string MushroomFeature(MushroomFieldGuideData entry, int index) => Field("mushroom", entry != null ? entry.Id : null, "feature." + index, entry != null && entry.IdFeatures != null && index >= 0 && index < entry.IdFeatures.Length ? entry.IdFeatures[index] : "");

        public static string Character(CharacterProfileData profile, string field, string fallback)
        {
            return Field("character", profile != null ? profile.Id : null, field, fallback);
        }

        public static string CharacterKit(CharacterProfileData profile, int index, string field, string fallback)
        {
            return Field("character", profile != null ? profile.Id : null, "kit." + index + "." + field, fallback);
        }

        private static string Field(string domain, string id, string field, string fallback)
        {
            if (string.IsNullOrEmpty(id)) return fallback ?? "";
            return Localization.Get(domain + "." + id + "." + field, fallback);
        }
    }
}
