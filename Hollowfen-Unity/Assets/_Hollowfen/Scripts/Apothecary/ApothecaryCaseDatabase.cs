using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowfen.Apothecary
{
    [CreateAssetMenu(fileName = "ApothecaryCaseDatabase",
        menuName = "Hollowfen/Apothecary/Patient Case Database")]
    public sealed class ApothecaryCaseDatabase : ScriptableObject
    {
        public const string ResourcesPath = "ApothecaryCaseDatabase";
        [SerializeField] private ApothecaryCaseData[] _cases = Array.Empty<ApothecaryCaseData>();

        public IReadOnlyList<ApothecaryCaseData> Cases => _cases ?? Array.Empty<ApothecaryCaseData>();

        public ApothecaryCaseData Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || _cases == null) return null;
            foreach (var entry in _cases)
                if (entry != null && string.Equals(entry.Id, id, StringComparison.Ordinal)) return entry;
            return null;
        }

        public static ApothecaryCaseDatabase Load() => Resources.Load<ApothecaryCaseDatabase>(ResourcesPath);
    }
}
