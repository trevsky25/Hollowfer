using System;
using Hollowfen.Data;
using Hollowfen.GameTime;
using Hollowfen.Map;
using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.Foraging
{
    public readonly struct MushroomFieldNote
    {
        public readonly bool HasRecordedContext;
        public readonly int Day;
        public readonly string RegionId;
        public readonly string LocationId;

        public MushroomFieldNote(bool hasRecordedContext, int day, string regionId,
            string locationId)
        {
            HasRecordedContext = hasRecordedContext;
            Day = day;
            RegionId = regionId ?? "";
            LocationId = locationId ?? "";
        }
    }

    /// <summary>
    /// Stores the first field-verification context as one per-save GameScores flag. The encoded
    /// row is backward-compatible with existing saves and deliberately stores stable authored ids,
    /// never localized display text.
    /// </summary>
    public static class MushroomFieldNotes
    {
        private const string Prefix = "mushroom_field_note|";
        private const char Separator = '|';
        private const float LandmarkRadius = 120f;

        public static bool RecordFirstVerification(MushroomFieldGuideData species,
            Vector3? worldPosition = null)
        {
            if (species == null || string.IsNullOrEmpty(species.Id)) return false;
            if (TryGet(species, out _)) return false;

            int day = TimeManager.Instance != null ? Mathf.Max(1, TimeManager.Instance.Day) : 1;
            string regionId = LocationRegistry.CurrentRegion ?? "";
            string locationId = "";
            if (worldPosition.HasValue)
            {
                LocationMarker nearest = LocationRegistry.FindNearest(
                    worldPosition.Value, LandmarkRadius);
                if (nearest != null && nearest.Data != null)
                {
                    locationId = nearest.Id ?? "";
                    if (string.IsNullOrEmpty(regionId)) regionId = nearest.Data.RegionId ?? "";
                }
            }

            string flag = Prefix + Clean(species.Id) + Separator + day + Separator +
                          Clean(regionId) + Separator + Clean(locationId);
            return GameScores.SetFlag(flag);
        }

        public static bool TryGet(MushroomFieldGuideData species, out MushroomFieldNote note)
        {
            note = default;
            if (species == null || string.IsNullOrEmpty(species.Id)) return false;
            string speciesPrefix = Prefix + Clean(species.Id) + Separator;
            foreach (string flag in GameScores.Flags)
            {
                if (string.IsNullOrEmpty(flag) || !flag.StartsWith(speciesPrefix,
                        StringComparison.Ordinal)) continue;
                string[] pieces = flag.Split(Separator);
                if (pieces.Length < 5) continue;
                if (!int.TryParse(pieces[2], out int day)) day = 1;
                note = new MushroomFieldNote(true, Mathf.Max(1, day), pieces[3], pieces[4]);
                return true;
            }
            return false;
        }

        public static MushroomFieldNote ForDisplay(MushroomFieldGuideData species)
        {
            if (TryGet(species, out MushroomFieldNote note)) return note;
            // Verified entries from saves predating field notes still receive Wren's permanent
            // annotation, just without inventing a day or place the save never recorded.
            return new MushroomFieldNote(false, 0, "", "");
        }

        public static string PlaceName(MushroomFieldNote note)
        {
            if (!string.IsNullOrEmpty(note.LocationId))
            {
                var markers = LocationRegistry.Markers;
                for (int i = 0; i < markers.Count; i++)
                {
                    LocationMarker marker = markers[i];
                    if (marker == null || marker.Id != note.LocationId || marker.Data == null) continue;
                    return Localization.Get(marker.Data.DisplayNameId,
                        RegionCatalog.DisplayName(note.RegionId));
                }
            }
            return RegionCatalog.DisplayName(note.RegionId);
        }

        private static string Clean(string value) =>
            string.IsNullOrEmpty(value) ? "" : value.Replace(Separator, '_');
    }
}
