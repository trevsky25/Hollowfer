using System.Collections.Generic;
using UnityEngine;

namespace Hollowfen.Data
{
    [CreateAssetMenu(fileName = "MushroomFieldGuideDatabase", menuName = "Hollowfen/Mushrooms/Field Guide Database")]
    public class MushroomFieldGuideDatabase : ScriptableObject
    {
        [SerializeField] private MushroomFieldGuideData[] _entries;

        public IReadOnlyList<MushroomFieldGuideData> Entries => _entries;
        public int Count => _entries != null ? _entries.Length : 0;
    }
}
