namespace UnityIndexer.Core.Models;

/// <summary>C# スクリプトから解析した型情報</summary>
public sealed class ScriptTypeInfo
{
    /// <summary>スクリプトアセットの GUID</summary>
    public required string AssetGuid { get; init; }

    /// <summary>名前空間</summary>
    public string? Namespace { get; init; }

    /// <summary>クラス名</summary>
    public required string ClassName { get; init; }

    /// <summary>完全修飾名</summary>
    public string FullName => string.IsNullOrEmpty(Namespace) ? ClassName : $"{Namespace}.{ClassName}";

    /// <summary>基底クラスの完全修飾名</summary>
    public string? BaseTypeName { get; init; }

    /// <summary>実装インターフェイス一覧</summary>
    public IReadOnlyList<string> Interfaces { get; init; } = [];

    /// <summary>型の種別</summary>
    public TypeKind Kind { get; init; }

    /// <summary>MonoBehaviour を継承しているか</summary>
    public bool IsMonoBehaviour { get; init; }

    /// <summary>ScriptableObject を継承しているか</summary>
    public bool IsScriptableObject { get; init; }

    /// <summary>Editor クラスか (UnityEditor 名前空間以下)</summary>
    public bool IsEditorClass { get; init; }

    /// <summary>[SerializeField] を持つフィールド一覧</summary>
    public IReadOnlyList<FieldInfo> SerializedFields { get; init; } = [];
}

public enum TypeKind
{
    Class,
    Struct,
    Interface,
    Enum,
    Record,
}

/// <summary>シリアライズ対象フィールド情報</summary>
public sealed class FieldInfo
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public bool IsPublic { get; init; }
    public bool HasSerializeFieldAttribute { get; init; }
    public string? Tooltip { get; init; }
}
