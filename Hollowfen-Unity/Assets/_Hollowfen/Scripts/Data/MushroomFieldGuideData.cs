using UnityEngine;

namespace Hollowfen.Data
{
    public enum ForageTier
    {
        BasketCommon = 1,
        Knifework = 2,
        Deepwood = 3,
        FinalLesson = 4,
    }

    public enum MushroomBuyer
    {
        None = 0,
        Marra = 1,
        Theo = 2,
    }

    public enum Edibility
    {
        Edible,
        Deadly,
        Psychoactive,
        Medicinal,
        Unknown
    }

    [CreateAssetMenu(fileName = "Mushroom_New", menuName = "Hollowfen/Mushrooms/Field Guide Entry")]
    public class MushroomFieldGuideData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _commonName;
        [SerializeField] private string _latinName;
        [SerializeField] private Edibility _edibility = Edibility.Edible;
        [SerializeField] private string _edibilityLabel;
        [SerializeField, TextArea(3, 10)] private string _description;
        [SerializeField] private string[] _idFeatures;
        [SerializeField] private string _habitat;
        [SerializeField] private string _season;
        [SerializeField, TextArea(2, 8)] private string _lookalikes;
        [SerializeField, TextArea(2, 8)] private string _notes;
        [SerializeField] private Sprite _photo;
        [SerializeField] private string _photoCredit;
        [SerializeField, Tooltip("Hand-drawn two-page spread shown as the primary journal entry.")]
        private Sprite _journalPage;
        [SerializeField] private GameObject _worldPrefab;
        [SerializeField, Tooltip("Optional dedicated model shown in the Field Guide. Assign only when this species has authored 3D art.")]
        private GameObject _journalPreviewPrefab;
        [SerializeField, Range(0.15f, 1.10f), Tooltip("Preview-only ambient exposure. Tuned per source albedo; never changes the gameplay material.")]
        private float _journalExposure = 0.42f;
        [SerializeField] private string _displayNameId;
        [SerializeField] private string _descriptionId;

        [Header("Gameplay profile")]
        [SerializeField, Tooltip("Story progression tier used by harvesting, trade, and authoring checks.")]
        private ForageTier _forageTier = ForageTier.BasketCommon;
        [SerializeField, Tooltip("Optional GameScores flag required before this species can be harvested.")]
        private string _requiredForageFlagId;
        [SerializeField, Min(1), Tooltip("Full dawns after harvest before a wild node returns.")]
        private int _wildRespawnDays = 2;
        [SerializeField, Min(0), Tooltip("Copper paid per specimen by Marra. Zero means she refuses it.")]
        private int _marraValueCopper;
        [SerializeField, Min(0), Tooltip("Copper paid per specimen by Theo. Zero means he refuses it.")]
        private int _theoValueCopper;
        [SerializeField, Tooltip("Whether this species can be planted in a cultivation bed.")]
        private bool _cultivable;
        [SerializeField, Min(0.25f), Tooltip("Game hours from planting to a mature cultivated flush.")]
        private float _cultivationHours = 6f;
        [SerializeField, Min(1), Tooltip("Specimens produced by one planted mushroom.")]
        private int _cultivationYield = 3;
        [SerializeField, Tooltip("Optional flag required in addition to Almy's lesson before cultivation.")]
        private string _cultivationUnlockFlagId;

        public string Id => _id;
        public string CommonName => _commonName;
        public string LatinName => _latinName;
        public Edibility Edibility => _edibility;
        public string EdibilityLabel => _edibilityLabel;
        public string Description => _description;
        public string[] IdFeatures => _idFeatures;
        public string Habitat => _habitat;
        public string Season => _season;
        public string Lookalikes => _lookalikes;
        public string Notes => _notes;
        public Sprite Photo => _photo;
        public string PhotoCredit => _photoCredit;
        public Sprite JournalPage => _journalPage;
        public GameObject WorldPrefab => _worldPrefab;
        public GameObject JournalPreviewPrefab => _journalPreviewPrefab;
        public float JournalExposure => _journalExposure;
        public string DisplayNameId => _displayNameId;
        public string DescriptionId => _descriptionId;
        public ForageTier Tier => _forageTier;
        public string RequiredForageFlagId => _requiredForageFlagId;
        public int WildRespawnDays => Mathf.Max(1, _wildRespawnDays);
        public bool Cultivable => _cultivable;
        public float CultivationHours => Mathf.Max(0.25f, _cultivationHours);
        public int CultivationYield => Mathf.Max(1, _cultivationYield);
        public string CultivationUnlockFlagId => _cultivationUnlockFlagId;

        public int ValueFor(MushroomBuyer buyer)
        {
            switch (buyer)
            {
                case MushroomBuyer.Marra: return Mathf.Max(0, _marraValueCopper);
                case MushroomBuyer.Theo: return Mathf.Max(0, _theoValueCopper);
                default: return 0;
            }
        }
    }
}
