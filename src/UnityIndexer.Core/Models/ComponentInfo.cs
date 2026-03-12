namespace UnityIndexer.Core.Models;

/// <summary>プレハブ/シーン内の GameObject に付いたコンポーネント情報</summary>
public sealed class ComponentInfo
{
    /// <summary>コンポーネントが属するアセット (プレハブ/シーン) の GUID</summary>
    public required string AssetGuid { get; init; }

    /// <summary>GameObject 名</summary>
    public required string GameObjectName { get; init; }

    /// <summary>スクリプトコンポーネントの場合、スクリプトの GUID</summary>
    public string? ScriptGuid { get; init; }

    /// <summary>Unity ビルトインコンポーネントの場合のクラス名 (例: "Rigidbody")</summary>
    public string? BuiltinComponentClass { get; init; }

    /// <summary>コンポーネントが有効かどうか</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>シーン内での GameObject パス (例: "Root/Child/Player")</summary>
    public string? HierarchyPath { get; init; }
}
