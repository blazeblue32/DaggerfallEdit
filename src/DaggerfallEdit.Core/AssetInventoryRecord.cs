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
    string DuplicatePolicy
);
