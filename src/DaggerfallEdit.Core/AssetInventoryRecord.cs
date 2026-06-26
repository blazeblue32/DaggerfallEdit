namespace DaggerfallEdit.Core;

public sealed record AssetInventoryRecord(
    string ModName,
    string DfmodPath,
    string AssetName,
    string ClassName,
    long PathId,
    string SemanticKind,
    string IdentityKey,
    long? ByteLength,
    string? ContainerPath,
    bool HasStableName,
    string DuplicatePolicy,
    string? DfuFileName,
    string? DfuTitle,
    bool? DfuEnabled,
    int? DfuLoadPriority,
    string? Mo2ModName,
    bool? Mo2Enabled,
    int? Mo2ListIndex
);
