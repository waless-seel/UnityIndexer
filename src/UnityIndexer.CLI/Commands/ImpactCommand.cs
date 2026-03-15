using System.CommandLine;
using System.Text.Json;
using UnityIndexer.Storage;

namespace UnityIndexer.CLI.Commands;

internal static class ImpactCommand
{
    public static Command Create()
    {
        var targetArg = new Argument<string>("target", "スクリプト名・GUID・相対パス（例: Assets/Scripts/Player.cs）");
        var depthOpt  = new Option<int>("--depth",   () => 5, "参照グラフの最大探索深さ");
        var jsonOpt   = new Option<bool>("--json",   "JSON 形式で出力する");
        var projectOpt = new Option<string?>("--project", "プロジェクトルートパス");

        var cmd = new Command("impact", "アセット変更の影響範囲を表示する（どのプレハブ/シーンが影響を受けるか）")
        {
            targetArg, depthOpt, jsonOpt, projectOpt,
        };

        cmd.SetHandler((string target, int depth, bool json, string? project) =>
        {
            using var db = DbHelper.TryOpen(project);
            if (db is null) return;

            var asset = db.FindAsset(target);
            if (asset is null)
            {
                Console.Error.WriteLine($"アセットが見つかりません: {target}");
                return;
            }

            // BFS で参照元グラフを traversal
            var impacted = FindImpactedAssets(db, asset.Guid, depth);

            if (json)
            {
                var output = new
                {
                    target = new { asset.Guid, asset.Path, asset.Name, asset.Type },
                    impacted = impacted.Select(r => new { r.Guid, r.Path, r.Name, r.Type }),
                };
                Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            Console.WriteLine($"対象: [{asset.Type}] {asset.Path}");
            Console.WriteLine($"GUID: {asset.Guid}");
            Console.WriteLine($"\n影響を受けるアセット（{impacted.Count} 件, 最大深さ {depth}）:");

            if (impacted.Count == 0)
            {
                Console.WriteLine("  (なし)");
                return;
            }

            foreach (var r in impacted.OrderBy(r => r.Type).ThenBy(r => r.Path))
                Console.WriteLine($"  [{r.Type}] {r.Path}");
        }, targetArg, depthOpt, jsonOpt, projectOpt);

        return cmd;
    }

    /// <summary>
    /// BFS で参照元グラフを traversal し、影響を受けるアセットを収集する。
    /// </summary>
    private static IReadOnlyList<AssetSearchResult> FindImpactedAssets(
        IndexDatabase db, string startGuid, int maxDepth)
    {
        var visited = new HashSet<string> { startGuid };
        var queue = new Queue<(string guid, int depth)>();
        queue.Enqueue((startGuid, 0));
        var results = new List<AssetSearchResult>();

        while (queue.Count > 0)
        {
            var (currentGuid, currentDepth) = queue.Dequeue();
            if (currentDepth >= maxDepth) continue;

            var referencingAssets = db.FindReferencingAssets(currentGuid);
            foreach (var referencing in referencingAssets)
            {
                if (!visited.Add(referencing.Guid)) continue;

                results.Add(referencing);
                queue.Enqueue((referencing.Guid, currentDepth + 1));
            }
        }

        return results;
    }
}
