using System.CommandLine;
using System.Diagnostics;
using UnityIndexer.Analyzer.Assets;
using UnityIndexer.Storage;

namespace UnityIndexer.CLI.Commands;

internal static class IndexCommand
{
    public static Command Create()
    {
        var pathArg = new Argument<string>("project-path", "Unity プロジェクトのルートパス");
        var forceOpt = new Option<bool>("--force", "既存インデックスを強制再構築する");
        var verboseOpt = new Option<bool>("--verbose", "詳細ログを表示する");

        var cmd = new Command("index", "Unity プロジェクトをインデックス化する")
        {
            pathArg, forceOpt, verboseOpt,
        };

        cmd.SetHandler((string path, bool force, bool verbose) =>
        {
            var projectRoot = Path.GetFullPath(path);
            if (!Directory.Exists(projectRoot))
            {
                Console.Error.WriteLine($"パスが存在しません: {projectRoot}");
                return;
            }

            var dbPath = DbHelper.GetDbPath(projectRoot);
            if (File.Exists(dbPath) && !force)
            {
                Console.WriteLine($"既存インデックスを使用します: {dbPath}");
                Console.WriteLine("再構築するには --force を指定してください。");
                return;
            }

            Console.WriteLine($"インデックス化開始: {projectRoot}");
            var sw = Stopwatch.StartNew();

            var progress = verbose
                ? new Progress<string>(msg => Console.WriteLine($"  {msg}"))
                : null;

            var analyzer = new AssetAnalyzer(projectRoot, progress);
            var index = analyzer.Analyze();

            Console.WriteLine($"解析完了: {index.Assets.Count} アセット, {index.References.Count} 参照関係");
            Console.WriteLine("データベースに書き込み中...");

            using var db = new IndexDatabase(dbPath);
            db.UpsertAssets(index.Assets.Values);
            db.UpsertReferences(index.References);
            db.UpsertComponents(index.Components);

            sw.Stop();
            Console.WriteLine($"完了 ({sw.Elapsed.TotalSeconds:F1}s): {dbPath}");
        }, pathArg, forceOpt, verboseOpt);

        return cmd;
    }
}
