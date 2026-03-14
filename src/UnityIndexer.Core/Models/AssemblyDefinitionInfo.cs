namespace UnityIndexer.Core.Models;

/// <summary>.asmdef ファイルから読み取ったアセンブリ定義情報</summary>
public sealed class AssemblyDefinitionInfo
{
    public required string AssetGuid { get; init; }
    public required string AssemblyName { get; init; }
    public IReadOnlyList<string> References { get; init; } = [];
    public IReadOnlyList<string> IncludePlatforms { get; init; } = [];
    public IReadOnlyList<string> ExcludePlatforms { get; init; } = [];
    public bool AllowUnsafeCode { get; init; }
    public bool AutoReferenced { get; init; } = true;
}
