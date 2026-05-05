using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowfen.Foraging
{
    public static class MushroomDiscovery
    {
        private const string PrefKey = "forage.discoveredIds";
        private const char Separator = ';';

        private static HashSet<string> _set;

        public static event Action<string> OnDiscovered;

        private static void EnsureLoaded()
        {
            if (_set != null) return;
            _set = new HashSet<string>();
            string raw = PlayerPrefs.GetString(PrefKey, string.Empty);
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (var id in raw.Split(Separator))
                    if (!string.IsNullOrEmpty(id)) _set.Add(id);
            }
        }

        public static bool IsDiscovered(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureLoaded();
            return _set.Contains(id);
        }

        // Returns true if newly discovered.
        public static bool MarkDiscovered(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureLoaded();
            if (!_set.Add(id)) return false;
            PlayerPrefs.SetString(PrefKey, string.Join(Separator.ToString(), _set));
            PlayerPrefs.Save();
            OnDiscovered?.Invoke(id);
            return true;
        }

        public static IEnumerable<string> All
        {
            get { EnsureLoaded(); return _set; }
        }
    }
}
