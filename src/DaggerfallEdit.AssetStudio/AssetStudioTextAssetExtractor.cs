using System.Text;
using AS = global::AssetStudio;
using DaggerfallEdit.Core;

namespace DaggerfallEdit.AssetStudio;

public static class AssetStudioTextAssetExtractor
{
    public static IEnumerable<TextAssetInfo> ExtractTextAssets(string modName, string dfmodPath)
    {
        var manager = new AS.AssetsManager();

        manager.LoadFiles(dfmodPath);

        foreach (var assetsFile in manager.assetsFileList)
        {
            foreach (var obj in assetsFile.Objects)
            {
                if (obj is not AS.TextAsset textAsset)
                    continue;

                string assetName = string.IsNullOrWhiteSpace(textAsset.m_Name)
                    ? $"TextAsset_{textAsset.m_PathID}"
                    : textAsset.m_Name;

                string text = DecodeTextAsset(textAsset.m_Script);

                yield return new TextAssetInfo(
                    ModName: modName,
                    DfmodPath: dfmodPath,
                    AssetName: assetName,
                    Text: text);
            }
        }
    }

    private static string DecodeTextAsset(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        return Encoding.UTF8.GetString(bytes);
    }
}