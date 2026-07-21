using UnityEngine;

namespace Hollowfen.Requests
{
    [CreateAssetMenu(fileName = "VillageRequestDatabase", menuName = "Hollowfen/Requests/Request Database")]
    public sealed class VillageRequestDatabase : ScriptableObject
    {
        [SerializeField] private VillageRequestData[] _requests;
        public VillageRequestData[] Requests => _requests;
    }
}
