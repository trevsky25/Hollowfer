using System;
using System.Collections.Generic;
using Hollowfen.Data;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.Foraging
{
    // Static, in-memory inventory of harvested mushrooms keyed by SO Id. Backed by SaveSlotMeta on disk
    // (slot 0 / autosave) so harvests persist immediately. Hydrates from the autosave on first access.
    //
    // Adding a UI for currency / non-mushroom items later: split the map by item type, or generalize the
    // value type to a small struct. For now the foraging slice only carries mushrooms.
    public static class InventoryRuntime
    {
        private static readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private static readonly Dictionary<string, MushroomFieldGuideData> _byId = new Dictionary<string, MushroomFieldGuideData>();
        private static MushroomFieldGuideDatabase _database;
        private static bool _hydrated;

        // Fired whenever a count changes. (id, newCount). UI should subscribe and refresh.
        public static event Action<string, int> OnChanged;

        public static int TotalCount
        {
            get
            {
                EnsureHydrated();
                int n = 0;
                foreach (var v in _counts.Values) n += v;
                return n;
            }
        }

        public static int DistinctCount
        {
            get { EnsureHydrated(); return _counts.Count; }
        }

        public static int GetCount(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            EnsureHydrated();
            return _counts.TryGetValue(id, out var n) ? n : 0;
        }

        public static int GetCount(MushroomFieldGuideData data) =>
            data == null ? 0 : GetCount(data.Id);

        public static IEnumerable<KeyValuePair<string, int>> All
        {
            get { EnsureHydrated(); return _counts; }
        }

        public static MushroomFieldGuideData ResolveData(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureHydrated();
            return _byId.TryGetValue(id, out var d) ? d : null;
        }

        public static void Add(MushroomFieldGuideData data, int amount = 1)
        {
            if (data == null || amount <= 0) return;
            EnsureHydrated();
            int next = (_counts.TryGetValue(data.Id, out var cur) ? cur : 0) + amount;
            _counts[data.Id] = next;
            _byId[data.Id] = data;
            Persist();
            OnChanged?.Invoke(data.Id, next);
        }

        public static bool Remove(MushroomFieldGuideData data, int amount = 1)
        {
            if (data == null || amount <= 0) return false;
            EnsureHydrated();
            if (!_counts.TryGetValue(data.Id, out var cur) || cur < amount) return false;
            int next = cur - amount;
            if (next <= 0) _counts.Remove(data.Id);
            else _counts[data.Id] = next;
            Persist();
            OnChanged?.Invoke(data.Id, next);
            return true;
        }

        // Empties the basket (firstSale: Marra takes the lot for the pot).
        public static void RemoveAll()
        {
            EnsureHydrated();
            if (_counts.Count == 0) return;
            _counts.Clear();
            Persist();
            OnChanged?.Invoke(null, 0);
        }

        // Used by save load to reset all in-memory state to a snapshot.
        public static void HydrateFrom(InventorySnapshot snap)
        {
            _counts.Clear();
            if (snap != null && snap.Ids != null && snap.Counts != null)
            {
                int n = Mathf.Min(snap.Ids.Length, snap.Counts.Length);
                for (int i = 0; i < n; i++)
                {
                    var id = snap.Ids[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    _counts[id] = Mathf.Max(0, snap.Counts[i]);
                }
            }
            ResolveDatabaseIfNeeded();
            // Re-resolve SO refs for hydrated ids so callers can render photos right away.
            if (_database != null && _database.Entries != null)
            {
                foreach (var id in _counts.Keys)
                {
                    foreach (var entry in _database.Entries)
                    {
                        if (entry != null && entry.Id == id) { _byId[id] = entry; break; }
                    }
                }
            }
            _hydrated = true;
            // Broadcast a wildcard change so UIs rebuild after a save load.
            OnChanged?.Invoke(null, 0);
        }

        public static InventorySnapshot ToSnapshot()
        {
            EnsureHydrated();
            var snap = new InventorySnapshot();
            snap.Ids = new string[_counts.Count];
            snap.Counts = new int[_counts.Count];
            int i = 0;
            foreach (var kv in _counts)
            {
                snap.Ids[i] = kv.Key;
                snap.Counts[i] = kv.Value;
                i++;
            }
            return snap;
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            ResolveDatabaseIfNeeded();
            try
            {
                var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
                if (meta != null && meta.Inventory != null)
                {
                    HydrateFrom(meta.Inventory);
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Inventory] Hydration failed: " + e.Message);
            }
        }

        private static void Persist()
        {
            try
            {
                SaveManager.AutoSaveInventory(ToSnapshot());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Inventory] Autosave failed: " + e.Message);
            }
        }

        private static void ResolveDatabaseIfNeeded()
        {
            if (_database != null) return;
            // The database SO lives at a known path; load via Resources is the runtime-safe path.
            // For now we rely on already-loaded assets — anything in _byId from gameplay Add() is fine.
            // If you need full hydration of names from disk, drop the SO into a Resources folder and
            // load here.
            _database = Resources.Load<MushroomFieldGuideDatabase>("MushroomFieldGuideDatabase");
        }
    }
}
