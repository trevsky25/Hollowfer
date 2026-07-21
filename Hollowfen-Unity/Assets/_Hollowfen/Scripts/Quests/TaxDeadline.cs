using Hollowfen.GameTime;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Voss's pressure (Act II, firstTax). Once the twelve-silver demand has been heard, every
    // sundown that passes without payment costs Village Hope — the village watches the Wenmar
    // bench and counts the days. The quest never hard-fails in v1; the deadline rolls to the
    // next sundown (recurring consequence states arrive with the full tax system).
    public class TaxDeadline : MonoBehaviour
    {
        private const string GraceSpentFlag = "wenmar_tax_grace_spent";

        [SerializeField, Tooltip("Village Hope lost at each missed sundown.")]
        private int _hopePenalty = 2;

        private void OnEnable()
        {
            TimeManager.OnSundown += HandleSundown;
        }

        private void OnDisable()
        {
            TimeManager.OnSundown -= HandleSundown;
        }

        private void HandleSundown()
        {
            if (!QuestManager.IsActive("firstTax")) return;
            if (!GameScores.HasFlag("voss_first_visit_seen")) return;
            if (GameScores.HasFlag("wenmar_tax_paid")) return;

            // The demand can be delivered only minutes before the first sunset. Give Wren one
            // complete overnight/dawn forage cycle before hope starts falling; otherwise the
            // twelve-silver target is technically possible in the world but not honestly paced.
            if (!GameScores.HasFlag(GraceSpentFlag))
            {
                GameScores.SetFlag(GraceSpentFlag);
                Debug.Log("[TaxDeadline] First sundown after Voss's demand — grace night recorded; " +
                          "the village penalty begins tomorrow.");
                return;
            }

            GameScores.AddVillageHope(-_hopePenalty);
            Debug.Log("[TaxDeadline] Sundown without payment — Village Hope -" + _hopePenalty + ". Voss waits.");
        }
    }
}
