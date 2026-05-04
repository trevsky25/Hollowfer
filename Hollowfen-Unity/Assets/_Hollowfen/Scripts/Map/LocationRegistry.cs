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

        public static event Action<string> LocationDiscovered;
        public static event Action<string> RegionChanged;

        public static IReadOnlyList<LocationMarker> Markers => _markers;
        public static string CurrentRegion { get; private set; }

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
            CurrentRegion = null;
            LocationDiscovered = null;
            RegionChanged = null;
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
        }

        public static void MarkDiscovered(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!_discovered.Add(id)) return;
            LocationDiscovered?.Invoke(id);
        }

        public static bool IsDiscovered(string id)
        {
            return !string.IsNullOrEmpty(id) && _discovered.Contains(id);
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
