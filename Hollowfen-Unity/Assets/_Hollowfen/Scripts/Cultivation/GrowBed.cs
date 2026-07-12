using Hollowfen.Data;
using Hollowfen.Foraging;
using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.Cultivation
{
    // A plantable cultivation bed (Almy's lesson, Act II). Empty bed + Wood Ear in the basket
    // → "Plant" prompt (same Foraging-layer trigger convention as MushroomNode). Planting
    // consumes one mushroom as spawn, then the game clock grows a cluster of scaled world
    // prefabs on the soil; at maturity their trigger colliders enable and harvest runs through
    // the normal MushroomNode inspect/forage path. When the last node is picked the bed
    // returns to empty. State lives in the GrowBeds store (slot-persisted).
    [DisallowMultipleComponent]
    public class GrowBed : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("Stable id for save state (e.g. millyard_bed_1).")]
        private string _bedId;
        [SerializeField, Tooltip("Species this bed grows (Tier-1 Wood Ear for v1).")]
        private MushroomFieldGuideData _species;
        [SerializeField, Tooltip("World prefab spawned as the growing cluster (MushroomWorld_WoodEar).")]
        private GameObject _mushroomPrefab;
        [SerializeField, Tooltip("Soil-top anchor the cluster grows out of.")]
        private Transform _spawnAnchor;
        [SerializeField, Tooltip("Game hours from planting to harvestable.")]
        private float _matureGameHours = 6f;
        [SerializeField, Tooltip("Mushrooms per planting.")]
        private int _yield = 3;
        [SerializeField, Tooltip("Cluster radius around the anchor, meters.")]
        private float _clusterRadius = 0.38f;
        [SerializeField, Tooltip("Scale multiplier at full growth vs the wild prefab — cultivated flushes grow proud.")]
        private float _matureScale = 2.2f;

        private GameObject[] _nodes;
        private bool _matureApplied;
        private float _lastGrowth = -1f;

        public string PromptVerb => "prompt.plant.verb";
        public string PromptTarget => Hollowfen.Localization.Get("growbed.name");

        public bool CanInteract(GameObject actor)
        {
            if (GrowBeds.Get(_bedId) != null) return false; // already planted
            if (!CultivationUnlocked()) return false;
            return _species != null && InventoryRuntime.GetCount(_species.Id) > 0;
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            var tm = GameTime.TimeManager.Instance;
            int day = tm != null ? tm.Day : 1;
            float hour = tm != null ? tm.Hour : 12f;

            InventoryRuntime.Remove(_species, 1);
            GrowBeds.Plant(_bedId, _species.Id, day, hour, _yield);
            SpawnNodes(_yield);
            ApplyGrowth(0f);
            Debug.Log("[GrowBed] " + _bedId + " planted with " + _species.CommonName + " (day " + day + ", " + hour.ToString("F1") + "h)");

            // The first planting is the climax of Almy's lesson.
            if (QuestManager.IsActive("almyTeach"))
                QuestManager.CompleteQuest("almyTeach");
        }

        private void Start()
        {
            // Restore from save: spawn whatever the record says is still in the ground.
            var rec = GrowBeds.Get(_bedId);
            if (rec != null && rec.Remaining > 0)
                SpawnNodes(rec.Remaining);
        }

        private void Update()
        {
            var rec = GrowBeds.Get(_bedId);
            if (rec == null || _nodes == null) return;

            float growth = GrowthFactor(rec);
            if (Mathf.Abs(growth - _lastGrowth) > 0.01f) ApplyGrowth(growth);

            if (growth >= 1f)
            {
                if (!_matureApplied) SetNodesHarvestable();

                // MushroomNode.Harvest deactivates its GameObject — poll the cluster and
                // mirror the count into the store; empty cluster resets the bed.
                int alive = 0;
                foreach (var n in _nodes)
                    if (n != null && n.activeSelf) alive++;
                if (alive != rec.Remaining)
                {
                    if (alive <= 0) ClearBed();
                    else GrowBeds.SetRemaining(_bedId, alive);
                }
            }
        }

        private static bool CultivationUnlocked()
        {
            if (QuestManager.IsCompleted("almyTeach")) return true;
            // Mid-quest: the lesson dialogue (act2_started) must come before the planting.
            return QuestManager.IsActive("almyTeach") && GameScores.HasFlag("act2_started");
        }

        private float GrowthFactor(GrowBeds.BedRecord rec)
        {
            var tm = GameTime.TimeManager.Instance;
            if (tm == null) return 1f;
            float elapsed = (tm.Day - rec.PlantedDay) * 24f + (tm.Hour - rec.PlantedHour);
            return _matureGameHours > 0f ? Mathf.Clamp01(elapsed / _matureGameHours) : 1f;
        }

        private void SpawnNodes(int count)
        {
            DespawnNodes();
            if (_mushroomPrefab == null || _spawnAnchor == null || count <= 0) return;
            _nodes = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                float ang = (360f / Mathf.Max(1, count)) * i + 23f;
                var offset = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * (count > 1 ? _clusterRadius : 0f);
                var go = Instantiate(_mushroomPrefab, _spawnAnchor.position + offset, Quaternion.Euler(0f, ang * 1.7f, 0f), _spawnAnchor);
                go.name = _mushroomPrefab.name + "_" + _bedId + "_" + i;
                var trigger = go.GetComponent<SphereCollider>();
                if (trigger != null) trigger.enabled = false; // not harvestable until mature
                _nodes[i] = go;
            }
            _matureApplied = false;
            _lastGrowth = -1f;
        }

        private void DespawnNodes()
        {
            if (_nodes == null) return;
            foreach (var n in _nodes)
                if (n != null) Destroy(n);
            _nodes = null;
        }

        private void ApplyGrowth(float growth)
        {
            _lastGrowth = growth;
            if (_nodes == null) return;
            float scale = Mathf.Lerp(0.3f, 1f, growth) * _matureScale;
            foreach (var n in _nodes)
            {
                if (n == null) continue;
                n.transform.localScale = _mushroomPrefab.transform.localScale * scale;
            }
        }

        private void SetNodesHarvestable()
        {
            _matureApplied = true;
            if (_nodes == null) return;
            foreach (var n in _nodes)
            {
                if (n == null) continue;
                var trigger = n.GetComponent<SphereCollider>();
                if (trigger != null) trigger.enabled = true;
            }
            Debug.Log("[GrowBed] " + _bedId + " is mature.");
        }

        private void ClearBed()
        {
            DespawnNodes();
            GrowBeds.Clear(_bedId);
            Debug.Log("[GrowBed] " + _bedId + " harvested clean — ready to replant.");
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.55f, 0.45f, 0.25f, 0.9f);
            var p = _spawnAnchor != null ? _spawnAnchor.position : transform.position;
            Gizmos.DrawWireCube(p + Vector3.up * 0.1f, new Vector3(1.2f, 0.2f, 1.2f));
        }
    }
}
