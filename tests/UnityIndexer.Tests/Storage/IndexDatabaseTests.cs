using UnityIndexer.Core.Models;
using UnityIndexer.Storage;

namespace UnityIndexer.Tests.Storage;

public class IndexDatabaseTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.db");
    private readonly IndexDatabase _db;

    public IndexDatabaseTests()
    {
        _db = new IndexDatabase(_dbPath);
    }

    [Fact]
    public void UpsertAndSearch_Asset_FoundByName()
    {
        _db.UpsertAssets([
            new AssetInfo
            {
                Guid = "aaaa1111bbbb2222cccc3333dddd4444",
                RelativePath = "Assets/Scripts/PlayerController.cs",
                Type = AssetType.Script,
                FileSizeBytes = 1024,
                LastModified = DateTime.UtcNow,
                IndexedAt = DateTime.UtcNow,
            }
        ]);

        var results = _db.SearchAssets("PlayerController");
        Assert.Single(results);
        Assert.Equal("aaaa1111bbbb2222cccc3333dddd4444", results[0].Guid);
    }

    [Fact]
    public void FindReferencingAssets_ReturnsCorrectAssets()
    {
        _db.UpsertAssets([
            new AssetInfo { Guid = "script-guid", RelativePath = "Assets/Scripts/Foo.cs", Type = AssetType.Script, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
            new AssetInfo { Guid = "prefab-guid", RelativePath = "Assets/Prefabs/Bar.prefab", Type = AssetType.Prefab, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
        ]);

        _db.UpsertReferences([
            new AssetReference { FromGuid = "prefab-guid", ToGuid = "script-guid", RefType = ReferenceType.Script }
        ]);

        var refs = _db.FindReferencingAssets("script-guid");
        Assert.Single(refs);
        Assert.Equal("prefab-guid", refs[0].Guid);
    }

    [Fact]
    public void FindAsset_ByPath_Found()
    {
        _db.UpsertAssets([
            new AssetInfo { Guid = "path-test-guid", RelativePath = "Assets/Scenes/Main.unity", Type = AssetType.Scene, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
        ]);

        var result = _db.FindAsset("Assets/Scenes/Main.unity");
        Assert.NotNull(result);
        Assert.Equal("path-test-guid", result!.Guid);
    }

    [Fact]
    public void UpsertScriptTypes_StoresAndRetrievesCorrectly()
    {
        // Arrange: まずアセットを登録（FK なしだが念のため）
        var guid = "script-type-test-guid";
        _db.UpsertAssets([
            new AssetInfo
            {
                Guid = guid,
                RelativePath = "Assets/Scripts/EnemyAI.cs",
                Type = AssetType.Script,
                FileSizeBytes = 512,
                LastModified = DateTime.UtcNow,
                IndexedAt = DateTime.UtcNow,
            }
        ]);

        var scriptType = new ScriptTypeInfo
        {
            AssetGuid = guid,
            Namespace = "MyGame.Enemy",
            ClassName = "EnemyAI",
            BaseTypeName = "UnityEngine.MonoBehaviour",
            Interfaces = ["IEnemy", "IDamageable"],
            Kind = TypeKind.Class,
            IsMonoBehaviour = true,
            IsScriptableObject = false,
            IsEditorClass = false,
            SerializedFields =
            [
                new FieldInfo { Name = "Speed", TypeName = "float", IsPublic = true, HasSerializeFieldAttribute = false },
                new FieldInfo { Name = "_health", TypeName = "int", IsPublic = false, HasSerializeFieldAttribute = true },
            ],
        };

        // Act
        _db.UpsertScriptTypes([scriptType]);

        // Assert: script_types テーブルに 1 件格納されていること
        Assert.Equal(1, _db.CountScriptTypes());

        var row = _db.GetScriptType(guid);
        Assert.NotNull(row);
        Assert.Equal("EnemyAI", row!.Value.ClassName);
        Assert.True(row.Value.IsMono);
        Assert.False(row.Value.IsSo);
        Assert.Equal("MyGame.Enemy", row.Value.Namespace);

        // ON CONFLICT UPDATE が正しく動作すること（重複エラーにならない）
        _db.UpsertScriptTypes([scriptType]);
        Assert.Equal(1, _db.CountScriptTypes());
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
