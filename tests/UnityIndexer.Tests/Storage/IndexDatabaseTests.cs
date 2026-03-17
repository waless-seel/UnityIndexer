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

    // -------------------------------------------------------
    // FindReferencedAssets
    // -------------------------------------------------------

    [Fact]
    public void FindReferencedAssets_ReturnsOutgoingRefs()
    {
        _db.UpsertAssets([
            new AssetInfo { Guid = "script-out-guid", RelativePath = "Assets/Scripts/Foo.cs", Type = AssetType.Script, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
            new AssetInfo { Guid = "prefab-out-guid", RelativePath = "Assets/Prefabs/Foo.prefab", Type = AssetType.Prefab, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
        ]);
        _db.UpsertReferences([
            new AssetReference { FromGuid = "prefab-out-guid", ToGuid = "script-out-guid", RefType = ReferenceType.Script }
        ]);

        var outgoing = _db.FindReferencedAssets("prefab-out-guid");

        Assert.Single(outgoing);
        Assert.Equal("script-out-guid", outgoing[0].Guid);
    }

    // -------------------------------------------------------
    // UpsertComponents / GetComponents
    // -------------------------------------------------------

    [Fact]
    public void UpsertComponents_GetComponents_Works()
    {
        _db.UpsertAssets([
            new AssetInfo { Guid = "prefab-comp-guid", RelativePath = "Assets/Prefabs/Player.prefab", Type = AssetType.Prefab, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
        ]);
        _db.UpsertComponents([
            new ComponentInfo { AssetGuid = "prefab-comp-guid", GameObjectName = "Player", BuiltinComponentClass = "Rigidbody", Enabled = true },
            new ComponentInfo { AssetGuid = "prefab-comp-guid", GameObjectName = "Player", BuiltinComponentClass = "Collider",  Enabled = false },
        ]);

        var components = _db.GetComponents("prefab-comp-guid");

        Assert.Equal(2, components.Count);
        Assert.All(components, c => Assert.Equal("Player", c.GameObjectName));
        Assert.Contains(components, c => c.BuiltinClass == "Rigidbody" && c.Enabled);
        Assert.Contains(components, c => c.BuiltinClass == "Collider"  && !c.Enabled);
    }

    [Fact]
    public void UpsertComponents_Replaces_ExistingComponents()
    {
        _db.UpsertAssets([
            new AssetInfo { Guid = "prefab-replace-guid", RelativePath = "Assets/Prefabs/Replace.prefab", Type = AssetType.Prefab, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
        ]);

        _db.UpsertComponents([
            new ComponentInfo { AssetGuid = "prefab-replace-guid", GameObjectName = "Root", BuiltinComponentClass = "OldComponent" },
        ]);
        // 再挿入: 既存コンポーネントは削除されて NewComponent だけになる
        _db.UpsertComponents([
            new ComponentInfo { AssetGuid = "prefab-replace-guid", GameObjectName = "Root", BuiltinComponentClass = "NewComponent" },
        ]);

        var components = _db.GetComponents("prefab-replace-guid");
        Assert.Single(components);
        Assert.Equal("NewComponent", components[0].BuiltinClass);
    }

    [Fact]
    public void GetComponents_WithLinkedScript_HasScriptPath()
    {
        _db.UpsertAssets([
            new AssetInfo { Guid = "script-comp-guid", RelativePath = "Assets/Scripts/PlayerController.cs", Type = AssetType.Script, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
            new AssetInfo { Guid = "prefab-linked-guid", RelativePath = "Assets/Prefabs/Player.prefab", Type = AssetType.Prefab, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
        ]);
        _db.UpsertComponents([
            new ComponentInfo { AssetGuid = "prefab-linked-guid", GameObjectName = "Player", ScriptGuid = "script-comp-guid", Enabled = true },
        ]);

        var components = _db.GetComponents("prefab-linked-guid");

        Assert.Single(components);
        Assert.Equal("script-comp-guid", components[0].ScriptGuid);
        Assert.Equal("Assets/Scripts/PlayerController.cs", components[0].ScriptPath);
        Assert.Equal("PlayerController", components[0].ScriptName);
    }

    // -------------------------------------------------------
    // CountAssets
    // -------------------------------------------------------

    [Fact]
    public void CountAssets_ReturnsCorrectCount()
    {
        _db.UpsertAssets([
            new AssetInfo { Guid = "ca-guid-1", RelativePath = "a.cs",      Type = AssetType.Script, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
            new AssetInfo { Guid = "ca-guid-2", RelativePath = "b.prefab",  Type = AssetType.Prefab, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
            new AssetInfo { Guid = "ca-guid-3", RelativePath = "c.unity",   Type = AssetType.Scene,  FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
        ]);

        Assert.Equal(3, _db.CountAssets());
    }

    // -------------------------------------------------------
    // UpsertAssemblies
    // -------------------------------------------------------

    [Fact]
    public void UpsertAssemblies_StoresCorrectly()
    {
        var asm = new AssemblyDefinitionInfo
        {
            AssetGuid      = "asm-test-guid",
            AssemblyName   = "MyGame.Core",
            References     = ["UnityEngine", "MyGame.Utils"],
            AllowUnsafeCode = true,
            AutoReferenced  = false,
        };

        // 例外なしで保存できること
        _db.UpsertAssemblies([asm]);

        // ON CONFLICT UPDATE: 2回挿入しても例外なし
        _db.UpsertAssemblies([asm]);

        // 直接 SQLite で確認
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT assembly_name, allow_unsafe, auto_referenced FROM assemblies WHERE asset_guid='asm-test-guid'";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read(), "assemblies テーブルに行が存在すること");
        Assert.Equal("MyGame.Core", reader.GetString(0));
        Assert.Equal(1, reader.GetInt32(1)); // allow_unsafe = true
        Assert.Equal(0, reader.GetInt32(2)); // auto_referenced = false
        Assert.False(reader.Read(), "重複行がないこと");
    }

    // -------------------------------------------------------
    // SearchAssets with type filter
    // -------------------------------------------------------

    [Fact]
    public void SearchAssets_WithTypeFilter_FiltersType()
    {
        _db.UpsertAssets([
            new AssetInfo { Guid = "filter-script", RelativePath = "Assets/Scripts/Foo.cs",     Type = AssetType.Script, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
            new AssetInfo { Guid = "filter-prefab", RelativePath = "Assets/Prefabs/Foo.prefab", Type = AssetType.Prefab, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
        ]);

        var scripts = _db.SearchAssets("Foo", typeFilter: AssetType.Script);
        Assert.Single(scripts);
        Assert.Equal("filter-script", scripts[0].Guid);

        var prefabs = _db.SearchAssets("Foo", typeFilter: AssetType.Prefab);
        Assert.Single(prefabs);
        Assert.Equal("filter-prefab", prefabs[0].Guid);
    }

    [Fact]
    public void SearchAssets_EmptyQuery_ReturnsAll()
    {
        _db.UpsertAssets([
            new AssetInfo { Guid = "all-guid-1", RelativePath = "Assets/A.cs",      Type = AssetType.Script, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
            new AssetInfo { Guid = "all-guid-2", RelativePath = "Assets/B.prefab",  Type = AssetType.Prefab, FileSizeBytes = 0, LastModified = DateTime.UtcNow, IndexedAt = DateTime.UtcNow },
        ]);

        var results = _db.SearchAssets("");
        Assert.Equal(2, results.Count);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
