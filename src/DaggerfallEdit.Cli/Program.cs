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
bool inventory = false;
bool inventoryAllDuplicates = false;
bool inventoryDetails = false;
int inventoryMaxGroups = 50;
int inventoryMaxRecordsPerGroup = 10;
string? inventoryKindFilter = null;
string? inventoryClassFilter = null;
string? inventoryIdentityContainsFilter = null;

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
    else if (args[i] == "--inventory")
    {
        inventory = true;
    }
    else if (args[i] == "--inventory-all-duplicates")
    {
        inventoryAllDuplicates = true;
    }
    else if (args[i] == "--inventory-details")
    {
        inventoryDetails = true;
    }
    else if (args[i] == "--inventory-max-groups" &&
        i + 1 < args.Length &&
        int.TryParse(args[i + 1], out int parsedInventoryMaxGroups))
    {
        inventoryMaxGroups = Math.Max(0, parsedInventoryMaxGroups);
        i++;
    }
    else if (args[i] == "--inventory-max-records" &&
        i + 1 < args.Length &&
        int.TryParse(args[i + 1], out int parsedInventoryMaxRecords))
    {
        inventoryMaxRecordsPerGroup = Math.Max(0, parsedInventoryMaxRecords);
        i++;
    }
    else if (args[i] == "--inventory-kind" && i + 1 < args.Length)
    {
        inventoryKindFilter = args[i + 1];
        i++;
    }
    else if (args[i] == "--inventory-class" && i + 1 < args.Length)
    {
        inventoryClassFilter = args[i + 1];
        i++;
    }
    else if (args[i] == "--inventory-identity-contains" && i + 1 < args.Length)
    {
        inventoryIdentityContainsFilter = args[i + 1];
        i++;
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

if (inventory)
{
    if (mode != "--mods")
    {
        Console.Error.WriteLine("Inventory mode currently supports --mods only.");
        FinishOutput(outWriter, originalOut, outPath);
        return 1;
    }

    List<AssetInventoryRecord> inventoryRecords = ScanDfmodInventory(root);

    Console.WriteLine();
    PrintAssetInventory(
        inventoryRecords,
        new InventoryReportOptions(
            IncludeSuppressedDuplicateGroups: inventoryAllDuplicates,
            PrintDuplicateDetails: inventoryDetails,
            MaxDuplicateGroups: inventoryMaxGroups,
            MaxRecordsPerDuplicateGroup: inventoryMaxRecordsPerGroup,
            SemanticKindFilter: inventoryKindFilter,
            ClassNameFilter: inventoryClassFilter,
            IdentityContainsFilter: inventoryIdentityContainsFilter));

    FinishOutput(outWriter, originalOut, outPath);
    return 0;
}

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

FinishOutput(outWriter, originalOut, outPath);

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

static List<AssetInventoryRecord> ScanDfmodInventory(string root)
{
    var dfmods = Directory
        .EnumerateFiles(root, "*.dfmod", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .ToList();

    Console.WriteLine($"Inventory scanning .dfmod files under: {root}");
    Console.WriteLine($"Found {dfmods.Count} .dfmod file(s).");

    var records = new List<AssetInventoryRecord>();

    foreach (string dfmod in dfmods)
    {
        string modName = GuessModName(root, dfmod);

        try
        {
            records.AddRange(AssetStudioAssetInventoryExtractor.ExtractAssets(modName, dfmod));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: Could not inventory .dfmod: {dfmod}");
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

static void PrintAssetInventory(List<AssetInventoryRecord> records, InventoryReportOptions options)
{
    List<AssetInventoryRecord> reportRecords = ApplyInventoryFilters(records, options);

    Console.WriteLine("Asset inventory");
    Console.WriteLine(new string('=', 80));
    Console.WriteLine($"Assets found: {records.Count}");

    if (reportRecords.Count != records.Count)
        Console.WriteLine($"Assets matching inventory filters: {reportRecords.Count}");

    Console.WriteLine($"Mods with assets: {reportRecords.Select(r => r.ModName).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");
    Console.WriteLine($"DFMod files with assets: {reportRecords.Select(r => r.DfmodPath).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");

    if (!string.IsNullOrWhiteSpace(options.SemanticKindFilter))
        Console.WriteLine($"Filter: Semantic kind = {options.SemanticKindFilter}");

    if (!string.IsNullOrWhiteSpace(options.ClassNameFilter))
        Console.WriteLine($"Filter: Unity class = {options.ClassNameFilter}");

    if (!string.IsNullOrWhiteSpace(options.IdentityContainsFilter))
        Console.WriteLine($"Filter: Identity contains = {options.IdentityContainsFilter}");

    Console.WriteLine();

    Console.WriteLine("Unity class counts:");
    foreach (var group in reportRecords
                 .GroupBy(r => r.ClassName, StringComparer.OrdinalIgnoreCase)
                 .OrderByDescending(g => g.Count())
                 .ThenBy(g => g.Key))
    {
        Console.WriteLine($"  {group.Key}: {group.Count()}");
    }

    Console.WriteLine();
    Console.WriteLine("Semantic kind counts:");
    foreach (var group in reportRecords
                 .GroupBy(r => r.SemanticKind, StringComparer.OrdinalIgnoreCase)
                 .OrderByDescending(g => g.Count())
                 .ThenBy(g => g.Key))
    {
        Console.WriteLine($"  {group.Key}: {group.Count()}");
    }

    Console.WriteLine();
    Console.WriteLine("Duplicate policy counts:");
    foreach (var group in reportRecords
                 .GroupBy(r => r.DuplicatePolicy, StringComparer.OrdinalIgnoreCase)
                 .OrderByDescending(g => g.Count())
                 .ThenBy(g => g.Key))
    {
        Console.WriteLine($"  {group.Key}: {group.Count()}");
    }

    Console.WriteLine();
    Console.WriteLine("Potential override identity duplicates across mods/files:");

    var allDuplicateGroups = reportRecords
        .GroupBy(r => r.IdentityKey, StringComparer.OrdinalIgnoreCase)
        .Select(g => new InventoryDuplicateGroup(
            IdentityKey: g.Key,
            Records: g.ToList(),
            SourceCount: g.Select(r => r.DfmodPath).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            ModCount: g.Select(r => r.ModName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            IsReportable: g.Any(r => IsReportableInventoryDuplicate(r))))
        .Where(g => g.SourceCount > 1)
        .OrderByDescending(g => g.IsReportable)
        .ThenByDescending(g => g.ModCount)
        .ThenByDescending(g => g.SourceCount)
        .ThenBy(g => g.IdentityKey)
        .ToList();

    var duplicateGroups = allDuplicateGroups
        .Where(g => options.IncludeSuppressedDuplicateGroups || g.IsReportable)
        .ToList();

    int suppressedDuplicateGroupCount = allDuplicateGroups.Count - allDuplicateGroups.Count(g => g.IsReportable);

    if (allDuplicateGroups.Count == 0)
    {
        Console.WriteLine("  None found.");
        return;
    }

    Console.WriteLine($"  Reportable duplicate group(s): {allDuplicateGroups.Count(g => g.IsReportable)}");
    Console.WriteLine($"  Suppressed noisy duplicate group(s): {suppressedDuplicateGroupCount}");

    if (options.IncludeSuppressedDuplicateGroups)
        Console.WriteLine($"  Groups selected for output: {duplicateGroups.Count} including suppressed groups");
    else
        Console.WriteLine($"  Groups selected for output: {duplicateGroups.Count}");

    if (!options.IncludeSuppressedDuplicateGroups && suppressedDuplicateGroupCount > 0)
        Console.WriteLine("  Use --inventory-all-duplicates to include suppressed metadata/dependency/internal Unity groups.");

    Console.WriteLine();

    PrintInventoryDuplicateOverview(duplicateGroups, options.MaxDuplicateGroups);

    if (!options.PrintDuplicateDetails)
    {
        Console.WriteLine();
        Console.WriteLine("Duplicate details are suppressed by default to keep inventory reports readable.");
        Console.WriteLine("Use --inventory-details to print grouped mod entries.");
        Console.WriteLine("Use --inventory-kind, --inventory-class, or --inventory-identity-contains to focus the report first.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"Duplicate detail groups shown: {Math.Min(options.MaxDuplicateGroups, duplicateGroups.Count)} of {duplicateGroups.Count}");

    if (options.MaxDuplicateGroups < duplicateGroups.Count)
        Console.WriteLine($"Use --inventory-max-groups <n> to show more groups.");

    Console.WriteLine();

    foreach (InventoryDuplicateGroup group in duplicateGroups.Take(options.MaxDuplicateGroups))
    {
        AssetInventoryRecord first = group.Records.First();
        string label = group.IsReportable ? "POTENTIAL OVERRIDE" : "SUPPRESSED DUPLICATE";

        Console.WriteLine($"{label}: {group.IdentityKey}");
        Console.WriteLine($"  Class: {first.ClassName}");
        Console.WriteLine($"  Semantic Kind: {first.SemanticKind}");
        Console.WriteLine($"  Duplicate Policy: {first.DuplicatePolicy}");
        Console.WriteLine($"  Mods: {group.ModCount}");
        Console.WriteLine($"  DFMod files: {group.SourceCount}");

        int i = 1;
        var orderedRecords = group.Records
            .OrderBy(r => r.ModName)
            .ThenBy(r => r.DfmodPath)
            .ThenBy(r => r.AssetName)
            .ThenBy(r => r.PathId)
            .ToList();

        foreach (AssetInventoryRecord record in orderedRecords.Take(options.MaxRecordsPerDuplicateGroup))
        {
            Console.WriteLine($"  {i}. Mod: {record.ModName}");
            Console.WriteLine($"     DFMod/File: {record.DfmodPath}");
            Console.WriteLine($"     Asset: {record.AssetName}");
            Console.WriteLine($"     Class: {record.ClassName}");
            Console.WriteLine($"     PathID: {record.PathId}");
            Console.WriteLine($"     Duplicate Policy: {record.DuplicatePolicy}");

            if (record.ByteLength.HasValue)
                Console.WriteLine($"     Payload Bytes: {record.ByteLength.Value}");

            if (!string.IsNullOrWhiteSpace(record.ContainerPath))
                Console.WriteLine($"     Container: {record.ContainerPath}");

            i++;
        }

        if (orderedRecords.Count > options.MaxRecordsPerDuplicateGroup)
            Console.WriteLine($"  ... {orderedRecords.Count - options.MaxRecordsPerDuplicateGroup} more record(s) suppressed. Use --inventory-max-records <n> to show more per group.");

        Console.WriteLine(new string('-', 80));
    }
}

static List<AssetInventoryRecord> ApplyInventoryFilters(
    List<AssetInventoryRecord> records,
    InventoryReportOptions options)
{
    IEnumerable<AssetInventoryRecord> query = records;

    if (!string.IsNullOrWhiteSpace(options.SemanticKindFilter))
    {
        query = query.Where(r =>
            r.SemanticKind.Equals(options.SemanticKindFilter, StringComparison.OrdinalIgnoreCase));
    }

    if (!string.IsNullOrWhiteSpace(options.ClassNameFilter))
    {
        query = query.Where(r =>
            r.ClassName.Equals(options.ClassNameFilter, StringComparison.OrdinalIgnoreCase));
    }

    if (!string.IsNullOrWhiteSpace(options.IdentityContainsFilter))
    {
        query = query.Where(r =>
            r.IdentityKey.Contains(options.IdentityContainsFilter, StringComparison.OrdinalIgnoreCase) ||
            r.AssetName.Contains(options.IdentityContainsFilter, StringComparison.OrdinalIgnoreCase));
    }

    return query.ToList();
}

static void PrintInventoryDuplicateOverview(
    List<InventoryDuplicateGroup> duplicateGroups,
    int maxGroups)
{
    Console.WriteLine($"Top duplicate groups shown: {Math.Min(maxGroups, duplicateGroups.Count)} of {duplicateGroups.Count}");

    if (maxGroups < duplicateGroups.Count)
        Console.WriteLine($"Use --inventory-max-groups <n> to list more groups in the overview/details.");

    Console.WriteLine();

    foreach (InventoryDuplicateGroup group in duplicateGroups.Take(maxGroups))
    {
        AssetInventoryRecord first = group.Records.First();
        string label = group.IsReportable ? "POTENTIAL" : "SUPPRESSED";

        Console.WriteLine($"  {label,-10} {group.ModCount,3} mod(s), {group.SourceCount,3} file(s) | {first.ClassName} | {first.SemanticKind} | {group.IdentityKey}");
    }
}

static bool IsReportableInventoryDuplicate(AssetInventoryRecord record)
{
    return record.DuplicatePolicy.StartsWith("Report:", StringComparison.OrdinalIgnoreCase);
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

static void FinishOutput(StreamWriter? outWriter, TextWriter originalOut, string? outPath)
{
    if (outWriter == null)
        return;

    Console.Out.Flush();
    Console.SetOut(originalOut);
    outWriter.Dispose();

    Console.WriteLine($"Report written to: {Path.GetFullPath(outPath!)}");
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
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --out <inventory-report.txt>");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --inventory-details --inventory-max-groups 25 --inventory-max-records 5");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --inventory-kind ExistingLocationOverride");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --inventory-class Texture2D --inventory-details");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --inventory-identity-contains location-11-197");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --inventory-all-duplicates");
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

public sealed record InventoryReportOptions(
    bool IncludeSuppressedDuplicateGroups,
    bool PrintDuplicateDetails,
    int MaxDuplicateGroups,
    int MaxRecordsPerDuplicateGroup,
    string? SemanticKindFilter,
    string? ClassNameFilter,
    string? IdentityContainsFilter
);

public sealed record InventoryDuplicateGroup(
    string IdentityKey,
    List<AssetInventoryRecord> Records,
    int SourceCount,
    int ModCount,
    bool IsReportable
);

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