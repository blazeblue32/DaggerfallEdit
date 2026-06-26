using System.Reflection;
using DaggerfallEdit.Core;
using AS = global::AssetStudio;

namespace DaggerfallEdit.AssetStudio;

public static class AssetStudioAssetInventoryExtractor
{
    public static IEnumerable<AssetInventoryRecord> ExtractAssets(string modName, string dfmodPath)
    {
        var manager = new AS.AssetsManager();

        manager.LoadFiles(dfmodPath);

        foreach (var assetsFile in manager.assetsFileList)
        {
            string? containerPath =
                TryGetStringMember(assetsFile, "originalPath") ??
                TryGetStringMember(assetsFile, "fullName") ??
                TryGetStringMember(assetsFile, "fileName");

            foreach (object obj in assetsFile.Objects)
            {
                string className = GetUnityClassName(obj);
                long pathId = TryGetLongMember(obj, "m_PathID") ?? 0;
                string? rawName = TryGetStringMember(obj, "m_Name");
                bool hasStableName = !string.IsNullOrWhiteSpace(rawName);

                string assetName = hasStableName
                    ? rawName!.Trim()
                    : $"{className}_{pathId}";

                string semanticKind = ClassifySemanticKind(className, assetName);
                string duplicatePolicy = ClassifyDuplicatePolicy(className, assetName, semanticKind, hasStableName);
                string identityKey = BuildIdentityKey(className, assetName, semanticKind, hasStableName, pathId);

                yield return new AssetInventoryRecord(
                    ModName: modName,
                    DfmodPath: dfmodPath,
                    AssetName: assetName,
                    ClassName: className,
                    PathId: pathId,
                    SemanticKind: semanticKind,
                    IdentityKey: identityKey,
                    ByteLength: TryGetKnownPayloadLength(obj, className),
                    ContainerPath: containerPath,
                    HasStableName: hasStableName,
                    DuplicatePolicy: duplicatePolicy,
                    DfuFileName: null,
                    DfuTitle: null,
                    DfuEnabled: null,
                    DfuLoadPriority: null,
                    Mo2ModName: null,
                    Mo2Enabled: null,
                    Mo2ListIndex: null);
            }
        }
    }

    private static string GetUnityClassName(object obj)
    {
        string typeName = obj.GetType().Name;

        return string.IsNullOrWhiteSpace(typeName)
            ? "Unknown"
            : typeName;
    }

    private static string ClassifySemanticKind(string className, string assetName)
    {
        if (className.Equals("TextAsset", StringComparison.OrdinalIgnoreCase))
        {
            if (IsKnownModMetadataAsset(assetName))
                return "ModMetadata";

            if (assetName.StartsWith("locationnew-", StringComparison.OrdinalIgnoreCase))
                return "NewLocation";

            if (LooksLikeExistingLocationAsset(assetName))
                return "ExistingLocationOverride";

            if (assetName.Equals("ItemTemplates", StringComparison.OrdinalIgnoreCase))
                return "ItemTemplates";

            if (LooksLikeClassicRmbBlockAsset(assetName))
                return "BlockLayoutRMB";

            if (LooksLikeClassicRdbBlockAsset(assetName))
                return "DungeonBlockRDB";

            if (LooksLikeTextureFrameMetadata(assetName))
                return "TextureFrameMetadata";

            if (LooksLikeQuestAsset(assetName))
                return "QuestOrQuestText";

            if (LooksLikeBookOrTextResource(assetName))
                return "BookOrTextResource";

            if (LooksLikeLocalizationAsset(assetName))
                return "LocalizationOrText";

            return "UnknownTextAsset";
        }

        return className switch
        {
            "Texture2D" => "TextureReplacement",
            "Sprite" => "SpriteReplacement",
            "AudioClip" => "AudioReplacement",
            "Mesh" => "MeshReplacement",
            "Material" => "MaterialReplacement",
            "Shader" => "ShaderReplacement",
            "GameObject" => "PrefabOrGameObject",
            "MonoBehaviour" => "MonoBehaviourData",
            "MonoScript" => "ScriptReference",
            "AnimationClip" => "AnimationReplacement",
            "Animator" => "AnimatorData",
            "AnimatorController" => "AnimatorControllerData",
            "AssetBundle" => "AssetBundleMetadata",
            _ => "UnityAsset"
        };
    }

    private static string ClassifyDuplicatePolicy(
        string className,
        string assetName,
        string semanticKind,
        bool hasStableName)
    {
        if (!hasStableName)
            return "Suppressed: unnamed Unity object";

        if (semanticKind.Equals("ModMetadata", StringComparison.OrdinalIgnoreCase))
            return "Suppressed: per-mod metadata/settings asset";

        if (semanticKind.Equals("AssetBundleMetadata", StringComparison.OrdinalIgnoreCase))
            return "Suppressed: AssetBundle metadata";

        if (semanticKind.Equals("ShaderReplacement", StringComparison.OrdinalIgnoreCase) ||
            semanticKind.Equals("ScriptReference", StringComparison.OrdinalIgnoreCase))
        {
            return "Suppressed: embedded Unity dependency/reference";
        }

        if (semanticKind.Equals("MonoBehaviourData", StringComparison.OrdinalIgnoreCase) ||
            semanticKind.Equals("AnimatorData", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Transform", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("MeshFilter", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("MeshRenderer", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("SkinnedMeshRenderer", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Object", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Avatar", StringComparison.OrdinalIgnoreCase))
        {
            return "Suppressed: Unity component/internal object";
        }

        return "Report: potential override identity";
    }

    private static bool IsKnownModMetadataAsset(string assetName)
    {
        return assetName.Equals("modsettings", StringComparison.OrdinalIgnoreCase) ||
               assetName.Equals("modpresets", StringComparison.OrdinalIgnoreCase) ||
               assetName.Equals("modmanifest", StringComparison.OrdinalIgnoreCase) ||
               assetName.Equals("modinfo", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeExistingLocationAsset(string assetName)
    {
        if (!assetName.StartsWith("location-", StringComparison.OrdinalIgnoreCase))
            return false;

        string[] parts = assetName.Split('-');

        return parts.Length >= 3 &&
               int.TryParse(parts[1], out _) &&
               int.TryParse(parts[2], out _);
    }

    private static bool LooksLikeClassicRmbBlockAsset(string assetName)
    {
        return assetName.EndsWith(".RMB", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeClassicRdbBlockAsset(string assetName)
    {
        return assetName.EndsWith(".RDB", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeTextureFrameMetadata(string assetName)
    {
        string[] parts = assetName.Split('_', '-', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 3 &&
               int.TryParse(parts[0], out _) &&
               int.TryParse(parts[1], out _) &&
               int.TryParse(parts[2], out _);
    }

    private static bool LooksLikeQuestAsset(string assetName)
    {
        return assetName.Contains("quest", StringComparison.OrdinalIgnoreCase) ||
               assetName.StartsWith("QRC", StringComparison.OrdinalIgnoreCase) ||
               assetName.StartsWith("QBN", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeBookOrTextResource(string assetName)
    {
        return assetName.Contains("book", StringComparison.OrdinalIgnoreCase) ||
               assetName.Contains("biog", StringComparison.OrdinalIgnoreCase) ||
               assetName.EndsWith(".TXT", StringComparison.OrdinalIgnoreCase) ||
               assetName.EndsWith(".RSC", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeLocalizationAsset(string assetName)
    {
        return assetName.Contains("local", StringComparison.OrdinalIgnoreCase) ||
               assetName.Contains("lang", StringComparison.OrdinalIgnoreCase) ||
               assetName.Contains("translation", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildIdentityKey(
        string className,
        string assetName,
        string semanticKind,
        bool hasStableName,
        long pathId)
    {
        string normalizedClass = NormalizeIdentityPart(className);
        string normalizedName = NormalizeIdentityPart(assetName);

        if (!hasStableName)
            normalizedName = $"<unnamed:{pathId}>";

        return $"{normalizedClass}/{semanticKind}/{normalizedName}";
    }

    private static string NormalizeIdentityPart(string value)
    {
        return value.Trim().Replace('\\', '/').ToLowerInvariant();
    }

    private static long? TryGetKnownPayloadLength(object obj, string className)
    {
        if (className.Equals("TextAsset", StringComparison.OrdinalIgnoreCase) &&
            TryGetByteArrayMember(obj, "m_Script") is { } scriptBytes)
        {
            return scriptBytes.LongLength;
        }

        if (className.Equals("Texture2D", StringComparison.OrdinalIgnoreCase) &&
            TryGetByteArrayMember(obj, "image_data") is { } imageBytes)
        {
            return imageBytes.LongLength;
        }

        if (className.Equals("AudioClip", StringComparison.OrdinalIgnoreCase) &&
            TryGetByteArrayMember(obj, "m_AudioData") is { } audioBytes)
        {
            return audioBytes.LongLength;
        }

        return null;
    }

    private static string? TryGetStringMember(object obj, string name)
    {
        object? value = TryGetMemberValue(obj, name);
        return value as string;
    }

    private static long? TryGetLongMember(object obj, string name)
    {
        object? value = TryGetMemberValue(obj, name);

        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            uint uintValue => uintValue,
            ulong ulongValue when ulongValue <= long.MaxValue => (long)ulongValue,
            _ => null
        };
    }

    private static byte[]? TryGetByteArrayMember(object obj, string name)
    {
        object? value = TryGetMemberValue(obj, name);
        return value as byte[];
    }

    private static object? TryGetMemberValue(object obj, string name)
    {
        Type? type = obj.GetType();

        while (type != null)
        {
            FieldInfo? field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null)
                return field.GetValue(obj);

            PropertyInfo? property = type.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property != null && property.GetIndexParameters().Length == 0)
                return property.GetValue(obj);

            type = type.BaseType;
        }

        return null;
    }
}
