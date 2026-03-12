# UnityIndexer タスク資料

> 作成日: 2026-03-11

## フェーズ構成

```
Phase 1: 基盤 (Core + ソリューション構成)
Phase 2: Analyzer (C# 解析 + Unity アセット解析)
Phase 3: Storage (SQLite 永続化)
Phase 4: CLI (コマンドライン実装)
Phase 5: MCP (オプション)
```

---

## Phase 1: プロジェクト基盤

### 1-1. ソリューション構成

```
UnityIndexer.sln
src/
  UnityIndexer.Core/         .NET 10 classlib
  UnityIndexer.Analyzer/     .NET 10 classlib
  UnityIndexer.Storage/      .NET 10 classlib
  UnityIndexer.CLI/          .NET 10 console
tests/
  UnityIndexer.Tests/        xUnit
```

### 1-2. 主要 NuGet パッケージ

| プロジェクト | パッケージ |
|---|---|
| Core | (依存なし) |
| Analyzer | `Microsoft.CodeAnalysis.CSharp.Workspaces` `Microsoft.CodeAnalysis.Workspaces.MSBuild` `YamlDotNet` `QuikGraph` |
| Storage | `Microsoft.Data.Sqlite` |
| CLI | `System.CommandLine` |

---

## Phase 2: Analyzer

### 2-1. Unity アセット解析（優先）

Unity アセット系ファイルはすべて **YAML** ベース。
Roslyn 不要、YamlDotNet で解析する。

#### 対象ファイルと取得情報

| ファイル種別 | 拡張子 | 取得情報 |
|---|---|---|
| メタファイル | `.meta` | GUID、importer 種別 |
| プレハブ | `.prefab` | GameObject 階層、コンポーネント一覧、スクリプト参照 (m_Script fileID/guid) |
| シーン | `.unity` | 同上 |
| マテリアル | `.mat` | シェーダー参照、テクスチャ参照 |
| アニメーターコントローラー | `.controller` | ステートマシン、クリップ参照 |
| ScriptableObject | `.asset` | スクリプト参照、シリアライズフィールド値 |
| アセンブリ定義 | `.asmdef` | 名前、参照アセンブリ一覧 |

#### GUID 解決の仕組み

```
.meta ファイル → guid: <32桁 hex>
→ GuidRegistry に登録
→ .prefab 内の m_Script: {fileID: X, guid: Y, type: Z} から Y で逆引き
→ "このプレハブはスクリプト Z を使っている" という関係を構築
```

#### 実装クラス構成 (UnityIndexer.Analyzer/Assets/)

```
MetaFileParser.cs          .meta → GuidEntry
PrefabSceneParser.cs       .prefab/.unity → GameObjectTree
MaterialParser.cs          .mat → MaterialInfo
AsmdefParser.cs            .asmdef → AssemblyDefinitionInfo
AssetAnalyzer.cs           プロジェクト全体を走査してアセットグラフ構築
```

### 2-2. C# コード解析

MSBuildWorkspace で Unity の `.sln` を読み込み、Roslyn で解析する。

#### 取得情報

- 型一覧（名前空間、基底クラス、インターフェイス実装）
- MonoBehaviour / ScriptableObject 継承判定
- フィールド・プロパティ（型、アクセシビリティ）
- メソッド（シグネチャ、呼び出し関係）
- `[SerializeField]` / `[Header]` 等の属性

#### 実装クラス構成 (UnityIndexer.Analyzer/Code/)

```
SolutionLoader.cs          MSBuildWorkspace でソリューション読み込み
TypeAnalyzer.cs            SemanticModel から型情報抽出
InheritanceGraph.cs        QuikGraph で継承グラフ構築
CallGraph.cs               メソッド呼び出しグラフ
```

### 2-3. 影響範囲計算 (UnityIndexer.Analyzer/Impact/)

```
ImpactAnalyzer.cs          スクリプト変更時に影響するプレハブ・シーンを計算
```

---

## Phase 3: Storage

### スキーマ概要（SQLite）

```sql
-- アセット基本情報
assets (id, guid, path, type, name, indexed_at)

-- コードの型情報
types (id, asset_id, namespace, name, base_type, kind, is_mono_behaviour)

-- アセット参照関係
asset_refs (from_guid, to_guid, ref_type)
-- ref_type: 'script', 'texture', 'material', 'prefab', etc.

-- プレハブ/シーン内のコンポーネント
components (id, asset_id, game_object_name, script_guid, enabled)

-- 検索用インデックス
fts_assets (rowid, name, path, type)   -- FTS5 仮想テーブル
```

### 実装クラス (UnityIndexer.Storage/)

```
IndexDatabase.cs           DB 接続管理・マイグレーション
AssetRepository.cs         アセット CRUD
TypeRepository.cs          型情報 CRUD
SearchRepository.cs        FTS5 全文検索
```

---

## Phase 4: CLI

### コマンド体系

```bash
unity-indexer index <project-path>   [--force] [--verbose]
unity-indexer search <query>         [--type script|prefab|scene|...] [--json]
unity-indexer refs <asset-path>      [--json]
unity-indexer impact <symbol>        [--json]
unity-indexer scene <scene-path>     [--json]
unity-indexer info <guid|path>       [--json]
```

### 出力フォーマット

- デフォルト: テーブル / ツリー（Spectre.Console）
- `--json`: JSON（AI からの利用を想定）

---

## Phase 5: MCP（後回し）

CLI コマンドと同じロジックを再利用。
`unity-indexer search` 等を内部で呼び出す形で実装。

---

## 実装優先順位

```
[高] Phase 1 → Phase 2-1 (アセット解析) → Phase 3 → Phase 4
[中] Phase 2-2 (C# 解析)
[低] Phase 2-3 (影響範囲) → Phase 5 (MCP)
```

### 理由

- Unity アセット解析は Roslyn 不要で実装できるため早期に価値を出せる
- C# 解析は MSBuildWorkspace セットアップが複雑で後回し
- アセット解析 + 検索 + CLI が揃えば実用的なツールになる

---

## 今セッションのスコープ

1. ソリューション構成作成
2. Core モデル定義
3. Analyzer - アセット解析（.meta, .prefab, .unity, .asmdef）
4. Storage - SQLite 基本スキーマ
5. CLI - `index` / `search` / `refs` コマンド
