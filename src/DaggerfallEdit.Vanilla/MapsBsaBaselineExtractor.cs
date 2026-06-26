using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using DaggerfallEdit.Core;

namespace DaggerfallEdit.Vanilla;

public static class MapsBsaBaselineExtractor
{
    private const int CollisionMapIdMask = 0x000fffff;
    private const int MapNameLength = 32;
    private const int MapTableEntryLength = 17;
    private const int MapPItemOffsetLength = 4;
    private const int LocationDoorLength = 6;
    private const int LocationRecordElementHeaderLength = 112;
    private const int BuildingListHeaderLength = 7;
    private const int BuildingDataLength = 26;
    private const int ExteriorDataMapIdOffset = 32;

    private static readonly Regex MapNamesRecordRegex = new(
        @"^MAPNAMES\.(\d{3})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] RegionNames =
    {
        "Alik'r Desert", "Dragontail Mountains", "Glenpoint Foothills", "Daggerfall Bluffs",
        "Yeorth Burrowland", "Dwynnen", "Ravennian Forest", "Devilrock",
        "Malekna Forest", "Isle of Balfiera", "Bantha", "Dak'fron",
        "Islands in the Western Iliac Bay", "Tamarilyn Point", "Lainlyn Cliffs", "Bjoulsae River",
        "Wrothgarian Mountains", "Daggerfall", "Glenpoint", "Betony", "Sentinel", "Anticlere", "Lainlyn", "Wayrest",
        "Gen Tem High Rock village", "Gen Rai Hammerfell village", "Orsinium Area", "Skeffington Wood",
        "Hammerfell bay coast", "Hammerfell sea coast", "High Rock bay coast", "High Rock sea coast",
        "Northmoor", "Menevia", "Alcaire", "Koegria", "Bhoriane", "Kambria", "Phrygias", "Urvaius",
        "Ykalon", "Daenia", "Shalgora", "Abibon-Gora", "Kairou", "Pothago", "Myrkwasa", "Ayasofya",
        "Tigonus", "Kozanset", "Satakalaam", "Totambu", "Mournoth", "Ephesus", "Santaki", "Antiphyllos",
        "Bergama", "Gavaudon", "Tulune", "Glenumbra Moors", "Ilessan Hills", "Cybiades"
    };

    public static List<MapIdRecord> ExtractVanillaMapIds(string arena2Path)
    {
        string mapsPath = Path.Combine(arena2Path, "MAPS.BSA");

        if (!File.Exists(mapsPath))
            throw new FileNotFoundException("Could not find MAPS.BSA.", mapsPath);

        BsaNameRecordReader bsa = BsaNameRecordReader.Load(mapsPath);
        var records = new List<MapIdRecord>();

        foreach (int regionIndex in GetRegionIndices(bsa))
        {
            string mapNamesRecord = $"MAPNAMES.{regionIndex:000}";
            string mapTableRecord = $"MAPTABLE.{regionIndex:000}";
            string mapPItemRecord = $"MAPPITEM.{regionIndex:000}";

            if (!bsa.TryGetRecord(mapNamesRecord, out ReadOnlyMemory<byte> mapNamesBytes) ||
                !bsa.TryGetRecord(mapTableRecord, out ReadOnlyMemory<byte> mapTableBytes) ||
                !bsa.TryGetRecord(mapPItemRecord, out ReadOnlyMemory<byte> mapPItemBytes))
            {
                continue;
            }

            // Daggerfall Unity treats regions with any empty MAPS.BSA region record as unloaded.
            // Some vanilla regions have zero-length records, so skip them instead of throwing
            // while building the baseline.
            if (mapNamesBytes.Length < 4 || mapTableBytes.Length == 0 || mapPItemBytes.Length == 0)
                continue;

            List<string> mapNames = ReadMapNames(mapNamesBytes.Span, mapNamesRecord);
            List<MapTableEntry> mapTable = ReadMapTable(mapTableBytes.Span, mapNames.Count, mapTableRecord);
            List<MapPItemIdentity?> mapPItemIdentities = ReadMapPItemIdentities(mapPItemBytes.Span, mapNames.Count, mapPItemRecord);
            string regionName = GetRegionName(regionIndex);

            for (int locationIndex = 0; locationIndex < mapTable.Count; locationIndex++)
            {
                MapTableEntry entry = mapTable[locationIndex];
                string locationName = locationIndex < mapNames.Count
                    ? mapNames[locationIndex]
                    : string.Empty;

                int x = entry.CollisionMapId % 1000;
                int y = (entry.CollisionMapId - x) / 1000;

                string assetName = $"vanilla-location-{regionIndex}-{locationIndex}";
                string? normalizedLocationName = string.IsNullOrWhiteSpace(locationName) ? null : locationName;

                records.Add(new MapIdRecord(
                    MapId: entry.CollisionMapId,
                    X: x,
                    Y: y,
                    Longitude: entry.Longitude,
                    Latitude: entry.Latitude,
                    ModName: "[Vanilla MAPS.BSA]",
                    DfmodPath: mapsPath,
                    AssetName: assetName,
                    JsonPath: $"{regionName}[{regionIndex}].{mapTableRecord}[{locationIndex}] rawMapId={entry.RawMapId}",
                    RecordName: normalizedLocationName,
                    AssetKind: "Baseline",
                    Source: "Vanilla MAPS.BSA MapTable collision key"));

                if (locationIndex < mapPItemIdentities.Count && mapPItemIdentities[locationIndex] is MapPItemIdentity identity)
                {
                    int exteriorMapId = identity.ExteriorMapId;
                    int exteriorX = exteriorMapId % 1000;
                    int exteriorY = (exteriorMapId - exteriorX) / 1000;

                    records.Add(new MapIdRecord(
                        MapId: exteriorMapId,
                        X: exteriorX,
                        Y: exteriorY,
                        Longitude: entry.Longitude,
                        Latitude: entry.Latitude,
                        ModName: "[Vanilla MAPS.BSA]",
                        DfmodPath: mapsPath,
                        AssetName: assetName,
                        JsonPath: $"{regionName}[{regionIndex}].{mapPItemRecord}[{locationIndex}].Exterior.ExteriorData.MapId",
                        RecordName: normalizedLocationName,
                        AssetKind: "Baseline",
                        Source: "Vanilla MAPS.BSA ExteriorData MapId"));

                    if (identity.LocationId > 0)
                    {
                        records.Add(new MapIdRecord(
                            MapId: identity.LocationId,
                            X: null,
                            Y: null,
                            Longitude: entry.Longitude,
                            Latitude: entry.Latitude,
                            ModName: "[Vanilla MAPS.BSA]",
                            DfmodPath: mapsPath,
                            AssetName: assetName,
                            JsonPath: $"{regionName}[{regionIndex}].{mapPItemRecord}[{locationIndex}].Exterior.RecordElement.Header.LocationId",
                            RecordName: normalizedLocationName,
                            AssetKind: "Baseline",
                            Source: "Vanilla MAPS.BSA LocationId"));
                    }
                }
            }
        }

        if (records.Count == 0)
            throw new InvalidDataException($"No MAPNAMES/MAPTABLE location records were found in: {mapsPath}");

        return records;
    }

    private static IEnumerable<int> GetRegionIndices(BsaNameRecordReader bsa)
    {
        return bsa.Records
            .Select(record => MapNamesRecordRegex.Match(record.Name))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value))
            .Distinct()
            .OrderBy(regionIndex => regionIndex);
    }

    private static List<string> ReadMapNames(ReadOnlySpan<byte> bytes, string recordName)
    {
        if (bytes.Length < 4)
            throw new InvalidDataException($"{recordName} is too small to contain a location count.");

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[..4]);
        long expectedLength = 4L + count * MapNameLength;

        if (expectedLength > bytes.Length)
            throw new InvalidDataException($"{recordName} declares {count} location name(s), but the record is truncated.");

        int countInt = checked((int)count);
        var names = new List<string>(countInt);

        for (int i = 0; i < countInt; i++)
        {
            int offset = 4 + i * MapNameLength;
            names.Add(ReadFixedAscii(bytes.Slice(offset, MapNameLength)));
        }

        return names;
    }

    private static List<MapTableEntry> ReadMapTable(ReadOnlySpan<byte> bytes, int locationCount, string recordName)
    {
        int expectedLength = checked(locationCount * MapTableEntryLength);

        if (bytes.Length < expectedLength)
            throw new InvalidDataException($"{recordName} is too small for {locationCount} map table entry/entries.");

        var entries = new List<MapTableEntry>(locationCount);

        for (int i = 0; i < locationCount; i++)
        {
            int offset = i * MapTableEntryLength;

            int rawMapId = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
            uint firstBitfield = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            int secondBitfield = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8, 4));

            // Mirrors Daggerfall Unity's MapsFile.ReadMapTable() field naming.
            // UESP documents these packed fields as LatitudeType and LongitudeType,
            // but DFU assigns the first packed field to Longitude and the second to Latitude.
            int longitude = (int)(firstBitfield & 0x01ffffffu) >> 8;
            int latitude = (secondBitfield & 0x00ffffff) >> 8;
            int collisionMapId = rawMapId & CollisionMapIdMask;

            entries.Add(new MapTableEntry(
                RawMapId: rawMapId,
                CollisionMapId: collisionMapId,
                Longitude: longitude,
                Latitude: latitude));
        }

        return entries;
    }


    private static List<MapPItemIdentity?> ReadMapPItemIdentities(ReadOnlySpan<byte> bytes, int locationCount, string recordName)
    {
        int offsetTableLength = checked(locationCount * MapPItemOffsetLength);

        if (bytes.Length < offsetTableLength)
            throw new InvalidDataException($"{recordName} is too small for {locationCount} exterior offset(s).");

        var identities = new List<MapPItemIdentity?>(locationCount);

        for (int i = 0; i < locationCount; i++)
        {
            int relativeOffset = BinaryPrimitives.ReadInt32LittleEndian(
                bytes.Slice(i * MapPItemOffsetLength, MapPItemOffsetLength));

            long locationOffset = (long)offsetTableLength + relativeOffset;

            if (locationOffset < offsetTableLength || locationOffset + 4 > bytes.Length)
            {
                identities.Add(null);
                continue;
            }

            MapPItemIdentity? identity = TryReadMapPItemIdentity(bytes, checked((int)locationOffset));
            identities.Add(identity);
        }

        return identities;
    }

    private static MapPItemIdentity? TryReadMapPItemIdentity(ReadOnlySpan<byte> bytes, int locationOffset)
    {
        if (locationOffset + 4 > bytes.Length)
            return null;

        uint doorCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(locationOffset, 4));

        long locationIdOffset =
            (long)locationOffset +
            4 +
            doorCount * LocationDoorLength +
            33;

        if (locationIdOffset + 2 > bytes.Length)
            return null;

        int locationId = BinaryPrimitives.ReadUInt16LittleEndian(
            bytes.Slice(checked((int)locationIdOffset), 2));

        long afterLocationRecordElement =
            (long)locationOffset +
            4 +
            doorCount * LocationDoorLength +
            LocationRecordElementHeaderLength;

        if (afterLocationRecordElement + BuildingListHeaderLength > bytes.Length)
            return null;

        ushort buildingCount = BinaryPrimitives.ReadUInt16LittleEndian(
            bytes.Slice(checked((int)afterLocationRecordElement), 2));

        long exteriorDataOffset =
            afterLocationRecordElement +
            BuildingListHeaderLength +
            buildingCount * BuildingDataLength;

        long mapIdOffset = exteriorDataOffset + ExteriorDataMapIdOffset;

        if (mapIdOffset + 4 > bytes.Length)
            return null;

        int exteriorMapId = BinaryPrimitives.ReadInt32LittleEndian(
            bytes.Slice(checked((int)mapIdOffset), 4));

        return new MapPItemIdentity(
            ExteriorMapId: exteriorMapId & CollisionMapIdMask,
            LocationId: locationId);
    }

    private static string GetRegionName(int regionIndex)
    {
        if (regionIndex >= 0 && regionIndex < RegionNames.Length)
            return RegionNames[regionIndex];

        return $"Region {regionIndex}";
    }

    private static string ReadFixedAscii(ReadOnlySpan<byte> bytes)
    {
        int length = bytes.IndexOf((byte)0);

        if (length < 0)
            length = bytes.Length;

        return Encoding.ASCII.GetString(bytes[..length]).TrimEnd();
    }

    private sealed record MapTableEntry(
        int RawMapId,
        int CollisionMapId,
        int Longitude,
        int Latitude
    );

    private sealed record MapPItemIdentity(
        int ExteriorMapId,
        int LocationId
    );
}
