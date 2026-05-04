using System;

namespace Hollowfen.Save
{
    [Serializable]
    public class SaveSlotMeta
    {
        public int SlotNumber;
        public long TimestampUnix;
        public string CurrentQuest;
        public int CurrentAct;
        public float TotalPlayTimeSeconds;
    }
}
