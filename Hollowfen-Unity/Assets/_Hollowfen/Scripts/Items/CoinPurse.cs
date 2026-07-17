using System;
using System.Collections.Generic;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.Items
{
    // Wren's money. Stored internally as total copper (1 silver = 12 copper, duodecimal like
    // the village's grain measures); display splits into silver/copper. Mirrors KeyItems:
    // hydrates from the autosave slot on first access, persists immediately on change.
    public static class CoinPurse
    {
        public const int CopperPerSilver = 12;
        public const int MaxLedgerEntries = 8;

        public readonly struct Transaction
        {
            public Transaction(int amountCopper, int balanceAfterCopper, string reasonId)
            {
                AmountCopper = amountCopper;
                BalanceAfterCopper = balanceAfterCopper;
                ReasonId = string.IsNullOrWhiteSpace(reasonId)
                    ? (amountCopper >= 0 ? "purse.transaction.earned" : "purse.transaction.spent")
                    : reasonId;
            }

            public int AmountCopper { get; }
            public int BalanceAfterCopper { get; }
            public string ReasonId { get; }
        }

        private static int _totalCopper;
        private static bool _hydrated;
        private static readonly List<Transaction> _transactions = new List<Transaction>(MaxLedgerEntries);

        // Fired after any balance change. (newTotalCopper)
        public static event Action<int> OnChanged;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            _totalCopper = 0;
            _hydrated = false;
            _transactions.Clear();
            OnChanged = null;
        }

        public static int TotalCopper { get { EnsureHydrated(); return _totalCopper; } }
        public static int SilverPart => TotalCopper / CopperPerSilver;
        public static int CopperPart => TotalCopper % CopperPerSilver;
        public static IReadOnlyList<Transaction> RecentTransactions
        {
            get { EnsureHydrated(); return _transactions; }
        }

        public static string Format(int totalCopper) =>
            (totalCopper / CopperPerSilver) + "s " + (totalCopper % CopperPerSilver) + "c";

        public static void Add(int copper, string reasonId = "purse.transaction.earned")
        {
            if (copper <= 0) return;
            EnsureHydrated();
            _totalCopper += copper;
            Record(copper, reasonId);
            Persist();
            OnChanged?.Invoke(_totalCopper);
        }

        public static bool TrySpend(int copper, string reasonId = "purse.transaction.spent")
        {
            if (copper <= 0) return true;
            EnsureHydrated();
            if (_totalCopper < copper) return false;
            _totalCopper -= copper;
            Record(-copper, reasonId);
            Persist();
            OnChanged?.Invoke(_totalCopper);
            return true;
        }

        // Used by save load to reset in-memory state to a snapshot.
        public static void HydrateFrom(int totalCopper, CoinLedgerSnapshot ledger = null)
        {
            _totalCopper = Mathf.Max(0, totalCopper);
            _transactions.Clear();
            if (ledger != null)
            {
                int count = Mathf.Min(ledger.AmountsCopper?.Length ?? 0,
                    Mathf.Min(ledger.BalancesAfterCopper?.Length ?? 0,
                        ledger.ReasonIds?.Length ?? 0));
                for (int i = 0; i < count && _transactions.Count < MaxLedgerEntries; i++)
                {
                    int amount = ledger.AmountsCopper[i];
                    if (amount == 0) continue;
                    _transactions.Add(new Transaction(amount,
                        Mathf.Max(0, ledger.BalancesAfterCopper[i]), ledger.ReasonIds[i]));
                }
            }
            _hydrated = true;
            OnChanged?.Invoke(_totalCopper);
        }

        public static CoinLedgerSnapshot ToLedgerSnapshot()
        {
            EnsureHydrated();
            int count = Mathf.Min(MaxLedgerEntries, _transactions.Count);
            var snapshot = new CoinLedgerSnapshot
            {
                AmountsCopper = new int[count],
                BalancesAfterCopper = new int[count],
                ReasonIds = new string[count]
            };
            for (int i = 0; i < count; i++)
            {
                snapshot.AmountsCopper[i] = _transactions[i].AmountCopper;
                snapshot.BalancesAfterCopper[i] = _transactions[i].BalanceAfterCopper;
                snapshot.ReasonIds[i] = _transactions[i].ReasonId;
            }
            return snapshot;
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try
            {
                var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
                if (meta != null)
                {
                    HydrateFrom(meta.CoinsCopper, meta.CoinLedger);
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CoinPurse] Hydration failed: " + e.Message);
            }
        }

        private static void Persist()
        {
            try
            {
                SaveManager.AutoSaveCoins(_totalCopper, ToLedgerSnapshot());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CoinPurse] Autosave failed: " + e.Message);
            }
        }

        private static void Record(int amountCopper, string reasonId)
        {
            _transactions.Insert(0, new Transaction(amountCopper, _totalCopper, reasonId));
            if (_transactions.Count > MaxLedgerEntries)
                _transactions.RemoveRange(MaxLedgerEntries, _transactions.Count - MaxLedgerEntries);
        }
    }
}
