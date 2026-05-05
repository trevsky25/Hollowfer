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

        // Inventory snapshot — parallel arrays of mushroom ids + counts. JsonUtility-friendly.
        public InventorySnapshot Inventory;
    }

    [Serializable]
    public class InventorySnapshot
    {
        public string[] Ids;
        public int[] Counts;
    }
}
