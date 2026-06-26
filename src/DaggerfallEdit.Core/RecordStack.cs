namespace DaggerfallEdit.Core;

public sealed record RecordIdentity(
    string Domain,
    string Kind,
    string Key,
    string DisplayName
);

public sealed record RecordProvider(
    string ProviderKind,
    string SourceName,
    string SourcePath,
    string? RelativePath,
    string? ContainerPath,
    string? ModName,
    bool IsActive,
    bool IsOverwrite,
    int WinnerSortOrder,
    string OrderLabel
);

public sealed record RecordFieldValue(
    string Path,
    string DisplayName,
    string? Value,
    string? NormalizedValue = null
);

public sealed record RecordInstance(
    RecordIdentity Identity,
    RecordProvider Provider,
    string InstancePath,
    string DisplayName,
    IReadOnlyList<RecordFieldValue> Fields,
    IReadOnlyDictionary<string, string> Metadata
);

public sealed record RecordStack(
    RecordIdentity Identity,
    IReadOnlyList<RecordInstance> Records,
    RecordInstance? WinningRecord
)
{
    public int RecordCount => Records.Count;

    public int ActiveRecordCount => Records.Count(r => r.Provider.IsActive);

    public int SourceCount => Records
        .Select(r => r.Provider.SourcePath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int ActiveSourceCount => Records
        .Where(r => r.Provider.IsActive)
        .Select(r => r.Provider.SourcePath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int ModCount => Records
        .Select(r => r.Provider.ModName ?? r.Provider.SourceName)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int ActiveModCount => Records
        .Where(r => r.Provider.IsActive)
        .Select(r => r.Provider.ModName ?? r.Provider.SourceName)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}

public static class RecordStackBuilder
{
    public static List<RecordStack> BuildStacks(IEnumerable<RecordInstance> instances)
    {
        return instances
            .GroupBy(i => i.Identity.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => BuildStack(g.ToList()))
            .OrderBy(s => s.Identity.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Identity.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<RecordStack> FromLooseContentRecords(IEnumerable<LooseContentRecord> records)
    {
        return BuildStacks(records.Select(FromLooseContentRecord));
    }

    public static RecordInstance FromLooseContentRecord(LooseContentRecord record)
    {
        var identity = new RecordIdentity(
            Domain: "LooseContent",
            Kind: record.ContentKind,
            Key: record.ContentIdentityKey,
            DisplayName: record.DisplayName);

        bool isActive = record.IsOverwrite || record.Mo2Enabled == true;
        int winnerSortOrder = record.IsOverwrite ? int.MinValue : record.Mo2ListIndex ?? int.MaxValue;

        var provider = new RecordProvider(
            ProviderKind: "LooseContent",
            SourceName: record.SourceName,
            SourcePath: record.FilePath,
            RelativePath: record.RelativePath,
            ContainerPath: null,
            ModName: record.Mo2ModName,
            IsActive: isActive,
            IsOverwrite: record.IsOverwrite,
            WinnerSortOrder: winnerSortOrder,
            OrderLabel: BuildLooseContentOrderLabel(record));

        var fields = new List<RecordFieldValue>();
        AddOptionalField(fields, "target", "Target", record.TargetName);
        AddOptionalField(fields, "replacement", "Replacement", record.ReplacementName);
        AddOptionalField(fields, "portrait", "Portrait", record.PortraitName);
        AddOptionalField(fields, "ruleContext", "Rule Context", record.RuleContext);
        AddOptionalField(fields, "jsonPath", "JSON Path", record.JsonPath);
        AddOptionalField(fields, "relativePath", "Relative Path", record.RelativePath);
        AddOptionalField(fields, "sourceFileKind", "Source File Kind", record.SourceFileKind);
        AddOptionalField(fields, "duplicatePolicy", "Duplicate Policy", record.DuplicatePolicy);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceName"] = record.SourceName,
            ["SourceFolderPath"] = record.SourceFolderPath,
            ["FilePath"] = record.FilePath,
            ["RelativePath"] = record.RelativePath,
            ["SourceFileKind"] = record.SourceFileKind,
            ["SourceExtension"] = record.SourceExtension,
            ["ContentKind"] = record.ContentKind,
            ["ContentIdentityKey"] = record.ContentIdentityKey,
            ["JsonPath"] = record.JsonPath,
            ["DisplayName"] = record.DisplayName,
            ["SourceByteLength"] = record.SourceByteLength.ToString(),
            ["DuplicatePolicy"] = record.DuplicatePolicy,
            ["IsOverwrite"] = record.IsOverwrite.ToString()
        };

        AddOptionalMetadata(metadata, "Target", record.TargetName);
        AddOptionalMetadata(metadata, "Replacement", record.ReplacementName);
        AddOptionalMetadata(metadata, "Portrait", record.PortraitName);
        AddOptionalMetadata(metadata, "RuleContext", record.RuleContext);
        AddOptionalMetadata(metadata, "Mo2ModName", record.Mo2ModName);
        AddOptionalMetadata(metadata, "Mo2Enabled", record.Mo2Enabled?.ToString());
        AddOptionalMetadata(metadata, "Mo2ListIndex", record.Mo2ListIndex?.ToString());
        AddOptionalMetadata(metadata, "Mo2LineNumber", record.Mo2LineNumber?.ToString());

        return new RecordInstance(
            Identity: identity,
            Provider: provider,
            InstancePath: record.JsonPath,
            DisplayName: record.DisplayName,
            Fields: fields,
            Metadata: metadata);
    }

    private static RecordStack BuildStack(List<RecordInstance> records)
    {
        var orderedRecords = records
            .OrderBy(r => r.Provider.WinnerSortOrder)
            .ThenBy(r => r.Provider.SourceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Provider.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.InstancePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RecordInstance? winner = orderedRecords.FirstOrDefault(r => r.Provider.IsActive);
        RecordIdentity identity = orderedRecords[0].Identity;
        return new RecordStack(identity, orderedRecords, winner);
    }

    private static void AddOptionalField(
        List<RecordFieldValue> fields,
        string path,
        string displayName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        fields.Add(new RecordFieldValue(path, displayName, value.Trim(), NormalizeFieldValue(value)));
    }

    private static void AddOptionalMetadata(
        Dictionary<string, string> metadata,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            metadata[key] = value.Trim();
    }

    private static string NormalizeFieldValue(string value)
    {
        return value.Trim().Replace("\\", "/").ToLowerInvariant();
    }

    private static string BuildLooseContentOrderLabel(LooseContentRecord record)
    {
        if (record.IsOverwrite)
            return "Overwrite";

        if (record.Mo2ListIndex.HasValue)
        {
            string enabled = record.Mo2Enabled == true ? "enabled" : "disabled";
            return $"MO2 {record.Mo2ListIndex.Value} ({enabled}) {record.Mo2ModName ?? record.SourceName}";
        }

        return "unmatched";
    }
}
