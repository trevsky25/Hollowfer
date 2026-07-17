using Hollowfen.Audio;
using Hollowfen.Data;
using Hollowfen.Foraging;
using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.Cultivation
{
    // A plantable cultivation bed (Almy's lesson, Act II). Empty bed + a known cultivable
    // mushroom in the basket → recipe picker. Planting
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
        private MushroomFieldGuideData _activeSpecies;
        private GameObject _activePrefab;
        private bool _matureApplied;
        private float _lastGrowth = -1f;

        public string PromptVerb => "prompt.plant.verb";
        public string PromptTarget => Hollowfen.Localization.Get("growbed.name");

        public bool CanInteract(GameObject actor)
        {
            if (GrowBeds.Get(_bedId) != null) return false; // already planted
            return CultivationScreen.HasPlantableSpecies();
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            CultivationScreen.Ensure().Open(this);
        }

        public void Plant(MushroomFieldGuideData species)
        {
            if (species == null || GrowBeds.Get(_bedId) != null) return;
            if (!MushroomRules.CanCultivate(species) || InventoryRuntime.GetCount(species.Id) <= 0) return;
            var tm = GameTime.TimeManager.Instance;
            int day = tm != null ? tm.Day : 1;
            float hour = tm != null ? tm.Hour : 12f;

            _activeSpecies = species;
            _activePrefab = species.WorldPrefab != null ? species.WorldPrefab : _mushroomPrefab;
            int yieldCount = species.CultivationYield > 0 ? species.CultivationYield : _yield;
            InventoryRuntime.Remove(species, 1);
            GrowBeds.Plant(_bedId, species.Id, day, hour, yieldCount);
            SpawnNodes(yieldCount);
            ApplyGrowth(0f);
            GameplaySfx.Plant();
            Debug.Log("[GrowBed] " + _bedId + " planted with " + species.CommonName + " (day " + day + ", " + hour.ToString("F1") + "h)");

            // The first planting is the climax of Almy's lesson.
            if (QuestManager.IsActive("almyTeach"))
                QuestManager.CompleteQuest("almyTeach");
        }

        private void Start()
        {
            // Restore from save: spawn whatever the record says is still in the ground.
            var rec = GrowBeds.Get(_bedId);
            if (rec != null && rec.Remaining > 0)
            {
                _activeSpecies = CultivationScreen.ResolveSpecies(rec.SpeciesId) ?? _species;
                _activePrefab = _activeSpecies != null && _activeSpecies.WorldPrefab != null
                    ? _activeSpecies.WorldPrefab
                    : _mushroomPrefab;
                SpawnNodes(rec.Remaining);
                if (GrowthFactor(rec) >= 1f)
                {
                    ApplyGrowth(1f);
                    SetNodesHarvestable(false);
                }
            }
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

                // Cultivated MushroomNodes remain active as lifecycle hosts but expose their
                // harvested state; mirror the live count into the store for partial-flush saves.
                int alive = 0;
                foreach (var n in _nodes)
                {
                    if (n == null) continue;
                    var node = n.GetComponent<MushroomNode>();
                    if (node == null || !node.IsHarvested) alive++;
                }
                if (alive != rec.Remaining)
                {
                    if (alive <= 0) ClearBed();
                    else GrowBeds.SetRemaining(_bedId, alive);
                }
            }
        }

        private float GrowthFactor(GrowBeds.BedRecord rec)
        {
            var tm = GameTime.TimeManager.Instance;
            if (tm == null) return 1f;
            float elapsed = (tm.Day - rec.PlantedDay) * 24f + (tm.Hour - rec.PlantedHour);
            float hours = _activeSpecies != null ? _activeSpecies.CultivationHours : _matureGameHours;
            return hours > 0f ? Mathf.Clamp01(elapsed / hours) : 1f;
        }

        private void SpawnNodes(int count)
        {
            DespawnNodes();
            var prefab = _activePrefab != null ? _activePrefab : _mushroomPrefab;
            if (prefab == null || _spawnAnchor == null || count <= 0) return;
            _nodes = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                float ang = (360f / Mathf.Max(1, count)) * i + 23f;
                var offset = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * (count > 1 ? _clusterRadius : 0f);
                var go = Instantiate(prefab, _spawnAnchor.position + offset, Quaternion.Euler(0f, ang * 1.7f, 0f), _spawnAnchor);
                go.name = prefab.name + "_" + _bedId + "_" + i;
                var node = go.GetComponent<MushroomNode>();
                if (node != null)
                {
                    node.ConfigureCultivated(_activeSpecies);
                    node.SetCultivatedHarvestable(false);
                }
                else
                {
                    foreach (var trigger in go.GetComponentsInChildren<Collider>(true))
                        if (trigger.isTrigger) trigger.enabled = false;
                }
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
                var prefab = _activePrefab != null ? _activePrefab : _mushroomPrefab;
                if (prefab != null) n.transform.localScale = prefab.transform.localScale * scale;
            }
        }

        private void SetNodesHarvestable(bool playFeedback = true)
        {
            _matureApplied = true;
            if (_nodes == null) return;
            if (playFeedback) GameplaySfx.CropMature();
            foreach (var n in _nodes)
            {
                if (n == null) continue;
                var node = n.GetComponent<MushroomNode>();
                if (node != null)
                {
                    node.SetCultivatedHarvestable(true);
                }
                else
                {
                    foreach (var trigger in n.GetComponentsInChildren<Collider>(true))
                        if (trigger.isTrigger) trigger.enabled = true;
                }
            }
            Debug.Log("[GrowBed] " + _bedId + " is mature.");
        }

        private void ClearBed()
        {
            DespawnNodes();
            GrowBeds.Clear(_bedId);
            _activeSpecies = null;
            _activePrefab = null;
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
