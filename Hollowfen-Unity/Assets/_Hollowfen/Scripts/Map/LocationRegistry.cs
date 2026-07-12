using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowfen.Map
{
    public static class LocationRegistry
    {
        private static readonly List<LocationMarker> _markers = new List<LocationMarker>();
        private static readonly HashSet<string> _discovered = new HashSet<string>();
        private static readonly List<RegionTrigger> _activeRegions = new List<RegionTrigger>();
        private static bool _hydrated;

        public static event Action<string> LocationDiscovered;
        public static event Action<string> RegionChanged;
        public static event Action<LocationMarker> WaypointChanged;

        public static IReadOnlyList<LocationMarker> Markers => _markers;
        public static string CurrentRegion { get; private set; }
        public static LocationMarker ActiveWaypoint { get; private set; }

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            _markers.Clear();
            _discovered.Clear();
            _activeRegions.Clear();
            _hydrated = false;
            CurrentRegion = null;
            ActiveWaypoint = null;
            LocationDiscovered = null;
            RegionChanged = null;
            WaypointChanged = null;
        }

        // Same persistence recipe as KeyItems: lazy-hydrate from the active slot, write back on change.
        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try
            {
                var meta = Save.SaveManager.GetSlotMeta(Save.SaveManager.ActiveSlot);
                if (meta != null && meta.DiscoveredLocationIds != null)
                    foreach (var id in meta.DiscoveredLocationIds)
                        if (!string.IsNullOrEmpty(id)) _discovered.Add(id);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LocationRegistry] Hydration failed: " + e.Message);
            }
        }

        public static void HydrateFromSave(string[] ids)
        {
            _discovered.Clear();
            if (ids != null)
                foreach (var id in ids)
                    if (!string.IsNullOrEmpty(id)) _discovered.Add(id);
            _hydrated = true;
            // Re-apply default discoveries for markers already registered this scene.
            for (int i = 0; i < _markers.Count; i++)
                if (_markers[i] != null && _markers[i].Data != null && _markers[i].Data.DiscoveredByDefault)
                    _discovered.Add(_markers[i].Id);
        }

        public static string[] DiscoveredToArray()
        {
            EnsureHydrated();
            var arr = new string[_discovered.Count];
            _discovered.CopyTo(arr);
            return arr;
        }

        public static void RegisterMarker(LocationMarker marker)
        {
            if (marker == null || marker.Data == null) return;
            if (_markers.Contains(marker)) return;
            _markers.Add(marker);
            if (marker.Data.DiscoveredByDefault)
                MarkDiscovered(marker.Id);
        }

        public static void UnregisterMarker(LocationMarker marker)
        {
            if (marker == null) return;
            _markers.Remove(marker);
            if (ActiveWaypoint == marker) ClearWaypoint();
        }

        public static void SetWaypoint(LocationMarker marker)
        {
            if (marker == ActiveWaypoint) return;
            ActiveWaypoint = marker;
            WaypointChanged?.Invoke(marker);
        }

        public static void ToggleWaypoint(LocationMarker marker)
        {
            if (marker == null) return;
            if (ActiveWaypoint == marker) ClearWaypoint();
            else SetWaypoint(marker);
        }

        public static void ClearWaypoint()
        {
            if (ActiveWaypoint == null) return;
            ActiveWaypoint = null;
            WaypointChanged?.Invoke(null);
        }

        public static void MarkDiscovered(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            EnsureHydrated();
            if (!_discovered.Add(id)) return;
            try
            {
                Save.SaveManager.AutoSaveDiscoveredLocations(DiscoveredToArray());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LocationRegistry] Autosave failed: " + e.Message);
            }
            LocationDiscovered?.Invoke(id);
        }

        public static bool IsDiscovered(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureHydrated();
            return _discovered.Contains(id);
        }

        public static int DiscoveredCount => _discovered.Count;

        public static LocationMarker FindNearest(Vector3 worldPos, float maxDistance = float.PositiveInfinity)
        {
            LocationMarker best = null;
            float bestSqr = maxDistance * maxDistance;
            for (int i = 0; i < _markers.Count; i++)
            {
                var m = _markers[i];
                if (m == null) continue;
                float sqr = (m.WorldPosition - worldPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = m;
                }
            }
            return best;
        }

        public static void PushRegion(RegionTrigger trigger)
        {
            if (trigger == null) return;
            if (!_activeRegions.Contains(trigger))
                _activeRegions.Add(trigger);
            RecomputeCurrentRegion();
        }

        public static void PopRegion(RegionTrigger trigger)
        {
            if (trigger == null) return;
            _activeRegions.Remove(trigger);
            RecomputeCurrentRegion();
        }

        private static void RecomputeCurrentRegion()
        {
            string winner = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < _activeRegions.Count; i++)
            {
                var r = _activeRegions[i];
                if (r == null) continue;
                if (r.Priority > bestPriority)
                {
                    bestPriority = r.Priority;
                    winner = r.RegionId;
                }
            }
            if (winner == CurrentRegion) return;
            CurrentRegion = winner;
            RegionChanged?.Invoke(CurrentRegion);
        }
    }
}
