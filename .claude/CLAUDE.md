# UnityIndexer - Claude Code Instructions

## プロジェクト概要

UnityIndexer は Unity プロジェクトのコードベース・アセット・その関係性を解析し、
構造化されたインデックスを生成するシステムです。

**目標:**
- Unity プロジェクトの全要素（C# スクリプト、アセット、シーン等）をインデックス化
- 人間向けインターフェイス（CLI、Web UI）の提供
- AI 向けインターフェイス（MCP サーバー、構造化出力）の提供

**インターフェイス優先順位:**
CLI で解決できる課題は CLI を優先する。MCP は CLI では対応しにくい対話的・コンテキスト依存のユースケースに限定する。

## リポジトリ構成（予定）

```
UnityIndexer/
├── src/
│   ├── UnityIndexer.Core/        # 共通モデル・ユーティリティ
│   ├── UnityIndexer.Analyzer/    # Roslyn による C# 解析
│   │   ├── Code/                 # 型解決・継承グラフ・コールグラフ
│   │   ├── Assets/               # .prefab/.unity/.meta (YAML) 解析
│   │   └── Impact/               # 影響範囲計算エンジン
│   ├── UnityIndexer.Storage/     # インデックス永続化 (SQLite)
│   └── UnityIndexer.CLI/         # System.CommandLine による CLI
├── tests/
├── docs/
└── examples/
```

## 開発ガイドライン

### コーディング規約
- 言語: C# (.NET 10)
- Unity が生成する `.sln` / `.slnx` / `.csproj` を `MSBuildWorkspace` で読み込む
  - クロスアセンブリ型解決・Unity アセンブリ参照が自動解決される
- XML ドキュメントコメント（`///`）をパブリック API に付与する
- nullable reference types を有効化する（`<Nullable>enable</Nullable>`）

### 主要 NuGet パッケージ
- `Microsoft.CodeAnalysis.CSharp.Workspaces` - Roslyn C# 解析
- `Microsoft.CodeAnalysis.Workspaces.MSBuild` - MSBuildWorkspace
- `YamlDotNet` - Unity アセット（.prefab/.unity）の YAML 解析
- `QuikGraph` - 参照グラフ・影響グラフの構築
- `System.CommandLine` - CLI
- `Microsoft.Data.Sqlite` - インデックス永続化

### インデックス対象

**コード:**
- C# スクリプト（MonoBehaviour、ScriptableObject、通常クラス等）
- アセンブリ定義（.asmdef）
- シェーダー（.shader、.hlsl、.cginc）

**アセット:**
- プレハブ（.prefab）
- シーン（.unity）
- テクスチャ・スプライト
- オーディオクリップ
- アニメーション・アニメーターコントローラー
- マテリアル
- ScriptableObject インスタンス

**メタデータ:**
- .meta ファイルによる GUID マッピング
- アセット間の参照関係（どのプレハブがどのスクリプトを使うか等）

### インターフェイス設計方針

**優先順位: CLI > MCP**

CLIで解決できる場合はCLIを選ぶ。MCPはCLIでは難しい用途（会話コンテキストを跨いだ参照、LLMによる自律的な探索）に留める。

#### CLI（最優先）
- `unity-indexer index <path>` / `unity-indexer search <query>` / `unity-indexer impact <symbol>`
- 出力形式: JSON（`--json`）、テーブル、ツリー表示
- AI（Claude等）は `subprocess` や `claude -p` 経由で CLI を呼び出せるため、CLIが充実していればMCP不要なケースが多い

#### MCP（CLIで対応できない場合のみ）
- 会話コンテキストを保持しながら複数回参照するケース
- LLM が自律的にプロジェクトを探索するエージェントユースケース
- MCP を実装する際も内部では CLI コマンドと同じロジックを再利用する

## 開発時の注意

- Unity の .meta ファイルは GUID マッピングに不可欠なので必ず解析する
- 大規模プロジェクト（数千アセット）での性能を意識する
- インデックスのキャッシュ・差分更新を考慮する
- `.gitignore` 対象（Library/ 等）はインデックス対象外
