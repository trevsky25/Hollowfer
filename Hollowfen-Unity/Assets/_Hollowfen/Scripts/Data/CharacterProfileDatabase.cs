using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowfen.Data
{
    [CreateAssetMenu(
        fileName = "PeopleOfHollowfenDatabase",
        menuName = "Hollowfen/Characters/Character Profile Database")]
    public class CharacterProfileDatabase : ScriptableObject
    {
        public const string ResourcesFallbackName = "PeopleOfHollowfenDatabase";

        [SerializeField] private CharacterProfileData[] _profiles;

        public IReadOnlyList<CharacterProfileData> Profiles =>
            _profiles ?? Array.Empty<CharacterProfileData>();

        public int Count => _profiles != null ? _profiles.Length : 0;

        public CharacterProfileData FindById(string id)
        {
            if (string.IsNullOrEmpty(id) || _profiles == null) return null;
            for (int i = 0; i < _profiles.Length; i++)
            {
                CharacterProfileData profile = _profiles[i];
                if (profile != null && string.Equals(profile.Id, id, StringComparison.Ordinal))
                    return profile;
            }
            return null;
        }

        public static CharacterProfileDatabase LoadFallback()
        {
            return Resources.Load<CharacterProfileDatabase>(ResourcesFallbackName);
        }
    }
}
