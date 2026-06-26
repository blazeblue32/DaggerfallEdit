using System.Text.Json;
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
string? dfuModsJsonPath = null;
string? mo2ModlistPath = null;
bool inventoryActiveOnly = false;
bool looseInventory = false;
bool looseContent = false;
bool looseIncludeDisabled = false;
string? overwritePath = null;
string? looseKindFilter = null;
string? looseExtensionFilter = null;
string? loosePathContainsFilter = null;
string? looseContentKindFilter = null;
string? looseContentIdentityContainsFilter = null;

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
    else if (args[i] == "--dfu-mods-json" && i + 1 < args.Length)
    {
        dfuModsJsonPath = args[i + 1];
        i++;
    }
    else if (args[i] == "--mo2-modlist" && i + 1 < args.Length)
    {
        mo2ModlistPath = args[i + 1];
        i++;
    }
    else if (args[i] == "--inventory-active-only")
    {
        inventoryActiveOnly = true;
    }
    else if (args[i] == "--loose-inventory")
    {
        looseInventory = true;
    }
    else if (args[i] == "--loose-content")
    {
        looseContent = true;
    }
    else if (args[i] == "--loose-include-disabled")
    {
        looseIncludeDisabled = true;
    }
    else if (args[i] == "--overwrite" && i + 1 < args.Length)
    {
        overwritePath = args[i + 1];
        i++;
    }
    else if (args[i] == "--loose-kind" && i + 1 < args.Length)
    {
        looseKindFilter = args[i + 1];
        i++;
    }
    else if (args[i] == "--loose-extension" && i + 1 < args.Length)
    {
        looseExtensionFilter = args[i + 1];
        i++;
    }
    else if (args[i] == "--loose-path-contains" && i + 1 < args.Length)
    {
        loosePathContainsFilter = args[i + 1];
        i++;
    }
    else if (args[i] == "--loose-content-kind" && i + 1 < args.Length)
    {
        looseContentKindFilter = args[i + 1];
        i++;
    }
    else if (args[i] == "--loose-content-identity-contains" && i + 1 < args.Length)
    {
        looseContentIdentityContainsFilter = args[i + 1];
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

if (looseInventory || looseContent)
{
    if (mode != "--mods")
    {
        Console.Error.WriteLine("Loose inventory mode currently supports --mods <MO2 mods root> only.");
        FinishOutput(outWriter, originalOut, outPath);
        return 1;
    }

    Mo2ModlistLoadOrder? mo2LoadOrder = LoadMo2ModlistLoadOrder(mo2ModlistPath);

    if (mo2LoadOrder == null)
    {
        Console.Error.WriteLine("Loose inventory mode requires --mo2-modlist <modlist.txt> so winners can be determined from MO2 order.");
        FinishOutput(outWriter, originalOut, outPath);
        return 1;
    }

    string? resolvedOverwritePath = ResolveOverwritePath(root, overwritePath);
    List<LooseFileInventoryRecord> looseRecords = ScanLooseFileInventory(root, mo2LoadOrder, resolvedOverwritePath);

    if (looseContent)
    {
        List<LooseContentRecord> looseContentRecords = ScanLooseContentInventory(looseRecords);

        Console.WriteLine();
        PrintLooseContentInventory(
            looseContentRecords,
            new LooseContentReportOptions(
                IncludeDisabled: looseIncludeDisabled,
                PrintDuplicateDetails: inventoryDetails,
                MaxDuplicateGroups: inventoryMaxGroups,
                MaxRecordsPerDuplicateGroup: inventoryMaxRecordsPerGroup,
                ContentKindFilter: looseContentKindFilter,
                IdentityContainsFilter: looseContentIdentityContainsFilter,
                PathContainsFilter: loosePathContainsFilter),
            mo2LoadOrder,
            resolvedOverwritePath);

        FinishOutput(outWriter, originalOut, outPath);
        return 0;
    }

    Console.WriteLine();
    PrintLooseFileInventory(
        looseRecords,
        new LooseInventoryReportOptions(
            IncludeDisabled: looseIncludeDisabled,
            PrintDuplicateDetails: inventoryDetails,
            MaxDuplicateGroups: inventoryMaxGroups,
            MaxRecordsPerDuplicateGroup: inventoryMaxRecordsPerGroup,
            FileKindFilter: looseKindFilter,
            ExtensionFilter: looseExtensionFilter,
            PathContainsFilter: loosePathContainsFilter),
        mo2LoadOrder,
        resolvedOverwritePath);

    FinishOutput(outWriter, originalOut, outPath);
    return 0;
}

if (inventory)
{
    if (mode != "--mods")
    {
        Console.Error.WriteLine("Inventory mode currently supports --mods only.");
        FinishOutput(outWriter, originalOut, outPath);
        return 1;
    }

    DfuModsJsonLoadOrder? dfuLoadOrder = LoadDfuModsJsonLoadOrder(dfuModsJsonPath);
    Mo2ModlistLoadOrder? mo2LoadOrder = LoadMo2ModlistLoadOrder(mo2ModlistPath);

    List<AssetInventoryRecord> inventoryRecords = ScanDfmodInventory(root);
    inventoryRecords = ApplyInventoryLoadOrder(inventoryRecords, dfuLoadOrder, mo2LoadOrder);

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
            IdentityContainsFilter: inventoryIdentityContainsFilter,
            ActiveOnly: inventoryActiveOnly),
        dfuLoadOrder,
        mo2LoadOrder);

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


static string? ResolveOverwritePath(string modsRoot, string? explicitOverwritePath)
{
    if (!string.IsNullOrWhiteSpace(explicitOverwritePath))
        return explicitOverwritePath;

    string? parent = Directory.GetParent(Path.GetFullPath(modsRoot))?.FullName;
    if (string.IsNullOrWhiteSpace(parent))
        return null;

    string candidate = Path.Combine(parent, "overwrite");
    return Directory.Exists(candidate) ? candidate : null;
}

static List<LooseFileInventoryRecord> ScanLooseFileInventory(
    string modsRoot,
    Mo2ModlistLoadOrder mo2LoadOrder,
    string? overwritePath)
{
    Console.WriteLine($"Loose inventory scanning physical MO2 mod folders under: {modsRoot}");
    Console.WriteLine("This scans physical mod folders directly, not MO2's virtual filesystem, so overwritten loser files remain visible.");

    var records = new List<LooseFileInventoryRecord>();
    var discoveredModFolders = Directory
        .EnumerateDirectories(modsRoot)
        .ToDictionary(path => LoadOrderKey.Normalize(Path.GetFileName(path)), path => path, StringComparer.OrdinalIgnoreCase);

    int matchedFolders = 0;
    int missingFolders = 0;

    foreach (Mo2ModEntry entry in mo2LoadOrder.ModEntries)
    {
        string key = LoadOrderKey.Normalize(entry.ModName);

        if (!discoveredModFolders.TryGetValue(key, out string? modFolderPath))
        {
            missingFolders++;
            continue;
        }

        matchedFolders++;

        records.AddRange(ScanLooseFilesInSource(
            sourceName: entry.ModName,
            sourceFolderPath: modFolderPath,
            mo2Entry: entry,
            isOverwrite: false));
    }

    if (!string.IsNullOrWhiteSpace(overwritePath) && Directory.Exists(overwritePath))
    {
        var overwriteEntry = new Mo2ModEntry(
            ModName: "Overwrite",
            Enabled: true,
            OrderIndex: -1,
            LineNumber: -1,
            IsSeparator: false);

        records.AddRange(ScanLooseFilesInSource(
            sourceName: "Overwrite",
            sourceFolderPath: overwritePath,
            mo2Entry: overwriteEntry,
            isOverwrite: true));
    }

    Console.WriteLine($"MO2 mod folders matched: {matchedFolders}");
    Console.WriteLine($"MO2 modlist entries without a physical folder match: {missingFolders}");

    if (!string.IsNullOrWhiteSpace(overwritePath))
        Console.WriteLine(Directory.Exists(overwritePath)
            ? $"Overwrite folder included: {overwritePath}"
            : $"Overwrite folder not found: {overwritePath}");

    Console.WriteLine($"Loose files found: {records.Count}");

    return records;
}

static IEnumerable<LooseFileInventoryRecord> ScanLooseFilesInSource(
    string sourceName,
    string sourceFolderPath,
    Mo2ModEntry mo2Entry,
    bool isOverwrite)
{
    foreach (string filePath in Directory.EnumerateFiles(sourceFolderPath, "*", SearchOption.AllDirectories))
    {
        if (ShouldSkipLooseFile(filePath))
            continue;

        string relativePath = NormalizeRelativePath(Path.GetRelativePath(sourceFolderPath, filePath));
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        string fileKind = ClassifyLooseFileKind(relativePath, extension);
        string duplicatePolicy = ClassifyLooseDuplicatePolicy(relativePath, extension, fileKind);
        string identityKey = $"path/{NormalizeIdentityPart(relativePath)}";
        long byteLength = 0;

        try
        {
            byteLength = new FileInfo(filePath).Length;
        }
        catch
        {
            // Keep zero length if the filesystem refuses metadata access.
        }

        yield return new LooseFileInventoryRecord(
            ModName: sourceName,
            ModFolderPath: sourceFolderPath,
            FilePath: filePath,
            RelativePath: relativePath,
            Extension: extension,
            ByteLength: byteLength,
            FileKind: fileKind,
            IdentityKey: identityKey,
            DuplicatePolicy: duplicatePolicy,
            IsOverwrite: isOverwrite,
            Mo2ModName: mo2Entry.ModName,
            Mo2Enabled: mo2Entry.Enabled,
            Mo2ListIndex: mo2Entry.OrderIndex,
            Mo2LineNumber: mo2Entry.LineNumber);
    }
}

static bool ShouldSkipLooseFile(string filePath)
{
    string fileName = Path.GetFileName(filePath);
    string extension = Path.GetExtension(filePath);

    if (extension.Equals(".dfmod", StringComparison.OrdinalIgnoreCase))
        return true;

    if (fileName.Equals("meta.ini", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return false;
}

static string NormalizeRelativePath(string path)
{
    return path.Trim().Replace('\\', '/');
}

static string ClassifyLooseFileKind(string relativePath, string extension)
{
    string fileName = Path.GetFileName(relativePath);

    if (fileName.Equals("modsettings", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("modpresets", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("modmanifest", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("modinfo", StringComparison.OrdinalIgnoreCase))
    {
        return "ModMetadata";
    }

    if (relativePath.Contains("/Quest", StringComparison.OrdinalIgnoreCase) ||
        fileName.Contains("quest", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".qrc", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".qbn", StringComparison.OrdinalIgnoreCase))
    {
        return "QuestOrQuestText";
    }

    if (extension.Equals(".rmb", StringComparison.OrdinalIgnoreCase))
        return "BlockLayoutRMB";

    if (extension.Equals(".rdb", StringComparison.OrdinalIgnoreCase))
        return "DungeonBlockRDB";

    if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        return "JsonData";

    if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".rsc", StringComparison.OrdinalIgnoreCase))
    {
        return "TextData";
    }

    if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".dds" or ".tga")
        return "TextureOrImage";

    if (extension is ".wav" or ".ogg" or ".mp3" or ".flac")
        return "Audio";

    if (extension is ".obj" or ".fbx" or ".dae" or ".glb" or ".gltf")
        return "Model";

    if (extension is ".dll" or ".pdb")
        return "PluginBinary";

    return "LooseFile";
}

static string ClassifyLooseDuplicatePolicy(string relativePath, string extension, string fileKind)
{
    if (fileKind.Equals("ModMetadata", StringComparison.OrdinalIgnoreCase))
        return "Suppressed: per-mod metadata/settings file";

    return "Report: loose path override";
}

static string NormalizeIdentityPart(string value)
{
    return value.Trim().Replace('\\', '/').ToLowerInvariant();
}


static List<LooseContentRecord> ScanLooseContentInventory(List<LooseFileInventoryRecord> looseFileRecords)
{
    Console.WriteLine("Loose content scanning known structured loose files.");
    Console.WriteLine("This scans content inside physical loose files, so overridden loser records remain visible for xEdit-style stacks.");

    var contentRecords = new List<LooseContentRecord>();
    Dictionary<string, string> activeLooseFileByRelativePath = BuildActiveLooseFilePathLookup(looseFileRecords);

    var jsonFiles = looseFileRecords
        .Where(r => r.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        .OrderBy(r => GetLooseWinnerSortValue(r))
        .ThenBy(r => r.RelativePath, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
        .ToList();

    int filesParsed = 0;
    int filesWithRecords = 0;
    int filesFailed = 0;

    foreach (LooseFileInventoryRecord fileRecord in jsonFiles)
    {
        try
        {
            bool looseFileActive = IsActiveLooseFileRecord(fileRecord, activeLooseFileByRelativePath);
            List<LooseContentRecord> fileContentRecords = ExtractLooseJsonContentRecords(fileRecord, looseFileActive).ToList();
            filesParsed++;

            if (fileContentRecords.Count > 0)
                filesWithRecords++;

            contentRecords.AddRange(fileContentRecords);
        }
        catch (Exception ex)
        {
            filesFailed++;
            Console.WriteLine($"WARN: Could not parse loose JSON content: {fileRecord.FilePath}");
            Console.WriteLine($"      {ex.GetType().Name}: {ex.Message}");
        }
    }

    Console.WriteLine($"Loose JSON files considered: {jsonFiles.Count}");
    Console.WriteLine($"Loose JSON files parsed: {filesParsed}");
    Console.WriteLine($"Loose JSON files with recognised content records: {filesWithRecords}");
    Console.WriteLine($"Loose JSON files with parse errors: {filesFailed}");
    Console.WriteLine($"Loose content records found: {contentRecords.Count}");

    return contentRecords;
}

static Dictionary<string, string> BuildActiveLooseFilePathLookup(List<LooseFileInventoryRecord> looseFileRecords)
{
    return looseFileRecords
        .Where(r => r.IsOverwrite || r.Mo2Enabled == true)
        .GroupBy(r => NormalizeRelativePath(r.RelativePath), StringComparer.OrdinalIgnoreCase)
        .Select(g => g
            .OrderBy(GetLooseWinnerSortValue)
            .ThenBy(r => r.ModName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
            .First())
        .ToDictionary(
            r => NormalizeRelativePath(r.RelativePath),
            r => r.FilePath,
            StringComparer.OrdinalIgnoreCase);
}

static bool IsActiveLooseFileRecord(
    LooseFileInventoryRecord fileRecord,
    Dictionary<string, string> activeLooseFileByRelativePath)
{
    if (!fileRecord.IsOverwrite && fileRecord.Mo2Enabled != true)
        return false;

    return activeLooseFileByRelativePath.TryGetValue(
            NormalizeRelativePath(fileRecord.RelativePath),
            out string? activeFilePath) &&
        activeFilePath.Equals(fileRecord.FilePath, StringComparison.OrdinalIgnoreCase);
}

static IEnumerable<LooseContentRecord> ExtractLooseJsonContentRecords(LooseFileInventoryRecord fileRecord, bool looseFileActive)
{
    string text = File.ReadAllText(fileRecord.FilePath);

    if (string.IsNullOrWhiteSpace(text))
        yield break;

    string jsonText = NormalizeLooseJsonForLenientParsing(text);

    using JsonDocument document = JsonDocument.Parse(jsonText, new JsonDocumentOptions
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    });

    foreach ((JsonElement element, string jsonPath) in EnumerateJsonObjects(document.RootElement, "$"))
    {
        if (TryBuildLooseJsonContentIdentity(
                fileRecord,
                element,
                jsonPath,
                out string contentKind,
                out string contentIdentityKey,
                out string displayName,
                out string? targetName,
                out string? replacementName,
                out string? portraitName,
                out string? ruleContext))
        {
            yield return new LooseContentRecord(
                SourceName: fileRecord.ModName,
                SourceFolderPath: fileRecord.ModFolderPath,
                FilePath: fileRecord.FilePath,
                RelativePath: fileRecord.RelativePath,
                SourceFileKind: fileRecord.FileKind,
                SourceExtension: fileRecord.Extension,
                ContentKind: contentKind,
                ContentIdentityKey: contentIdentityKey,
                JsonPath: jsonPath,
                DisplayName: displayName,
                TargetName: targetName,
                ReplacementName: replacementName,
                PortraitName: portraitName,
                RuleContext: ruleContext,
                LooseFileActive: looseFileActive,
                SourceByteLength: fileRecord.ByteLength,
                DuplicatePolicy: GetLooseContentDuplicatePolicy(contentKind),
                IsOverwrite: fileRecord.IsOverwrite,
                Mo2ModName: fileRecord.Mo2ModName,
                Mo2Enabled: fileRecord.Mo2Enabled,
                Mo2ListIndex: fileRecord.Mo2ListIndex,
                Mo2LineNumber: fileRecord.Mo2LineNumber);
        }
    }
}


static string GetLooseContentDuplicatePolicy(string contentKind)
{
    return IsRuntimeRuleSetContentKind(contentKind)
        ? "Report: loose content runtime rule overlap"
        : "Report: loose content record override";
}

static bool IsRuntimeRuleSetContentKind(string contentKind)
{
    return contentKind.Equals("FlatReplacementRecord", StringComparison.OrdinalIgnoreCase);
}

static string NormalizeLooseJsonForLenientParsing(string text)
{
    // Some DFU loose data files are accepted by their target mod even though they are
    // not strict JSON. The common case seen in FlatReplacements is numeric region
    // values with leading zeroes, e.g. [00, 01, 04]. System.Text.Json correctly
    // rejects those, so normalize only numeric tokens outside strings.
    var builder = new System.Text.StringBuilder(text.Length);
    bool inString = false;
    bool escaped = false;

    for (int i = 0; i < text.Length; i++)
    {
        char c = text[i];

        if (inString)
        {
            builder.Append(c);

            if (escaped)
            {
                escaped = false;
            }
            else if (c == '\\')
            {
                escaped = true;
            }
            else if (c == '"')
            {
                inString = false;
            }

            continue;
        }

        if (c == '"')
        {
            inString = true;
            builder.Append(c);
            continue;
        }

        if (c == '}')
        {
            builder.Append(c);

            // Some loose JSON-like data files contain adjacent objects inside an
            // array without the required comma between them:
            //     }
            //     {
            // Normalize only this structural case while preserving whitespace.
            int next = i + 1;
            while (next < text.Length && char.IsWhiteSpace(text[next]))
                next++;

            if (next < text.Length && text[next] == '{')
                builder.Append(',');

            continue;
        }

        if (c == '-' && i + 2 < text.Length && text[i + 1] == '0' && char.IsDigit(text[i + 2]) && IsLooseJsonNumberTokenStart(text, i))
        {
            int digitStart = i + 1;
            int digitEnd = digitStart;
            while (digitEnd < text.Length && char.IsDigit(text[digitEnd]))
                digitEnd++;

            string digits = text.Substring(digitStart, digitEnd - digitStart).TrimStart('0');
            builder.Append('-');
            builder.Append(digits.Length == 0 ? "0" : digits);
            i = digitEnd - 1;
            continue;
        }

        if (c == '0' && i + 1 < text.Length && char.IsDigit(text[i + 1]) && IsLooseJsonNumberTokenStart(text, i))
        {
            int digitEnd = i;
            while (digitEnd < text.Length && char.IsDigit(text[digitEnd]))
                digitEnd++;

            string digits = text.Substring(i, digitEnd - i).TrimStart('0');
            builder.Append(digits.Length == 0 ? "0" : digits);
            i = digitEnd - 1;
            continue;
        }

        builder.Append(c);
    }

    return builder.ToString();
}

static bool IsLooseJsonNumberTokenStart(string text, int index)
{
    for (int i = index - 1; i >= 0; i--)
    {
        char c = text[i];

        if (char.IsWhiteSpace(c))
            continue;

        return c == '[' || c == '{' || c == ',' || c == ':';
    }

    return true;
}

static IEnumerable<(JsonElement Element, string JsonPath)> EnumerateJsonObjects(JsonElement element, string jsonPath, int depth = 0)
{
    if (depth > 64)
        yield break;

    if (element.ValueKind == JsonValueKind.Object)
    {
        yield return (element, jsonPath);

        foreach (JsonProperty property in element.EnumerateObject())
        {
            foreach ((JsonElement child, string childPath) in EnumerateJsonObjects(property.Value, $"{jsonPath}.{property.Name}", depth + 1))
                yield return (child, childPath);
        }
    }
    else if (element.ValueKind == JsonValueKind.Array)
    {
        int index = 0;
        foreach (JsonElement child in element.EnumerateArray())
        {
            foreach ((JsonElement nestedChild, string childPath) in EnumerateJsonObjects(child, $"{jsonPath}[{index}]", depth + 1))
                yield return (nestedChild, childPath);

            index++;
        }
    }
}

static bool TryBuildLooseJsonContentIdentity(
    LooseFileInventoryRecord fileRecord,
    JsonElement element,
    string jsonPath,
    out string contentKind,
    out string contentIdentityKey,
    out string displayName,
    out string? targetName,
    out string? replacementName,
    out string? portraitName,
    out string? ruleContext)
{
    contentKind = string.Empty;
    contentIdentityKey = string.Empty;
    displayName = string.Empty;
    targetName = null;
    replacementName = null;
    portraitName = null;
    ruleContext = null;

    if (element.ValueKind != JsonValueKind.Object)
        return false;

    if (IsFlatReplacementPath(fileRecord.RelativePath))
    {
        // Flat Replacer rule identity is the original flat being targeted. The
        // replacement flat and talk-window portrait are useful metadata, but they
        // should not be the primary override key.
        int? targetArchive = GetJsonIntAny(element, "TextureArchive", "textureArchive");
        int? targetRecord = GetJsonIntAny(element, "TextureRecord", "textureRecord");
        int? targetFrame = GetJsonIntAny(element, "TextureFrame", "textureFrame", "Frame", "frame");

        if (targetArchive.HasValue && targetRecord.HasValue)
        {
            string framePart = targetFrame.HasValue ? $"-{targetFrame.Value}" : "";
            contentKind = "FlatReplacementRecord";
            contentIdentityKey = $"loosecontent/flatreplacement/target/{targetArchive.Value}_{targetRecord.Value}{framePart}";
            displayName = $"target {targetArchive.Value}_{targetRecord.Value}{framePart}";
            targetName = $"{targetArchive.Value}_{targetRecord.Value}{framePart}";

            replacementName = BuildFlatReplacementReplacementName(element);
            portraitName = BuildFlatReplacementPortraitName(element);
            ruleContext = BuildFlatReplacementRuleContext(element);
            return true;
        }
    }

    string? id = GetJsonScalarStringAny(element, "id", "Id", "ID", "key", "Key", "name", "Name", "FileName", "fileName", "Title", "title");
    if (!string.IsNullOrWhiteSpace(id))
    {
        contentKind = IsFlatReplacementPath(fileRecord.RelativePath) ? "FlatReplacementJsonRecord" : "JsonRecord";
        string domain = BuildLooseJsonContentDomain(fileRecord.RelativePath);
        contentIdentityKey = $"loosecontent/{NormalizeIdentityPart(contentKind)}/{domain}/{NormalizeIdentityPart(id)}";
        displayName = id.Trim();
        return true;
    }

    return false;
}

static bool IsFlatReplacementPath(string relativePath)
{
    return relativePath.Contains("FlatReplacements/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains("FlatReplacement/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains("flatreplacement", StringComparison.OrdinalIgnoreCase);
}

static string? BuildFlatReplacementReplacementName(JsonElement element)
{
    string? flatTextureName = GetJsonScalarStringAny(element, "FlatTextureName", "flatTextureName");
    if (!string.IsNullOrWhiteSpace(flatTextureName) &&
        !flatTextureName.Trim().Equals("-1", StringComparison.OrdinalIgnoreCase))
    {
        return $"custom {flatTextureName.Trim()}";
    }

    int? replacementArchive = GetJsonIntAny(element, "ReplaceTextureArchive", "replaceTextureArchive");
    int? replacementRecord = GetJsonIntAny(element, "ReplaceTextureRecord", "replaceTextureRecord");
    int? replacementFrame = GetJsonIntAny(element, "ReplaceTextureFrame", "replaceTextureFrame");

    if (replacementArchive.HasValue && replacementRecord.HasValue)
    {
        string framePart = replacementFrame.HasValue ? $"-{replacementFrame.Value}" : "";
        return $"{replacementArchive.Value}_{replacementRecord.Value}{framePart}";
    }

    return null;
}

static string? BuildFlatReplacementPortraitName(JsonElement element)
{
    string? portrait = GetJsonScalarStringAny(element, "FlatPortrait", "flatPortrait");

    if (string.IsNullOrWhiteSpace(portrait) || portrait.Trim().Equals("-1", StringComparison.OrdinalIgnoreCase))
        return null;

    return portrait.Trim();
}

static string? BuildFlatReplacementRuleContext(JsonElement element)
{
    var parts = new List<string>();

    AddJsonScalarContextPart(parts, element, "Regions", "regions");
    AddJsonScalarContextPart(parts, element, "LocationTypes", "locationTypes");
    AddJsonScalarContextPart(parts, element, "FactionId", "factionId");
    AddJsonScalarContextPart(parts, element, "BuildingType", "buildingType");
    AddJsonScalarContextPart(parts, element, "SocialGroup", "socialGroup");
    AddJsonScalarContextPart(parts, element, "NameBank", "nameBank");
    AddJsonScalarContextPart(parts, element, "Race", "race");
    AddJsonScalarContextPart(parts, element, "Gender", "gender");
    AddJsonScalarContextPart(parts, element, "QualityMin", "qualityMin");
    AddJsonScalarContextPart(parts, element, "QualityMax", "qualityMax");
    AddJsonScalarContextPart(parts, element, "Priority", "priority");
    AddJsonScalarContextPart(parts, element, "UseExactDimensions", "useExactDimensions");

    return parts.Count == 0 ? null : string.Join("; ", parts);
}

static void AddJsonScalarContextPart(List<string> parts, JsonElement element, string canonicalName, string alternateName)
{
    if (!TryGetJsonPropertyAny(element, out JsonElement property, canonicalName, alternateName))
        return;

    string value = property.ValueKind switch
    {
        JsonValueKind.Array => FormatJsonScalarArray(property),
        JsonValueKind.String => property.GetString() ?? string.Empty,
        JsonValueKind.Number => property.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => property.GetRawText()
    };

    if (!string.IsNullOrWhiteSpace(value))
        parts.Add($"{canonicalName}={value}");
}

static string FormatJsonScalarArray(JsonElement arrayElement)
{
    var values = new List<string>();

    foreach (JsonElement item in arrayElement.EnumerateArray())
    {
        string value = item.ValueKind switch
        {
            JsonValueKind.String => item.GetString() ?? string.Empty,
            JsonValueKind.Number => item.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => item.GetRawText()
        };

        values.Add(value);
    }

    return $"[{string.Join(", ", values)}]";
}

static bool TryGetJsonPropertyAny(JsonElement element, out JsonElement property, params string[] propertyNames)
{
    foreach (string propertyName in propertyNames)
    {
        if (element.TryGetProperty(propertyName, out property))
            return true;
    }

    property = default;
    return false;
}

static string BuildLooseJsonContentDomain(string relativePath)
{
    string normalized = NormalizeRelativePath(relativePath);
    string? directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/');

    if (!string.IsNullOrWhiteSpace(directory))
        return NormalizeIdentityPart(directory);

    string fileName = Path.GetFileNameWithoutExtension(normalized);
    return NormalizeIdentityPart(fileName.Length > 0 ? fileName : normalized);
}

static int? GetJsonIntAny(JsonElement element, params string[] propertyNames)
{
    foreach (string propertyName in propertyNames)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
            continue;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int intValue))
            return intValue;

        if (property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), out int parsedValue))
        {
            return parsedValue;
        }
    }

    return null;
}

static string? GetJsonScalarStringAny(JsonElement element, params string[] propertyNames)
{
    foreach (string propertyName in propertyNames)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
            continue;

        if (property.ValueKind == JsonValueKind.String)
            return property.GetString();

        if (property.ValueKind == JsonValueKind.Number)
            return property.GetRawText();

        if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            return property.GetBoolean().ToString();
    }

    return null;
}

static void PrintLooseContentInventory(
    List<LooseContentRecord> records,
    LooseContentReportOptions options,
    Mo2ModlistLoadOrder mo2LoadOrder,
    string? overwritePath)
{
    List<LooseContentRecord> reportRecords = ApplyLooseContentFilters(records, options);
    List<RecordStack> recordStacks = RecordStackBuilder.FromLooseContentRecords(reportRecords);

    Console.WriteLine("Loose content record inventory");
    Console.WriteLine(new string('=', 80));
    Console.WriteLine($"Loose content records found: {records.Count}");

    if (reportRecords.Count != records.Count)
        Console.WriteLine($"Loose content records matching filters: {reportRecords.Count}");

    Console.WriteLine($"Loose source folders with content records: {reportRecords.Select(r => r.SourceFolderPath).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");
    Console.WriteLine($"Runtime-visible loose source folders with content records: {reportRecords.Where(r => r.LooseFileActive).Select(r => r.SourceFolderPath).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");
    Console.WriteLine($"Generic record stacks built: {recordStacks.Count}");
    Console.WriteLine($"Generic record stacks with multiple source files: {recordStacks.Count(s => s.SourceCount > 1)}");
    Console.WriteLine($"Generic record stacks with multiple active providers: {recordStacks.Count(s => s.ActiveSourceCount > 1)}");
    Console.WriteLine($"Generic record stacks with active field differences: {recordStacks.Count(s => GetLooseContentFieldDifferences(s).Count > 0)}");

    if (!options.IncludeDisabled)
        Console.WriteLine("Filter: runtime-visible loose files only for loose content analysis");

    if (!string.IsNullOrWhiteSpace(options.ContentKindFilter))
        Console.WriteLine($"Filter: Loose content kind = {options.ContentKindFilter}");

    if (!string.IsNullOrWhiteSpace(options.IdentityContainsFilter))
        Console.WriteLine($"Filter: Content identity contains = {options.IdentityContainsFilter}");

    if (!string.IsNullOrWhiteSpace(options.PathContainsFilter))
        Console.WriteLine($"Filter: Relative path contains = {options.PathContainsFilter}");

    Console.WriteLine();
    Console.WriteLine("MO2 loose-content order:");
    Console.WriteLine($"  File: {mo2LoadOrder.Path}");
    Console.WriteLine($"  Entries: {mo2LoadOrder.Entries.Count}");
    Console.WriteLine($"  Enabled entries: {mo2LoadOrder.Entries.Count(e => e.Enabled)}");
    Console.WriteLine($"  Disabled entries: {mo2LoadOrder.Entries.Count(e => !e.Enabled)}");
    Console.WriteLine("  Visibility rule used here: same relative path uses loose-file winner; earlier enabled modlist.txt entries override later entries, and Overwrite is highest priority.");
    Console.WriteLine("  FlatReplacement records do not have a load-order winner. All records from runtime-visible JSON files are candidates; Flat Replacer Priority/random selection decides matching replacements.");
    Console.WriteLine("  Note: This pass reports recognised internal records inside structured loose files, not whole-file path overrides.");

    if (!string.IsNullOrWhiteSpace(overwritePath))
        Console.WriteLine($"  Overwrite folder: {overwritePath}");

    Console.WriteLine();
    Console.WriteLine("Loose content kind counts:");
    foreach (var group in reportRecords
                 .GroupBy(r => r.ContentKind, StringComparer.OrdinalIgnoreCase)
                 .OrderByDescending(g => g.Count())
                 .ThenBy(g => g.Key))
    {
        Console.WriteLine($"  {group.Key}: {group.Count()}");
    }

    Console.WriteLine();
    Console.WriteLine("Loose content source file kind counts:");
    foreach (var group in reportRecords
                 .GroupBy(r => r.SourceFileKind, StringComparer.OrdinalIgnoreCase)
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
    Console.WriteLine("Loose content record stacks:");

    var duplicateGroups = BuildLooseContentDuplicateGroupsFromStacks(recordStacks);
    var recordStacksByIdentity = recordStacks.ToDictionary(s => s.Identity.Key, StringComparer.OrdinalIgnoreCase);

    if (duplicateGroups.Count == 0)
    {
        Console.WriteLine("  None found.");
        return;
    }

    Console.WriteLine($"  Reportable loose content stack group(s): {duplicateGroups.Count(g => g.IsReportable)}");
    Console.WriteLine($"  Groups selected for output: {duplicateGroups.Count}");
    Console.WriteLine();

    PrintLooseContentDuplicateOverview(duplicateGroups, options.MaxDuplicateGroups);

    if (!options.PrintDuplicateDetails)
    {
        Console.WriteLine();
        Console.WriteLine("Loose content duplicate details are suppressed by default to keep reports readable.");
        Console.WriteLine("Use --inventory-details to print grouped loose-content provider stacks.");
        Console.WriteLine("Use --loose-content-kind, --loose-content-identity-contains, or --loose-path-contains to focus the report first.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"Loose content duplicate detail groups shown: {Math.Min(options.MaxDuplicateGroups, duplicateGroups.Count)} of {duplicateGroups.Count}");

    if (options.MaxDuplicateGroups < duplicateGroups.Count)
        Console.WriteLine($"Use --inventory-max-groups <n> to show more groups.");

    Console.WriteLine();

    foreach (LooseContentDuplicateGroup group in duplicateGroups.Take(options.MaxDuplicateGroups))
    {
        LooseContentRecord first = group.Records.First();
        Console.WriteLine($"CONTENT STACK: {group.IdentityKey}");
        Console.WriteLine($"  Content Kind: {first.ContentKind}");
        Console.WriteLine($"  Source File Kind: {first.SourceFileKind}");
        Console.WriteLine($"  Mods: {group.ModCount}");
        Console.WriteLine($"  Files: {group.SourceCount}");

        if (recordStacksByIdentity.TryGetValue(group.IdentityKey, out RecordStack? recordStack))
        {
            List<RecordFieldDifference> fieldDifferences = GetLooseContentFieldDifferences(recordStack);
            if (fieldDifferences.Count > 0)
                Console.WriteLine($"  Active field differences: {FormatRecordFieldDifferenceSummary(fieldDifferences)}");
        }

        var orderedRecords = OrderLooseContentRecordsForDisplay(group.Records).ToList();
        int index = 1;
        foreach (LooseContentRecord record in orderedRecords.Take(options.MaxRecordsPerDuplicateGroup))
        {
            Console.WriteLine($"  {index}. Source: {record.SourceName}");
            Console.WriteLine($"     MO2 Order: {FormatMo2LooseContentOrder(record)}");
            Console.WriteLine($"     Relative Path: {record.RelativePath}");
            Console.WriteLine($"     File: {record.FilePath}");
            Console.WriteLine($"     Content Kind: {record.ContentKind}");
            Console.WriteLine($"     Content: {record.DisplayName}");

            if (!string.IsNullOrWhiteSpace(record.TargetName))
                Console.WriteLine($"     Target: {record.TargetName}");

            if (!string.IsNullOrWhiteSpace(record.ReplacementName))
                Console.WriteLine($"     Replacement: {record.ReplacementName}");

            if (!string.IsNullOrWhiteSpace(record.PortraitName))
                Console.WriteLine($"     Portrait: {record.PortraitName}");

            if (!string.IsNullOrWhiteSpace(record.RuleContext))
                Console.WriteLine($"     Rule Context: {record.RuleContext}");

            Console.WriteLine($"     JSON Path: {record.JsonPath}");
            Console.WriteLine($"     Duplicate Policy: {record.DuplicatePolicy}");
            Console.WriteLine($"     Loose File Visibility: {FormatLooseContentVisibility(record)}");

            if (IsRuntimeRuleSetContentKind(record.ContentKind))
            {
                if (record.LooseFileActive)
                    Console.WriteLine("     Active runtime candidate: yes");
            }
            else if (IsWinningLooseContentRecord(record, group.Records))
            {
                Console.WriteLine("     Winning loose-content override: yes");
            }

            index++;
        }

        int suppressedCount = orderedRecords.Count - Math.Min(options.MaxRecordsPerDuplicateGroup, orderedRecords.Count);
        if (suppressedCount > 0)
            Console.WriteLine($"  ... {suppressedCount} more record(s) suppressed. Use --inventory-max-records <n> to show more per group.");

        Console.WriteLine(new string('-', 80));
    }
}


static List<RecordFieldDifference> GetLooseContentFieldDifferences(RecordStack stack)
{
    return RecordStackAnalyzer.FindActiveFieldDifferences(stack, GetLooseContentSemanticFieldPaths());
}

static IReadOnlySet<string> GetLooseContentSemanticFieldPaths()
{
    return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "replacement",
        "portrait",
        "ruleContext"
    };
}

static string FormatRecordFieldDifferenceSummary(IReadOnlyList<RecordFieldDifference> fieldDifferences)
{
    return string.Join(", ", fieldDifferences
        .OrderBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
        .Select(d => $"{d.DisplayName} ({d.VariantCount} variants)"));
}

static List<LooseContentDuplicateGroup> BuildLooseContentDuplicateGroupsFromStacks(List<RecordStack> recordStacks)
{
    return recordStacks
        .Where(s => s.SourceCount > 1)
        .Select(s => new LooseContentDuplicateGroup(
            IdentityKey: s.Identity.Key,
            Records: s.Records
                .Select(RehydrateLooseContentRecord)
                .Where(record => record != null)
                .Cast<LooseContentRecord>()
                .ToList(),
            SourceCount: s.SourceCount,
            ModCount: s.ModCount,
            IsReportable: s.Records.Any(r =>
                r.Metadata.TryGetValue("DuplicatePolicy", out string? duplicatePolicy) &&
                !string.IsNullOrEmpty(duplicatePolicy) &&
                duplicatePolicy.StartsWith("Report:", StringComparison.OrdinalIgnoreCase))))
        .Where(g => g.Records.Count > 0)
        .OrderByDescending(g => g.IsReportable)
        .ThenBy(g => LooseContentKindSortRank(g.Records.First().ContentKind))
        .ThenByDescending(g => g.ModCount)
        .ThenByDescending(g => g.SourceCount)
        .ThenBy(g => g.IdentityKey)
        .ToList();
}

static LooseContentRecord? RehydrateLooseContentRecord(RecordInstance instance)
{
    var metadata = instance.Metadata;

    if (!metadata.TryGetValue("SourceName", out string? sourceName) ||
        !metadata.TryGetValue("SourceFolderPath", out string? sourceFolderPath) ||
        !metadata.TryGetValue("FilePath", out string? filePath) ||
        !metadata.TryGetValue("RelativePath", out string? relativePath) ||
        !metadata.TryGetValue("SourceFileKind", out string? sourceFileKind) ||
        !metadata.TryGetValue("SourceExtension", out string? sourceExtension) ||
        !metadata.TryGetValue("ContentKind", out string? contentKind) ||
        !metadata.TryGetValue("ContentIdentityKey", out string? contentIdentityKey) ||
        !metadata.TryGetValue("JsonPath", out string? jsonPath) ||
        !metadata.TryGetValue("DisplayName", out string? displayName) ||
        !metadata.TryGetValue("SourceByteLength", out string? sourceByteLengthText) ||
        !metadata.TryGetValue("DuplicatePolicy", out string? duplicatePolicy) ||
        !metadata.TryGetValue("IsOverwrite", out string? isOverwriteText) ||
        !metadata.TryGetValue("LooseFileActive", out string? looseFileActiveText))
    {
        return null;
    }

    long.TryParse(sourceByteLengthText, out long sourceByteLength);
    bool.TryParse(isOverwriteText, out bool isOverwrite);
    bool.TryParse(looseFileActiveText, out bool looseFileActive);

    bool? mo2Enabled = null;
    if (metadata.TryGetValue("Mo2Enabled", out string? mo2EnabledText) &&
        bool.TryParse(mo2EnabledText, out bool parsedMo2Enabled))
    {
        mo2Enabled = parsedMo2Enabled;
    }

    int? mo2ListIndex = null;
    if (metadata.TryGetValue("Mo2ListIndex", out string? mo2ListIndexText) &&
        int.TryParse(mo2ListIndexText, out int parsedMo2ListIndex))
    {
        mo2ListIndex = parsedMo2ListIndex;
    }

    int? mo2LineNumber = null;
    if (metadata.TryGetValue("Mo2LineNumber", out string? mo2LineNumberText) &&
        int.TryParse(mo2LineNumberText, out int parsedMo2LineNumber))
    {
        mo2LineNumber = parsedMo2LineNumber;
    }

    metadata.TryGetValue("Target", out string? targetName);
    metadata.TryGetValue("Replacement", out string? replacementName);
    metadata.TryGetValue("Portrait", out string? portraitName);
    metadata.TryGetValue("RuleContext", out string? ruleContext);
    metadata.TryGetValue("Mo2ModName", out string? mo2ModName);

    return new LooseContentRecord(
        SourceName: sourceName,
        SourceFolderPath: sourceFolderPath,
        FilePath: filePath,
        RelativePath: relativePath,
        SourceFileKind: sourceFileKind,
        SourceExtension: sourceExtension,
        ContentKind: contentKind,
        ContentIdentityKey: contentIdentityKey,
        JsonPath: jsonPath,
        DisplayName: displayName,
        TargetName: targetName,
        ReplacementName: replacementName,
        PortraitName: portraitName,
        RuleContext: ruleContext,
        LooseFileActive: looseFileActive,
        SourceByteLength: sourceByteLength,
        DuplicatePolicy: duplicatePolicy,
        IsOverwrite: isOverwrite,
        Mo2ModName: mo2ModName,
        Mo2Enabled: mo2Enabled,
        Mo2ListIndex: mo2ListIndex,
        Mo2LineNumber: mo2LineNumber);
}

static List<LooseContentRecord> ApplyLooseContentFilters(
    List<LooseContentRecord> records,
    LooseContentReportOptions options)
{
    IEnumerable<LooseContentRecord> query = records;

    if (!options.IncludeDisabled)
        query = query.Where(r => r.LooseFileActive);

    if (!string.IsNullOrWhiteSpace(options.ContentKindFilter))
    {
        query = query.Where(r =>
            r.ContentKind.Equals(options.ContentKindFilter, StringComparison.OrdinalIgnoreCase));
    }

    if (!string.IsNullOrWhiteSpace(options.IdentityContainsFilter))
    {
        query = query.Where(r =>
            r.ContentIdentityKey.Contains(options.IdentityContainsFilter, StringComparison.OrdinalIgnoreCase) ||
            r.DisplayName.Contains(options.IdentityContainsFilter, StringComparison.OrdinalIgnoreCase));
    }

    if (!string.IsNullOrWhiteSpace(options.PathContainsFilter))
    {
        query = query.Where(r =>
            r.RelativePath.Contains(options.PathContainsFilter, StringComparison.OrdinalIgnoreCase));
    }

    return query.ToList();
}

static void PrintLooseContentDuplicateOverview(List<LooseContentDuplicateGroup> duplicateGroups, int maxGroups)
{
    Console.WriteLine($"Top loose content override groups shown: {Math.Min(maxGroups, duplicateGroups.Count)} of {duplicateGroups.Count}");

    if (maxGroups < duplicateGroups.Count)
        Console.WriteLine($"Use --inventory-max-groups <n> to list more groups in the overview/details.");

    Console.WriteLine();

    foreach (LooseContentDuplicateGroup group in duplicateGroups.Take(maxGroups))
    {
        LooseContentRecord first = group.Records.First();
        string resolution = FormatLooseContentResolution(first.ContentKind, group.Records);
        Console.WriteLine($"  CONTENT    {group.ModCount,3} mod(s), {group.SourceCount,3} file(s) | {first.ContentKind} | {group.IdentityKey} | {resolution}");
    }
}

static IEnumerable<LooseContentRecord> OrderLooseContentRecordsForDisplay(IEnumerable<LooseContentRecord> records)
{
    return records
        .OrderBy(r => GetLooseContentWinnerSortValue(r))
        .ThenBy(r => r.SourceName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.RelativePath, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.JsonPath, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase);
}

static int GetLooseContentWinnerSortValue(LooseContentRecord record)
{
    if (record.IsOverwrite)
        return int.MinValue;

    return record.Mo2ListIndex ?? int.MaxValue;
}

static LooseContentRecord? GetWinningLooseContentRecord(IEnumerable<LooseContentRecord> records)
{
    return records
        .Where(r => r.LooseFileActive)
        .OrderBy(GetLooseContentWinnerSortValue)
        .ThenBy(r => r.SourceName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.JsonPath, StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault();
}

static bool IsWinningLooseContentRecord(LooseContentRecord record, IEnumerable<LooseContentRecord> groupRecords)
{
    LooseContentRecord? winner = GetWinningLooseContentRecord(groupRecords);
    return winner != null && winner.Equals(record);
}

static string FormatLooseContentResolution(string contentKind, IEnumerable<LooseContentRecord> records)
{
    if (IsRuntimeRuleSetContentKind(contentKind))
        return "runtime candidates; Priority/random selection";

    return $"winner: {FormatLooseContentWinner(records)}";
}

static string FormatLooseContentVisibility(LooseContentRecord record)
{
    if (record.LooseFileActive)
        return "runtime-visible";

    if (record.Mo2Enabled == false)
        return "inactive (MO2 disabled)";

    return "inactive (loose path overridden)";
}

static string FormatLooseContentWinner(IEnumerable<LooseContentRecord> records)
{
    LooseContentRecord? winner = GetWinningLooseContentRecord(records);

    if (winner == null)
        return "no runtime-visible loose content provider";

    return winner.IsOverwrite
        ? "Overwrite"
        : $"MO2 {winner.Mo2ListIndex}: {winner.Mo2ModName ?? winner.SourceName}";
}

static string FormatMo2LooseContentOrder(LooseContentRecord record)
{
    if (record.IsOverwrite)
        return "Overwrite (highest priority)";

    if (record.Mo2ListIndex.HasValue)
    {
        string enabled = record.Mo2Enabled == true ? "enabled" : "disabled";
        return $"{record.Mo2ListIndex.Value} ({enabled}) {record.Mo2ModName ?? record.SourceName}";
    }

    return "(not matched)";
}


static int LooseContentKindSortRank(string contentKind)
{
    return contentKind switch
    {
        "FlatReplacementRecord" => 0,
        "FlatReplacementJsonRecord" => 1,
        "JsonRecord" => 10,
        _ => 50
    };
}

static void PrintLooseFileInventory(
    List<LooseFileInventoryRecord> records,
    LooseInventoryReportOptions options,
    Mo2ModlistLoadOrder mo2LoadOrder,
    string? overwritePath)
{
    List<LooseFileInventoryRecord> reportRecords = ApplyLooseInventoryFilters(records, options);

    Console.WriteLine("Loose file inventory");
    Console.WriteLine(new string('=', 80));
    Console.WriteLine($"Loose files found: {records.Count}");

    if (reportRecords.Count != records.Count)
        Console.WriteLine($"Loose files matching filters: {reportRecords.Count}");

    Console.WriteLine($"Loose source folders with files: {reportRecords.Select(r => r.ModFolderPath).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");
    Console.WriteLine($"Enabled loose source folders with files: {reportRecords.Where(r => r.Mo2Enabled == true).Select(r => r.ModFolderPath).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");

    if (!options.IncludeDisabled)
        Console.WriteLine("Filter: enabled MO2 mods only for loose file winner/path-conflict analysis");

    if (!string.IsNullOrWhiteSpace(options.FileKindFilter))
        Console.WriteLine($"Filter: Loose file kind = {options.FileKindFilter}");

    if (!string.IsNullOrWhiteSpace(options.ExtensionFilter))
        Console.WriteLine($"Filter: Extension = {NormalizeExtensionFilter(options.ExtensionFilter)}");

    if (!string.IsNullOrWhiteSpace(options.PathContainsFilter))
        Console.WriteLine($"Filter: Relative path contains = {options.PathContainsFilter}");

    Console.WriteLine();
    Console.WriteLine("MO2 loose-file order:");
    Console.WriteLine($"  File: {mo2LoadOrder.Path}");
    Console.WriteLine($"  Entries: {mo2LoadOrder.Entries.Count}");
    Console.WriteLine($"  Enabled entries: {mo2LoadOrder.Entries.Count(e => e.Enabled)}");
    Console.WriteLine($"  Disabled entries: {mo2LoadOrder.Entries.Count(e => !e.Enabled)}");
    Console.WriteLine("  Winner rule used here: earlier enabled modlist.txt entries override later entries for loose files; Overwrite, when present, is treated as highest priority.");
    Console.WriteLine("  Note: This order is only used for loose files. DFU Mods.json still decides .dfmod enabled state and load priority.");
    Console.WriteLine("  This pass reports same-relative-path file overrides only. Content-level loose record merging comes next.");

    if (!string.IsNullOrWhiteSpace(overwritePath))
        Console.WriteLine($"  Overwrite folder: {overwritePath}");

    Console.WriteLine();
    Console.WriteLine("Loose file kind counts:");
    foreach (var group in reportRecords
                 .GroupBy(r => r.FileKind, StringComparer.OrdinalIgnoreCase)
                 .OrderByDescending(g => g.Count())
                 .ThenBy(g => g.Key))
    {
        Console.WriteLine($"  {group.Key}: {group.Count()}");
    }

    Console.WriteLine();
    Console.WriteLine("Loose extension counts:");
    foreach (var group in reportRecords
                 .GroupBy(r => string.IsNullOrWhiteSpace(r.Extension) ? "(none)" : r.Extension, StringComparer.OrdinalIgnoreCase)
                 .OrderByDescending(g => g.Count())
                 .ThenBy(g => g.Key)
                 .Take(50))
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
    Console.WriteLine("Loose path overrides:");

    var duplicateGroups = reportRecords
        .GroupBy(r => r.IdentityKey, StringComparer.OrdinalIgnoreCase)
        .Select(g => new LooseFileDuplicateGroup(
            IdentityKey: g.Key,
            Records: g.ToList(),
            SourceCount: g.Select(r => r.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            ModCount: g.Select(r => r.ModName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            IsReportable: g.Any(r => IsReportableLooseDuplicate(r))))
        .Where(g => g.SourceCount > 1 && g.IsReportable)
        .OrderBy(g => LooseFileKindSortRank(g.Records.First().FileKind))
        .ThenByDescending(g => g.ModCount)
        .ThenByDescending(g => g.SourceCount)
        .ThenBy(g => g.IdentityKey)
        .ToList();

    if (duplicateGroups.Count == 0)
    {
        Console.WriteLine("  None found.");
        return;
    }

    Console.WriteLine($"  Reportable loose path override group(s): {duplicateGroups.Count}");
    Console.WriteLine($"  Groups selected for output: {duplicateGroups.Count}");
    Console.WriteLine();

    PrintLooseDuplicateOverview(duplicateGroups, options.MaxDuplicateGroups);

    if (!options.PrintDuplicateDetails)
    {
        Console.WriteLine();
        Console.WriteLine("Loose duplicate details are suppressed by default to keep reports readable.");
        Console.WriteLine("Use --inventory-details to print grouped loose-file provider stacks.");
        Console.WriteLine("Use --loose-kind, --loose-extension, or --loose-path-contains to focus the report first.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"Loose duplicate detail groups shown: {Math.Min(options.MaxDuplicateGroups, duplicateGroups.Count)} of {duplicateGroups.Count}");

    if (options.MaxDuplicateGroups < duplicateGroups.Count)
        Console.WriteLine($"Use --inventory-max-groups <n> to show more groups.");

    Console.WriteLine();

    foreach (LooseFileDuplicateGroup group in duplicateGroups.Take(options.MaxDuplicateGroups))
    {
        LooseFileInventoryRecord first = group.Records.First();
        Console.WriteLine($"PATH OVERRIDE: {first.RelativePath}");
        Console.WriteLine($"  Kind: {first.FileKind}");
        Console.WriteLine($"  Extension: {(string.IsNullOrWhiteSpace(first.Extension) ? "(none)" : first.Extension)}");
        Console.WriteLine($"  Mods: {group.ModCount}");
        Console.WriteLine($"  Files: {group.SourceCount}");

        int i = 1;
        var orderedRecords = OrderLooseRecordsForDisplay(group.Records).ToList();

        foreach (LooseFileInventoryRecord record in orderedRecords.Take(options.MaxRecordsPerDuplicateGroup))
        {
            Console.WriteLine($"  {i}. Source: {record.ModName}");
            Console.WriteLine($"     MO2 Order: {FormatMo2LooseOrder(record)}");
            Console.WriteLine($"     Relative Path: {record.RelativePath}");
            Console.WriteLine($"     File: {record.FilePath}");
            Console.WriteLine($"     Kind: {record.FileKind}");
            Console.WriteLine($"     Bytes: {record.ByteLength}");
            Console.WriteLine($"     Duplicate Policy: {record.DuplicatePolicy}");

            if (IsWinningLooseRecord(record, group.Records))
                Console.WriteLine("     Winning loose-file override: yes");

            i++;
        }

        if (orderedRecords.Count > options.MaxRecordsPerDuplicateGroup)
            Console.WriteLine($"  ... {orderedRecords.Count - options.MaxRecordsPerDuplicateGroup} more file(s) suppressed. Use --inventory-max-records <n> to show more per group.");

        Console.WriteLine(new string('-', 80));
    }
}

static List<LooseFileInventoryRecord> ApplyLooseInventoryFilters(
    List<LooseFileInventoryRecord> records,
    LooseInventoryReportOptions options)
{
    IEnumerable<LooseFileInventoryRecord> query = records;

    if (!options.IncludeDisabled)
        query = query.Where(r => r.Mo2Enabled == true || r.IsOverwrite);

    if (!string.IsNullOrWhiteSpace(options.FileKindFilter))
    {
        query = query.Where(r =>
            r.FileKind.Equals(options.FileKindFilter, StringComparison.OrdinalIgnoreCase));
    }

    if (!string.IsNullOrWhiteSpace(options.ExtensionFilter))
    {
        string extension = NormalizeExtensionFilter(options.ExtensionFilter);
        query = query.Where(r => r.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    if (!string.IsNullOrWhiteSpace(options.PathContainsFilter))
    {
        query = query.Where(r =>
            r.RelativePath.Contains(options.PathContainsFilter, StringComparison.OrdinalIgnoreCase));
    }

    return query.ToList();
}

static string NormalizeExtensionFilter(string extension)
{
    extension = extension.Trim();

    if (extension.Length == 0)
        return extension;

    return extension.StartsWith(".", StringComparison.OrdinalIgnoreCase)
        ? extension.ToLowerInvariant()
        : $".{extension.ToLowerInvariant()}";
}

static void PrintLooseDuplicateOverview(List<LooseFileDuplicateGroup> duplicateGroups, int maxGroups)
{
    Console.WriteLine($"Top loose path override groups shown: {Math.Min(maxGroups, duplicateGroups.Count)} of {duplicateGroups.Count}");

    if (maxGroups < duplicateGroups.Count)
        Console.WriteLine($"Use --inventory-max-groups <n> to list more groups in the overview/details.");

    Console.WriteLine();

    foreach (LooseFileDuplicateGroup group in duplicateGroups.Take(maxGroups))
    {
        LooseFileInventoryRecord first = group.Records.First();
        string winner = FormatLooseWinner(group.Records);
        Console.WriteLine($"  PATH       {group.ModCount,3} mod(s), {group.SourceCount,3} file(s) | {first.FileKind} | {first.RelativePath} | winner: {winner}");
    }
}

static IEnumerable<LooseFileInventoryRecord> OrderLooseRecordsForDisplay(IEnumerable<LooseFileInventoryRecord> records)
{
    return records
        .OrderBy(r => GetLooseWinnerSortValue(r))
        .ThenBy(r => r.ModName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.RelativePath, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase);
}

static int GetLooseWinnerSortValue(LooseFileInventoryRecord record)
{
    if (record.IsOverwrite)
        return int.MinValue;

    return record.Mo2ListIndex ?? int.MaxValue;
}

static LooseFileInventoryRecord? GetWinningLooseRecord(IEnumerable<LooseFileInventoryRecord> records)
{
    return records
        .Where(r => r.Mo2Enabled == true || r.IsOverwrite)
        .OrderBy(GetLooseWinnerSortValue)
        .ThenBy(r => r.ModName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault();
}

static bool IsWinningLooseRecord(LooseFileInventoryRecord record, IEnumerable<LooseFileInventoryRecord> groupRecords)
{
    LooseFileInventoryRecord? winner = GetWinningLooseRecord(groupRecords);
    return winner != null && winner.Equals(record);
}

static string FormatLooseWinner(IEnumerable<LooseFileInventoryRecord> records)
{
    LooseFileInventoryRecord? winner = GetWinningLooseRecord(records);

    if (winner == null)
        return "no enabled loose provider";

    return winner.IsOverwrite
        ? "Overwrite"
        : $"MO2 {winner.Mo2ListIndex}: {winner.Mo2ModName ?? winner.ModName}";
}

static string FormatMo2LooseOrder(LooseFileInventoryRecord record)
{
    if (record.IsOverwrite)
        return "Overwrite (highest priority)";

    if (record.Mo2ListIndex.HasValue)
    {
        string enabled = record.Mo2Enabled == true ? "enabled" : "disabled";
        return $"{record.Mo2ListIndex.Value} ({enabled}) {record.Mo2ModName ?? record.ModName}";
    }

    return "(not matched)";
}

static bool IsReportableLooseDuplicate(LooseFileInventoryRecord record)
{
    return record.DuplicatePolicy.StartsWith("Report:", StringComparison.OrdinalIgnoreCase);
}

static int LooseFileKindSortRank(string fileKind)
{
    return fileKind switch
    {
        "JsonData" => 0,
        "TextData" => 1,
        "QuestOrQuestText" => 2,
        "BlockLayoutRMB" => 3,
        "DungeonBlockRDB" => 4,
        "PluginBinary" => 5,
        "TextureOrImage" => 20,
        "Audio" => 21,
        "Model" => 22,
        _ => 50
    };
}

static void PrintAssetInventory(
    List<AssetInventoryRecord> records,
    InventoryReportOptions options,
    DfuModsJsonLoadOrder? dfuLoadOrder = null,
    Mo2ModlistLoadOrder? mo2LoadOrder = null)
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

    if (options.ActiveOnly)
        Console.WriteLine("Filter: DFU enabled records only");

    if (dfuLoadOrder != null)
    {
        int matchedDfuRecords = reportRecords.Count(r => r.DfuLoadPriority.HasValue);
        int unmatchedDfuRecords = reportRecords.Count - matchedDfuRecords;

        Console.WriteLine();
        Console.WriteLine("DFU Mods.json load order:");
        Console.WriteLine($"  File: {dfuLoadOrder.Path}");
        Console.WriteLine($"  Entries: {dfuLoadOrder.Entries.Count}");
        Console.WriteLine($"  Enabled entries: {dfuLoadOrder.Entries.Count(e => e.Enabled)}");
        Console.WriteLine($"  Disabled entries: {dfuLoadOrder.Entries.Count(e => !e.Enabled)}");
        Console.WriteLine($"  Records matched by .dfmod filename/title: {matchedDfuRecords}");
        Console.WriteLine($"  Records without DFU load-order match: {unmatchedDfuRecords}");
    }

    if (mo2LoadOrder != null)
    {
        int matchedMo2Records = reportRecords.Count(r => r.Mo2ListIndex.HasValue);
        int unmatchedMo2Records = reportRecords.Count - matchedMo2Records;

        Console.WriteLine();
        Console.WriteLine("MO2 modlist.txt context for .dfmod source folders:");
        Console.WriteLine($"  File: {mo2LoadOrder.Path}");
        Console.WriteLine($"  Entries: {mo2LoadOrder.Entries.Count}");
        Console.WriteLine($"  Enabled entries: {mo2LoadOrder.Entries.Count(e => e.Enabled)}");
        Console.WriteLine($"  Disabled entries: {mo2LoadOrder.Entries.Count(e => !e.Enabled)}");
        Console.WriteLine($"  Records matched by MO2 mod folder: {matchedMo2Records}");
        Console.WriteLine($"  Records without MO2 modlist match: {unmatchedMo2Records}");
        Console.WriteLine("  Note: MO2 context is not used to decide .dfmod winners. DFU Mods.json decides .dfmod enabled state and load priority.");
    }

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
        .ThenBy(g => InventorySemanticSortRank(g.Records.First().SemanticKind))
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
        var orderedRecords = OrderInventoryRecordsForDisplay(group.Records).ToList();

        foreach (AssetInventoryRecord record in orderedRecords.Take(options.MaxRecordsPerDuplicateGroup))
        {
            Console.WriteLine($"  {i}. Mod: {record.ModName}");
            Console.WriteLine($"     DFMod/File: {record.DfmodPath}");
            Console.WriteLine($"     Asset: {record.AssetName}");
            Console.WriteLine($"     Class: {record.ClassName}");
            Console.WriteLine($"     PathID: {record.PathId}");
            Console.WriteLine($"     Duplicate Policy: {record.DuplicatePolicy}");

            if (record.DfuLoadPriority.HasValue)
            {
                string enabled = record.DfuEnabled == true ? "enabled" : "disabled";
                Console.WriteLine($"     DFU Load Order: {record.DfuLoadPriority.Value} ({enabled}) {record.DfuTitle ?? record.DfuFileName ?? "(unknown)"}");
            }
            else
            {
                Console.WriteLine("     DFU Load Order: (not matched)");
            }

            if (record.Mo2ListIndex.HasValue)
            {
                string enabled = record.Mo2Enabled == true ? "enabled" : "disabled";
                Console.WriteLine($"     MO2 Modlist: line/order {record.Mo2ListIndex.Value} ({enabled}) {record.Mo2ModName}");
            }

            if (IsWinningDfuRecord(record, group.Records))
                Console.WriteLine("     Winning enabled DFU override: yes");

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

    if (options.ActiveOnly)
    {
        query = query.Where(r => r.DfuEnabled == true);
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

        string winner = FormatDfuWinner(group.Records);
        Console.WriteLine($"  {label,-10} {group.ModCount,3} mod(s), {group.SourceCount,3} file(s) | {first.ClassName} | {first.SemanticKind} | {group.IdentityKey} | winner: {winner}");
    }
}


static DfuModsJsonLoadOrder? LoadDfuModsJsonLoadOrder(string? path)
{
    if (string.IsNullOrWhiteSpace(path))
        return null;

    if (!File.Exists(path))
    {
        Console.WriteLine($"WARN: DFU Mods.json file does not exist: {path}");
        return null;
    }

    try
    {
        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);

        var entries = new List<DfuModEntry>();
        int index = 0;

        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            string fileName = GetJsonString(element, "FileName") ?? string.Empty;
            string title = GetJsonString(element, "Title") ?? fileName;
            bool enabled = GetJsonBool(element, "Enabled") ?? true;
            int loadPriority = GetJsonInt(element, "LoadPriority") ?? index;

            if (!string.IsNullOrWhiteSpace(fileName) || !string.IsNullOrWhiteSpace(title))
            {
                entries.Add(new DfuModEntry(
                    FileName: fileName.Trim(),
                    Title: title.Trim(),
                    Enabled: enabled,
                    LoadPriority: loadPriority,
                    SourceIndex: index));
            }

            index++;
        }

        return new DfuModsJsonLoadOrder(Path.GetFullPath(path), entries);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WARN: Could not read DFU Mods.json: {path}");
        Console.WriteLine($"      {ex.GetType().Name}: {ex.Message}");
        return null;
    }
}

static Mo2ModlistLoadOrder? LoadMo2ModlistLoadOrder(string? path)
{
    if (string.IsNullOrWhiteSpace(path))
        return null;

    if (!File.Exists(path))
    {
        Console.WriteLine($"WARN: MO2 modlist.txt file does not exist: {path}");
        return null;
    }

    try
    {
        var entries = new List<Mo2ModEntry>();
        int lineNumber = 0;
        int orderIndex = 0;

        foreach (string rawLine in File.ReadLines(path))
        {
            lineNumber++;
            string line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                continue;

            char marker = line[0];
            if (marker != '+' && marker != '-')
                continue;

            string modName = line[1..].Trim();
            if (modName.Length == 0)
                continue;

            bool isSeparator = modName.EndsWith("_separator", StringComparison.OrdinalIgnoreCase);

            entries.Add(new Mo2ModEntry(
                ModName: modName,
                Enabled: marker == '+',
                OrderIndex: orderIndex,
                LineNumber: lineNumber,
                IsSeparator: isSeparator));

            orderIndex++;
        }

        return new Mo2ModlistLoadOrder(Path.GetFullPath(path), entries);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WARN: Could not read MO2 modlist.txt: {path}");
        Console.WriteLine($"      {ex.GetType().Name}: {ex.Message}");
        return null;
    }
}

static string? GetJsonString(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out JsonElement property) &&
           property.ValueKind == JsonValueKind.String
        ? property.GetString()
        : null;
}

static bool? GetJsonBool(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out JsonElement property) &&
           (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
        ? property.GetBoolean()
        : null;
}

static int? GetJsonInt(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out JsonElement property) &&
           property.ValueKind == JsonValueKind.Number &&
           property.TryGetInt32(out int value)
        ? value
        : null;
}

static List<AssetInventoryRecord> ApplyInventoryLoadOrder(
    List<AssetInventoryRecord> records,
    DfuModsJsonLoadOrder? dfuLoadOrder,
    Mo2ModlistLoadOrder? mo2LoadOrder)
{
    if (dfuLoadOrder == null && mo2LoadOrder == null)
        return records;

    return records
        .Select(record =>
        {
            DfuModEntry? dfuEntry = dfuLoadOrder?.FindForDfmod(record.DfmodPath, record.ModName);
            Mo2ModEntry? mo2Entry = mo2LoadOrder?.FindForModFolder(record.ModName);

            return record with
            {
                DfuFileName = dfuEntry?.FileName,
                DfuTitle = dfuEntry?.Title,
                DfuEnabled = dfuEntry?.Enabled,
                DfuLoadPriority = dfuEntry?.LoadPriority,
                Mo2ModName = mo2Entry?.ModName,
                Mo2Enabled = mo2Entry?.Enabled,
                Mo2ListIndex = mo2Entry?.OrderIndex
            };
        })
        .ToList();
}

static IEnumerable<AssetInventoryRecord> OrderInventoryRecordsForDisplay(IEnumerable<AssetInventoryRecord> records)
{
    return records
        .OrderBy(r => r.DfuLoadPriority.HasValue ? 0 : 1)
        .ThenBy(r => r.DfuLoadPriority ?? int.MaxValue)
        .ThenBy(r => r.Mo2ListIndex.HasValue ? 0 : 1)
        .ThenBy(r => r.Mo2ListIndex ?? int.MaxValue)
        .ThenBy(r => r.ModName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.DfmodPath, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.AssetName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.PathId);
}

static AssetInventoryRecord? GetWinningDfuRecord(IEnumerable<AssetInventoryRecord> records)
{
    return records
        .Where(r => r.DfuEnabled == true && r.DfuLoadPriority.HasValue)
        .OrderByDescending(r => r.DfuLoadPriority.GetValueOrDefault())
        .ThenBy(r => r.DfmodPath, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.AssetName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.PathId)
        .FirstOrDefault();
}

static bool IsWinningDfuRecord(AssetInventoryRecord record, IEnumerable<AssetInventoryRecord> groupRecords)
{
    AssetInventoryRecord? winner = GetWinningDfuRecord(groupRecords);
    return winner != null && winner.Equals(record);
}

static string FormatDfuWinner(IEnumerable<AssetInventoryRecord> records)
{
    List<AssetInventoryRecord> list = records.ToList();
    AssetInventoryRecord? winner = GetWinningDfuRecord(list);

    if (winner != null)
        return $"DFU {winner.DfuLoadPriority}: {winner.DfuTitle ?? winner.DfuFileName ?? winner.ModName}";

    if (list.Any(r => r.DfuLoadPriority.HasValue))
        return "no enabled DFU provider";

    return "unknown";
}

static bool IsReportableInventoryDuplicate(AssetInventoryRecord record)
{
    return record.DuplicatePolicy.StartsWith("Report:", StringComparison.OrdinalIgnoreCase);
}

static int InventorySemanticSortRank(string semanticKind)
{
    return semanticKind switch
    {
        "NewLocation" => 0,
        "ExistingLocationOverride" => 1,
        "ItemTemplates" => 2,
        "BlockLayoutRMB" => 3,
        "DungeonBlockRDB" => 4,
        "QuestOrQuestText" => 5,
        "LocalizationOrText" => 6,
        "BookOrTextResource" => 7,
        "UnknownTextAsset" => 8,
        "TextureFrameMetadata" => 9,
        "TextureReplacement" => 20,
        "SpriteReplacement" => 21,
        "MaterialReplacement" => 22,
        "MeshReplacement" => 23,
        "AudioReplacement" => 24,
        "PrefabOrGameObject" => 25,
        _ => 50
    };
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
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --dfu-mods-json <Mods.json> --out <inventory-report.txt>");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --dfu-mods-json <Mods.json> --mo2-modlist <modlist.txt> --inventory-active-only");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --inventory-details --inventory-max-groups 25 --inventory-max-records 5");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --inventory-kind ExistingLocationOverride");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --inventory-kind ItemTemplates --inventory-details");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --inventory-kind BlockLayoutRMB --inventory-max-groups 100");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --inventory-class Texture2D --inventory-details");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --inventory-identity-contains location-11-197");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --inventory --inventory-all-duplicates");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --loose-inventory --mo2-modlist <modlist.txt> --out <loose-report.txt>");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --loose-inventory --mo2-modlist <modlist.txt> --overwrite <overwrite-folder>");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --loose-inventory --mo2-modlist <modlist.txt> --inventory-details --inventory-max-groups 25");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --loose-inventory --mo2-modlist <modlist.txt> --loose-path-contains Textures");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --loose-content --mo2-modlist <modlist.txt> --out <loose-content-report.txt>");
    Console.WriteLine("  DaggerfallEdit.Cli --mods  <mods-root-folder> --loose-content --mo2-modlist <modlist.txt> --loose-content-kind FlatReplacementRecord --inventory-details");
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

public sealed record LooseContentReportOptions(
    bool IncludeDisabled,
    bool PrintDuplicateDetails,
    int MaxDuplicateGroups,
    int MaxRecordsPerDuplicateGroup,
    string? ContentKindFilter,
    string? IdentityContainsFilter,
    string? PathContainsFilter
);

public sealed record LooseContentDuplicateGroup(
    string IdentityKey,
    List<LooseContentRecord> Records,
    int SourceCount,
    int ModCount,
    bool IsReportable
);

public sealed record LooseInventoryReportOptions(
    bool IncludeDisabled,
    bool PrintDuplicateDetails,
    int MaxDuplicateGroups,
    int MaxRecordsPerDuplicateGroup,
    string? FileKindFilter,
    string? ExtensionFilter,
    string? PathContainsFilter
);

public sealed record LooseFileDuplicateGroup(
    string IdentityKey,
    List<LooseFileInventoryRecord> Records,
    int SourceCount,
    int ModCount,
    bool IsReportable
);

public sealed record InventoryReportOptions(
    bool IncludeSuppressedDuplicateGroups,
    bool PrintDuplicateDetails,
    int MaxDuplicateGroups,
    int MaxRecordsPerDuplicateGroup,
    string? SemanticKindFilter,
    string? ClassNameFilter,
    string? IdentityContainsFilter,
    bool ActiveOnly
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
public sealed record DfuModEntry(
    string FileName,
    string Title,
    bool Enabled,
    int LoadPriority,
    int SourceIndex
);

public sealed class DfuModsJsonLoadOrder
{
    private readonly Dictionary<string, DfuModEntry> byFileName;
    private readonly Dictionary<string, DfuModEntry> byTitle;

    public DfuModsJsonLoadOrder(string path, List<DfuModEntry> entries)
    {
        Path = path;
        Entries = entries;
        byFileName = BuildLookup(entries, e => e.FileName);
        byTitle = BuildLookup(entries, e => e.Title);
    }

    public string Path { get; }

    public List<DfuModEntry> Entries { get; }

    public DfuModEntry? FindForDfmod(string dfmodPath, string modName)
    {
        string stem = System.IO.Path.GetFileNameWithoutExtension(dfmodPath);

        return TryFind(stem) ?? TryFind(modName);
    }

    private DfuModEntry? TryFind(string value)
    {
        string key = LoadOrderKey.Normalize(value);

        if (byFileName.TryGetValue(key, out DfuModEntry? byFile))
            return byFile;

        if (byTitle.TryGetValue(key, out DfuModEntry? byTitleEntry))
            return byTitleEntry;

        return null;
    }

    private static Dictionary<string, DfuModEntry> BuildLookup(
        IEnumerable<DfuModEntry> entries,
        Func<DfuModEntry, string> keySelector)
    {
        var result = new Dictionary<string, DfuModEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (DfuModEntry entry in entries)
        {
            string key = LoadOrderKey.Normalize(keySelector(entry));

            if (key.Length == 0)
                continue;

            result.TryAdd(key, entry);
        }

        return result;
    }
}

public sealed record Mo2ModEntry(
    string ModName,
    bool Enabled,
    int OrderIndex,
    int LineNumber,
    bool IsSeparator
);

public sealed class Mo2ModlistLoadOrder
{
    private readonly Dictionary<string, Mo2ModEntry> byModName;

    public Mo2ModlistLoadOrder(string path, List<Mo2ModEntry> entries)
    {
        Path = path;
        Entries = entries;
        byModName = entries
            .Where(e => !e.IsSeparator)
            .GroupBy(e => LoadOrderKey.Normalize(e.ModName), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    public string Path { get; }

    public List<Mo2ModEntry> Entries { get; }

    public IEnumerable<Mo2ModEntry> ModEntries => Entries.Where(e => !e.IsSeparator);

    public IEnumerable<Mo2ModEntry> EnabledModEntries => ModEntries.Where(e => e.Enabled);

    public Mo2ModEntry? FindForModFolder(string modName)
    {
        string key = LoadOrderKey.Normalize(modName);
        return byModName.TryGetValue(key, out Mo2ModEntry? entry)
            ? entry
            : null;
    }
}

public static class LoadOrderKey
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .Replace('\\', '/')
            .ToLowerInvariant();
    }
}
