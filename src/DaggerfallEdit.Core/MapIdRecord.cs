namespace DaggerfallEdit.Core;

public sealed record MapIdRecord(
    int MapId,
    int? X,
    int? Y,
    int? Longitude,
    int? Latitude,
    string ModName,
    string DfmodPath,
    string AssetName,
    string JsonPath,
    string? RecordName,
    string AssetKind,
    string Source
);