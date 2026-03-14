using System.CommandLine;
using System.Text.Json;
using UnityIndexer.Core.Models;
using UnityIndexer.Storage;

namespace UnityIndexer.CLI.Commands;

/// <summary>
/// unity-indexer scene &lt;target&gt;
/// プレハブまたはシーン内の GameObject 一覧とアタッチされたスクリプトを表示する。
/// </summary>
internal static class SceneCommand
{
    public static Command Create()
    {
        var targetArg = new Argument<string>("target", "プレハブ/シーンの GUID または相対パス");
        var jsonOpt = new Option<bool>("--json", "JSON 形式で出力する");
        var projectOpt = new Option<string?>("--project", "プロジェクトルートパス");

        var cmd = new Command("scene", "プレハブ/シーン内の GameObject とコンポーネントを表示する")
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

            if (asset.Type is not "Prefab" and not "Scene")
            {
                Console.Error.WriteLine($"プレハブまたはシーンを指定してください（指定された種別: {asset.Type}）");
                return;
            }

            var components = db.GetComponents(asset.Guid);

            if (json)
            {
                var output = new
                {
                    asset = new { asset.Guid, asset.Path, asset.Name, asset.Type },
                    gameObjects = components
                        .GroupBy(c => c.GameObjectName)
                        .Select(g => new
                        {
                            name = g.Key,
                            scripts = g
                                .Where(c => c.ScriptGuid is not null)
                                .Select(c => new
                                {
                                    scriptGuid = c.ScriptGuid,
                                    scriptName = c.ScriptName,
                                    scriptPath = c.ScriptPath,
                                    enabled = c.Enabled,
                                })
                                .ToList(),
                        })
                        .ToList(),
                };
                Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            Console.WriteLine($"[{asset.Type}] {asset.Path}");
            Console.WriteLine($"GUID: {asset.Guid}");
            Console.WriteLine();

            var grouped = components.GroupBy(c => c.GameObjectName).ToList();

            if (grouped.Count == 0)
            {
                Console.WriteLine("(コンポーネントなし)");
                return;
            }

            foreach (var group in grouped)
            {
                Console.WriteLine($"  GameObject: {group.Key}");
                foreach (var c in group)
                {
                    var enabledMark = c.Enabled ? "✓" : "✗";
                    if (c.ScriptName is not null)
                        Console.WriteLine($"    [{enabledMark}] {c.ScriptName}  ({c.ScriptPath})");
                    else if (c.BuiltinClass is not null)
                        Console.WriteLine($"    [{enabledMark}] {c.BuiltinClass}  (builtin)");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"合計: {grouped.Count} GameObjects, {components.Count} コンポーネント");
        }, targetArg, jsonOpt, projectOpt);

        return cmd;
    }
}
