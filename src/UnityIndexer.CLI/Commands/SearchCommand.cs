using System.CommandLine;
using System.Text.Json;
using UnityIndexer.Core.Models;
using UnityIndexer.Storage;

namespace UnityIndexer.CLI.Commands;

internal static class SearchCommand
{
    public static Command Create()
    {
        var queryArg = new Argument<string>("query", () => "", "検索キーワード（省略時は全件）");
        var typeOpt = new Option<string?>("--type", "アセット種別でフィルタ (script/prefab/scene/material/texture/audio/...)");
        var limitOpt = new Option<int>("--limit", () => 50, "最大表示件数");
        var jsonOpt = new Option<bool>("--json", "JSON 形式で出力する");
        var projectOpt = new Option<string?>("--project", "プロジェクトルートパス（省略時はカレントディレクトリから検索）");

        var cmd = new Command("search", "インデックス済みアセットを検索する")
        {
            queryArg, typeOpt, limitOpt, jsonOpt, projectOpt,
        };

        cmd.SetHandler((string query, string? type, int limit, bool json, string? project) =>
        {
            using var db = DbHelper.TryOpen(project);
            if (db is null) return;

            AssetType? typeFilter = null;
            if (type is not null && Enum.TryParse<AssetType>(type, ignoreCase: true, out var parsed))
                typeFilter = parsed;

            var results = db.SearchAssets(query, typeFilter, limit);

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            if (results.Count == 0)
            {
                Console.WriteLine("該当するアセットが見つかりませんでした。");
                return;
            }

            // テーブル形式出力
            var typeWidth = results.Max(r => r.Type.Length);
            var nameWidth = Math.Min(results.Max(r => r.Name.Length), 40);
            Console.WriteLine($"{"TYPE".PadRight(typeWidth)}  {"NAME".PadRight(nameWidth)}  PATH");
            Console.WriteLine(new string('-', typeWidth + nameWidth + 50));
            foreach (var r in results)
            {
                var name = r.Name.Length > nameWidth ? r.Name[..nameWidth] : r.Name;
                Console.WriteLine($"{r.Type.PadRight(typeWidth)}  {name.PadRight(nameWidth)}  {r.Path}");
            }
            Console.WriteLine($"\n{results.Count} 件");
        }, queryArg, typeOpt, limitOpt, jsonOpt, projectOpt);

        return cmd;
    }
}
