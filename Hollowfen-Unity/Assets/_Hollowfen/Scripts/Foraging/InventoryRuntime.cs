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
        public enum BatchRemovalFailure
        {
            None,
            InvalidBatch,
            InsufficientStock,
            PersistenceUnavailable,
        }

        public readonly struct BasketSale
        {
            public BasketSale(int soldCount, int refusedCount, int copper)
            {
                SoldCount = soldCount;
                RefusedCount = refusedCount;
                Copper = copper;
            }

            public int SoldCount { get; }
            public int RefusedCount { get; }
            public int Copper { get; }
        }

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
            NotifyChangedSafely(data.Id, next);
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
            NotifyChangedSafely(data.Id, next);
            return true;
        }

        // Compatibility overload for callers that only need success/failure.
        public static bool TryRemoveBatch(MushroomFieldGuideData[] species, int[] amounts)
        {
            return TryRemoveBatch(species, amounts, out _);
        }

        // Atomic multi-species delivery: validate and aggregate the entire order, stage the
        // resulting basket, and require a durable higher-revision save before publishing it to
        // live state. A refused/failed save therefore consumes nothing and emits no change event.
        public static bool TryRemoveBatch(MushroomFieldGuideData[] species, int[] amounts,
            out BatchRemovalFailure failure)
        {
            failure = BatchRemovalFailure.InvalidBatch;
            if (species == null || amounts == null || species.Length == 0 || species.Length != amounts.Length)
                return false;
            EnsureHydrated();

            var required = new Dictionary<string, int>(StringComparer.Ordinal);
            var resolved = new Dictionary<string, MushroomFieldGuideData>(StringComparer.Ordinal);
            for (int i = 0; i < species.Length; i++)
            {
                var entry = species[i];
                int amount = amounts[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || amount <= 0) return false;
                required.TryGetValue(entry.Id, out int current);
                long combined = (long)current + amount;
                if (combined > int.MaxValue) return false;
                required[entry.Id] = (int)combined;
                resolved[entry.Id] = entry;
            }

            failure = BatchRemovalFailure.InsufficientStock;
            foreach (var row in required)
                if (!_counts.TryGetValue(row.Key, out int current) || current < row.Value) return false;

            var staged = new Dictionary<string, int>(_counts, StringComparer.Ordinal);
            foreach (var row in required)
            {
                int next = staged[row.Key] - row.Value;
                if (next <= 0) staged.Remove(row.Key);
                else staged[row.Key] = next;
            }

            failure = BatchRemovalFailure.PersistenceUnavailable;
            var snapshot = SnapshotFrom(staged);
            if (!SaveManager.IsAtomicTransactionActive && !TryPersistBatchSnapshot(snapshot)) return false;

            _counts.Clear();
            foreach (var row in staged) _counts[row.Key] = row.Value;
            foreach (var row in resolved) _byId[row.Key] = row.Value;
            NotifyChangedSafely(null, 0);
            failure = BatchRemovalFailure.None;
            return true;
        }

        // Empties the basket (firstSale: Marra takes the lot for the pot).
        public static void RemoveAll()
        {
            EnsureHydrated();
            if (_counts.Count == 0) return;
            _counts.Clear();
            Persist();
            NotifyChangedSafely(null, 0);
        }

        public static BasketSale SellTo(MushroomBuyer buyer)
        {
            EnsureHydrated();
            BasketSale quote = QuoteFor(buyer);
            if (quote.SoldCount <= 0) return quote;

            var remove = new List<string>();
            foreach (var pair in _counts)
            {
                var species = ResolveData(pair.Key);
                int each = MushroomRules.SaleValue(species, buyer);
                if (each > 0) remove.Add(pair.Key);
            }

            if (remove.Count > 0)
            {
                foreach (var id in remove) _counts.Remove(id);
                Persist();
                NotifyChangedSafely(null, 0);
            }
            return quote;
        }

        // Read-only quote used by Wren's Purse. Buyer policy still lives on the mushroom data,
        // so the preview and the committed sale cannot drift apart.
        public static BasketSale QuoteFor(MushroomBuyer buyer)
        {
            EnsureHydrated();
            if (buyer == MushroomBuyer.None || _counts.Count == 0)
                return new BasketSale(0, TotalCount, 0);

            int sold = 0;
            int refused = 0;
            int copper = 0;
            foreach (var pair in _counts)
            {
                var species = ResolveData(pair.Key);
                int each = MushroomRules.SaleValue(species, buyer);
                if (each <= 0) refused += pair.Value;
                else
                {
                    sold += pair.Value;
                    copper += pair.Value * each;
                }
            }
            return new BasketSale(sold, refused, copper);
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
            NotifyChangedSafely(null, 0);
        }

        public static InventorySnapshot ToSnapshot()
        {
            EnsureHydrated();
            return SnapshotFrom(_counts);
        }

        private static InventorySnapshot SnapshotFrom(Dictionary<string, int> counts)
        {
            var ids = new List<string>(counts.Keys);
            ids.Sort(StringComparer.Ordinal);
            var snapshot = new InventorySnapshot
            {
                Ids = ids.ToArray(),
                Counts = new int[ids.Count],
            };
            for (int i = 0; i < ids.Count; i++) snapshot.Counts[i] = counts[ids[i]];
            return snapshot;
        }

        private static bool TryPersistBatchSnapshot(InventorySnapshot snapshot)
        {
            try
            {
                var before = SaveManager.InspectSlot(SaveManager.ActiveSlot);
                if (!before.CanLoad) return false;

                Exception writeFailure = null;
                try { SaveManager.AutoSaveInventory(snapshot); }
                catch (Exception exception) { writeFailure = exception; }

                // A replacement can fail after its temp was force-flushed. Inspecting again
                // treats that higher-revision temp as a successful durable commit, so callers
                // never receive failure after ingredients have become the recovery winner.
                var after = SaveManager.InspectSlot(SaveManager.ActiveSlot);
                bool committed = after.CanLoad && after.Revision > before.Revision &&
                                 SnapshotMatches(after.Meta?.Inventory, snapshot);
                if (!committed && writeFailure != null)
                    Debug.LogWarning("[Inventory] Batch autosave failed: " + writeFailure.Message);
                return committed;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Inventory] Batch autosave verification failed: " + exception.Message);
                return false;
            }
        }

        private static bool SnapshotMatches(InventorySnapshot actual, InventorySnapshot expected)
        {
            if (!TrySnapshotDictionary(actual, out var actualCounts) ||
                !TrySnapshotDictionary(expected, out var expectedCounts) ||
                actualCounts.Count != expectedCounts.Count)
                return false;
            foreach (var row in expectedCounts)
            {
                if (!actualCounts.TryGetValue(row.Key, out int count) || count != row.Value) return false;
            }
            return true;
        }

        private static bool TrySnapshotDictionary(InventorySnapshot snapshot,
            out Dictionary<string, int> counts)
        {
            counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (snapshot == null) return true;
            if (snapshot.Ids == null || snapshot.Counts == null || snapshot.Ids.Length != snapshot.Counts.Length)
                return false;
            for (int i = 0; i < snapshot.Ids.Length; i++)
            {
                string id = snapshot.Ids[i];
                int count = snapshot.Counts[i];
                if (string.IsNullOrWhiteSpace(id) || count < 0 || counts.ContainsKey(id)) return false;
                counts[id] = count;
            }
            return true;
        }

        private static void NotifyChangedSafely(string id, int count)
        {
            var handlers = OnChanged;
            if (handlers == null) return;
            foreach (Action<string, int> handler in handlers.GetInvocationList())
            {
                var callback = handler;
                SaveManager.PublishAfterAtomicCommit(() => callback(id, count));
            }
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
