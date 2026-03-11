# UnityIndexer - Claude Code Instructions

## プロジェクト概要

UnityIndexer は Unity プロジェクトのコードベース・アセット・その関係性を解析し、
構造化されたインデックスを生成するシステムです。

**目標:**
- Unity プロジェクトの全要素（C# スクリプト、アセット、シーン等）をインデックス化
- 人間向けインターフェイス（CLI、Web UI）の提供
- AI 向けインターフェイス（MCP サーバー、構造化出力）の提供

## リポジトリ構成（予定）

```
UnityIndexer/
├── src/
│   ├── indexer/          # インデックス生成コア
│   │   ├── code/         # C# スクリプト解析
│   │   ├── assets/       # アセット解析
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

## 開発ガイドライン

### コーディング規約
- 言語: Python 3.11+（メイン実装）
- 型ヒントを必ず付与する
- パブリック関数にはドキュメント文字列を書く

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

### AI インターフェイス設計方針
- MCP (Model Context Protocol) サーバーとして実装
- ツール例:
  - `search_assets` - アセット検索
  - `get_script_info` - スクリプト詳細取得
  - `find_references` - 参照元・参照先の探索
  - `get_scene_hierarchy` - シーン階層取得
  - `summarize_project` - プロジェクト概要生成

### 人間向けインターフェイス設計方針
- CLI: `unity-indexer index <path>` / `unity-indexer search <query>`
- 出力形式: JSON、テーブル、ツリー表示

## 開発時の注意

- Unity の .meta ファイルは GUID マッピングに不可欠なので必ず解析する
- 大規模プロジェクト（数千アセット）での性能を意識する
- インデックスのキャッシュ・差分更新を考慮する
- `.gitignore` 対象（Library/ 等）はインデックス対象外
