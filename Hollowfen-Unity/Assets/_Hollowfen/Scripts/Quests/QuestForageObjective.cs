using System.Collections.Generic;
using Hollowfen.Data;
using Hollowfen.Foraging;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Completes a quest once the player has harvested every species in the required list
    // while that quest is active (story.md firstForage: "three safe basics and one strange
    // gold-stemmed find"). Species harvested BEFORE the quest started still count if they're
    // already in the inventory — Wren has the basket either way.
    public class QuestForageObjective : MonoBehaviour
    {
        [SerializeField] private QuestData _quest;
        [SerializeField] private MushroomFieldGuideData[] _requiredSpecies;

        private readonly HashSet<string> _harvested = new HashSet<string>();

        private void OnEnable()
        {
            MushroomNode.OnAnyHarvested += HandleHarvested;
        }

        private void OnDisable()
        {
            MushroomNode.OnAnyHarvested -= HandleHarvested;
        }

        private void HandleHarvested(MushroomFieldGuideData data)
        {
            if (data == null || _quest == null) return;
            if (!QuestManager.IsActive(_quest.Id)) return;
            _harvested.Add(data.Id);
            if (AllGathered()) QuestManager.CompleteQuest(_quest.Id);
        }

        private bool AllGathered()
        {
            if (_requiredSpecies == null || _requiredSpecies.Length == 0) return _harvested.Count > 0;
            foreach (var s in _requiredSpecies)
            {
                if (s == null) continue;
                if (!_harvested.Contains(s.Id) && InventoryRuntime.GetCount(s) == 0) return false;
            }
            return true;
        }
    }
}
