using System;
using System.Collections.Generic;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.Items
{
    // Static registry of narrative key items (mill key, father's journal, sealed letter...).
    // Mirrors InventoryRuntime's pattern: in-memory set, hydrates from the autosave slot on
    // first access, persists immediately on change. Ids are canon ids from docs/story.md
    // ("item.mill_key"); display names come from Localization ("item.mill_key.name").
    public static class KeyItems
    {
        private static readonly HashSet<string> _ids = new HashSet<string>();
        private static bool _hydrated;

        // Fired when a new key item lands in Wren's possession. (id)
        public static event Action<string> OnGranted;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            _ids.Clear();
            _hydrated = false;
            OnGranted = null;
        }

        public static bool Has(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureHydrated();
            return _ids.Contains(id);
        }

        public static IReadOnlyCollection<string> All
        {
            get { EnsureHydrated(); return _ids; }
        }

        public static bool Grant(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureHydrated();
            if (!_ids.Add(id)) return false;
            Persist();
            OnGranted?.Invoke(id);
            return true;
        }

        // Used by save load to reset all in-memory state to a snapshot.
        public static void HydrateFrom(string[] ids)
        {
            _ids.Clear();
            if (ids != null)
                foreach (var id in ids)
                    if (!string.IsNullOrEmpty(id)) _ids.Add(id);
            _hydrated = true;
        }

        public static string[] ToArray()
        {
            EnsureHydrated();
            var arr = new string[_ids.Count];
            _ids.CopyTo(arr);
            return arr;
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try
            {
                var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
                if (meta != null && meta.KeyItemIds != null)
                    foreach (var id in meta.KeyItemIds)
                        if (!string.IsNullOrEmpty(id)) _ids.Add(id);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KeyItems] Hydration failed: " + e.Message);
            }
        }

        private static void Persist()
        {
            try
            {
                SaveManager.AutoSaveKeyItems(ToArray());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KeyItems] Autosave failed: " + e.Message);
            }
        }
    }
}
