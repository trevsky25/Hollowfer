using System;
using System.Collections.Generic;
using Hollowfen.Save;
using Hollowfen.Weather;
using UnityEngine;

namespace Hollowfen.Foraging
{
    /// <summary>
    /// Slot-persistent lifecycle for authored wild mushroom nodes. Only harvested nodes are
    /// stored; availability is derived from the current day and the species' respawn cadence.
    /// Cultivated nodes deliberately stay in GrowBeds, which already owns partial-flush state.
    /// </summary>
    public static class ForageNodeStates
    {
        private static readonly Dictionary<string, int> HarvestedDays = new Dictionary<string, int>();
        private static bool _hydrated;

        public static event Action<string> OnChanged;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            HarvestedDays.Clear();
            _hydrated = false;
            OnChanged = null;
        }

        public static bool IsAvailable(string nodeId, int currentDay, int respawnDays)
        {
            if (string.IsNullOrEmpty(nodeId)) return true;
            EnsureHydrated();
            if (!HarvestedDays.TryGetValue(nodeId, out var cutDay)) return true;
            int adjustedDays = WeatherSystem.AdjustedWildRespawnDays(respawnDays, cutDay);
            return currentDay >= cutDay + adjustedDays;
        }

        public static void MarkHarvested(string nodeId, int day)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            EnsureHydrated();
            HarvestedDays[nodeId] = Mathf.Max(1, day);
            Persist();
            OnChanged?.Invoke(nodeId);
        }

        public static void HydrateFrom(ForageNodeSnapshot snapshot)
        {
            HarvestedDays.Clear();
            if (snapshot != null && snapshot.Ids != null && snapshot.HarvestedDays != null)
            {
                int count = Mathf.Min(snapshot.Ids.Length, snapshot.HarvestedDays.Length);
                for (int i = 0; i < count; i++)
                {
                    if (string.IsNullOrEmpty(snapshot.Ids[i])) continue;
                    HarvestedDays[snapshot.Ids[i]] = Mathf.Max(1, snapshot.HarvestedDays[i]);
                }
            }
            _hydrated = true;
            OnChanged?.Invoke(null);
        }

        public static ForageNodeSnapshot ToSnapshot()
        {
            EnsureHydrated();
            var ids = new List<string>(HarvestedDays.Keys);
            ids.Sort(StringComparer.Ordinal);
            var snapshot = new ForageNodeSnapshot
            {
                Ids = new string[ids.Count],
                HarvestedDays = new int[ids.Count],
            };
            for (int i = 0; i < ids.Count; i++)
            {
                snapshot.Ids[i] = ids[i];
                snapshot.HarvestedDays[i] = HarvestedDays[ids[i]];
            }
            return snapshot;
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try
            {
                HydrateFrom(SaveManager.GetSlotMeta(SaveManager.ActiveSlot)?.ForageNodes);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ForageNodeStates] Hydration failed: " + e.Message);
            }
        }

        private static void Persist()
        {
            try
            {
                SaveManager.AutoSaveForageNodes(ToSnapshot());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ForageNodeStates] Autosave failed: " + e.Message);
            }
        }
    }
}
