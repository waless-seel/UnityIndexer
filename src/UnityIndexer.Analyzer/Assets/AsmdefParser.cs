using System.Text.Json;
using UnityIndexer.Core.Models;

namespace UnityIndexer.Analyzer.Assets;

/// <summary>
/// Unity Assembly Definition (.asmdef) ファイルを解析する。
/// .asmdef は JSON フォーマット。
/// </summary>
public static class AsmdefParser
{
    public static AssemblyDefinitionInfo? Parse(string assetGuid, string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var name = root.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString() ?? Path.GetFileNameWithoutExtension(filePath)
                : Path.GetFileNameWithoutExtension(filePath);

            var references = ReadStringArray(root, "references");
            var includePlatforms = ReadStringArray(root, "includePlatforms");
            var excludePlatforms = ReadStringArray(root, "excludePlatforms");

            var allowUnsafe = root.TryGetProperty("allowUnsafeCode", out var unsafeProp)
                && unsafeProp.GetBoolean();

            var autoRef = !root.TryGetProperty("autoReferenced", out var autoRefProp)
                || autoRefProp.GetBoolean();

            return new AssemblyDefinitionInfo
            {
                AssetGuid = assetGuid,
                AssemblyName = name,
                References = references,
                IncludePlatforms = includePlatforms,
                ExcludePlatforms = excludePlatforms,
                AllowUnsafeCode = allowUnsafe,
                AutoReferenced = autoRef,
            };
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            var s = item.GetString();
            if (s is not null)
                list.Add(s);
        }
        return list;
    }
}
