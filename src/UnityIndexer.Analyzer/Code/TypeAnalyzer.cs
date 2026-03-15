using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityIndexer.Core.Models;
using CoreTypeKind = UnityIndexer.Core.Models.TypeKind;
using RoslynTypeKind = Microsoft.CodeAnalysis.TypeKind;

namespace UnityIndexer.Analyzer.Code;

/// <summary>
/// Roslyn の <see cref="Compilation"/> から C# 型情報を抽出する。
/// </summary>
public static class TypeAnalyzer
{
    /// <summary>
    /// <see cref="Solution"/> 内のすべてのプロジェクトを解析し、<see cref="ScriptTypeInfo"/> リストを返す。
    /// </summary>
    /// <param name="solution">解析対象のソリューション</param>
    /// <param name="pathToGuid">相対パス → GUID マップ（AssetAnalyzer が構築するもの）</param>
    /// <param name="projectRoot">Unity プロジェクトのルートディレクトリ（絶対パス）</param>
    /// <param name="progress">進捗メッセージのコールバック</param>
    /// <param name="ct">キャンセルトークン</param>
    public static async Task<IReadOnlyList<ScriptTypeInfo>> AnalyzeAsync(
        Solution solution,
        IReadOnlyDictionary<string, string> pathToGuid,
        string projectRoot,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<ScriptTypeInfo>();

        foreach (var project in solution.Projects)
        {
            ct.ThrowIfCancellationRequested();

            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;

            var partial = AnalyzeCompilation(compilation, pathToGuid, projectRoot);
            results.AddRange(partial);
        }

        progress?.Report($"C# 型解析完了: {results.Count} 型");
        return results;
    }

    /// <summary>
    /// 単一の <see cref="Compilation"/> を解析する（テスト・再利用用のオーバーロード）。
    /// </summary>
    /// <param name="compilation">解析対象のコンパイル</param>
    /// <param name="pathToGuid">相対パス → GUID マップ</param>
    /// <param name="projectRoot">Unity プロジェクトのルートディレクトリ（絶対パス）</param>
    public static IReadOnlyList<ScriptTypeInfo> AnalyzeCompilation(
        Compilation compilation,
        IReadOnlyDictionary<string, string> pathToGuid,
        string projectRoot)
    {
        var results = new List<ScriptTypeInfo>();

        foreach (var type in GetAllTypes(compilation.GlobalNamespace))
        {
            if (type.TypeKind == RoslynTypeKind.Error) continue;

            var guid = ResolveGuid(type, projectRoot, pathToGuid);
            if (guid is null) continue;

            results.Add(BuildScriptTypeInfo(guid, type));
        }

        return results;
    }

    // -------------------------------------------------------
    // プライベートヘルパー
    // -------------------------------------------------------

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in GetNestedTypes(type))
                yield return nested;
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        foreach (var type in GetAllTypes(childNs))
            yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deep in GetNestedTypes(nested))
                yield return deep;
        }
    }

    private static string? ResolveGuid(
        INamedTypeSymbol type,
        string projectRoot,
        IReadOnlyDictionary<string, string> pathToGuid)
    {
        var location = type.Locations.FirstOrDefault(l => l.IsInSource);
        if (location?.SourceTree?.FilePath is not { Length: > 0 } absPath)
            return null;

        // 絶対パス → projectRoot からの相対パスに変換
        var relative = Path.GetRelativePath(projectRoot, absPath);
        // Windows/Unix パス区切りを統一
        relative = relative.Replace('\\', '/');

        return pathToGuid.TryGetValue(relative, out var guid) ? guid : null;
    }

    private static ScriptTypeInfo BuildScriptTypeInfo(string guid, INamedTypeSymbol type)
    {
        var interfaces = type.Interfaces
            .Select(i => i.ToDisplayString())
            .ToList();

        var serializedFields = ExtractSerializedFields(type);

        return new ScriptTypeInfo
        {
            AssetGuid = guid,
            Namespace = string.IsNullOrEmpty(type.ContainingNamespace?.ToDisplayString())
                ? null
                : type.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : type.ContainingNamespace.ToDisplayString(),
            ClassName = type.Name,
            BaseTypeName = type.BaseType is { } bt && bt.SpecialType != SpecialType.System_Object
                ? bt.ToDisplayString()
                : null,
            Interfaces = interfaces,
            Kind = MapTypeKind(type.TypeKind, type.IsRecord),
            IsMonoBehaviour = InheritsFrom(type, "MonoBehaviour"),
            IsScriptableObject = InheritsFrom(type, "ScriptableObject"),
            IsEditorClass = IsEditorClass(type),
            SerializedFields = serializedFields,
        };
    }

    private static bool InheritsFrom(INamedTypeSymbol? type, string baseName)
    {
        var current = type?.BaseType;
        while (current is not null)
        {
            if (current.Name == baseName) return true;
            current = current.BaseType;
        }
        return false;
    }

    private static bool IsEditorClass(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        return ns.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase)
            || ns.Contains(".Editor.", StringComparison.OrdinalIgnoreCase)
            || ns.EndsWith(".Editor", StringComparison.OrdinalIgnoreCase);
    }

    private static CoreTypeKind MapTypeKind(RoslynTypeKind kind, bool isRecord) => kind switch
    {
        RoslynTypeKind.Struct    => CoreTypeKind.Struct,
        RoslynTypeKind.Interface => CoreTypeKind.Interface,
        RoslynTypeKind.Enum      => CoreTypeKind.Enum,
        RoslynTypeKind.Class when isRecord => CoreTypeKind.Record,
        _                        => CoreTypeKind.Class,
    };

    private static IReadOnlyList<Core.Models.FieldInfo> ExtractSerializedFields(INamedTypeSymbol type)
    {
        var fields = new List<Core.Models.FieldInfo>();

        foreach (var member in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.IsStatic || member.IsConst) continue;

            var isPublic = member.DeclaredAccessibility == Accessibility.Public;
            var hasSerializeField = member.GetAttributes()
                .Any(a => a.AttributeClass?.Name is "SerializeField" or "SerializeFieldAttribute");

            if (!isPublic && !hasSerializeField) continue;

            // [HideInInspector] が付いている場合はスキップ
            if (member.GetAttributes().Any(a => a.AttributeClass?.Name is "HideInInspector" or "HideInInspectorAttribute"))
                continue;

            var tooltip = member.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name is "Tooltip" or "TooltipAttribute")
                ?.ConstructorArguments.FirstOrDefault().Value as string;

            fields.Add(new Core.Models.FieldInfo
            {
                Name = member.Name,
                TypeName = member.Type.ToDisplayString(),
                IsPublic = isPublic,
                HasSerializeFieldAttribute = hasSerializeField,
                Tooltip = tooltip,
            });
        }

        return fields;
    }
}
