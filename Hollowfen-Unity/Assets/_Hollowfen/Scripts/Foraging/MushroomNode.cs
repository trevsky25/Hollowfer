using System;
using Hollowfen.Audio;
using Hollowfen.Data;
using Hollowfen.GameTime;
using UnityEngine;

namespace Hollowfen.Foraging
{
    [DisallowMultipleComponent]
    public class MushroomNode : MonoBehaviour, IInteractable
    {
        private const string FirstHarvestPrefKey = "forage.firstHarvestSeen";

        [SerializeField] private MushroomFieldGuideData _data;
        [SerializeField, Tooltip("Stable scene-instance id used by the wild-forage save state. Runtime cultivated nodes leave this empty.")]
        private string _nodeId;
        [SerializeField, Min(0), Tooltip("Optional per-node respawn override in game days. Zero uses the species profile.")]
        private int _respawnGameDaysOverride;

        private Renderer[] _renderers;
        private Collider[] _colliders;
        private bool[] _rendererDefaults;
        private bool[] _colliderDefaults;
        private bool _cultivated;
        private bool _harvested;
        private bool _deferStateRefresh;

        public static event Action<MushroomFieldGuideData> OnAnyHarvested;

        public MushroomFieldGuideData Data => _data;
        public string NodeId => _nodeId;
        public bool IsCultivated => _cultivated;
        public bool IsHarvested => _harvested;

        public string PromptVerb => "prompt.inspect.verb";
        public string PromptTarget => _data != null ? _data.CommonName : "Mushroom";

        public bool CanInteract(GameObject actor) =>
            _data != null && !_harvested && gameObject.activeInHierarchy;

        private void Awake()
        {
            CacheVisualState();
            ForageNodeStates.OnChanged += HandleNodeStateChanged;
            TimeManager.OnDayChanged += HandleDayChanged;
        }

        private void Start()
        {
            if (_cultivated) return;
            if (string.IsNullOrEmpty(_nodeId))
            {
                // Editor tooling stamps every authored node. This deterministic fallback keeps
                // hand-placed test nodes functional without silently sharing state.
                _nodeId = "wild." + (_data != null ? _data.Id : "unknown") + "." +
                          transform.position.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "." +
                          transform.position.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                Debug.LogWarning("[MushroomNode] Missing authored node id; using position fallback '" + _nodeId + "'.", this);
            }
            RefreshWildState();
        }

        private void OnDestroy()
        {
            ForageNodeStates.OnChanged -= HandleNodeStateChanged;
            TimeManager.OnDayChanged -= HandleDayChanged;
        }

        public void ConfigureCultivated(MushroomFieldGuideData species)
        {
            _cultivated = true;
            if (species != null) _data = species;
            _nodeId = null;
            _harvested = false;
            SetVisualHarvested(false);
        }

        // Grow beds keep the specimen visible while it matures, but its authored interaction
        // colliders must remain unavailable. Restore only the colliders that the prefab actually
        // shipped with enabled; never wake dormant helper/physics colliders by accident.
        public void SetCultivatedHarvestable(bool harvestable)
        {
            if (!_cultivated) return;
            if (_colliders == null) CacheVisualState();
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = harvestable && _colliderDefaults[i];
        }

        // Interact opens the InspectScreen. Its forage button begins the cinematic cutting challenge.
        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            InspectScreen.Open(this);
        }

        // Synchronous-only commit path (used as the last step of the cinematic OR if a caller
        // ever wants the legacy instant pickup behavior).
        public void Harvest()
        {
            if (_data == null || _harvested || !gameObject.activeInHierarchy) return;
            if (!MushroomRules.CanHarvest(_data)) return;
            CommitHarvestState(false);
            SetVisualHarvested(true);
        }

        // Starts the two-handed cutting challenge. InspectScreen has already hidden without
        // releasing input/time state; ForageCuttingChallenge owns that lifecycle until success/cancel.
        public void BeginHarvest()
        {
            if (_data == null || _harvested || !gameObject.activeInHierarchy) return;
            if (!MushroomRules.CanHarvest(_data)) return;
            ForageCuttingChallenge.Play(this);
        }

        // Same as Harvest() above but without the SetActive(false) — the coroutine owns deactivation
        // so it can do post-commit cleanup on the still-active host.
        private void CommitHarvestState(bool deferVisual)
        {
            if (_data == null || _harvested) return;
            _harvested = true;
            _deferStateRefresh = deferVisual;
            Debug.Log($"[Forage] {_data.CommonName} +1");
            GameplaySfx.ForageCollected();
            OnAnyHarvested?.Invoke(_data);
            MushroomDiscovery.MarkDiscovered(_data.Id);
            InventoryRuntime.Add(_data, 1);
            if (!_cultivated)
            {
                int day = TimeManager.Instance != null ? TimeManager.Instance.Day : 1;
                ForageNodeStates.MarkHarvested(_nodeId, day);
            }
            if (!PlayerPrefs.HasKey(FirstHarvestPrefKey))
            {
                PlayerPrefs.SetInt(FirstHarvestPrefKey, 1);
                PlayerPrefs.Save();
                GameEvents.TriggerAchievement("ACH_FORAGE_FIRST");
            }
        }

        // Challenge-only split: commit while the specimen is still on camera, then deactivate
        // after the collection beat and camera restore. Internal prevents bypassing the challenge.
        internal void CommitHarvestFromChallenge()
        {
            if (_data == null || _harvested || !gameObject.activeInHierarchy) return;
            CommitHarvestState(true);
        }

        internal void DeactivateAfterChallenge()
        {
            _deferStateRefresh = false;
            SetVisualHarvested(true);
        }

        private void HandleNodeStateChanged(string changedId)
        {
            if (_cultivated || _deferStateRefresh) return;
            if (!string.IsNullOrEmpty(changedId) && changedId != _nodeId) return;
            RefreshWildState();
        }

        private void HandleDayChanged(int day)
        {
            if (!_cultivated) RefreshWildState();
        }

        private void RefreshWildState()
        {
            if (_cultivated || string.IsNullOrEmpty(_nodeId)) return;
            int day = TimeManager.Instance != null ? TimeManager.Instance.Day : 1;
            int respawnDays = _respawnGameDaysOverride > 0
                ? _respawnGameDaysOverride
                : (_data != null ? _data.WildRespawnDays : 2);
            bool available = ForageNodeStates.IsAvailable(_nodeId, day, respawnDays);
            _harvested = !available;
            SetVisualHarvested(!available);
        }

        private void CacheVisualState()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _rendererDefaults = new bool[_renderers.Length];
            _colliderDefaults = new bool[_colliders.Length];
            for (int i = 0; i < _renderers.Length; i++) _rendererDefaults[i] = _renderers[i].enabled;
            for (int i = 0; i < _colliders.Length; i++) _colliderDefaults[i] = _colliders[i].enabled;
        }

        private void SetVisualHarvested(bool harvested)
        {
            if (_renderers == null || _colliders == null) CacheVisualState();
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = !harvested && _rendererDefaults[i];
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = !harvested && _colliderDefaults[i];
        }
    }
}
