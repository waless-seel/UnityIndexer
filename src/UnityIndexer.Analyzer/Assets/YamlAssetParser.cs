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

    // guid: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx (32桁)
    private static readonly System.Text.RegularExpressions.Regex GuidRefRegex =
        new(@"\bguid:\s*([0-9a-fA-F]{32})\b", System.Text.RegularExpressions.RegexOptions.Compiled);

    // m_Name: PlayerController
    private static readonly System.Text.RegularExpressions.Regex GameObjectNameRegex =
        new(@"m_Name:\s*(.+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    // fileID: 12345678
    private static readonly System.Text.RegularExpressions.Regex FileIdRegex =
        new(@"fileID:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.Compiled);

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
        // セクション番号:
        //   1   = GameObject
        //   4   = Transform
        //   114 = MonoBehaviour
        //
        // 修正方針: 先に全 GameObject セクションの fileID → 名前マップを構築し、
        // MonoBehaviour の m_GameObject.fileID を使ってオーナー名を解決する。
        // これにより、MonoBehaviour と GameObject が離れたセクションにある場合でも正確に名前が取れる。

        var fileIdToName = BuildFileIdToNameMap(lines);

        bool inMonoBehaviour = false;
        string? currentOwnerFileId = null;
        var referencedGuids = new HashSet<string>();

        foreach (var line in lines)
        {
            // セクション区切り: "--- !u!114 &123456789"
            if (line.StartsWith("--- !u!", StringComparison.Ordinal))
            {
                inMonoBehaviour = line.Contains("!u!114 "); // 114 = MonoBehaviour
                if (inMonoBehaviour)
                    currentOwnerFileId = null; // MonoBehaviour セクション開始時にリセット
                continue;
            }

            // MonoBehaviour セクション内
            if (inMonoBehaviour)
            {
                // m_GameObject: {fileID: XXXXX} からオーナー GameObject の fileID を取得
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("m_GameObject:", StringComparison.Ordinal))
                {
                    currentOwnerFileId = ExtractFileId(line);
                }

                var scriptMatch = ScriptRefRegex.Match(line);
                if (scriptMatch.Success)
                {
                    var scriptGuid = scriptMatch.Groups[1].Value;
                    var goName = (currentOwnerFileId is not null
                        && fileIdToName.TryGetValue(currentOwnerFileId, out var n))
                        ? n : "Unknown";

                    result.Components.Add(new ComponentInfo
                    {
                        AssetGuid = assetGuid,
                        GameObjectName = goName,
                        ScriptGuid = scriptGuid,
                    });
                    result.References.Add(new AssetReference
                    {
                        FromGuid = assetGuid,
                        ToGuid = scriptGuid,
                        RefType = ReferenceType.Script,
                        GameObjectName = goName,
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
                    if (!result.References.Exists(r => r.ToGuid == guid && r.RefType == ReferenceType.Script))
                    {
                        result.References.Add(new AssetReference
                        {
                            FromGuid = assetGuid,
                            ToGuid = guid,
                            RefType = DetectRefType(line),
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// ファイル全体を先読みして fileID → GameObject 名のマップを構築する。
    /// GameObject セクション (--- !u!1 &XXXXX) の m_Name を収集する。
    /// </summary>
    private static Dictionary<string, string> BuildFileIdToNameMap(string[] lines)
    {
        var map = new Dictionary<string, string>();
        string? currentFileId = null;
        bool inGameObject = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("--- !u!", StringComparison.Ordinal))
            {
                inGameObject = line.Contains("!u!1 "); // 1 = GameObject
                var ampIdx = line.IndexOf('&');
                currentFileId = ampIdx >= 0 ? line[(ampIdx + 1)..].Trim() : null;
                continue;
            }

            if (inGameObject && currentFileId is not null
                && line.TrimStart().StartsWith("m_Name:", StringComparison.Ordinal))
            {
                var match = GameObjectNameRegex.Match(line);
                if (match.Success)
                {
                    var name = match.Groups[1].Value.Trim();
                    if (name.Length > 0)
                        map[currentFileId] = name;
                }
            }
        }

        return map;
    }

    /// <summary>行から fileID の値を抽出する（0 は外部参照なので null 扱い）</summary>
    private static string? ExtractFileId(string line)
    {
        var m = FileIdRegex.Match(line);
        return (m.Success && m.Groups[1].Value != "0") ? m.Groups[1].Value : null;
    }

    private static ReferenceType DetectRefType(string line)
    {
        if (line.Contains("m_Texture") || line.Contains("_MainTex") || line.Contains("_BumpMap"))
            return ReferenceType.Texture;
        if (line.Contains("m_Material") || line.Contains("m_Materials"))
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
