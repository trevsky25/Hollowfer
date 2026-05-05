using System.Collections.Generic;
using UnityEngine;

namespace Hollowfen.Data
{
    [CreateAssetMenu(fileName = "StoryCardDatabase", menuName = "Hollowfen/Story/Story Card Database")]
    public class StoryCardDatabase : ScriptableObject
    {
        [SerializeField] private StoryCardData[] _cards;

        public IReadOnlyList<StoryCardData> Cards => _cards;
        public int Count => _cards != null ? _cards.Length : 0;
    }
}
