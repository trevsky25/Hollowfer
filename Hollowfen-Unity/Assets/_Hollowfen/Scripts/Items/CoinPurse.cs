using System;
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

        private static int _totalCopper;
        private static bool _hydrated;

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
            OnChanged = null;
        }

        public static int TotalCopper { get { EnsureHydrated(); return _totalCopper; } }
        public static int SilverPart => TotalCopper / CopperPerSilver;
        public static int CopperPart => TotalCopper % CopperPerSilver;

        public static string Format(int totalCopper) =>
            (totalCopper / CopperPerSilver) + "s " + (totalCopper % CopperPerSilver) + "c";

        public static void Add(int copper)
        {
            if (copper <= 0) return;
            EnsureHydrated();
            _totalCopper += copper;
            Persist();
            OnChanged?.Invoke(_totalCopper);
        }

        public static bool TrySpend(int copper)
        {
            if (copper <= 0) return true;
            EnsureHydrated();
            if (_totalCopper < copper) return false;
            _totalCopper -= copper;
            Persist();
            OnChanged?.Invoke(_totalCopper);
            return true;
        }

        // Used by save load to reset in-memory state to a snapshot.
        public static void HydrateFrom(int totalCopper)
        {
            _totalCopper = Mathf.Max(0, totalCopper);
            _hydrated = true;
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try
            {
                var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
                if (meta != null) _totalCopper = Mathf.Max(0, meta.CoinsCopper);
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
                SaveManager.AutoSaveCoins(_totalCopper);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CoinPurse] Autosave failed: " + e.Message);
            }
        }
    }
}
