using DaggerfallEdit.Core;

namespace DaggerfallEdit.Vanilla;

public static class VanillaMapIdExtractor
{
    public static List<MapIdRecord> ExtractVanillaMapIds(string arena2Path)
    {
        return MapsBsaBaselineExtractor.ExtractVanillaMapIds(arena2Path);
    }
}
