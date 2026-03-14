using UnityIndexer.Analyzer.Assets;
using UnityIndexer.Core.Models;

namespace UnityIndexer.Tests.Assets;

public class YamlAssetParserTests
{
    [Fact]
    public void Parse_PrefabWithScript_ReturnsScriptComponent()
    {
        var prefabContent = """
            %YAML 1.1
            %TAG !u! tag:unity3d.com,2011:
            --- !u!1 &100000
            GameObject:
              m_ObjectHideFlags: 0
              m_Name: Player
            --- !u!114 &100001
            MonoBehaviour:
              m_GameObject: {fileID: 100000}
              m_Script: {fileID: 11500000, guid: aaaa0000bbbb1111cccc2222dddd3333, type: 3}
              m_Name:
            """;

        var tmp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.prefab");
        File.WriteAllText(tmp, prefabContent);

        var result = YamlAssetParser.Parse("prefab-guid-0000", tmp);

        Assert.Single(result.Components);
        Assert.Equal("aaaa0000bbbb1111cccc2222dddd3333", result.Components[0].ScriptGuid);

        var scriptRef = result.References.FirstOrDefault(r => r.RefType == ReferenceType.Script);
        Assert.NotNull(scriptRef);
        Assert.Equal("aaaa0000bbbb1111cccc2222dddd3333", scriptRef!.ToGuid);

        File.Delete(tmp);
    }

    /// <summary>
    /// MonoBehaviour と GameObject が別セクションにある場合でも
    /// m_GameObject.fileID を使って正しく GameObject 名を解決できることを確認する。
    /// </summary>
    [Fact]
    public void Parse_GameObjectNameResolvedByFileId()
    {
        var prefabContent = """
            %YAML 1.1
            %TAG !u! tag:unity3d.com,2011:
            --- !u!1 &111111
            GameObject:
              m_Name: Enemy
            --- !u!1 &222222
            GameObject:
              m_Name: Player
            --- !u!114 &333333
            MonoBehaviour:
              m_GameObject: {fileID: 222222}
              m_Script: {fileID: 11500000, guid: eeee0000ffff1111aaaa2222bbbb3333, type: 3}
            """;

        var tmp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.prefab");
        File.WriteAllText(tmp, prefabContent);

        var result = YamlAssetParser.Parse("prefab-guid-1111", tmp);

        Assert.Single(result.Components);
        // MonoBehaviour は Player (fileID: 222222) にアタッチされているはず
        Assert.Equal("Player", result.Components[0].GameObjectName);

        File.Delete(tmp);
    }

    [Fact]
    public void Parse_EmptyFile_ReturnsNoComponents()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.prefab");
        File.WriteAllText(tmp, "%YAML 1.1\n");

        var result = YamlAssetParser.Parse("test-guid", tmp);

        Assert.Empty(result.Components);
        Assert.Empty(result.References);

        File.Delete(tmp);
    }
}
