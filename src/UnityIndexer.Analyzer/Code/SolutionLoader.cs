using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace UnityIndexer.Analyzer.Code;

/// <summary>
/// Unity .sln / .slnx ファイルを MSBuildWorkspace で読み込む。
/// </summary>
public static class SolutionLoader
{
    private static bool _registered;
    private static readonly object _lock = new();

    /// <summary>
    /// 指定した Unity プロジェクトルートから .sln ファイルを見つけて <see cref="Solution"/> を返す。
    /// .sln が存在しない場合は <see langword="null"/> を返す。
    /// </summary>
    /// <param name="projectRoot">Unity プロジェクトのルートディレクトリ</param>
    /// <param name="progress">進捗メッセージのコールバック</param>
    /// <param name="ct">キャンセルトークン</param>
    public static async Task<Solution?> LoadAsync(
        string projectRoot,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        // MSBuild を一度だけ登録
        lock (_lock)
        {
            if (!_registered)
            {
                MSBuildLocator.RegisterDefaults();
                _registered = true;
            }
        }

        var slnFile = Directory.EnumerateFiles(projectRoot, "*.sln", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(projectRoot, "*.slnx", SearchOption.TopDirectoryOnly))
            .FirstOrDefault();

        if (slnFile is null)
        {
            progress?.Report(".sln ファイルが見つかりません。C# コード解析をスキップします。");
            return null;
        }

        progress?.Report($"ソリューション読み込み中: {Path.GetFileName(slnFile)}");

        var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, e) =>
            progress?.Report($"  [warn] {e.Diagnostic.Message}");

        var solution = await workspace.OpenSolutionAsync(slnFile, cancellationToken: ct);
        return solution;
    }
}
