using UnityIndexer.Analyzer.Assets;
using UnityIndexer.Core.Models;

namespace UnityIndexer.Tests.Assets;

public class MetaFileParserTests
{
    [Fact]
    public void Parse_ValidMetaFile_ReturnsCorrectGuid()
    {
        var tmp = Path.GetTempPath();
        var assetPath = Path.Combine(tmp, "TestScript.cs");
        var metaPath = assetPath + ".meta";

        File.WriteAllText(assetPath, "// dummy");
        File.WriteAllText(metaPath, """
            fileFormatVersion: 2
            guid: abcdef1234567890abcdef1234567890
            MonoImporter:
              serializedVersion: 2
            """);

        var result = MetaFileParser.Parse(metaPath, tmp);

        Assert.NotNull(result);
        Assert.Equal("abcdef1234567890abcdef1234567890", result!.Guid);
        Assert.Equal(AssetType.Script, result.Type);
        Assert.Equal("TestScript", result.Name);

        File.Delete(assetPath);
        File.Delete(metaPath);
    }

    [Fact]
    public void Parse_MissingAssetFile_ReturnsNull()
    {
        var tmp = Path.GetTempPath();
        var metaPath = Path.Combine(tmp, "Ghost.cs.meta");
        File.WriteAllText(metaPath, "guid: 0000000000000000000000000000dead");

        var result = MetaFileParser.Parse(metaPath, tmp);

        Assert.Null(result);
        File.Delete(metaPath);
    }

    [Fact]
    public void Parse_MetaWithoutGuid_ReturnsNull()
    {
        var tmp = Path.GetTempPath();
        var assetPath = Path.Combine(tmp, "NoGuid.png");
        var metaPath = assetPath + ".meta";
        File.WriteAllText(assetPath, "dummy");
        File.WriteAllText(metaPath, "fileFormatVersion: 2\n");

        var result = MetaFileParser.Parse(metaPath, tmp);

        Assert.Null(result);
        File.Delete(assetPath);
        File.Delete(metaPath);
    }
}
