using System.Text.Json;
using System.Text.RegularExpressions;

namespace DaggerfallEdit.Core;

public static class MapIdScanner
{
    public static IEnumerable<MapIdRecord> ExtractMapIds(TextAssetInfo asset)
    {
        if (string.IsNullOrWhiteSpace(asset.Text))
            yield break;

        if (!LooksTextual(asset.Text))
            yield break;

        bool parsedJson = false;
        List<MapIdRecord>? jsonRecords = null;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(asset.Text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            jsonRecords = WalkJson(asset, doc.RootElement, "$").ToList();
            parsedJson = true;
        }
        catch
        {
            // Not valid JSON. Fall back to loose regex scanning below.
        }

        if (parsedJson && jsonRecords != null)
        {
            foreach (MapIdRecord record in jsonRecords)
                yield return record;

            yield break;
        }

        foreach (MapIdRecord record in RegexFallback(asset))
            yield return record;
    }

    private static IEnumerable<MapIdRecord> WalkJson(TextAssetInfo asset, JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                int? mapId = TryGetIntProperty(element, "MapId")
                          ?? TryGetIntProperty(element, "mapId")
                          ?? TryGetIntProperty(element, "MapID")
                          ?? TryGetIntProperty(element, "mapID");

                int? longitude = TryGetIntProperty(element, "Longitude")
                              ?? TryGetIntProperty(element, "longitude");

                int? latitude = TryGetIntProperty(element, "Latitude")
                             ?? TryGetIntProperty(element, "latitude");

                string? recordName = TryGetStringProperty(element, "Name")
                                  ?? TryGetStringProperty(element, "name")
                                  ?? TryGetStringProperty(element, "LocationName")
                                  ?? TryGetStringProperty(element, "locationName");

                if (mapId.HasValue)
                {
                    yield return FromLocationMapId(asset, mapId.Value, path, recordName, "MapId field");
                }
                else if (longitude.HasValue && latitude.HasValue)
                {
                    int derived = DeriveMapId(longitude.Value, latitude.Value);

                    yield return FromWorldCoordinates(
                        asset,
                        derived,
                        longitude.Value,
                        latitude.Value,
                        path,
                        recordName,
                        "Longitude/Latitude derived");
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string childPath = path + "." + property.Name;

                    foreach (MapIdRecord record in WalkJson(asset, property.Value, childPath))
                        yield return record;
                }

                break;
            }

            case JsonValueKind.Array:
            {
                int index = 0;

                foreach (JsonElement child in element.EnumerateArray())
                {
                    foreach (MapIdRecord record in WalkJson(asset, child, $"{path}[{index}]"))
                        yield return record;

                    index++;
                }

                break;
            }
        }
    }

    private static IEnumerable<MapIdRecord> RegexFallback(TextAssetInfo asset)
    {
        var mapRegex = new Regex(
            @"(?i)[""']?\bmapid\b[""']?\s*[:=]\s*(\d+)",
            RegexOptions.Compiled);

        foreach (Match match in mapRegex.Matches(asset.Text))
        {
            if (int.TryParse(match.Groups[1].Value, out int mapId))
            {
                yield return FromLocationMapId(
                    asset,
                    mapId,
                    $"regex:line:{LineNumber(asset.Text, match.Index)}",
                    null,
                    "Regex MapId");
            }
        }

        var lonRegex = new Regex(
            @"(?i)[""']?\blongitude\b[""']?\s*[:=]\s*(-?\d+)",
            RegexOptions.Compiled);

        var latRegex = new Regex(
            @"(?i)[""']?\blatitude\b[""']?\s*[:=]\s*(-?\d+)",
            RegexOptions.Compiled);

        Match lon = lonRegex.Match(asset.Text);
        Match lat = latRegex.Match(asset.Text);

        if (lon.Success &&
            lat.Success &&
            int.TryParse(lon.Groups[1].Value, out int longitude) &&
            int.TryParse(lat.Groups[1].Value, out int latitude))
        {
            int mapId = DeriveMapId(longitude, latitude);

            yield return FromWorldCoordinates(
                asset,
                mapId,
                longitude,
                latitude,
                $"regex:line:{LineNumber(asset.Text, lon.Index)}",
                null,
                "Regex Longitude/Latitude derived");
        }
    }

    private static MapIdRecord FromLocationMapId(
        TextAssetInfo asset,
        int mapId,
        string path,
        string? recordName,
        string source)
    {
        return new MapIdRecord(
            MapId: mapId,
            X: null,
            Y: null,
            Longitude: null,
            Latitude: null,
            ModName: asset.ModName,
            DfmodPath: asset.DfmodPath,
            AssetName: asset.AssetName,
            JsonPath: path,
            RecordName: recordName,
            AssetKind: ClassifyAssetKind(asset.AssetName),
            Source: source);
    }

    private static MapIdRecord FromWorldCoordinates(
        TextAssetInfo asset,
        int mapId,
        int longitude,
        int latitude,
        string path,
        string? recordName,
        string source)
    {
        int x = longitude / 128;
        int y = 499 - (latitude / 128);

        return new MapIdRecord(
            MapId: mapId,
            X: x,
            Y: y,
            Longitude: longitude,
            Latitude: latitude,
            ModName: asset.ModName,
            DfmodPath: asset.DfmodPath,
            AssetName: asset.AssetName,
            JsonPath: path,
            RecordName: recordName,
            AssetKind: ClassifyAssetKind(asset.AssetName),
            Source: source);
    }

    public static int DeriveMapId(int longitude, int latitude)
    {
        int x = longitude / 128;
        int y = 499 - (latitude / 128);

        return y * 1000 + x;
    }

    private static string ClassifyAssetKind(string assetName)
    {
        if (assetName.StartsWith("locationnew-", StringComparison.OrdinalIgnoreCase))
            return "NewLocation";

        if (Regex.IsMatch(assetName, @"^location-\d+-\d+$", RegexOptions.IgnoreCase))
            return "ExistingLocationOverride";

        return "Unknown";
    }

    private static int? TryGetIntProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result))
            return result;

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), out int parsed))
            return parsed;

        return null;
    }

    private static string? TryGetStringProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
            return null;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool LooksTextual(string text)
    {
        int sample = Math.Min(text.Length, 4096);
        int controlCount = 0;

        for (int i = 0; i < sample; i++)
        {
            char c = text[i];

            if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                controlCount++;
        }

        return controlCount < sample / 20;
    }

    private static int LineNumber(string text, int index)
    {
        int line = 1;

        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
                line++;
        }

        return line;
    }
}