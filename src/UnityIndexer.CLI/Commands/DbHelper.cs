using UnityIndexer.Storage;

namespace UnityIndexer.CLI.Commands;

internal static class DbHelper
{
    private const string DbFileName = ".unity-indexer.db";

    /// <summary>カレントディレクトリまたは親ディレクトリの DB を探して開く</summary>
    public static IndexDatabase? TryOpen(string? projectPath = null)
    {
        var dir = projectPath ?? Directory.GetCurrentDirectory();
        var dbPath = FindDb(dir);
        if (dbPath is null)
        {
            Console.Error.WriteLine("インデックスが見つかりません。先に `unity-indexer index <path>` を実行してください。");
            return null;
        }
        return new IndexDatabase(dbPath);
    }

    public static string GetDbPath(string projectRoot)
        => Path.Combine(projectRoot, DbFileName);

    private static string? FindDb(string startDir)
    {
        var dir = startDir;
        for (int i = 0; i < 5; i++)
        {
            var candidate = Path.Combine(dir, DbFileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent is null) break;
            dir = parent;
        }
        return null;
    }
}
