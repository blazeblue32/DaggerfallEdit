namespace DaggerfallEdit.Core;

public sealed record LooseFileInventoryRecord(
    string ModName,
    string ModFolderPath,
    string FilePath,
    string RelativePath,
    string Extension,
    long ByteLength,
    string FileKind,
    string IdentityKey,
    string DuplicatePolicy,
    bool IsOverwrite,
    string? Mo2ModName,
    bool? Mo2Enabled,
    int? Mo2ListIndex,
    int? Mo2LineNumber
);
