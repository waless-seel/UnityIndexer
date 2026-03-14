using UnityIndexer.Core.Models;

namespace UnityIndexer.Analyzer.Assets;

/// <summary>
/// Unity プロジェクトの Assets/ フォルダ以下を走査し、
/// アセット情報・GUID マッピング・参照関係を収集する。
/// </summary>
public sealed class AssetAnalyzer
{
    private readonly string _projectRoot;
    private readonly IProgress<string>? _progress;

    // 解析スキップするディレクトリ名
    private static readonly HashSet<string> SkipDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Library", "Temp", "Logs", "obj", "Build", "Builds",
        ".git", ".vs", "node_modules",
    };

    // YAML 解析対象の拡張子（.prefab/.unity/.mat/.asset は参照グラフを構築する）
    private static readonly HashSet<string> YamlRefExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".prefab", ".unity", ".mat", ".asset", ".controller",
    };

    public AssetAnalyzer(string projectRoot, IProgress<string>? progress = null)
    {
        _projectRoot = projectRoot;
        _progress = progress;
    }

    /// <summary>プロジェクト全体を解析して ProjectIndex を構築する</summary>
    public ProjectIndex Analyze()
    {
        var index = new ProjectIndex { ProjectRoot = _projectRoot };

        // Phase 1: .meta ファイルを走査して GUID マップを構築
        _progress?.Report("Phase 1: .meta ファイルを収集中...");
        CollectAssets(index);

        // Phase 2: YAML アセットを解析して参照関係を構築
        _progress?.Report("Phase 2: アセット参照関係を解析中...");
        CollectReferences(index);

        // Phase 3: .asmdef を解析
        _progress?.Report("Phase 3: アセンブリ定義を解析中...");
        CollectAssemblyDefinitions(index);

        _progress?.Report($"完了: {index.Assets.Count} アセット, {index.References.Count} 参照関係");
        return index;
    }

    private void CollectAssets(ProjectIndex index)
    {
        foreach (var metaFile in EnumerateFiles(_projectRoot, "*.meta"))
        {
            var asset = MetaFileParser.Parse(metaFile, _projectRoot);
            if (asset is null) continue;

            index.Assets[asset.Guid] = asset;
            index.PathToGuid[asset.RelativePath] = asset.Guid;
        }
    }

    private void CollectReferences(ProjectIndex index)
    {
        foreach (var (guid, asset) in index.Assets)
        {
            var ext = Path.GetExtension(asset.RelativePath);
            if (!YamlRefExtensions.Contains(ext)) continue;

            var absolutePath = Path.Combine(_projectRoot, asset.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath)) continue;

            _progress?.Report($"  解析中: {asset.RelativePath}");
            var parsed = YamlAssetParser.Parse(guid, absolutePath);

            index.Components.AddRange(parsed.Components);

            // 参照先が既知の GUID のみ登録（不明 GUID は外部パッケージ等なので無視しない）
            index.References.AddRange(parsed.References);
        }
    }

    private void CollectAssemblyDefinitions(ProjectIndex index)
    {
        foreach (var asmdefFile in EnumerateFiles(_projectRoot, "*.asmdef"))
        {
            var relativePath = Path.GetRelativePath(_projectRoot, asmdefFile).Replace('\\', '/');
            if (!index.PathToGuid.TryGetValue(relativePath, out var guid)) continue;

            var info = AsmdefParser.Parse(guid, asmdefFile);
            if (info is null) continue;

            index.Assemblies[guid] = info;
        }
    }

    private IEnumerable<string> EnumerateFiles(string root, string pattern)
    {
        var queue = new Queue<string>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var dir = queue.Dequeue();
            var dirName = Path.GetFileName(dir);

            if (SkipDirectories.Contains(dirName) && dir != root)
                continue;

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir, pattern); }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var file in files)
                yield return file;

            IEnumerable<string> subDirs;
            try { subDirs = Directory.EnumerateDirectories(dir); }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var sub in subDirs)
                queue.Enqueue(sub);
        }
    }
}
