using System.Text.Json;

namespace DaggerfallEdit.Core;

public sealed record SourceFingerprint(
    long Length,
    long LastWriteUtcTicks
);

public sealed record CachedMapIdScan(
    string SourceKind,
    string SourcePath,
    SourceFingerprint Fingerprint,
    List<MapIdRecord> Records
);

public sealed class ScanCacheData
{
    public int SchemaVersion { get; set; } = ScanCache.CurrentSchemaVersion;
    public List<CachedMapIdScan> Entries { get; set; } = new();
}

public sealed class ScanCache
{
    public const int CurrentSchemaVersion = 3;

    private readonly string cachePath;
    private readonly ScanCacheData data;

    private ScanCache(string cachePath, ScanCacheData data)
    {
        this.cachePath = cachePath;
        this.data = data;
    }

    public static ScanCache Load(string cachePath)
    {
        try
        {
            if (File.Exists(cachePath))
            {
                string json = File.ReadAllText(cachePath);

                ScanCacheData? loaded = JsonSerializer.Deserialize<ScanCacheData>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (loaded != null && loaded.SchemaVersion == CurrentSchemaVersion)
                    return new ScanCache(cachePath, loaded);
            }
        }
        catch
        {
            // Bad cache should not break scanning. Start fresh.
        }

        return new ScanCache(cachePath, new ScanCacheData());
    }

    public void Save()
    {
        string? directory = Path.GetDirectoryName(cachePath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(
            data,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(cachePath, json);
    }

    public bool TryGet(string sourceKind, string sourcePath, out List<MapIdRecord> records)
    {
        records = new List<MapIdRecord>();

        string fullPath = Path.GetFullPath(sourcePath);

        if (!File.Exists(fullPath))
            return false;

        SourceFingerprint current = CreateFingerprint(fullPath);

        CachedMapIdScan? entry = data.Entries.FirstOrDefault(e =>
            string.Equals(e.SourceKind, sourceKind, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
            return false;

        if (entry.Fingerprint.Length != current.Length ||
            entry.Fingerprint.LastWriteUtcTicks != current.LastWriteUtcTicks)
            return false;

        records = entry.Records;
        return true;
    }

    public void Put(string sourceKind, string sourcePath, List<MapIdRecord> records)
    {
        string fullPath = Path.GetFullPath(sourcePath);

        if (!File.Exists(fullPath))
            return;

        SourceFingerprint current = CreateFingerprint(fullPath);

        data.Entries.RemoveAll(e =>
            string.Equals(e.SourceKind, sourceKind, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase));

        data.Entries.Add(new CachedMapIdScan(
            SourceKind: sourceKind,
            SourcePath: fullPath,
            Fingerprint: current,
            Records: records));
    }

    private static SourceFingerprint CreateFingerprint(string path)
    {
        FileInfo info = new FileInfo(path);

        return new SourceFingerprint(
            Length: info.Length,
            LastWriteUtcTicks: info.LastWriteTimeUtc.Ticks);
    }
}