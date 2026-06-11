using System;
using System.Collections.Generic;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.Quests
{
    // The meters and flags that gate the four endings (story.md): Village Hope, Knowledge,
    // per-NPC relationship values, and the named progression flags (act1_started ...
    // game_complete, ending_*). Same persistence recipe as the other stores: hydrates from
    // the active save slot on first access, persists immediately on change.
    public static class GameScores
    {
        private static int _villageHope;
        private static int _knowledge;
        private static readonly Dictionary<string, int> _relationships = new Dictionary<string, int>();
        private static readonly HashSet<string> _flags = new HashSet<string>();
        private static bool _hydrated;

        // Fired after any meter/flag change so HUD overlays can refresh. Cheap; no payload.
        public static event Action OnChanged;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            _villageHope = 0;
            _knowledge = 0;
            _relationships.Clear();
            _flags.Clear();
            _hydrated = false;
            OnChanged = null;
        }

        public static int VillageHope { get { EnsureHydrated(); return _villageHope; } }
        public static int Knowledge { get { EnsureHydrated(); return _knowledge; } }

        public static void AddVillageHope(int delta)
        {
            if (delta == 0) return;
            EnsureHydrated();
            _villageHope = Mathf.Max(0, _villageHope + delta);
            Persist();
            OnChanged?.Invoke();
        }

        public static void AddKnowledge(int delta)
        {
            if (delta == 0) return;
            EnsureHydrated();
            _knowledge = Mathf.Max(0, _knowledge + delta);
            Persist();
            OnChanged?.Invoke();
        }

        public static int GetRelationship(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return 0;
            EnsureHydrated();
            return _relationships.TryGetValue(npcId, out var v) ? v : 0;
        }

        public static void AddRelationship(string npcId, int delta)
        {
            if (string.IsNullOrEmpty(npcId) || delta == 0) return;
            EnsureHydrated();
            _relationships[npcId] = GetRelationship(npcId) + delta;
            Persist();
            OnChanged?.Invoke();
        }

        public static bool HasFlag(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureHydrated();
            return _flags.Contains(id);
        }

        // Returns true if newly set.
        public static bool SetFlag(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureHydrated();
            if (!_flags.Add(id)) return false;
            Persist();
            OnChanged?.Invoke();
            return true;
        }

        public static IEnumerable<KeyValuePair<string, int>> Relationships
        {
            get { EnsureHydrated(); return _relationships; }
        }

        public static IEnumerable<string> Flags
        {
            get { EnsureHydrated(); return _flags; }
        }

        // Used by save load to reset in-memory state to a snapshot.
        public static void HydrateFrom(SaveSlotMeta meta)
        {
            _villageHope = meta != null ? Mathf.Max(0, meta.VillageHope) : 0;
            _knowledge = meta != null ? Mathf.Max(0, meta.Knowledge) : 0;
            _relationships.Clear();
            _flags.Clear();
            if (meta != null && meta.RelationshipNpcIds != null && meta.RelationshipValues != null)
            {
                int n = Mathf.Min(meta.RelationshipNpcIds.Length, meta.RelationshipValues.Length);
                for (int i = 0; i < n; i++)
                    if (!string.IsNullOrEmpty(meta.RelationshipNpcIds[i]))
                        _relationships[meta.RelationshipNpcIds[i]] = meta.RelationshipValues[i];
            }
            if (meta != null && meta.GameFlagIds != null)
                foreach (var f in meta.GameFlagIds)
                    if (!string.IsNullOrEmpty(f)) _flags.Add(f);
            _hydrated = true;
            OnChanged?.Invoke();
        }

        public static void WriteTo(SaveSlotMeta meta)
        {
            if (meta == null) return;
            EnsureHydrated();
            meta.VillageHope = _villageHope;
            meta.Knowledge = _knowledge;
            meta.RelationshipNpcIds = new string[_relationships.Count];
            meta.RelationshipValues = new int[_relationships.Count];
            int i = 0;
            foreach (var kv in _relationships)
            {
                meta.RelationshipNpcIds[i] = kv.Key;
                meta.RelationshipValues[i] = kv.Value;
                i++;
            }
            meta.GameFlagIds = new string[_flags.Count];
            _flags.CopyTo(meta.GameFlagIds);
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try
            {
                var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
                if (meta != null) HydrateFrom(meta);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameScores] Hydration failed: " + e.Message);
            }
        }

        private static void Persist()
        {
            try
            {
                SaveManager.AutoSaveScores();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameScores] Autosave failed: " + e.Message);
            }
        }
    }
}
