# UnityIndexer

Unity プロジェクトのコードベース・アセット・その関係性を解析し、人間と AI の両方が活用できるインデックスシステムです。

## 概要

大規模な Unity プロジェクトでは、スクリプト・プレハブ・シーン・テクスチャ等の膨大なアセットが存在し、全体像を把握することが困難です。UnityIndexer はプロジェクト全体を解析してインデックスを生成し、以下のインターフェイスを通じて情報を提供します。

- **人間向け**: CLI ツール、Web UI による検索・ブラウジング
- **AI 向け**: MCP (Model Context Protocol) サーバー経由で LLM がプロジェクト構造を参照可能

## 主な機能（予定）

### インデックス対象

| カテゴリ | 対象 |
|----------|------|
| **コード** | C# スクリプト、MonoBehaviour、ScriptableObject、シェーダー、.asmdef |
| **アセット** | プレハブ、シーン、テクスチャ、オーディオ、アニメーション、マテリアル |
| **メタデータ** | GUID マッピング (.meta)、アセット間参照関係 |

### インターフェイス

#### CLI（人間向け）
```bash
# プロジェクトをインデックス化
unity-indexer index ./MyUnityProject

# アセット検索
unity-indexer search "PlayerController"

# 参照関係を調べる
unity-indexer refs Assets/Scripts/Player.cs

# シーン階層を表示
unity-indexer scene Assets/Scenes/Main.unity
```

#### MCP サーバー（AI 向け）
Claude 等の LLM から Unity プロジェクトの情報を直接参照できます。

```json
// 利用可能なツール例
{
  "tools": [
    "search_assets",       // アセット検索
    "get_script_info",     // スクリプト詳細
    "find_references",     // 参照元・参照先探索
    "get_scene_hierarchy", // シーン階層取得
    "summarize_project"    // プロジェクト概要
  ]
}
```

## アーキテクチャ

```
UnityIndexer/
├── src/
│   ├── indexer/          # インデックス生成コア
│   │   ├── code/         # C# スクリプト解析
│   │   ├── assets/       # アセットファイル解析
│   │   └── relations/    # 参照関係解析
│   ├── storage/          # インデックスデータ永続化
│   ├── interfaces/
│   │   ├── cli/          # 人間向け CLI
│   │   ├── web/          # 人間向け Web UI
│   │   └── mcp/          # AI 向け MCP サーバー
│   └── core/             # 共通モデル・ユーティリティ
├── tests/
├── docs/
└── examples/
```

## 開発状況

- [ ] コアインデックスエンジン（C# 解析、アセット解析、GUID マッピング）
- [ ] インデックスストレージ
- [ ] CLI インターフェイス
- [ ] MCP サーバー
- [ ] Web UI

## 技術スタック

- **言語**: Python 3.11+
- **MCP**: [Model Context Protocol](https://modelcontextprotocol.io/)
- **C# 解析**: tree-sitter 等
- **ストレージ**: SQLite（予定）

## ライセンス

MIT License - 詳細は [LICENSE](LICENSE) を参照
