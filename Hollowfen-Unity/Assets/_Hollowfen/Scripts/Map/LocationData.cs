using UnityEngine;

namespace Hollowfen.Map
{
    [CreateAssetMenu(fileName = "LocationData_New", menuName = "Hollowfen/Map/Location Data")]
    public class LocationData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameId;
        [SerializeField] private string _shortDescriptionId;
        [SerializeField] private Sprite _mapIcon;
        [SerializeField] private bool _discoveredByDefault;
        [SerializeField] private string _regionId;

        public string Id => _id;
        public string DisplayNameId => _displayNameId;
        public string ShortDescriptionId => _shortDescriptionId;
        public Sprite MapIcon => _mapIcon;
        public bool DiscoveredByDefault => _discoveredByDefault;
        public string RegionId => _regionId;
    }
}
