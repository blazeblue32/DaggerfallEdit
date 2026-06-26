using System.Reflection;
using DaggerfallEdit.Core;

namespace DaggerfallEdit.Vanilla;

public static class VanillaMapIdExtractor
{
    public static List<MapIdRecord> ExtractVanillaMapIds(string arena2Path)
    {
        string mapsPath = Path.Combine(arena2Path, "MAPS.BSA");

        if (!File.Exists(mapsPath))
            throw new FileNotFoundException("Could not find MAPS.BSA.", mapsPath);

        string managedDir = FindManagedDirectory(arena2Path);
        string assemblyPath = Path.Combine(managedDir, "Assembly-CSharp.dll");

        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException("Could not find Assembly-CSharp.dll.", assemblyPath);

        RegisterAssemblyResolver(managedDir);

        Assembly assembly = Assembly.LoadFrom(assemblyPath);

        Type mapsFileType = assembly.GetType("DaggerfallConnect.Arena2.MapsFile")
            ?? throw new InvalidOperationException("Could not find DaggerfallConnect.Arena2.MapsFile.");

        Type fileUsageType = assembly.GetType("DaggerfallConnect.FileUsage")
            ?? throw new InvalidOperationException("Could not find DaggerfallConnect.FileUsage.");

        object useMemory = Enum.Parse(fileUsageType, "UseMemory");

        object mapsFile = Activator.CreateInstance(mapsFileType)
            ?? throw new InvalidOperationException("Could not create MapsFile instance.");

        MethodInfo loadMethod = mapsFileType.GetMethod("Load")
            ?? throw new InvalidOperationException("Could not find MapsFile.Load().");

        bool loaded = (bool)(loadMethod.Invoke(mapsFile, new[] { mapsPath, useMemory, true }) ?? false);

        if (!loaded)
            throw new InvalidOperationException($"MapsFile.Load() failed for: {mapsPath}");

        PropertyInfo? autoDiscardProperty = mapsFileType.GetProperty("AutoDiscard");
        autoDiscardProperty?.SetValue(mapsFile, false);

        PropertyInfo regionCountProperty = mapsFileType.GetProperty("RegionCount")
            ?? throw new InvalidOperationException("Could not find MapsFile.RegionCount.");

        int regionCount = Convert.ToInt32(regionCountProperty.GetValue(mapsFile));

        MethodInfo getRegionMethod = mapsFileType.GetMethod("GetRegion", new[] { typeof(int) })
            ?? throw new InvalidOperationException("Could not find MapsFile.GetRegion(int).");

        var records = new List<MapIdRecord>();

        for (int regionIndex = 0; regionIndex < regionCount; regionIndex++)
        {
            object? region = getRegionMethod.Invoke(mapsFile, new object[] { regionIndex });

            if (region == null)
                continue;

            string regionName = GetMemberValue(region, "Name")?.ToString() ?? $"Region {regionIndex}";

            Array? mapTable = GetMemberValue(region, "MapTable") as Array;
            Array? mapNames = GetMemberValue(region, "MapNames") as Array;

            if (mapTable == null)
                continue;

            for (int locationIndex = 0; locationIndex < mapTable.Length; locationIndex++)
            {
                object? mapTableEntry = mapTable.GetValue(locationIndex);

                if (mapTableEntry == null)
                    continue;

                int mapId = Convert.ToInt32(GetMemberValue(mapTableEntry, "MapId") ?? 0);
                int longitude = Convert.ToInt32(GetMemberValue(mapTableEntry, "Longitude") ?? 0);
                int latitude = Convert.ToInt32(GetMemberValue(mapTableEntry, "Latitude") ?? 0);

                string? locationName = null;

                if (mapNames != null && locationIndex < mapNames.Length)
                    locationName = mapNames.GetValue(locationIndex)?.ToString();

                records.Add(new MapIdRecord(
                    MapId: mapId,
                    X: null,
                    Y: null,
                    Longitude: longitude,
                    Latitude: latitude,
                    ModName: "[Vanilla MAPS.BSA]",
                    DfmodPath: mapsPath,
                    AssetName: $"vanilla-location-{regionIndex}-{locationIndex}",
                    JsonPath: $"{regionName}[{regionIndex}].Location[{locationIndex}]",
                    RecordName: locationName,
                    AssetKind: "Baseline",
                    Source: "Vanilla MAPS.BSA"));
            }
        }

        return records;
    }

    private static object? GetMemberValue(object obj, string name)
    {
        Type type = obj.GetType();

        PropertyInfo? property = type.GetProperty(
            name,
            BindingFlags.Public | BindingFlags.Instance);

        if (property != null)
            return property.GetValue(obj);

        FieldInfo? field = type.GetField(
            name,
            BindingFlags.Public | BindingFlags.Instance);

        if (field != null)
            return field.GetValue(obj);

        return null;
    }

    private static string FindManagedDirectory(string arena2Path)
    {
        DirectoryInfo? current = new DirectoryInfo(Path.GetFullPath(arena2Path));

        while (current != null)
        {
            string directManaged = Path.Combine(current.FullName, "Managed");
            string directAssembly = Path.Combine(directManaged, "Assembly-CSharp.dll");

            if (File.Exists(directAssembly))
                return directManaged;

            string unityDataManaged = Path.Combine(current.FullName, "DaggerfallUnity_Data", "Managed");
            string unityDataAssembly = Path.Combine(unityDataManaged, "Assembly-CSharp.dll");

            if (File.Exists(unityDataAssembly))
                return unityDataManaged;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find DaggerfallUnity_Data\\Managed or Managed containing Assembly-CSharp.dll.");
    }

    private static void RegisterAssemblyResolver(string managedDir)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            string assemblyName = new AssemblyName(args.Name).Name ?? string.Empty;
            string candidate = Path.Combine(managedDir, assemblyName + ".dll");

            if (File.Exists(candidate))
                return Assembly.LoadFrom(candidate);

            return null;
        };
    }
}