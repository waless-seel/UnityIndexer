using System.CommandLine;
using UnityIndexer.CLI.Commands;

var root = new RootCommand("Unity プロジェクトのインデックスツール");

root.AddCommand(IndexCommand.Create());
root.AddCommand(SearchCommand.Create());
root.AddCommand(RefsCommand.Create());
root.AddCommand(InfoCommand.Create());

return await root.InvokeAsync(args);
