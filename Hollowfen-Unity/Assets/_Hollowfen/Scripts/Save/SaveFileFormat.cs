using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Hollowfen.Save
{
    public enum SaveInspectionStatus
    {
        Empty,
        Ready,
        Recovered,
        Corrupt,
        IncompatibleNewerVersion,
    }

    public sealed class SaveSlotInspection
    {
        internal SaveSlotInspection(SaveInspectionStatus status, SaveSlotMeta meta,
            string sourcePath, long revision, string detail)
        {
            Status = status;
            Meta = meta;
            SourcePath = sourcePath;
            Revision = revision;
            Detail = detail;
        }

        public SaveInspectionStatus Status { get; }
        public SaveSlotMeta Meta { get; }
        public string SourcePath { get; }
        public long Revision { get; }
        public string Detail { get; }
        public bool CanLoad => Status == SaveInspectionStatus.Ready || Status == SaveInspectionStatus.Recovered;
        public bool HasArtifacts => Status != SaveInspectionStatus.Empty;
    }

    /// <summary>
    /// Versioned, checksummed wrapper around SaveSlotMeta. Historical flat JSON is accepted as
    /// schema zero, but only when it carries recognizable save anchors; `{}` is never a save.
    /// </summary>
    internal static class SaveFileFormat
    {
        internal const string FormatId = "hollowfen.save";
        internal const int CurrentSchemaVersion = 1;
        internal const long MaxFileBytes = 16L * 1024L * 1024L;
        private const int MaxRows = 4096;
        private const float MaxPlayTimeSeconds = 315576000f; // ten years

        [Serializable]
        private sealed class SaveFileEnvelope
        {
            public string FormatId;
            public int SchemaVersion;
            public long Revision;
            public long WrittenUnixMilliseconds;
            public string PayloadJson;
            public string IntegritySha256;
        }

        internal sealed class Candidate
        {
            internal bool Exists;
            internal bool IsValid;
            internal bool IsIncompatible;
            internal bool IsLegacy;
            internal SaveSlotMeta Meta;
            internal long Revision;
            internal long WrittenUnixMilliseconds;
            internal string Path;
            internal int Priority;
            internal string Error;
        }

        internal static Candidate DecodeFile(string path, int slot, int priority)
        {
            var candidate = new Candidate { Path = path, Priority = priority, Exists = File.Exists(path) };
            if (!candidate.Exists) return candidate;

            try
            {
                var info = new FileInfo(path);
                if (info.Length <= 1 || info.Length > MaxFileBytes)
                    throw new InvalidDataException($"file size {info.Length} is outside the supported range");

                string raw = File.ReadAllText(path);
                var envelope = JsonUtility.FromJson<SaveFileEnvelope>(raw);
                if (envelope != null && !string.IsNullOrEmpty(envelope.FormatId))
                {
                    DecodeEnvelope(candidate, envelope, slot, info.LastWriteTimeUtc);
                    return candidate;
                }

                var legacy = JsonUtility.FromJson<SaveSlotMeta>(raw);
                if (legacy == null || !LooksLikeLegacySave(raw, legacy))
                    throw new InvalidDataException("JSON does not contain a recognizable Hollowfen save payload");

                candidate.IsLegacy = true;
                candidate.Revision = 0;
                candidate.WrittenUnixMilliseconds = SafeFileMilliseconds(info.LastWriteTimeUtc);
                candidate.Meta = legacy;
                Normalize(candidate.Meta, slot, info.LastWriteTimeUtc);
                candidate.IsValid = true;
            }
            catch (Exception exception)
            {
                candidate.Error = exception.Message;
            }
            return candidate;
        }

        internal static string Encode(SaveSlotMeta meta, long revision, long writtenUnixMilliseconds)
        {
            string payload = JsonUtility.ToJson(meta, false);
            var envelope = new SaveFileEnvelope
            {
                FormatId = FormatId,
                SchemaVersion = CurrentSchemaVersion,
                Revision = Math.Max(1L, revision),
                WrittenUnixMilliseconds = writtenUnixMilliseconds,
                PayloadJson = payload,
            };
            envelope.IntegritySha256 = ComputeIntegrity(envelope.SchemaVersion, envelope.Revision,
                envelope.WrittenUnixMilliseconds, envelope.PayloadJson);
            return JsonUtility.ToJson(envelope, true);
        }

        // Atomic multi-system commits compare the staged payload with the decoded recovery
        // winner. Normalize clones on both sides so harmless null-to-empty migrations do not
        // turn a durable write into an ambiguous failure after it has reached disk.
        internal static string CanonicalPayloadJson(SaveSlotMeta meta, int slot)
        {
            if (meta == null) return string.Empty;
            var clone = JsonUtility.FromJson<SaveSlotMeta>(JsonUtility.ToJson(meta, false));
            if (clone == null) return string.Empty;
            Normalize(clone, slot, DateTime.UtcNow);
            return JsonUtility.ToJson(clone, false);
        }

        private static void DecodeEnvelope(Candidate candidate, SaveFileEnvelope envelope, int slot,
            DateTime fileWriteTimeUtc)
        {
            if (!string.Equals(envelope.FormatId, FormatId, StringComparison.Ordinal))
                throw new InvalidDataException("unrecognized save format identifier");
            if (envelope.SchemaVersion <= 0)
                throw new InvalidDataException("invalid schema version");
            if (envelope.Revision <= 0)
                throw new InvalidDataException("invalid save revision");
            if (string.IsNullOrEmpty(envelope.PayloadJson))
                throw new InvalidDataException("save payload is empty");

            string expected = ComputeIntegrity(envelope.SchemaVersion, envelope.Revision,
                envelope.WrittenUnixMilliseconds, envelope.PayloadJson);
            if (!FixedTimeEquals(expected, envelope.IntegritySha256))
                throw new InvalidDataException("save integrity checksum does not match its payload");

            candidate.Revision = envelope.Revision;
            candidate.WrittenUnixMilliseconds = envelope.WrittenUnixMilliseconds;
            if (envelope.SchemaVersion > CurrentSchemaVersion)
            {
                candidate.IsValid = true;
                candidate.IsIncompatible = true;
                candidate.Error = $"save schema {envelope.SchemaVersion} is newer than supported schema {CurrentSchemaVersion}";
                return;
            }
            if (envelope.SchemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException($"unsupported historical envelope schema {envelope.SchemaVersion}");

            var meta = JsonUtility.FromJson<SaveSlotMeta>(envelope.PayloadJson);
            if (meta == null)
                throw new InvalidDataException("save payload produced no journal data");
            candidate.Meta = meta;
            Normalize(candidate.Meta, slot, fileWriteTimeUtc);
            candidate.IsValid = true;
        }

        private static bool LooksLikeLegacySave(string raw, SaveSlotMeta meta)
        {
            int anchors = 0;
            if (raw.IndexOf("\"SlotNumber\"", StringComparison.Ordinal) >= 0 && meta.SlotNumber >= 0 && meta.SlotNumber < SaveManager.TotalSlots)
                anchors++;
            if (raw.IndexOf("\"TimestampUnix\"", StringComparison.Ordinal) >= 0 && meta.TimestampUnix > 0)
                anchors++;
            if (raw.IndexOf("\"CurrentQuest\"", StringComparison.Ordinal) >= 0 && !string.IsNullOrWhiteSpace(meta.CurrentQuest))
                anchors++;
            if (raw.IndexOf("\"CurrentAct\"", StringComparison.Ordinal) >= 0 && meta.CurrentAct >= 1 && meta.CurrentAct <= 4)
                anchors++;
            return anchors >= 2;
        }

        private static void Normalize(SaveSlotMeta meta, int slot, DateTime fileWriteTimeUtc)
        {
            meta.SlotNumber = Mathf.Clamp(slot, 0, SaveManager.TotalSlots - 1);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long earliest = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            long latest = now + 366L * 24L * 60L * 60L;
            if (meta.TimestampUnix < earliest || meta.TimestampUnix > latest)
                meta.TimestampUnix = SafeFileSeconds(fileWriteTimeUtc, now);

            meta.CurrentAct = Mathf.Clamp(meta.CurrentAct <= 0 ? 1 : meta.CurrentAct, 1, 4);
            meta.TotalPlayTimeSeconds = IsFinite(meta.TotalPlayTimeSeconds)
                ? Mathf.Clamp(meta.TotalPlayTimeSeconds, 0f, MaxPlayTimeSeconds)
                : 0f;
            meta.CoinsCopper = Mathf.Max(0, meta.CoinsCopper);
            meta.VillageHope = Mathf.Max(0, meta.VillageHope);
            meta.Knowledge = Mathf.Max(0, meta.Knowledge);
            meta.GameDay = Mathf.Max(0, meta.GameDay);
            meta.GameHour = IsFinite(meta.GameHour) ? Mathf.Repeat(meta.GameHour, 24f) : 0f;

            if (meta.HasPlayerTransform &&
                (!IsFinite(meta.PlayerPosX) || !IsFinite(meta.PlayerPosY) ||
                 !IsFinite(meta.PlayerPosZ) || !IsFinite(meta.PlayerYaw)))
                meta.HasPlayerTransform = false;

            NormalizeInventory(meta.Inventory);
            NormalizeRelationships(meta);
            NormalizeLedger(meta.CoinLedger);
            NormalizeGrowBeds(meta.GrowBeds);
            NormalizeForage(meta.ForageNodes);
            NormalizeRequests(meta.VillageRequests);
            NormalizeRestorations(meta.RestorationProjects);
            NormalizeApothecaryCases(meta.ApothecaryCases);
            NormalizeVillagerRelationships(meta.VillagerRelationships);

            meta.KeyItemIds = Trim(meta.KeyItemIds, MaxRows);
            meta.CompletedQuestIds = Trim(meta.CompletedQuestIds, MaxRows);
            meta.UnlockedStoryCardIds = Trim(meta.UnlockedStoryCardIds, MaxRows);
            meta.DiscoveredMushroomIds = Trim(meta.DiscoveredMushroomIds, MaxRows);
            meta.DiscoveredLocationIds = Trim(meta.DiscoveredLocationIds, MaxRows);
            meta.GameFlagIds = Trim(meta.GameFlagIds, MaxRows);
        }

        private static void NormalizeInventory(InventorySnapshot snapshot)
        {
            if (snapshot == null) return;
            int count = SharedLength(snapshot.Ids, snapshot.Counts);
            snapshot.Ids = Trim(snapshot.Ids, count);
            snapshot.Counts = Trim(snapshot.Counts, count);
            for (int i = 0; i < snapshot.Counts.Length; i++) snapshot.Counts[i] = Mathf.Max(0, snapshot.Counts[i]);
        }

        private static void NormalizeRelationships(SaveSlotMeta meta)
        {
            int count = SharedLength(meta.RelationshipNpcIds, meta.RelationshipValues);
            meta.RelationshipNpcIds = Trim(meta.RelationshipNpcIds, count);
            meta.RelationshipValues = Trim(meta.RelationshipValues, count);
        }

        private static void NormalizeLedger(CoinLedgerSnapshot snapshot)
        {
            if (snapshot == null) return;
            int count = SharedLength(snapshot.AmountsCopper, snapshot.BalancesAfterCopper, snapshot.ReasonIds);
            snapshot.AmountsCopper = Trim(snapshot.AmountsCopper, count);
            snapshot.BalancesAfterCopper = Trim(snapshot.BalancesAfterCopper, count);
            snapshot.ReasonIds = Trim(snapshot.ReasonIds, count);
        }

        private static void NormalizeGrowBeds(GrowBedSnapshot snapshot)
        {
            if (snapshot == null) return;
            int count = SharedLength(snapshot.Ids, snapshot.SpeciesIds, snapshot.PlantedDays,
                snapshot.PlantedHours, snapshot.Remaining);
            snapshot.Ids = Trim(snapshot.Ids, count);
            snapshot.SpeciesIds = Trim(snapshot.SpeciesIds, count);
            snapshot.PlantedDays = Trim(snapshot.PlantedDays, count);
            snapshot.PlantedHours = Trim(snapshot.PlantedHours, count);
            snapshot.Remaining = Trim(snapshot.Remaining, count);
            for (int i = 0; i < count; i++)
            {
                snapshot.PlantedDays[i] = Mathf.Max(0, snapshot.PlantedDays[i]);
                snapshot.PlantedHours[i] = IsFinite(snapshot.PlantedHours[i]) ? Mathf.Repeat(snapshot.PlantedHours[i], 24f) : 0f;
                snapshot.Remaining[i] = Mathf.Max(0, snapshot.Remaining[i]);
            }
        }

        private static void NormalizeForage(ForageNodeSnapshot snapshot)
        {
            if (snapshot == null) return;
            int count = SharedLength(snapshot.Ids, snapshot.HarvestedDays);
            snapshot.Ids = Trim(snapshot.Ids, count);
            snapshot.HarvestedDays = Trim(snapshot.HarvestedDays, count);
            for (int i = 0; i < count; i++) snapshot.HarvestedDays[i] = Mathf.Max(0, snapshot.HarvestedDays[i]);
        }

        private static void NormalizeRequests(VillageRequestSnapshot snapshot)
        {
            if (snapshot == null) return;
            snapshot.CompletedOneShotIds = Trim(snapshot.CompletedOneShotIds, MaxRows);
            int count = SharedLength(snapshot.DailyNpcIds, snapshot.DailyRequestIds, snapshot.DailyDays);
            snapshot.DailyNpcIds = Trim(snapshot.DailyNpcIds, count);
            snapshot.DailyRequestIds = Trim(snapshot.DailyRequestIds, count);
            snapshot.DailyDays = Trim(snapshot.DailyDays, count);
            for (int i = 0; i < count; i++) snapshot.DailyDays[i] = Mathf.Max(0, snapshot.DailyDays[i]);
            snapshot.TrackedDay = Mathf.Max(0, snapshot.TrackedDay);
        }

        private static void NormalizeRestorations(RestorationSnapshot snapshot)
        {
            if (snapshot == null) return;
            int count = SharedLength(snapshot.ProjectIds, snapshot.Stages,
                snapshot.StartedDays, snapshot.ChangedDays);
            snapshot.ProjectIds = Trim(snapshot.ProjectIds, count);
            snapshot.Stages = Trim(snapshot.Stages, count);
            snapshot.StartedDays = Trim(snapshot.StartedDays, count);
            snapshot.ChangedDays = Trim(snapshot.ChangedDays, count);
            for (int i = 0; i < count; i++)
            {
                snapshot.Stages[i] = Mathf.Clamp(snapshot.Stages[i], 0, 5);
                snapshot.StartedDays[i] = Mathf.Max(0, snapshot.StartedDays[i]);
                snapshot.ChangedDays[i] = Mathf.Max(0, snapshot.ChangedDays[i]);
            }
        }

        private static void NormalizeVillagerRelationships(VillagerRelationshipSnapshot snapshot)
        {
            if (snapshot == null) return;
            int memoryCount = SharedLength(snapshot.MemoryNpcIds, snapshot.MemoryIds, snapshot.MemoryDays);
            snapshot.MemoryNpcIds = Trim(snapshot.MemoryNpcIds, memoryCount);
            snapshot.MemoryIds = Trim(snapshot.MemoryIds, memoryCount);
            snapshot.MemoryDays = Trim(snapshot.MemoryDays, memoryCount);
            for (int i = 0; i < memoryCount; i++) snapshot.MemoryDays[i] = Mathf.Max(1, snapshot.MemoryDays[i]);

            int bondCount = SharedLength(snapshot.BondNpcAIds, snapshot.BondNpcBIds, snapshot.BondValues);
            snapshot.BondNpcAIds = Trim(snapshot.BondNpcAIds, bondCount);
            snapshot.BondNpcBIds = Trim(snapshot.BondNpcBIds, bondCount);
            snapshot.BondValues = Trim(snapshot.BondValues, bondCount);
            for (int i = 0; i < bondCount; i++) snapshot.BondValues[i] = Mathf.Clamp(snapshot.BondValues[i], -100, 100);

            int favorCount = SharedLength(snapshot.FavorIds, snapshot.FavorStages);
            snapshot.FavorIds = Trim(snapshot.FavorIds, favorCount);
            snapshot.FavorStages = Trim(snapshot.FavorStages, favorCount);
            for (int i = 0; i < favorCount; i++) snapshot.FavorStages[i] = Mathf.Max(0, snapshot.FavorStages[i]);
        }

        private static void NormalizeApothecaryCases(ApothecaryCaseSnapshot snapshot)
        {
            if (snapshot == null) return;
            int count = SharedLength(snapshot.Ids, snapshot.Stages, snapshot.StartedDays,
                snapshot.EvidenceMasks, snapshot.InterviewMasks, snapshot.DecisionIds,
                snapshot.FollowUpDays, snapshot.ResolvedDays);
            snapshot.Ids = Trim(snapshot.Ids, count);
            snapshot.Stages = Trim(snapshot.Stages, count);
            snapshot.StartedDays = Trim(snapshot.StartedDays, count);
            snapshot.EvidenceMasks = Trim(snapshot.EvidenceMasks, count);
            snapshot.InterviewMasks = Trim(snapshot.InterviewMasks, count);
            snapshot.DecisionIds = Trim(snapshot.DecisionIds, count);
            snapshot.FollowUpDays = Trim(snapshot.FollowUpDays, count);
            snapshot.ResolvedDays = Trim(snapshot.ResolvedDays, count);
            for (int i = 0; i < count; i++)
            {
                snapshot.Stages[i] = Mathf.Clamp(snapshot.Stages[i], 0, 3);
                snapshot.StartedDays[i] = Mathf.Max(0, snapshot.StartedDays[i]);
                snapshot.EvidenceMasks[i] = Mathf.Max(0, snapshot.EvidenceMasks[i]);
                snapshot.InterviewMasks[i] = Mathf.Max(0, snapshot.InterviewMasks[i]);
                snapshot.FollowUpDays[i] = Mathf.Max(0, snapshot.FollowUpDays[i]);
                snapshot.ResolvedDays[i] = Mathf.Max(0, snapshot.ResolvedDays[i]);
            }
        }

        private static int SharedLength(params Array[] arrays)
        {
            int count = MaxRows;
            foreach (Array array in arrays) count = Math.Min(count, array == null ? 0 : array.Length);
            return Math.Max(0, count);
        }

        private static T[] Trim<T>(T[] source, int count)
        {
            if (source == null || count <= 0) return Array.Empty<T>();
            count = Math.Min(Math.Min(count, source.Length), MaxRows);
            if (source.Length == count) return source;
            var result = new T[count];
            Array.Copy(source, result, count);
            return result;
        }

        private static string ComputeIntegrity(int version, long revision, long writtenUnixMilliseconds, string payload)
        {
            string input = version + "\n" + revision + "\n" + writtenUnixMilliseconds + "\n" + payload;
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        private static bool FixedTimeEquals(string expected, string actual)
        {
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual) || expected.Length != actual.Length)
                return false;
            int difference = 0;
            for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ actual[i];
            return difference == 0;
        }

        private static long SafeFileMilliseconds(DateTime fileWriteTimeUtc)
        {
            try { return new DateTimeOffset(fileWriteTimeUtc).ToUnixTimeMilliseconds(); }
            catch { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }
        }

        private static long SafeFileSeconds(DateTime fileWriteTimeUtc, long fallback)
        {
            try
            {
                long value = new DateTimeOffset(fileWriteTimeUtc).ToUnixTimeSeconds();
                return value > 0 ? value : fallback;
            }
            catch { return fallback; }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
