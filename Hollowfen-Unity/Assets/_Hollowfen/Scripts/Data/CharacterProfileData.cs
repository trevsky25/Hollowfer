using System;
using UnityEngine;

namespace Hollowfen.Data
{
    [CreateAssetMenu(fileName = "Character_New", menuName = "Hollowfen/Characters/Character Profile")]
    public class CharacterProfileData : ScriptableObject
    {
        [Serializable]
        public struct KitItem
        {
            public string Name;
            [TextArea(1, 3)] public string OneLine;
        }

        [SerializeField] private string _id;
        [SerializeField] private string _characterName;
        [SerializeField] private string _role;
        [SerializeField] private string _age;
        [SerializeField] private string _home;
        [SerializeField] private string _work;
        [SerializeField] private string _keepsake;
        [SerializeField, TextArea(1, 3)] private string _tagline;
        [SerializeField, TextArea(3, 10)] private string _leadParagraph;
        [SerializeField, TextArea(3, 10)] private string _backgroundParagraph;
        [SerializeField, TextArea(3, 10)] private string _perspectiveParagraph;
        [SerializeField] private KitItem[] _kitItems;
        [SerializeField] private Sprite _heroPortrait;
        [SerializeField, TextArea(2, 6)] private string _pullquote;
        [SerializeField] private string _displayNameId;
        [SerializeField] private string _descriptionId;

        [Header("Interactive journal study")]
        [SerializeField] private GameObject _journalModelPrefab;
        [SerializeField] private AnimationClip _journalIdleClip;
        [SerializeField, Range(0f, 0.4f)] private float _journalExposure = 0.15f;

        [Header("Field study plates (batch-61 Wren dossier)")]
        [SerializeField] private Sprite _studySheet;
        [SerializeField] private Sprite _figureFront;
        [SerializeField] private Sprite _figureBack;
        [SerializeField] private Sprite _figureThreeQuarter;
        [SerializeField] private Sprite _knifePlate;

        public string Id => _id;
        public string CharacterName => _characterName;
        public string Role => _role;
        public string Age => _age;
        public string Home => _home;
        public string Work => _work;
        public string Keepsake => _keepsake;
        public string Tagline => _tagline;
        public string LeadParagraph => _leadParagraph;
        public string BackgroundParagraph => _backgroundParagraph;
        public string PerspectiveParagraph => _perspectiveParagraph;
        public KitItem[] KitItems => _kitItems;
        public Sprite HeroPortrait => _heroPortrait;
        public string Pullquote => _pullquote;
        public string DisplayNameId => _displayNameId;
        public string DescriptionId => _descriptionId;
        public GameObject JournalModelPrefab => _journalModelPrefab;
        public AnimationClip JournalIdleClip => _journalIdleClip;
        public float JournalExposure => _journalExposure;
        public Sprite StudySheet => _studySheet;
        public Sprite FigureFront => _figureFront;
        public Sprite FigureBack => _figureBack;
        public Sprite FigureThreeQuarter => _figureThreeQuarter;
        public Sprite KnifePlate => _knifePlate;
    }
}
