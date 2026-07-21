using System;
using System.Collections.Generic;
using Hollowfen.GameTime;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.NPCs
{
    /// <summary>
    /// The village's durable social history. GameScores answers "how close is Wren to Bram?";
    /// this store answers "what did Bram live through with Wren?", "how have Bram and Marra
    /// changed toward one another?", and "where is Bram's personal favor chain?".
    /// </summary>
    public static class VillagerRelationships
    {
        public readonly struct MemoryRecord
        {
            public MemoryRecord(string npcId, string memoryId, int day)
            {
                NpcId = npcId;
                MemoryId = memoryId;
                Day = day;
            }

            public string NpcId { get; }
            public string MemoryId { get; }
            public int Day { get; }
        }

        public readonly struct BondRecord
        {
            public BondRecord(string firstNpcId, string secondNpcId, int value)
            {
                FirstNpcId = firstNpcId;
                SecondNpcId = secondNpcId;
                Value = value;
            }

            public string FirstNpcId { get; }
            public string SecondNpcId { get; }
            public int Value { get; }
        }

        private static readonly Dictionary<string, MemoryRecord> Memories =
            new Dictionary<string, MemoryRecord>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> Bonds =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> Favors =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static bool _hydrated;

        public static event Action OnChanged;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            Memories.Clear();
            Bonds.Clear();
            Favors.Clear();
            _hydrated = false;
            OnChanged = null;
        }

        public static bool HasMemory(string npcId, string memoryId)
        {
            if (string.IsNullOrWhiteSpace(npcId) || string.IsNullOrWhiteSpace(memoryId)) return false;
            EnsureHydrated();
            return Memories.ContainsKey(MemoryKey(npcId, memoryId));
        }

        public static int MemoryDay(string npcId, string memoryId)
        {
            if (string.IsNullOrWhiteSpace(npcId) || string.IsNullOrWhiteSpace(memoryId)) return 0;
            EnsureHydrated();
            return Memories.TryGetValue(MemoryKey(npcId, memoryId), out var record) ? record.Day : 0;
        }

        public static IReadOnlyList<MemoryRecord> MemoriesFor(string npcId)
        {
            EnsureHydrated();
            var result = new List<MemoryRecord>();
            if (string.IsNullOrWhiteSpace(npcId)) return result;
            foreach (var pair in Memories)
                if (string.Equals(pair.Value.NpcId, npcId, StringComparison.Ordinal))
                    result.Add(pair.Value);
            result.Sort((left, right) => right.Day != left.Day
                ? right.Day.CompareTo(left.Day)
                : string.CompareOrdinal(left.MemoryId, right.MemoryId));
            return result;
        }

        // Returns true only the first time this NPC gains this recollection.
        public static bool Remember(string npcId, string memoryId, int day = 0)
        {
            if (string.IsNullOrWhiteSpace(npcId) || string.IsNullOrWhiteSpace(memoryId)) return false;
            EnsureHydrated();
            string key = MemoryKey(npcId, memoryId);
            if (Memories.ContainsKey(key)) return false;
            if (day <= 0) day = TimeManager.Instance != null ? TimeManager.Instance.Day : 1;
            Memories[key] = new MemoryRecord(npcId.Trim(), memoryId.Trim(), Mathf.Max(1, day));
            PersistAndPublish();
            return true;
        }

        public static int GetBond(string firstNpcId, string secondNpcId)
        {
            if (!TryBondKey(firstNpcId, secondNpcId, out var key)) return 0;
            EnsureHydrated();
            return Bonds.TryGetValue(key, out var value) ? value : 0;
        }

        public static int AddBond(string firstNpcId, string secondNpcId, int delta)
        {
            if (delta == 0 || !TryBondKey(firstNpcId, secondNpcId, out var key))
                return GetBond(firstNpcId, secondNpcId);
            EnsureHydrated();
            int value = Mathf.Clamp(GetBond(firstNpcId, secondNpcId) + delta, -100, 100);
            Bonds[key] = value;
            PersistAndPublish();
            return value;
        }

        public static IReadOnlyList<BondRecord> BondsFor(string npcId)
        {
            EnsureHydrated();
            string id = Clean(npcId);
            var result = new List<BondRecord>();
            if (id.Length == 0) return result;
            foreach (var pair in Bonds)
            {
                SplitBondKey(pair.Key, out var first, out var second);
                if (string.Equals(first, id, StringComparison.Ordinal) ||
                    string.Equals(second, id, StringComparison.Ordinal))
                    result.Add(new BondRecord(first, second, pair.Value));
            }
            result.Sort((left, right) => Mathf.Abs(right.Value).CompareTo(Mathf.Abs(left.Value)));
            return result;
        }

        public static int FavorStage(string favorId)
        {
            if (string.IsNullOrWhiteSpace(favorId)) return 0;
            EnsureHydrated();
            return Favors.TryGetValue(favorId.Trim(), out var stage) ? Mathf.Max(0, stage) : 0;
        }

        // Favor chains are monotonic. Replaying or loading older authored dialogue cannot regress one.
        public static bool AdvanceFavor(string favorId, int stage)
        {
            if (string.IsNullOrWhiteSpace(favorId) || stage <= 0) return false;
            EnsureHydrated();
            string key = favorId.Trim();
            int current = FavorStage(key);
            if (stage <= current) return false;
            Favors[key] = stage;
            PersistAndPublish();
            return true;
        }

        public static void HydrateFrom(VillagerRelationshipSnapshot snapshot)
        {
            Memories.Clear();
            Bonds.Clear();
            Favors.Clear();

            if (snapshot != null)
            {
                int memoryCount = SharedLength(snapshot.MemoryNpcIds, snapshot.MemoryIds, snapshot.MemoryDays);
                for (int i = 0; i < memoryCount; i++)
                {
                    string npc = Clean(snapshot.MemoryNpcIds[i]);
                    string memory = Clean(snapshot.MemoryIds[i]);
                    if (npc.Length == 0 || memory.Length == 0) continue;
                    string key = MemoryKey(npc, memory);
                    int day = Mathf.Max(1, snapshot.MemoryDays[i]);
                    if (!Memories.TryGetValue(key, out var prior) || day >= prior.Day)
                        Memories[key] = new MemoryRecord(npc, memory, day);
                }

                int bondCount = SharedLength(snapshot.BondNpcAIds, snapshot.BondNpcBIds, snapshot.BondValues);
                for (int i = 0; i < bondCount; i++)
                {
                    if (!TryBondKey(snapshot.BondNpcAIds[i], snapshot.BondNpcBIds[i], out var key)) continue;
                    Bonds[key] = Mathf.Clamp(snapshot.BondValues[i], -100, 100);
                }

                int favorCount = SharedLength(snapshot.FavorIds, snapshot.FavorStages);
                for (int i = 0; i < favorCount; i++)
                {
                    string id = Clean(snapshot.FavorIds[i]);
                    if (id.Length == 0) continue;
                    int stage = Mathf.Max(0, snapshot.FavorStages[i]);
                    if (!Favors.TryGetValue(id, out var prior) || stage > prior) Favors[id] = stage;
                }
            }

            _hydrated = true;
            PublishChanged();
        }

        public static VillagerRelationshipSnapshot ToSnapshot()
        {
            EnsureHydrated();
            var snapshot = new VillagerRelationshipSnapshot
            {
                MemoryNpcIds = new string[Memories.Count],
                MemoryIds = new string[Memories.Count],
                MemoryDays = new int[Memories.Count],
                BondNpcAIds = new string[Bonds.Count],
                BondNpcBIds = new string[Bonds.Count],
                BondValues = new int[Bonds.Count],
                FavorIds = new string[Favors.Count],
                FavorStages = new int[Favors.Count],
            };

            int index = 0;
            foreach (var pair in Memories)
            {
                snapshot.MemoryNpcIds[index] = pair.Value.NpcId;
                snapshot.MemoryIds[index] = pair.Value.MemoryId;
                snapshot.MemoryDays[index] = pair.Value.Day;
                index++;
            }

            index = 0;
            foreach (var pair in Bonds)
            {
                SplitBondKey(pair.Key, out snapshot.BondNpcAIds[index], out snapshot.BondNpcBIds[index]);
                snapshot.BondValues[index] = pair.Value;
                index++;
            }

            index = 0;
            foreach (var pair in Favors)
            {
                snapshot.FavorIds[index] = pair.Key;
                snapshot.FavorStages[index] = pair.Value;
                index++;
            }
            return snapshot;
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try
            {
                var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
                if (meta != null) HydrateFrom(meta.VillagerRelationships);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[VillagerRelationships] Hydration failed: " + exception.Message);
            }
        }

        private static void PersistAndPublish()
        {
            try { SaveManager.AutoSaveVillagerRelationships(ToSnapshot()); }
            catch (Exception exception)
            {
                Debug.LogWarning("[VillagerRelationships] Autosave failed: " + exception.Message);
            }
            PublishChanged();
        }

        private static void PublishChanged()
        {
            var handlers = OnChanged;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                Action callback = handler;
                SaveManager.PublishAfterAtomicCommit(callback);
            }
        }

        private static string MemoryKey(string npcId, string memoryId) => Clean(npcId) + "\n" + Clean(memoryId);

        private static bool TryBondKey(string firstNpcId, string secondNpcId, out string key)
        {
            string first = Clean(firstNpcId);
            string second = Clean(secondNpcId);
            if (first.Length == 0 || second.Length == 0 || string.Equals(first, second, StringComparison.Ordinal))
            {
                key = string.Empty;
                return false;
            }
            key = string.CompareOrdinal(first, second) < 0 ? first + "\n" + second : second + "\n" + first;
            return true;
        }

        private static void SplitBondKey(string key, out string first, out string second)
        {
            int split = key.IndexOf('\n');
            first = split > 0 ? key.Substring(0, split) : key;
            second = split >= 0 && split + 1 < key.Length ? key.Substring(split + 1) : string.Empty;
        }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private static int SharedLength(params Array[] arrays)
        {
            int result = int.MaxValue;
            foreach (var array in arrays) result = Mathf.Min(result, array != null ? array.Length : 0);
            return result == int.MaxValue ? 0 : Mathf.Max(0, result);
        }
    }
}
