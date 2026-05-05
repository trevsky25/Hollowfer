using System;
using Hollowfen.Data;
using UnityEngine;

namespace Hollowfen.Foraging
{
    [DisallowMultipleComponent]
    public class MushroomNode : MonoBehaviour, IInteractable
    {
        private const string FirstHarvestPrefKey = "forage.firstHarvestSeen";

        [SerializeField] private MushroomFieldGuideData _data;
        [SerializeField] private float _respawnSeconds = 0f;

        public static event Action<MushroomFieldGuideData> OnAnyHarvested;

        public MushroomFieldGuideData Data => _data;

        public string PromptVerb => "prompt.inspect.verb";
        public string PromptTarget => _data != null ? _data.CommonName : "Mushroom";

        public bool CanInteract(GameObject actor) => _data != null && gameObject.activeInHierarchy;

        // Interact == open the InspectScreen. Harvest happens from inside the screen.
        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            InspectScreen.Open(this);
        }

        // Called by InspectScreen when the user picks Forage.
        public void Harvest()
        {
            if (_data == null || !gameObject.activeInHierarchy) return;

            Debug.Log($"[Forage] {_data.CommonName} +1");

            OnAnyHarvested?.Invoke(_data);
            MushroomDiscovery.MarkDiscovered(_data.Id);
            InventoryRuntime.Add(_data, 1);

            if (!PlayerPrefs.HasKey(FirstHarvestPrefKey))
            {
                PlayerPrefs.SetInt(FirstHarvestPrefKey, 1);
                PlayerPrefs.Save();
                GameEvents.TriggerAchievement("ACH_FORAGE_FIRST");
            }

            gameObject.SetActive(false);
        }
    }
}
