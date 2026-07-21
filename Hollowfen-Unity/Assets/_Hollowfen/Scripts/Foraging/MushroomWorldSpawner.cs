using System;
using System.Collections.Generic;
using System.Linq;
using Hollowfen.Data;
using UnityEngine;

namespace Hollowfen.Foraging
{
    /// <summary>
    /// Deterministic whole-map mushroom ecology. Zones follow Hollowfen's physical habitats and
    /// story tiers; the same save always sees the same nodes in the same places, and their stable
    /// ids use the ordinary wild-node cooldown store. Authored hero specimens remain untouched.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public sealed class MushroomWorldSpawner : MonoBehaviour
    {
        [Serializable]
        public struct HabitatZone
        {
            public string id;
            public Vector2 center;
            public Vector2 extents;
            [Min(1)] public int population;
            [Min(8)] public int attemptsPerNode;
            public string[] speciesIds;
        }

        [SerializeField] private HabitatZone[] _zones = DefaultZones();
        [SerializeField, Min(1.5f)] private float _minimumSpacing = 3.5f;
        [SerializeField, Range(5f, 45f)] private float _maximumSlope = 28f;
        [SerializeField] private bool _spawnOnStart = true;

        private readonly List<Vector3> _occupied = new List<Vector3>();
        private Transform _generatedRoot;

        public IReadOnlyList<HabitatZone> Zones => _zones;
        public int RequestedPopulation => _zones != null ? _zones.Sum(zone => zone.population) : 0;
        public int SpawnedCount { get; private set; }

        private void Start()
        {
            if (_spawnOnStart) Populate();
        }

        [ContextMenu("Populate Mushroom Ecology")]
        public void Populate()
        {
            if (SpawnedCount > 0 || _zones == null || _zones.Length == 0) return;
            MushroomFieldGuideDatabase database = Resources.Load<MushroomFieldGuideDatabase>(
                "MushroomFieldGuideDatabase");
            if (database == null || database.Entries == null)
            {
                Debug.LogError("[MushroomWorldSpawner] MushroomFieldGuideDatabase is missing.", this);
                return;
            }

            _generatedRoot = new GameObject("Generated_MushroomEcology").transform;
            _generatedRoot.SetParent(transform, false);
            _occupied.Clear();
            foreach (MushroomNode node in FindObjectsByType<MushroomNode>(FindObjectsInactive.Include))
                if (node != null && !node.IsCultivated) _occupied.Add(node.transform.position);

            var byId = database.Entries.Where(entry => entry != null)
                .ToDictionary(entry => entry.Id, entry => entry, StringComparer.Ordinal);
            for (int zoneIndex = 0; zoneIndex < _zones.Length; zoneIndex++)
                PopulateZone(_zones[zoneIndex], zoneIndex, byId);

            Debug.Log("[MushroomWorldSpawner] Populated " + SpawnedCount + " / " +
                      RequestedPopulation + " deterministic wild nodes across " + _zones.Length +
                      " habitats.", this);
        }

        private void PopulateZone(HabitatZone zone, int zoneIndex,
            IReadOnlyDictionary<string, MushroomFieldGuideData> byId)
        {
            var species = new List<MushroomFieldGuideData>();
            if (zone.speciesIds != null)
                foreach (string id in zone.speciesIds)
                    if (!string.IsNullOrEmpty(id) && byId.TryGetValue(id, out MushroomFieldGuideData entry) &&
                        entry.WorldPrefab != null)
                        species.Add(entry);
            if (species.Count == 0)
            {
                Debug.LogWarning("[MushroomWorldSpawner] Habitat '" + zone.id + "' has no usable species.", this);
                return;
            }

            var zoneRoot = new GameObject("Habitat_" + zone.id).transform;
            zoneRoot.SetParent(_generatedRoot, false);
            var random = new System.Random(StableHash("hollowfen.ecology." + zone.id));
            int placed = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(zone.population * Mathf.Max(8, zone.attemptsPerNode),
                zone.population * 8);
            while (placed < zone.population && attempts++ < maxAttempts)
            {
                float x = zone.center.x + ((float)random.NextDouble() * 2f - 1f) * zone.extents.x;
                float z = zone.center.y + ((float)random.NextDouble() * 2f - 1f) * zone.extents.y;
                if (!TryGround(new Vector3(x, 140f, z), out Vector3 position, out Vector3 normal))
                    continue;
                if (Vector3.Angle(normal, Vector3.up) > _maximumSlope || !HasSpacing(position))
                    continue;
                if (HasBlockingCollider(position)) continue;

                MushroomFieldGuideData entry = species[random.Next(species.Count)];
                GameObject instance = Instantiate(entry.WorldPrefab, position,
                    Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), zoneRoot);
                instance.name = "Generated_" + entry.Id + "_" + placed.ToString("00");
                MushroomNode node = instance.GetComponent<MushroomNode>() ??
                                    instance.GetComponentInChildren<MushroomNode>(true);
                if (node == null)
                {
                    Destroy(instance);
                    continue;
                }
                string nodeId = "wild.generated." + zone.id + "." + placed.ToString("00");
                node.ConfigureGenerated(entry, nodeId);
                _occupied.Add(position);
                placed++;
                SpawnedCount++;
            }
            if (placed < zone.population)
                Debug.LogWarning("[MushroomWorldSpawner] Habitat '" + zone.id + "' placed " + placed +
                                 " / " + zone.population + " nodes after terrain and spacing checks.", this);
        }

        private bool TryGround(Vector3 origin, out Vector3 position, out Vector3 normal)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 220f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (!(hit.collider is TerrainCollider)) continue;
                position = hit.point + Vector3.up * 0.015f;
                normal = hit.normal;
                return true;
            }
            position = default;
            normal = Vector3.up;
            return false;
        }

        private bool HasSpacing(Vector3 candidate)
        {
            float minimumSqr = _minimumSpacing * _minimumSpacing;
            for (int i = 0; i < _occupied.Count; i++)
            {
                Vector3 delta = candidate - _occupied[i];
                delta.y = 0f;
                if (delta.sqrMagnitude < minimumSqr) return false;
            }
            return true;
        }

        private static bool HasBlockingCollider(Vector3 candidate)
        {
            Collider[] overlaps = Physics.OverlapSphere(candidate + Vector3.up * 0.65f, 0.8f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
                if (overlap != null && !(overlap is TerrainCollider)) return true;
            return false;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 16777619;
                return (int)(hash & 0x7fffffff);
            }
        }

        public static HabitatZone[] DefaultZones() => new[]
        {
            Zone("south-fields", 270f, 80f, 55f, 22f, 5,
                "fieldMushroom", "fieldCap", "goldfoot"),
            Zone("village-lanes", 280f, 150f, 55f, 35f, 6,
                "fieldCap", "pinecrest", "goldfoot"),
            Zone("wend-banks", 225f, 235f, 80f, 20f, 6,
                "fieldCap", "pinecrest", "coppercup", "lacewig"),
            Zone("chapel-cottages", 205f, 300f, 65f, 25f, 6,
                "pinecrest", "chanterelle", "bonepale", "brightspore"),
            Zone("clear-cut", 145f, 365f, 25f, 25f, 5,
                "coppercup", "bonepale", "porcini", "deadlyGalerina"),
            Zone("old-wood-edge", 255f, 395f, 70f, 24f, 7,
                "chanterelle", "porcini", "flyAgaric", "moonring", "hollowheart"),
            Zone("deep-old-wood", 260f, 445f, 55f, 20f, 6,
                "wendlight", "hollowheart", "deathCap", "destroyingAngel", "aldermark"),
        };

        private static HabitatZone Zone(string id, float x, float z, float extentX,
            float extentZ, int population, params string[] speciesIds) => new HabitatZone
        {
            id = id,
            center = new Vector2(x, z),
            extents = new Vector2(extentX, extentZ),
            population = population,
            attemptsPerNode = 30,
            speciesIds = speciesIds,
        };
    }
}
