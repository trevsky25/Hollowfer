using System;
using UnityEngine;

namespace Hollowfen.Restoration
{
    [CreateAssetMenu(fileName = "RestorationProjectDatabase", menuName = "Hollowfen/Restoration/Project Database")]
    public sealed class RestorationProjectDatabase : ScriptableObject
    {
        [SerializeField] private RestorationProjectData[] _projects = Array.Empty<RestorationProjectData>();

        public RestorationProjectData[] Projects => _projects;
    }
}
