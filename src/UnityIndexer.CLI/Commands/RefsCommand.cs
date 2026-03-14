using System.CommandLine;
using System.Text.Json;
using UnityIndexer.Storage;

namespace UnityIndexer.CLI.Commands;

internal static class RefsCommand
{
    public static Command Create()
    {
        var targetArg = new Argument<string>("target", "GUID または相対パス（例: Assets/Scripts/Player.cs）");
        var directionOpt = new Option<string>("--direction", () => "both", "参照方向: 'in'（被参照）/ 'out'（参照先）/ 'both'");
        var jsonOpt = new Option<bool>("--json", "JSON 形式で出力する");
        var projectOpt = new Option<string?>("--project", "プロジェクトルートパス");

        var cmd = new Command("refs", "アセットの参照関係を表示する")
        {
            targetArg, directionOpt, jsonOpt, projectOpt,
        };

        cmd.SetHandler((string target, string direction, bool json, string? project) =>
        {
            using var db = DbHelper.TryOpen(project);
            if (db is null) return;

            var asset = db.FindAsset(target);
            if (asset is null)
            {
                Console.Error.WriteLine($"アセットが見つかりません: {target}");
                return;
            }

            var inRefs = direction is "in" or "both"
                ? db.FindReferencingAssets(asset.Guid)
                : [];

            var outRefs = direction is "out" or "both"
                ? db.FindReferencedAssets(asset.Guid)
                : [];

            if (json)
            {
                var output = new
                {
                    asset = new { asset.Guid, asset.Path, asset.Name, asset.Type },
                    referencedBy = inRefs,
                    references = outRefs,
                };
                Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            Console.WriteLine($"対象: [{asset.Type}] {asset.Path}");
            Console.WriteLine($"GUID: {asset.Guid}");

            if (direction is "in" or "both")
            {
                Console.WriteLine($"\n参照元（{inRefs.Count} 件）:");
                foreach (var r in inRefs)
                    Console.WriteLine($"  [{r.Type}] {r.Path}");
                if (inRefs.Count == 0)
                    Console.WriteLine("  (なし)");
            }

            if (direction is "out" or "both")
            {
                Console.WriteLine($"\n参照先（{outRefs.Count} 件）:");
                foreach (var r in outRefs)
                    Console.WriteLine($"  [{r.Type}] {r.Path}");
                if (outRefs.Count == 0)
                    Console.WriteLine("  (なし)");
            }
        }, targetArg, directionOpt, jsonOpt, projectOpt);

        return cmd;
    }
}
