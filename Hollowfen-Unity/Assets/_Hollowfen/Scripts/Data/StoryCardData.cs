using UnityEngine;

namespace Hollowfen.Data
{
    [CreateAssetMenu(fileName = "StoryCard_New", menuName = "Hollowfen/Story/Story Card")]
    public class StoryCardData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _act;
        [SerializeField] private string _scene;
        [SerializeField] private string _title;
        [SerializeField] private string _subtitle;
        [SerializeField, TextArea(3, 10)] private string _body;
        [SerializeField, TextArea(2, 6)] private string _wrenNote;
        [SerializeField] private string[] _beats;
        [SerializeField] private Sprite _image;
        [SerializeField] private int _unlockAt;
        [SerializeField] private string _questId;
        [SerializeField] private string _displayNameId;
        [SerializeField] private string _descriptionId;

        public string Id => _id;
        public string Act => _act;
        public string Scene => _scene;
        public string Title => _title;
        public string Subtitle => _subtitle;
        public string Body => _body;
        public string WrenNote => _wrenNote;
        public string[] Beats => _beats;
        public Sprite Image => _image;
        public int UnlockAt => _unlockAt;
        public string QuestId => _questId;
        public string DisplayNameId => _displayNameId;
        public string DescriptionId => _descriptionId;
    }
}
