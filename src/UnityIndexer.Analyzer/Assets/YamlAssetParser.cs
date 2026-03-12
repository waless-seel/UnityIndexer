using UnityIndexer.Core.Models;

namespace UnityIndexer.Analyzer.Assets;

/// <summary>
/// Unity の YAML アセットファイル (.prefab / .unity / .mat / .asset) を解析する。
/// Unity の YAML は標準 YAML と異なる独自タグ (!u! 等) を含むため、
/// YamlDotNet の標準パーサーでは解析できない部分がある。
/// そのため、正規表現ベースのスキャンで必要な情報を抽出する。
/// </summary>
public static class YamlAssetParser
{
    // m_Script: {fileID: 11500000, guid: xxxxxxxx, type: 3}
    private static readonly System.Text.RegularExpressions.Regex ScriptRefRegex =
        new(@"m_Script:\s*\{[^}]*guid:\s*([0-9a-fA-F]+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    // fileID: 0, guid: xxxxxxxx, type: 2  (各種アセット参照)
    private static readonly System.Text.RegularExpressions.Regex GuidRefRegex =
        new(@"\bguid:\s*([0-9a-fA-F]{32})\b", System.Text.RegularExpressions.RegexOptions.Compiled);

    // GameObject: {fileID: xxxxxx}  followed by m_Name: "..."
    private static readonly System.Text.RegularExpressions.Regex GameObjectNameRegex =
        new(@"m_Name:\s*(.+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// プレハブまたはシーンファイルを解析してコンポーネント情報と参照一覧を返す。
    /// </summary>
    public static ParsedYamlAsset Parse(string assetGuid, string filePath)
    {
        var result = new ParsedYamlAsset { AssetGuid = assetGuid };

        try
        {
            var lines = File.ReadAllLines(filePath);
            ParseComponents(lines, assetGuid, result);
        }
        catch (IOException)
        {
            // 読み取りエラーは無視
        }

        return result;
    }

    private static void ParseComponents(string[] lines, string assetGuid, ParsedYamlAsset result)
    {
        // Unity YAML はオブジェクトを "--- !u!XXX &YYYYYYY" で区切る
        // MonoBehaviour セクションを探してスクリプト参照を抽出する

        string? currentSection = null;
        string? currentGameObjectName = null;
        bool inMonoBehaviour = false;
        var referencedGuids = new HashSet<string>();

        foreach (var line in lines)
        {
            // セクション区切り
            if (line.StartsWith("--- !u!", StringComparison.Ordinal))
            {
                inMonoBehaviour = line.Contains("!u!114 "); // 114 = MonoBehaviour
                currentSection = line;
                continue;
            }

            // GameObject 名を追跡
            if (line.TrimStart().StartsWith("m_Name:", StringComparison.Ordinal))
            {
                var match = GameObjectNameRegex.Match(line);
                if (match.Success)
                    currentGameObjectName = match.Groups[1].Value.Trim();
            }

            // MonoBehaviour セクション内のスクリプト参照
            if (inMonoBehaviour)
            {
                var scriptMatch = ScriptRefRegex.Match(line);
                if (scriptMatch.Success)
                {
                    var scriptGuid = scriptMatch.Groups[1].Value;
                    result.Components.Add(new ComponentInfo
                    {
                        AssetGuid = assetGuid,
                        GameObjectName = currentGameObjectName ?? "Unknown",
                        ScriptGuid = scriptGuid,
                    });
                    result.References.Add(new AssetReference
                    {
                        FromGuid = assetGuid,
                        ToGuid = scriptGuid,
                        RefType = ReferenceType.Script,
                        GameObjectName = currentGameObjectName,
                    });
                    referencedGuids.Add(scriptGuid);
                }
            }

            // 全 GUID 参照を収集（スクリプト以外も含む）
            foreach (System.Text.RegularExpressions.Match m in GuidRefRegex.Matches(line))
            {
                var guid = m.Groups[1].Value;
                if (guid != assetGuid && referencedGuids.Add(guid))
                {
                    // すでに Script として追加済みのものは除外
                    if (!result.References.Exists(r => r.ToGuid == guid && r.RefType == ReferenceType.Script))
                    {
                        result.References.Add(new AssetReference
                        {
                            FromGuid = assetGuid,
                            ToGuid = guid,
                            RefType = DetectRefType(line),
                            GameObjectName = currentGameObjectName,
                        });
                    }
                }
            }
        }
    }

    private static ReferenceType DetectRefType(string line)
    {
        if (line.Contains("m_Texture") || line.Contains("_MainTex") || line.Contains("_BumpMap"))
            return ReferenceType.Texture;
        if (line.Contains("m_Material") || line.Contains("Materials"))
            return ReferenceType.Material;
        if (line.Contains("m_PrefabInstance") || line.Contains("m_SourcePrefab"))
            return ReferenceType.Prefab;
        if (line.Contains("m_AudioClip"))
            return ReferenceType.AudioClip;
        if (line.Contains("m_AnimationClips") || line.Contains("m_Clip"))
            return ReferenceType.AnimationClip;
        if (line.Contains("m_Controller"))
            return ReferenceType.AnimatorController;
        if (line.Contains("m_Shader"))
            return ReferenceType.Shader;
        return ReferenceType.Other;
    }
}

/// <summary>YAML アセット解析結果</summary>
public sealed class ParsedYamlAsset
{
    public required string AssetGuid { get; init; }
    public List<ComponentInfo> Components { get; } = [];
    public List<AssetReference> References { get; } = [];
}
