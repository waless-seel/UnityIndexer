namespace UnityIndexer.Core.Models;

/// <summary>インデックス済みアセットの基本情報</summary>
public sealed class AssetInfo
{
    /// <summary>Unity が .meta ファイルで管理する GUID (32桁 hex)</summary>
    public required string Guid { get; init; }

    /// <summary>プロジェクトルートからの相対パス (例: Assets/Scripts/Player.cs)</summary>
    public required string RelativePath { get; init; }

    /// <summary>ファイル名（拡張子なし）</summary>
    public string Name => Path.GetFileNameWithoutExtension(RelativePath);

    /// <summary>アセット種別</summary>
    public AssetType Type { get; init; }

    /// <summary>ファイルサイズ（バイト）</summary>
    public long FileSizeBytes { get; init; }

    /// <summary>最終更新時刻</summary>
    public DateTime LastModified { get; init; }

    /// <summary>インデックス化した時刻</summary>
    public DateTime IndexedAt { get; init; }

    public override string ToString() => $"[{Type}] {RelativePath} ({Guid})";
}
