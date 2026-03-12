using System.CommandLine;
using System.Text.Json;
using UnityIndexer.Storage;

namespace UnityIndexer.CLI.Commands;

internal static class InfoCommand
{
    public static Command Create()
    {
        var targetArg = new Argument<string>("target", "GUID または相対パス");
        var jsonOpt = new Option<bool>("--json", "JSON 形式で出力する");
        var projectOpt = new Option<string?>("--project", "プロジェクトルートパス");

        var cmd = new Command("info", "アセットの詳細情報を表示する")
        {
            targetArg, jsonOpt, projectOpt,
        };

        cmd.SetHandler((string target, bool json, string? project) =>
        {
            using var db = DbHelper.TryOpen(project);
            if (db is null) return;

            var asset = db.FindAsset(target);
            if (asset is null)
            {
                Console.Error.WriteLine($"アセットが見つかりません: {target}");
                return;
            }

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(asset, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            Console.WriteLine($"名前  : {asset.Name}");
            Console.WriteLine($"パス  : {asset.Path}");
            Console.WriteLine($"GUID  : {asset.Guid}");
            Console.WriteLine($"種別  : {asset.Type}");

            var inCount = db.FindReferencingAssets(asset.Guid).Count;
            var outCount = db.FindReferencedAssets(asset.Guid).Count;
            Console.WriteLine($"参照元: {inCount} 件");
            Console.WriteLine($"参照先: {outCount} 件");
            Console.WriteLine($"ヒント: `unity-indexer refs \"{asset.Path}\"` で参照関係を表示");
        }, targetArg, jsonOpt, projectOpt);

        return cmd;
    }
}
