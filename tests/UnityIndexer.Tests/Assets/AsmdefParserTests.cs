using UnityIndexer.Analyzer.Assets;

namespace UnityIndexer.Tests.Assets;

public class AsmdefParserTests : IDisposable
{
    private readonly string _tmpDir = Path.Combine(Path.GetTempPath(), $"asmdef-tests-{Guid.NewGuid()}");

    public AsmdefParserTests()
    {
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tmpDir))
            Directory.Delete(_tmpDir, true);
    }

    private string WriteAsmdef(string fileName, string json)
    {
        var path = Path.Combine(_tmpDir, fileName);
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Parse_FullAsmdef_AllFieldsExtracted()
    {
        var path = WriteAsmdef("MyGame.Core.asmdef", """
            {
                "name": "MyGame.Core",
                "references": ["UnityEngine", "MyGame.Utils"],
                "includePlatforms": ["Editor"],
                "excludePlatforms": [],
                "allowUnsafeCode": true,
                "autoReferenced": false
            }
            """);

        var result = AsmdefParser.Parse("test-guid", path);

        Assert.NotNull(result);
        Assert.Equal("test-guid", result!.AssetGuid);
        Assert.Equal("MyGame.Core", result.AssemblyName);
        Assert.Equal(2, result.References.Count);
        Assert.Contains("UnityEngine", result.References);
        Assert.Contains("MyGame.Utils", result.References);
        Assert.Single(result.IncludePlatforms);
        Assert.Empty(result.ExcludePlatforms);
        Assert.True(result.AllowUnsafeCode);
        Assert.False(result.AutoReferenced);
    }

    [Fact]
    public void Parse_MinimalAsmdef_DefaultsApplied()
    {
        var path = WriteAsmdef("Minimal.asmdef", """
            {
                "name": "Minimal"
            }
            """);

        var result = AsmdefParser.Parse("min-guid", path);

        Assert.NotNull(result);
        Assert.Equal("Minimal", result!.AssemblyName);
        Assert.Empty(result.References);
        Assert.Empty(result.IncludePlatforms);
        Assert.Empty(result.ExcludePlatforms);
        Assert.False(result.AllowUnsafeCode);
        Assert.True(result.AutoReferenced); // default is true
    }

    [Fact]
    public void Parse_NoNameField_UsesFilename()
    {
        var path = WriteAsmdef("MyGame.Editor.asmdef", """
            {
                "references": []
            }
            """);

        var result = AsmdefParser.Parse("no-name-guid", path);

        Assert.NotNull(result);
        Assert.Equal("MyGame.Editor", result!.AssemblyName);
    }

    [Fact]
    public void Parse_AllowUnsafeCode_True()
    {
        var path = WriteAsmdef("Unsafe.asmdef", """
            {
                "name": "Unsafe",
                "allowUnsafeCode": true
            }
            """);

        var result = AsmdefParser.Parse("unsafe-guid", path);

        Assert.NotNull(result);
        Assert.True(result!.AllowUnsafeCode);
    }

    [Fact]
    public void Parse_EmptyArrayFields_ReturnsEmptyLists()
    {
        var path = WriteAsmdef("Empty.asmdef", """
            {
                "name": "Empty",
                "references": [],
                "includePlatforms": [],
                "excludePlatforms": []
            }
            """);

        var result = AsmdefParser.Parse("empty-guid", path);

        Assert.NotNull(result);
        Assert.Empty(result!.References);
        Assert.Empty(result.IncludePlatforms);
        Assert.Empty(result.ExcludePlatforms);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        var path = WriteAsmdef("Bad.asmdef", "this is { not valid json");

        var result = AsmdefParser.Parse("bad-guid", path);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_MissingFile_ReturnsNull()
    {
        var result = AsmdefParser.Parse("missing-guid", Path.Combine(_tmpDir, "DoesNotExist.asmdef"));

        Assert.Null(result);
    }
}
