namespace DaggerfallEdit.Core;

public sealed record LooseContentRecord(
    string SourceName,
    string SourceFolderPath,
    string FilePath,
    string RelativePath,
    string SourceFileKind,
    string SourceExtension,
    string ContentKind,
    string ContentIdentityKey,
    string JsonPath,
    string DisplayName,
    string? TargetName,
    string? ReplacementName,
    string? PortraitName,
    string? RuleContext,
    bool LooseFileActive,
    long SourceByteLength,
    string DuplicatePolicy,
    bool IsOverwrite,
    string? Mo2ModName,
    bool? Mo2Enabled,
    int? Mo2ListIndex,
    int? Mo2LineNumber
);
