using DaggerfallEdit.AssetStudio;
using DaggerfallEdit.Core;
using DaggerfallEdit.Vanilla;

if (args.Length < 2)
{
    PrintUsage();
    return 1;
}

string mode = args[0];
string root = args[1];

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"Folder does not exist: {root}");
    return 1;
}

int? filterMapId = null;
int? filterLocationId = null;
string? baselinePath = null;
string? arena2Path = null;
string minSeverity = "info";
string? outPath = null;
string cacheDir = ".dfedit-cache";
bool noCache = false;
bool rebuildCache = false;

for (int i = 2; i < args.Length; i++)
{
    if (args[i] == "--filter-mapid" &&
        i + 1 < args.Length &&
        int.TryParse(args[i + 1], out int parsed))
    {
        filterMapId = parsed;
        i++;
    }
    else if (args[i] == "--filter-locationid" &&
        i + 1 < args.Length &&
        int.TryParse(args[i + 1], out int parsedLocationId))
    {
        filterLocationId = parsedLocationId;
        i++;
    }
    else if (args[i] == "--baseline" && i + 1 < args.Length)
    {
        baselinePath = args[i + 1];
        i++;
    }
	else if (args[i] == "--min-severity" && i + 1 < args.Length)
	{
		minSeverity = args[i + 1];
		i++;
	}
	else if (args[i] == "--out" && i + 1 < args.Length)
	{
		outPath = args[i + 1];
		i++;
	}
	else if (args[i] == "--arena2" && i + 1 < args.Length)
	{
		arena2Path = args[i + 1];
		i++;
	}
	else if (args[i] == "--cache-dir" && i + 1 < args.Length)
	{
		cacheDir = args[i + 1];
		i++;
	}
	else if (args[i] == "--no-cache")
	{
		noCache = true;
	}
	else if (args[i] == "--rebuild-cache")
	{
		rebuildCache = true;
	}
}

TextWriter originalOut = Console.Out;
StreamWriter? outWriter = null;

if (!string.IsNullOrWhiteSpace(outPath))
{
    string fullOutPath = Path.GetFullPath(outPath);
    string? outDirectory = Path.GetDirectoryName(fullOutPath);

    if (!string.IsNullOrWhiteSpace(outDirectory))
        Directory.CreateDirectory(outDirectory);

    outWriter = new StreamWriter(fullOutPath, append: false);
    outWriter.AutoFlush = true;
    Console.SetOut(outWriter);
}

string cachePath = Path.Combine(cacheDir, "mapid-cache.json");
ScanCache? cache = noCache ? null : ScanCache.Load(cachePath);

List<MapIdRecord> records = mode switch
{
    "--loose" => ScanLooseFiles(root),
    "--mods" => ScanDfmods(root, cache, rebuildCache),
    _ => UnknownMode(mode)
};

Console.WriteLine();
Console.WriteLine($"Detected {records.Count} MapId/location candidate record(s).");

if (!string.IsNullOrWhiteSpace(baselinePath))
{
    List<MapIdRecord> baselineRecords = LoadBaselineMapIds(baselinePath);
    records.AddRange(baselineRecords);

    Console.WriteLine($"Loaded {baselineRecords.Count} baseline MapId record(s) from: {baselinePath}");
}

if (!string.IsNullOrWhiteSpace(arena2Path))
{
    List<MapIdRecord> vanillaRecords = LoadVanillaMapIds(arena2Path, cache, rebuildCache);
    records.AddRange(vanillaRecords);

    Console.WriteLine($"Loaded {vanillaRecords.Count} vanilla MapId record(s) from MAPS.BSA.");
}

List<MapIdRecord> allRecords = records.ToList();

if (filterMapId.HasValue)
{
    records = records
        .Where(r => r.MapId == filterMapId.Value && GetCollisionKeyKind(r) == "MapId")
        .ToList();

    Console.WriteLine($"Applied filter: MapId {filterMapId.Value}");
}

if (filterLocationId.HasValue)
{
    records = records
        .Where(r => r.MapId == filterLocationId.Value && GetCollisionKeyKind(r) == "LocationId")
        .ToList();

    Console.WriteLine($"Applied filter: LocationId {filterLocationId.Value}");
}

Console.WriteLine();

cache?.Save();

PrintConflicts(records, filterMapId.HasValue || filterLocationId.HasValue, minSeverity, allRecords);

if (outWriter != null)
{
    Console.Out.Flush();
    Console.SetOut(originalOut);
    outWriter.Dispose();

    Console.WriteLine($"Report written to: {Path.GetFullPath(outPath!)}");
}

return 0;

static List<MapIdRecord> ScanLooseFiles(string root)
{
    var files = Directory
        .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
        .Where(path =>
            path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .ToList();

    Console.WriteLine($"Scanning loose files under: {root}");
    Console.WriteLine($"Found {files.Count} text/json file(s).");

    var records = new List<MapIdRecord>();

    foreach (string file in files)
    {
        try
        {
            string text = File.ReadAllText(file);

            var asset = new TextAssetInfo(
                ModName: GuessModName(root, file),
                DfmodPath: file,
                AssetName: Path.GetFileName(file),
                Text: text);

            records.AddRange(MapIdScanner.ExtractMapIds(asset));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: Could not scan loose file: {file}");
            Console.WriteLine($"      {ex.GetType().Name}: {ex.Message}");
        }
    }

    return records;
}

static List<MapIdRecord> ScanDfmods(string root, ScanCache? cache, bool rebuildCache)
{
    var dfmods = Directory
        .EnumerateFiles(root, "*.dfmod", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .ToList();

    Console.WriteLine($"Scanning .dfmod files under: {root}");
    Console.WriteLine($"Found {dfmods.Count} .dfmod file(s).");

    var records = new List<MapIdRecord>();

    int cacheHits = 0;
    int scanned = 0;

    foreach (string dfmod in dfmods)
    {
        string modName = GuessModName(root, dfmod);

        if (cache != null &&
            !rebuildCache &&
            cache.TryGet("dfmod", dfmod, out List<MapIdRecord> cachedRecords))
        {
            records.AddRange(cachedRecords);
            cacheHits++;
            continue;
        }

        var dfmodRecords = new List<MapIdRecord>();

        try
        {
            foreach (TextAssetInfo asset in AssetStudioTextAssetExtractor.ExtractTextAssets(modName, dfmod))
            {
                dfmodRecords.AddRange(MapIdScanner.ExtractMapIds(asset));
            }

            cache?.Put("dfmod", dfmod, dfmodRecords);
            records.AddRange(dfmodRecords);
            scanned++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: Could not scan .dfmod: {dfmod}");
            Console.WriteLine($"      {ex.GetType().Name}: {ex.Message}");
        }
    }

    if (cache != null)
        Console.WriteLine($"Cache: {cacheHits} .dfmod hit(s), {scanned} .dfmod scanned.");

    return records;
}

static List<MapIdRecord> LoadVanillaMapIds(string arena2Path, ScanCache? cache, bool rebuildCache)
{
    string mapsPath = Path.Combine(arena2Path, "MAPS.BSA");

    if (cache != null &&
        !rebuildCache &&
        cache.TryGet("vanilla-maps", mapsPath, out List<MapIdRecord> cachedRecords))
    {
        Console.WriteLine("Cache: vanilla MAPS.BSA hit.");
        return cachedRecords;
    }

    Console.WriteLine("Scanning vanilla MAPS.BSA baseline...");

    List<MapIdRecord> records = VanillaMapIdExtractor.ExtractVanillaMapIds(arena2Path);

    cache?.Put("vanilla-maps", mapsPath, records);

    return records;
}

static List<MapIdRecord> LoadBaselineMapIds(string path)
{
    var records = new List<MapIdRecord>();

    if (!File.Exists(path))
    {
        Console.WriteLine($"WARN: Baseline file does not exist: {path}");
        return records;
    }

    int lineNumber = 0;

    foreach (string rawLine in File.ReadLines(path))
    {
        lineNumber++;

        string line = rawLine.Trim();

        if (line.Length == 0 || line.StartsWith("#"))
            continue;

        if (!int.TryParse(line, out int mapId))
        {
            Console.WriteLine($"WARN: Invalid baseline MapId at line {lineNumber}: {line}");
            continue;
        }

        records.Add(new MapIdRecord(
            MapId: mapId,
            X: null,
            Y: null,
            Longitude: null,
            Latitude: null,
            ModName: "[Vanilla/Baseline]",
            DfmodPath: path,
            AssetName: Path.GetFileName(path),
            JsonPath: $"line:{lineNumber}",
            RecordName: null,
            AssetKind: "Baseline",
            Source: "Baseline MapId"));
    }

    return records;
}

static void PrintConflicts(
    List<MapIdRecord> records,
    bool showSingleAssetGroups,
    string minSeverity,
    List<MapIdRecord>? contextRecords = null)
{
    contextRecords ??= records;

    Dictionary<string, AssetIdentitySummary> identitySummariesByAsset = BuildIdentitySummaryLookup(contextRecords);

    var groups = records
        .GroupBy(r => new CollisionKey(GetCollisionKeyKind(r), r.MapId))
        .OrderBy(g => SortCollisionKeyKind(g.Key.Kind))
        .ThenBy(g => g.Key.Value)
        .ToList();

    int criticalCount = 0;
    int warningCount = 0;
    int infoCount = 0;

    foreach (var group in groups)
    {
        var uniqueAssets = group
            .GroupBy(GetAssetKey, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                MapIdRecord first = g.First();

                string paths = string.Join(
                    ", ",
                    g.Select(r => r.JsonPath)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(p => p));

                string sources = string.Join(
                    " + ",
                    g.Select(r => r.Source)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(s => s));

                return new
                {
                    Record = first,
                    Paths = paths,
                    Sources = sources,
                    Count = g.Count()
                };
            })
            .ToList();

        bool isInterAsset = uniqueAssets.Count > 1;

        string keyKind = group.Key.Kind;
        int keyValue = group.Key.Value;
		ConflictClassification classification = ClassifyConflict(uniqueAssets.Select(e => e.Record).ToList(), keyKind);

		if (!isInterAsset && !showSingleAssetGroups)
			continue;

		string outputSeverity = isInterAsset ? classification.Severity : "INFO";

		if (!ShouldIncludeSeverity(outputSeverity, minSeverity))
			continue;

        if (!isInterAsset)
        {
            infoCount++;
            Console.WriteLine($"INFO: {keyKind} {keyValue}");
            Console.WriteLine("  Only found inside one asset. Multiple JSON paths inside the same asset are not a conflict.");
        }
        else
        {
            switch (classification.Severity)
            {
                case "CRITICAL":
                    criticalCount++;
                    break;

                case "WARNING":
                    warningCount++;
                    break;

                default:
                    infoCount++;
                    break;
            }

            Console.WriteLine($"{classification.Severity}: {keyKind} {keyValue}");
            Console.WriteLine($"  {classification.Title}");
            Console.WriteLine($"  {classification.Explanation}");
        }

        MapIdRecord firstRecord = group.First();

        if (keyKind == "MapId" && firstRecord.X.HasValue && firstRecord.Y.HasValue)
        {
            Console.WriteLine($"  Pixel: X={firstRecord.X}, Y={firstRecord.Y}");
            Console.WriteLine($"  Derived DFU position: Longitude={firstRecord.Longitude}, Latitude={firstRecord.Latitude}");
        }
        else if (keyKind == "LocationId")
        {
            Console.WriteLine("  LocationId identity record. DFU also indexes these when enumerating maps.");
            Console.WriteLine("  If this insert fails, DFU may log the current location MapId rather than the duplicate LocationId.");
        }
        else
        {
            Console.WriteLine("  Location MapId record, not a derived map-pixel coordinate.");
        }

        Console.WriteLine();

        int i = 1;

        foreach (var entry in uniqueAssets
                     .OrderBy(e => SortAssetKind(e.Record.AssetKind))
                     .ThenBy(e => e.Record.ModName)
                     .ThenBy(e => e.Record.DfmodPath)
                     .ThenBy(e => e.Record.AssetName))
        {
            MapIdRecord record = entry.Record;

            Console.WriteLine($"  {i}. Mod: {record.ModName}");
            Console.WriteLine($"     DFMod/File: {record.DfmodPath}");
            Console.WriteLine($"     Asset: {record.AssetName}");
            Console.WriteLine($"     Asset Kind: {record.AssetKind}");
            Console.WriteLine($"     Records: {entry.Paths}");
            Console.WriteLine($"     Name: {record.RecordName ?? "(unknown)"}");
            Console.WriteLine($"     Source: {entry.Sources}");

            if (identitySummariesByAsset.TryGetValue(GetAssetKey(record), out var identitySummary) && identitySummary is not null)
            {
                Console.WriteLine($"     MapId(s): {identitySummary.MapIds}");
                Console.WriteLine($"     LocationId(s): {identitySummary.LocationIds}");
            }

            if (entry.Count > 1)
                Console.WriteLine($"     Internal hits: {entry.Count}");

            Console.WriteLine();

            i++;
        }

        Console.WriteLine(new string('-', 80));
    }

    Console.WriteLine("Summary:");
    Console.WriteLine($"  Critical: {criticalCount}");
    Console.WriteLine($"  Warnings: {warningCount}");
    Console.WriteLine($"  Info:     {infoCount}");
}

static Dictionary<string, AssetIdentitySummary> BuildIdentitySummaryLookup(List<MapIdRecord> records)
{
    return records
        .GroupBy(GetAssetKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            g => g.Key,
            g => new AssetIdentitySummary(
                MapIds: FormatIdentityValues(g, "MapId"),
                LocationIds: FormatIdentityValues(g, "LocationId")),
            StringComparer.OrdinalIgnoreCase);
}

static string FormatIdentityValues(IEnumerable<MapIdRecord> records, string keyKind)
{
    var values = records
        .Where(r => GetCollisionKeyKind(r) == keyKind)
        .GroupBy(r => r.MapId)
        .OrderBy(g => g.Key)
        .Select(g =>
        {
            string sources = string.Join(
                " + ",
                g.Select(r => r.Source)
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .OrderBy(source => source));

            return string.IsNullOrWhiteSpace(sources)
                ? g.Key.ToString()
                : $"{g.Key} ({sources})";
        })
        .ToList();

    return values.Count == 0
        ? "(none found)"
        : string.Join(", ", values);
}

static string GetAssetKey(MapIdRecord record)
{
    return $"{record.ModName}|{record.DfmodPath}|{record.AssetName}";
}

static ConflictClassification ClassifyConflict(List<MapIdRecord> records, string keyKind)
{
    int newLocationCount = records.Count(r => r.AssetKind == "NewLocation");
    bool hasNewLocation = newLocationCount > 0;
    bool hasBaseline = records.Any(r => r.AssetKind == "Baseline");
    bool hasExistingOverride = records.Any(r => r.AssetKind == "ExistingLocationOverride");

    if (keyKind == "LocationId")
    {
        if (hasNewLocation && hasBaseline)
        {
            return new ConflictClassification(
                Severity: "CRITICAL",
                Title: "Confirmed new-location collision with baseline/vanilla LocationId.",
                Explanation: "A locationnew-* asset is using a LocationId that is present in the baseline. DFU stores LocationId-to-MapId links while enumerating maps, so this can trigger the same startup failure even when the logged value is the new location's MapId.");
        }

        if (newLocationCount > 1)
        {
            return new ConflictClassification(
                Severity: "CRITICAL",
                Title: "New location LocationId collision.",
                Explanation: "Multiple locationnew-* assets share the same LocationId. New locations should have unique LocationIds as well as unique map-pixel MapIds. DFU can report this as a MapId collision because it logs the current location MapId for either dictionary insert failure.");
        }

        if (hasNewLocation)
        {
            return new ConflictClassification(
                Severity: "WARNING",
                Title: "New location shares LocationId with another asset.",
                Explanation: "The scanner found a shared LocationId involving a locationnew-* asset, but there is no baseline record in this group to prove it is a vanilla collision.");
        }

        if (hasBaseline && hasExistingOverride)
        {
            return new ConflictClassification(
                Severity: "INFO",
                Title: "Existing location override overlaps baseline LocationId.",
                Explanation: "This usually means a mod is intentionally overriding an existing vanilla location. This is normal overwrite behavior.");
        }

        if (hasExistingOverride)
        {
            return new ConflictClassification(
                Severity: "WARNING",
                Title: "Multiple existing-location overrides share the same LocationId.",
                Explanation: "This is probably a normal overwrite-style conflict between mods editing the same existing location, not a new-location startup collision.");
        }

        return new ConflictClassification(
            Severity: "WARNING",
            Title: "Unclassified LocationId overlap.",
            Explanation: "The scanner found the same LocationId in multiple assets, but could not classify the asset names well enough to determine whether this is dangerous.");
    }

    if (hasNewLocation && hasBaseline)
    {
        return new ConflictClassification(
            Severity: "CRITICAL",
            Title: "Confirmed new-location collision with baseline/vanilla MapId.",
            Explanation: "A locationnew-* asset is using a MapId that is present in the baseline. This is the kind of collision that can trigger DFU map enumeration errors.");
    }

    if (newLocationCount > 1)
    {
        return new ConflictClassification(
            Severity: "CRITICAL",
            Title: "New location MapId collision.",
            Explanation: "Multiple locationnew-* assets share the same MapId. New locations should use unique MapIds, so this is likely to break map enumeration.");
    }

    if (hasNewLocation && hasExistingOverride)
    {
        return new ConflictClassification(
            Severity: "WARNING",
            Title: "New location shares MapId with an existing-location override.",
            Explanation: "The existing-location override does not create a second location by itself, but it indicates this MapId belongs to an existing location record. The locationnew-* asset may therefore be colliding with vanilla/base data. This should be confirmed with a full vanilla baseline.");
    }

    if (hasNewLocation)
    {
        return new ConflictClassification(
            Severity: "WARNING",
            Title: "New location shares MapId with another unclassified asset.",
            Explanation: "A locationnew-* asset overlaps another asset, but the scanner cannot yet prove whether this is a vanilla collision, another new location, or unrelated data.");
    }

    if (hasBaseline && hasExistingOverride)
    {
        return new ConflictClassification(
            Severity: "INFO",
            Title: "Existing location override overlaps baseline.",
            Explanation: "This usually means a mod is intentionally overriding an existing vanilla location. This is normal overwrite behavior.");
    }

    if (hasExistingOverride)
    {
        return new ConflictClassification(
            Severity: "WARNING",
            Title: "Multiple existing-location overrides share the same MapId.",
            Explanation: "This is probably a normal overwrite-style conflict between mods editing the same existing location. It may need a compatibility patch, but it is not the same as a broken new-location ID collision.");
    }

    return new ConflictClassification(
        Severity: "WARNING",
        Title: "Unclassified MapId overlap.",
        Explanation: "The scanner found the same MapId in multiple assets, but could not classify the asset names well enough to determine whether this is dangerous.");
}

static string GetCollisionKeyKind(MapIdRecord record)
{
    return record.Source.Contains("LocationId", StringComparison.OrdinalIgnoreCase)
        ? "LocationId"
        : "MapId";
}

static int SortCollisionKeyKind(string keyKind)
{
    return keyKind switch
    {
        "MapId" => 0,
        "LocationId" => 1,
        _ => 2
    };
}

static bool ShouldIncludeSeverity(string severity, string minSeverity)
{
    int severityRank = SeverityRank(severity);
    int minRank = SeverityRank(minSeverity);

    return severityRank >= minRank;
}

static int SeverityRank(string severity)
{
    return severity.Trim().ToUpperInvariant() switch
    {
        "CRITICAL" => 3,
        "WARNING" => 2,
        "WARN" => 2,
        "INFO" => 1,
        "ALL" => 1,
        _ => 1
    };
}

static int SortAssetKind(string assetKind)
{
    return assetKind switch
    {
        "Baseline" => 0,
        "NewLocation" => 1,
        "ExistingLocationOverride" => 2,
        _ => 3
    };
}

static string GuessModName(string root, string filePath)
{
    string relative = Path.GetRelativePath(root, filePath);
    string[] parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    if (parts.Length > 1)
        return parts[0];

    return Path.GetFileNameWithoutExtension(filePath);
}

static List<MapIdRecord> UnknownMode(string mode)
{
    Console.Error.WriteLine($"Unknown mode: {mode}");
    PrintUsage();
    Environment.ExitCode = 1;
    return new List<MapIdRecord>();
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  DaggerfallEdit.Cli --loose <folder>");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder>");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --filter-mapid <id>");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --filter-locationid <id>");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --baseline <mapids.txt>");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --baseline <mapids.txt> --filter-mapid <id>");
	Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --min-severity critical");
	Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --out <report.txt>");
	Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --baseline <mapids.txt> --min-severity critical --out <report.txt>");
	Console.WriteLine("  DaggerfallEdit.Cli --mods <mods-root-folder> --arena2 <arena2-folder>");
	Console.WriteLine("  DaggerfallEdit.Cli --mods <mods-root-folder> --arena2 <arena2-folder> --min-severity critical");
	Console.WriteLine("  DaggerfallEdit.Cli --mods <mods-root-folder> --arena2 <arena2-folder> --rebuild-cache");
	Console.WriteLine("  DaggerfallEdit.Cli --mods <mods-root-folder> --arena2 <arena2-folder> --no-cache");
}

public sealed record AssetIdentitySummary(
    string MapIds,
    string LocationIds
);

public sealed record CollisionKey(
    string Kind,
    int Value
);

public sealed record ConflictClassification(
    string Severity,
    string Title,
    string Explanation
);