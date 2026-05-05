using UnityEngine;

namespace Hollowfen.Data
{
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
        [SerializeField] private GameObject _worldPrefab;
        [SerializeField] private string _displayNameId;
        [SerializeField] private string _descriptionId;

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
        public GameObject WorldPrefab => _worldPrefab;
        public string DisplayNameId => _displayNameId;
        public string DescriptionId => _descriptionId;
    }
}
