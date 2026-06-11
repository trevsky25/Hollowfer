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

        // Narrative key items in Wren's possession (e.g. "item.mill_key").
        public string[] KeyItemIds;

        // Quest progression — completed quest ids and unlocked story card ids.
        public string[] CompletedQuestIds;
        public string[] UnlockedStoryCardIds;

        // Wren's money, in total copper (12 copper = 1 silver).
        public int CoinsCopper;

        // One-shot narrative beats.
        public bool HomecomingIntroSeen;

        // Field-guide discovery (migrated out of PlayerPrefs).
        public string[] DiscoveredMushroomIds;

        // Last saved player transform in Scene_Hollowfen.
        public bool HasPlayerTransform;
        public float PlayerPosX;
        public float PlayerPosY;
        public float PlayerPosZ;
        public float PlayerYaw;

        // Score engine — the meters and flags that gate the four endings.
        public int VillageHope;
        public int Knowledge;
        public string[] RelationshipNpcIds;
        public int[] RelationshipValues;
        public string[] GameFlagIds;

        // Game clock (0 GameDay = legacy save, treated as day 1).
        public int GameDay;
        public float GameHour;
    }

    [Serializable]
    public class InventorySnapshot
    {
        public string[] Ids;
        public int[] Counts;
    }
}
