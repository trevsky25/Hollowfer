using System;
using System.Collections.Generic;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.Cultivation
{
    // Static store for grow-bed state, keyed by each bed's stable scene id. Same persistence
    // recipe as InventoryRuntime / KeyItems: hydrates from the active save slot on first
    // access, persists immediately on change. Growth progress is derived from the planted
    // day/hour against the TimeManager clock, so no timers are serialized.
    public static class GrowBeds
    {
        public class BedRecord
        {
            public string SpeciesId;
            public int PlantedDay;
            public float PlantedHour;
            public int Remaining; // harvestable nodes left once mature
        }

        private static readonly Dictionary<string, BedRecord> _beds = new Dictionary<string, BedRecord>();
        private static bool _hydrated;

        // Fired after any bed change (planted, harvested down, cleared). (bedId)
        public static event Action<string> OnChanged;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            _beds.Clear();
            _hydrated = false;
            OnChanged = null;
        }

        public static BedRecord Get(string bedId)
        {
            if (string.IsNullOrEmpty(bedId)) return null;
            EnsureHydrated();
            return _beds.TryGetValue(bedId, out var r) ? r : null;
        }

        public static void Plant(string bedId, string speciesId, int day, float hour, int yieldCount)
        {
            if (string.IsNullOrEmpty(bedId)) return;
            EnsureHydrated();
            _beds[bedId] = new BedRecord { SpeciesId = speciesId, PlantedDay = day, PlantedHour = hour, Remaining = yieldCount };
            Persist();
            OnChanged?.Invoke(bedId);
        }

        public static void SetRemaining(string bedId, int remaining)
        {
            EnsureHydrated();
            if (!_beds.TryGetValue(bedId, out var r)) return;
            if (r.Remaining == remaining) return;
            r.Remaining = remaining;
            Persist();
            OnChanged?.Invoke(bedId);
        }

        public static void Clear(string bedId)
        {
            EnsureHydrated();
            if (!_beds.Remove(bedId)) return;
            Persist();
            OnChanged?.Invoke(bedId);
        }

        // Used by save load to reset all in-memory state to a snapshot.
        public static void HydrateFrom(GrowBedSnapshot snap)
        {
            _beds.Clear();
            if (snap != null && snap.Ids != null)
            {
                int n = snap.Ids.Length;
                for (int i = 0; i < n; i++)
                {
                    if (string.IsNullOrEmpty(snap.Ids[i])) continue;
                    _beds[snap.Ids[i]] = new BedRecord
                    {
                        SpeciesId = snap.SpeciesIds != null && i < snap.SpeciesIds.Length ? snap.SpeciesIds[i] : null,
                        PlantedDay = snap.PlantedDays != null && i < snap.PlantedDays.Length ? snap.PlantedDays[i] : 1,
                        PlantedHour = snap.PlantedHours != null && i < snap.PlantedHours.Length ? snap.PlantedHours[i] : 0f,
                        Remaining = snap.Remaining != null && i < snap.Remaining.Length ? snap.Remaining[i] : 0,
                    };
                }
            }
            _hydrated = true;
        }

        public static GrowBedSnapshot ToSnapshot()
        {
            EnsureHydrated();
            var snap = new GrowBedSnapshot
            {
                Ids = new string[_beds.Count],
                SpeciesIds = new string[_beds.Count],
                PlantedDays = new int[_beds.Count],
                PlantedHours = new float[_beds.Count],
                Remaining = new int[_beds.Count],
            };
            int i = 0;
            foreach (var kv in _beds)
            {
                snap.Ids[i] = kv.Key;
                snap.SpeciesIds[i] = kv.Value.SpeciesId;
                snap.PlantedDays[i] = kv.Value.PlantedDay;
                snap.PlantedHours[i] = kv.Value.PlantedHour;
                snap.Remaining[i] = kv.Value.Remaining;
                i++;
            }
            return snap;
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try
            {
                var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
                if (meta != null) HydrateFrom(meta.GrowBeds);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GrowBeds] Hydration failed: " + e.Message);
            }
        }

        private static void Persist()
        {
            try
            {
                SaveManager.AutoSaveGrowBeds(ToSnapshot());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GrowBeds] Autosave failed: " + e.Message);
            }
        }
    }
}
