namespace UnityIndexer.Core.Models;

/// <summary>解析済みプロジェクト全体のインデックス（メモリ内表現）</summary>
public sealed class ProjectIndex
{
    /// <summary>プロジェクトルートの絶対パス</summary>
    public required string ProjectRoot { get; init; }

    /// <summary>GUID → AssetInfo のマップ</summary>
    public Dictionary<string, AssetInfo> Assets { get; } = new();

    /// <summary>相対パス → GUID のマップ（逆引き用）</summary>
    public Dictionary<string, string> PathToGuid { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>アセット間の参照エッジ一覧</summary>
    public List<AssetReference> References { get; } = [];

    /// <summary>プレハブ/シーン内コンポーネント一覧</summary>
    public List<ComponentInfo> Components { get; } = [];

    /// <summary>C# スクリプト型情報</summary>
    public Dictionary<string, ScriptTypeInfo> ScriptTypes { get; } = new(); // guid → type

    /// <summary>.asmdef 情報</summary>
    public Dictionary<string, AssemblyDefinitionInfo> Assemblies { get; } = new(); // guid → asmdef

    /// <summary>インデックス生成日時</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public AssetInfo? FindByGuid(string guid)
        => Assets.TryGetValue(guid, out var a) ? a : null;

    public AssetInfo? FindByPath(string relativePath)
        => PathToGuid.TryGetValue(relativePath, out var guid) ? FindByGuid(guid) : null;

    /// <summary>指定 GUID のアセットを参照している全アセットを返す</summary>
    public IEnumerable<AssetInfo> FindReferencingAssets(string toGuid)
        => References
            .Where(r => r.ToGuid == toGuid)
            .Select(r => FindByGuid(r.FromGuid))
            .OfType<AssetInfo>();

    /// <summary>指定 GUID のアセットが参照している全アセットを返す</summary>
    public IEnumerable<AssetInfo> FindReferencedAssets(string fromGuid)
        => References
            .Where(r => r.FromGuid == fromGuid)
            .Select(r => FindByGuid(r.ToGuid))
            .OfType<AssetInfo>();
}
