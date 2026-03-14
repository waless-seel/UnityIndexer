namespace UnityIndexer.Core.Models;

/// <summary>アセット間の参照関係種別</summary>
public enum ReferenceType
{
    Script,       // プレハブ/シーンがスクリプトを参照
    Texture,      // マテリアルがテクスチャを参照
    Material,     // プレハブ/シーンがマテリアルを参照
    Prefab,       // シーン/プレハブがプレハブをネスト
    AudioClip,
    AnimationClip,
    AnimatorController,
    ScriptableObject,
    Shader,
    Assembly,     // .asmdef が別 .asmdef を参照
    Other,
}

/// <summary>アセット間の参照エッジ</summary>
public sealed class AssetReference
{
    /// <summary>参照元アセットの GUID</summary>
    public required string FromGuid { get; init; }

    /// <summary>参照先アセットの GUID</summary>
    public required string ToGuid { get; init; }

    /// <summary>参照種別</summary>
    public ReferenceType RefType { get; init; }

    /// <summary>参照元の GameObject 名（プレハブ/シーン内コンポーネントの場合）</summary>
    public string? GameObjectName { get; init; }
}
