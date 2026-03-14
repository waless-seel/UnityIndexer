using UnityIndexer.Core.Models;
using UnityIndexer.Core.Utilities;

namespace UnityIndexer.Analyzer.Assets;

/// <summary>
/// Unity .meta ファイルを解析して GUID を抽出する。
/// .meta は YAML だが guid 行だけ読めば十分なため、YamlDotNet を使わず
/// 単純な行スキャンで処理する（パフォーマンス優先）。
/// </summary>
public static class MetaFileParser
{
    /// <summary>
    /// .meta ファイルから GUID を読み取り、対応するアセット情報を返す。
    /// アセットファイルは .meta を除いたパスに存在する前提。
    /// </summary>
    /// <param name="metaFilePath">.meta ファイルの絶対パス</param>
    /// <param name="projectRoot">プロジェクトルートの絶対パス</param>
    /// <returns>解析結果。GUID が見つからない場合は null。</returns>
    public static AssetInfo? Parse(string metaFilePath, string projectRoot)
    {
        var assetPath = metaFilePath[..^5]; // ".meta" を除去
        if (!File.Exists(assetPath))
            return null;

        var guid = ReadGuid(metaFilePath);
        if (guid is null)
            return null;

        var relativePath = Path.GetRelativePath(projectRoot, assetPath)
            .Replace('\\', '/');

        var fileInfo = new FileInfo(assetPath);
        return new AssetInfo
        {
            Guid = guid,
            RelativePath = relativePath,
            Type = AssetTypeDetector.Detect(assetPath),
            FileSizeBytes = fileInfo.Length,
            LastModified = fileInfo.LastWriteTimeUtc,
            IndexedAt = DateTime.UtcNow,
        };
    }

    private static string? ReadGuid(string metaFilePath)
    {
        try
        {
            foreach (var line in File.ReadLines(metaFilePath))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("guid:", StringComparison.Ordinal))
                {
                    var guid = trimmed["guid:".Length..].Trim();
                    return guid.Length > 0 ? guid : null;
                }
            }
        }
        catch (IOException)
        {
            // ファイル読み取りエラーは無視
        }
        return null;
    }
}
