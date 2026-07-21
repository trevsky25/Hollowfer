using System;

namespace Hollowfen.Save
{
    [Serializable]
    public class SaveSlotMeta
    {
        public int SlotNumber;
        public long TimestampUnix;
        public string CurrentQuest;
        public string CurrentQuestId;
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
        public CoinLedgerSnapshot CoinLedger;

        // One-shot narrative beats.
        public bool HomecomingIntroSeen;

        // Field-guide discovery (migrated out of PlayerPrefs).
        public string[] DiscoveredMushroomIds;

        // Map locations Wren has named by visiting them.
        public string[] DiscoveredLocationIds;

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

        // Cultivation — one row per grow bed that has ever been planted.
        public GrowBedSnapshot GrowBeds;

        // Wild forage ecology — harvested scene-node ids and the day each was cut.
        // A node's species data owns its respawn cadence, so balance changes need no migration.
        public ForageNodeSnapshot ForageNodes;

        // Repeatable village work — persistent one-shots, today's claimed NPC orders, and
        // the optional request currently pinned beneath the story quest HUD.
        public VillageRequestSnapshot VillageRequests;

        // Living restoration — one monotonic row per authored village project. Quest/flag
        // rules can migrate legacy saves upward, but a project never regresses on reload.
        public RestorationSnapshot RestorationProjects;

        // Tobin's restored preparation bench — bottled/jarred results and recipes Wren has
        // successfully worked through. Null in historical saves means an empty apothecary shelf.
        public ApothecarySnapshot Apothecary;

        // Tobin's appointment ledger — investigations, revealed evidence, chosen preparations,
        // and delayed follow-ups. Null in historical saves means no cases have been opened.
        public ApothecaryCaseSnapshot ApothecaryCases;

        // Personal history — durable recollections, NPC-to-NPC bonds, and each villager's
        // optional favor chain. Null in historical saves means no personal moments recorded.
        public VillagerRelationshipSnapshot VillagerRelationships;
    }

    // Parallel arrays, JsonUtility-friendly (same recipe as InventorySnapshot).
    // Remaining = harvestable nodes left once mature; growth progress derives from
    // planted day/hour vs the game clock, so saves carry no timer state.
    [Serializable]
    public class GrowBedSnapshot
    {
        public string[] Ids;
        public string[] SpeciesIds;
        public int[] PlantedDays;
        public float[] PlantedHours;
        public int[] Remaining;
    }

    [Serializable]
    public class InventorySnapshot
    {
        public string[] Ids;
        public int[] Counts;
    }

    // Newest entries are stored first. Parallel arrays keep the ledger compatible with
    // JsonUtility and allow old saves (where this object is null) to load unchanged.
    [Serializable]
    public class CoinLedgerSnapshot
    {
        public int[] AmountsCopper;
        public int[] BalancesAfterCopper;
        public string[] ReasonIds;
    }

    [Serializable]
    public class ForageNodeSnapshot
    {
        public string[] Ids;
        public int[] HarvestedDays;
    }

    [Serializable]
    public class VillageRequestSnapshot
    {
        public string[] CompletedOneShotIds;
        public string[] DailyNpcIds;
        public string[] DailyRequestIds;
        public int[] DailyDays;
        public string TrackedRequestId;
        public int TrackedDay;
    }

    [Serializable]
    public class RestorationSnapshot
    {
        public string[] ProjectIds;
        public int[] Stages;
        public int[] StartedDays;
        public int[] ChangedDays;
    }

    [Serializable]
    public class ApothecarySnapshot
    {
        public string[] ProductIds;
        public int[] ProductCounts;
        public string[] CraftedRecipeIds;
        public bool InteriorLightsOn;
    }

    [Serializable]
    public class ApothecaryCaseSnapshot
    {
        public string[] Ids;
        public int[] Stages;
        public int[] StartedDays;
        public int[] EvidenceMasks;
        public int[] InterviewMasks;
        public string[] DecisionIds;
        public int[] FollowUpDays;
        public int[] ResolvedDays;
    }

    [Serializable]
    public class VillagerRelationshipSnapshot
    {
        public string[] MemoryNpcIds;
        public string[] MemoryIds;
        public int[] MemoryDays;
        public string[] BondNpcAIds;
        public string[] BondNpcBIds;
        public int[] BondValues;
        public string[] FavorIds;
        public int[] FavorStages;
    }
}
