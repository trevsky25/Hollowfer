using System;
using System.Collections.Generic;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.Foraging
{
    // Per-save registry of discovered species. Lives in SaveSlotMeta (active slot) like the
    // other stores; sessions from before the save unification migrate their PlayerPrefs
    // record into the slot on first access, then the pref is retired.
    public static class MushroomDiscovery
    {
        private const string LegacyPrefKey = "forage.discoveredIds";
        private const char Separator = ';';

        private static readonly HashSet<string> _set = new HashSet<string>();
        private static bool _hydrated;

        public static event Action<string> OnDiscovered;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            _set.Clear();
            _hydrated = false;
            OnDiscovered = null;
        }

        public static bool IsDiscovered(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureHydrated();
            return _set.Contains(id);
        }

        // Returns true if newly discovered.
        public static bool MarkDiscovered(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureHydrated();
            if (!_set.Add(id)) return false;
            Persist();
            OnDiscovered?.Invoke(id);
            return true;
        }

        public static IEnumerable<string> All
        {
            get { EnsureHydrated(); return _set; }
        }

        // Used by save load to reset in-memory state to a snapshot.
        public static void HydrateFrom(string[] ids)
        {
            _set.Clear();
            if (ids != null)
                foreach (var id in ids)
                    if (!string.IsNullOrEmpty(id)) _set.Add(id);
            _hydrated = true;
        }

        public static string[] ToArray()
        {
            EnsureHydrated();
            var arr = new string[_set.Count];
            _set.CopyTo(arr);
            return arr;
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try
            {
                var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
                if (meta != null && meta.DiscoveredMushroomIds != null && meta.DiscoveredMushroomIds.Length > 0)
                {
                    foreach (var id in meta.DiscoveredMushroomIds)
                        if (!string.IsNullOrEmpty(id)) _set.Add(id);
                    return;
                }

                // One-time migration from the pre-unification PlayerPrefs record.
                string raw = PlayerPrefs.GetString(LegacyPrefKey, string.Empty);
                if (!string.IsNullOrEmpty(raw))
                {
                    foreach (var id in raw.Split(Separator))
                        if (!string.IsNullOrEmpty(id)) _set.Add(id);
                    Persist();
                    PlayerPrefs.DeleteKey(LegacyPrefKey);
                    PlayerPrefs.Save();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Discovery] Hydration failed: " + e.Message);
            }
        }

        private static void Persist()
        {
            try
            {
                SaveManager.AutoSaveDiscovery(ToArray());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Discovery] Autosave failed: " + e.Message);
            }
        }
    }
}
