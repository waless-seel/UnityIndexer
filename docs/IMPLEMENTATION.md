# UnityIndexer 実装資料

> 作成日: 2026-03-12
> 対象ブランチ: `claude/init-unity-indexing-L6tha`
> テスト: 8件 全通過

---

## ソリューション構成

```
UnityIndexer.sln
src/
  UnityIndexer.Core/       依存なし。モデル定義のみ。
  UnityIndexer.Analyzer/   Core を参照。アセット・コード解析。
  UnityIndexer.Storage/    Core を参照。SQLite 永続化。
  UnityIndexer.CLI/        Core + Analyzer + Storage を参照。CLI エントリポイント。
tests/
  UnityIndexer.Tests/      Core + Analyzer + Storage を参照。xUnit。
```

### NuGet パッケージ

| プロジェクト | パッケージ | 用途 |
|---|---|---|
| Analyzer | `Microsoft.CodeAnalysis.CSharp.Workspaces` | Roslyn C# 解析（未使用、将来用） |
| Analyzer | `Microsoft.CodeAnalysis.Workspaces.MSBuild` | MSBuildWorkspace（未使用、将来用） |
| Analyzer | `YamlDotNet` | 追加済み（現状未使用） |
| Analyzer | `QuikGraph` | 追加済み（現状未使用） |
| Storage | `Microsoft.Data.Sqlite` | SQLite 接続 |
| CLI | `System.CommandLine` `2.0.0-beta4.22272.1` | CLI フレームワーク |
| CLI | `Spectre.Console` | 追加済み（現状未使用） |

> **注意**: `YamlDotNet` / `QuikGraph` / `Spectre.Console` はパッケージ追加済みだが、
> 現時点では未使用。将来の C# 解析・グラフ構築・リッチ表示で使用予定。

---

## UnityIndexer.Core

### モデル一覧

#### `AssetType` (enum)
ファイル拡張子から判定するアセット種別。

```
Unknown, Script, Prefab, Scene, Material, Texture, Audio,
Animation, AnimatorController, ScriptableObject, Shader,
AssemblyDefinition, Font, Meta, Other
```

#### `AssetInfo`
アセット 1 件の基本情報。イミュータブル（init プロパティ）。

| プロパティ | 型 | 説明 |
|---|---|---|
| `Guid` | `string` | .meta から取得した 32桁 hex |
| `RelativePath` | `string` | プロジェクトルートからの相対パス |
| `Name` | `string` (computed) | ファイル名（拡張子なし） |
| `Type` | `AssetType` | 種別 |
| `FileSizeBytes` | `long` | ファイルサイズ |
| `LastModified` | `DateTime` | ファイル最終更新時刻 |
| `IndexedAt` | `DateTime` | インデックス化時刻 |

#### `ReferenceType` (enum) + `AssetReference`
アセット間の参照エッジ。有向グラフの辺に相当。

```
ReferenceType: Script, Texture, Material, Prefab, AudioClip,
               AnimationClip, AnimatorController, ScriptableObject,
               Shader, Assembly, Other

AssetReference: FromGuid → ToGuid, RefType, GameObjectName?
```

#### `ComponentInfo`
プレハブ/シーン内の GameObject に付いたコンポーネント情報。

| プロパティ | 説明 |
|---|---|
| `AssetGuid` | 含まれるプレハブ/シーンの GUID |
| `GameObjectName` | GameObject 名 |
| `ScriptGuid` | スクリプトコンポーネントの場合のスクリプト GUID |
| `BuiltinComponentClass` | ビルトインコンポーネント名 (例: "Rigidbody") |
| `Enabled` | コンポーネントが有効か |
| `HierarchyPath` | 階層パス (例: "Root/Child/Player") |

#### `ScriptTypeInfo` + `FieldInfo`
C# スクリプトの型情報（Roslyn 解析結果の格納先）。**現時点では未入力**。

| プロパティ | 説明 |
|---|---|
| `AssetGuid` | スクリプトアセットの GUID |
| `Namespace` / `ClassName` / `FullName` | 名前空間・クラス名 |
| `BaseTypeName` | 基底クラス |
| `Interfaces` | 実装インターフェイス一覧 |
| `Kind` | Class / Struct / Interface / Enum / Record |
| `IsMonoBehaviour` / `IsScriptableObject` / `IsEditorClass` | フラグ |
| `SerializedFields` | `[SerializeField]` フィールド一覧 |

#### `AssemblyDefinitionInfo`
.asmdef ファイルの解析結果。

| プロパティ | 説明 |
|---|---|
| `AssetGuid` | .asmdef アセットの GUID |
| `AssemblyName` | アセンブリ名 |
| `References` | 参照アセンブリ名一覧 |
| `IncludePlatforms` / `ExcludePlatforms` | プラットフォームフィルタ |
| `AllowUnsafeCode` / `AutoReferenced` | フラグ |

#### `ProjectIndex`
メモリ内のインデックス全体。`AssetAnalyzer` が構築し、`IndexDatabase` に書き込む。

| メンバ | 説明 |
|---|---|
| `Assets` | `Dict<guid, AssetInfo>` |
| `PathToGuid` | `Dict<relPath, guid>`（大文字小文字無視） |
| `References` | `List<AssetReference>` |
| `Components` | `List<ComponentInfo>` |
| `ScriptTypes` | `Dict<guid, ScriptTypeInfo>`（現状空） |
| `Assemblies` | `Dict<guid, AssemblyDefinitionInfo>` |
| `FindByGuid` / `FindByPath` | 逆引きヘルパー |
| `FindReferencingAssets` / `FindReferencedAssets` | グラフ走査ヘルパー |

### ユーティリティ

#### `AssetTypeDetector`
拡張子 → `AssetType` の静的マップで判定。大文字小文字無視。
対応拡張子: `.cs .prefab .unity .mat .shader .hlsl .cginc .anim .controller .asset .asmdef .meta .png .jpg .jpeg .tga .psd .exr .hdr .wav .mp3 .ogg .aif .aiff .ttf .otf`

---

## UnityIndexer.Analyzer

### `MetaFileParser`
**ファイル**: `Assets/MetaFileParser.cs`

`.meta` ファイルから GUID と対応するアセット情報を生成する。

- YAML フル解析ではなく**行スキャン**で `guid:` 行だけ抽出（高速）
- `.meta` に対応するアセットファイルが存在しない場合は `null` を返す
- `AssetTypeDetector` でアセット種別を判定

```
Parse(metaFilePath, projectRoot) → AssetInfo?
```

**制限・課題**:
- フォルダの `.meta` ファイル（対応するファイルが存在しない）は null を返すため、
  フォルダ自体はインデックスされない

---

### `YamlAssetParser`
**ファイル**: `Assets/YamlAssetParser.cs`

`.prefab` / `.unity` / `.mat` / `.asset` / `.controller` を正規表現ベースで解析し、
コンポーネント情報とアセット参照を抽出する。

Unity YAML は標準 YAML と異なり `!u!114` 等の独自タグを含むため、
YamlDotNet の標準パーサーが使えない → **正規表現スキャン**で対応。

#### 検出ロジック

| 対象 | 検出方法 |
|---|---|
| MonoBehaviour セクション | `--- !u!114 &XXXXXX` の行で `inMonoBehaviour = true` |
| スクリプト参照 | `m_Script: {fileID: ..., guid: XXXX, type: 3}` を `ScriptRefRegex` でマッチ |
| 全 GUID 参照 | `\bguid:\s*([0-9a-fA-F]{32})\b` を `GuidRefRegex` で全行スキャン |
| GameObject 名 | `m_Name: xxx` を `GameObjectNameRegex` で追跡 |
| 参照種別 | 行内のキーワード (`m_Texture`, `m_Material`, `m_AudioClip` 等) で判定 |

**制限・課題**:
- `currentGameObjectName` がセクション単位でリセットされない問題がある
  → プレハブ内で GameObject 名と MonoBehaviour が離れたセクションにある場合、
  前の GameObject 名が引き継がれる可能性がある
- 参照種別判定はヒューリスティック。誤判定の余地あり

---

### `AsmdefParser`
**ファイル**: `Assets/AsmdefParser.cs`

`.asmdef` (JSON フォーマット) を `System.Text.Json` で解析して `AssemblyDefinitionInfo` を返す。

```
Parse(assetGuid, filePath) → AssemblyDefinitionInfo?
```

解析フィールド: `name`, `references`, `includePlatforms`, `excludePlatforms`,
`allowUnsafeCode`, `autoReferenced`

---

### `AssetAnalyzer`
**ファイル**: `Assets/AssetAnalyzer.cs`

プロジェクト全体を走査して `ProjectIndex` を構築するオーケストレーター。

```
new AssetAnalyzer(projectRoot, progress?)
  .Analyze() → ProjectIndex
```

#### 処理フロー

```
Phase 1: *.meta を全走査
           └─ MetaFileParser.Parse() で GUID + AssetInfo を収集
           └─ index.Assets / index.PathToGuid に登録

Phase 2: YamlRefExtensions に該当するアセットを解析
           └─ YamlAssetParser.Parse() でコンポーネント・参照を収集
           └─ index.Components / index.References に追加

Phase 3: *.asmdef を全走査
           └─ AsmdefParser.Parse() でアセンブリ定義を収集
           └─ index.Assemblies に登録
```

#### スキップするディレクトリ
`Library, Temp, Logs, obj, Build, Builds, .git, .vs, node_modules`

**制限・課題**:
- Phase 2 は全 YAML アセットを毎回フルスキャンする。差分更新非対応
- `ScriptTypes` は未実装（Roslyn 解析が必要）

---

## UnityIndexer.Storage

### `IndexDatabase`
**ファイル**: `IndexDatabase.cs`

SQLite データベースの管理クラス。`IDisposable`。

#### スキーマ

```sql
assets (
    guid TEXT PRIMARY KEY,
    path TEXT, name TEXT, type TEXT,
    file_size INTEGER, modified_at TEXT, indexed_at TEXT
)

asset_refs (
    from_guid TEXT, to_guid TEXT, ref_type TEXT, go_name TEXT,
    PRIMARY KEY (from_guid, to_guid, ref_type)
)

components (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    asset_guid TEXT, go_name TEXT, script_guid TEXT,
    builtin_class TEXT, enabled INTEGER, hierarchy_path TEXT
)

script_types (  ← 現状未使用（Roslyn 解析実装後に使用予定）
    asset_guid TEXT PRIMARY KEY,
    namespace TEXT, class_name TEXT, base_type TEXT,
    kind TEXT, is_mono INTEGER, is_so INTEGER, is_editor INTEGER
)

assemblies (  ← 現状未使用（AsmdefParser の結果を書き込む処理が未実装）
    asset_guid TEXT PRIMARY KEY,
    assembly_name TEXT, references_json TEXT,
    allow_unsafe INTEGER, auto_referenced INTEGER
)

fts_assets USING fts5(  ← assets の FTS5 仮想テーブル
    guid UNINDEXED, name, path, type UNINDEXED,
    content='assets'
)
```

FTS5 は INSERT/UPDATE/DELETE トリガーで `assets` テーブルと自動同期。

#### 主要メソッド

| メソッド | 説明 |
|---|---|
| `UpsertAssets(assets)` | バルク Upsert (ON CONFLICT DO UPDATE) |
| `UpsertReferences(refs)` | バルク Upsert |
| `UpsertComponents(components)` | アセット単位で全削除→再挿入 |
| `SearchAssets(query, typeFilter?, limit)` | FTS5 全文検索（query 空は全件） |
| `FindReferencingAssets(toGuid)` | 被参照アセット検索 |
| `FindReferencedAssets(fromGuid)` | 参照先アセット検索 |
| `FindAsset(guidOrPath)` | GUID または相対パスで 1 件検索 |
| `CountAssets()` | 総件数 |

**FTS クエリ形式**: `"<query>"*`（前方一致のプレフィックス検索）

**制限・課題**:
- `assemblies` テーブルへの書き込み処理 (`UpsertAssemblies`) が未実装
- `script_types` テーブルへの書き込み処理も未実装
- コンポーネントの Upsert が全削除→再挿入なので、差分更新時に無駄が多い

---

## UnityIndexer.CLI

### エントリポイント `Program.cs`

```csharp
var root = new RootCommand("Unity プロジェクトのインデックスツール");
root.AddCommand(IndexCommand.Create());
root.AddCommand(SearchCommand.Create());
root.AddCommand(RefsCommand.Create());
root.AddCommand(InfoCommand.Create());
return await root.InvokeAsync(args);
```

### `DbHelper`

インデックス DB の場所管理。ファイル名は `.unity-indexer.db`。

| メソッド | 説明 |
|---|---|
| `TryOpen(projectPath?)` | カレントから最大 5 階層親まで DB を探して開く |
| `GetDbPath(projectRoot)` | DB ファイルパスを返す |

---

### `IndexCommand` — `unity-indexer index <project-path>`

| 引数/オプション | デフォルト | 説明 |
|---|---|---|
| `<project-path>` | (必須) | Unity プロジェクトルートパス |
| `--force` | false | 既存 DB を強制再構築 |
| `--verbose` | false | 詳細ログ（解析ファイルを逐次表示） |

処理フロー:
1. パス存在確認
2. DB がある場合 `--force` なしなら中断
3. `AssetAnalyzer.Analyze()` でインメモリインデックス構築
4. `IndexDatabase` に Upsert

---

### `SearchCommand` — `unity-indexer search [query]`

| 引数/オプション | デフォルト | 説明 |
|---|---|---|
| `[query]` | `""` (全件) | 検索キーワード |
| `--type` | null | 種別フィルタ (script/prefab/scene/...) |
| `--limit` | 50 | 最大件数 |
| `--json` | false | JSON 出力 |
| `--project` | null | プロジェクトパス |

出力（テーブル形式）:
```
TYPE      NAME            PATH
--------------------------------------------------------------
Script    PlayerController  Assets/Scripts/PlayerController.cs
Prefab    Player            Assets/Prefabs/Player.prefab
```

---

### `RefsCommand` — `unity-indexer refs <target>`

| 引数/オプション | デフォルト | 説明 |
|---|---|---|
| `<target>` | (必須) | GUID または相対パス |
| `--direction` | `both` | `in` (被参照) / `out` (参照先) / `both` |
| `--json` | false | JSON 出力 |

JSON 出力形式:
```json
{
  "asset": { "Guid": "...", "Path": "...", "Name": "...", "Type": "..." },
  "referencedBy": [...],
  "references": [...]
}
```

---

### `InfoCommand` — `unity-indexer info <target>`

GUID または相対パスでアセットの詳細情報と参照件数を表示する。
`refs` コマンドへの誘導メッセージを出力する。

---

## テスト (`UnityIndexer.Tests`)

| テストクラス | テスト数 | 内容 |
|---|---|---|
| `MetaFileParserTests` | 3 | 正常解析 / アセットファイル不在 / GUID なし |
| `YamlAssetParserTests` | 3 | スクリプト参照検出 / **fileID による GameObject 名解決** / 空ファイル |
| `IndexDatabaseTests` | 3 | 検索 / 参照関係 / パス検索 |

---

## 既知の課題・見直しポイント

### 解決済み ✓

| # | 場所 | 内容 |
|---|---|---|
| 1 | `YamlAssetParser` | ~~`currentGameObjectName` のリセットタイミングが不正確~~ → fileID マップ方式に変更 |
| 2 | `IndexDatabase` | ~~`UpsertAssemblies` が未実装~~ → 実装済み |
| 3 | `IndexCommand` | ~~`db.UpsertAssemblies()` の呼び出しがない~~ → 追加済み |
| 4 | `CLI` | ~~`scene` コマンドなし~~ → 追加済み (`SceneCommand`) |

### 優先度: 中（残課題）

| # | 場所 | 課題 |
|---|---|---|
| 5 | `YamlAssetParser` | 参照種別の判定がヒューリスティック。`m_Materials` と `m_Material` どちらも Material になるが競合の余地あり |
| 6 | `IndexDatabase.UpsertComponents` | 全削除→再挿入方式。差分更新時に非効率 |
| 7 | `AssetAnalyzer` | 差分更新非対応。毎回フルスキャン |
| 8 | `SearchCommand` | `--type` フィルタと FTS クエリで別 SQL 文になっており冗長 |

### 優先度: 低（将来実装）

| # | 課題 |
|---|---|
| 9  | `UnityIndexer.Analyzer/Code/` — Roslyn による C# 解析が未実装 |
| 10 | `impact` コマンド — スクリプト変更時の影響アセット計算 |
| 11 | `Spectre.Console` 活用 — 現状 `Console.WriteLine` 直接出力 |
| 12 | MCP サーバー実装 |

---

## 次セッション以降の残課題

### 優先度: 高

#### A. Roslyn による C# コード解析（`UnityIndexer.Analyzer/Code/`）

未実装。`ScriptTypes` テーブルは定義済みだが書き込みが行われていない。

```
実装対象:
  SolutionLoader.cs
    - MSBuildWorkspace で Unity の .sln を読み込む
    - 注意: Microsoft.Build をランタイムに登録する必要がある
      (MSBuildLocator.RegisterDefaults() または dotnet-sdk のパス指定)

  TypeAnalyzer.cs
    - Compilation / SemanticModel から型一覧を抽出
    - MonoBehaviour / ScriptableObject 判定（基底クラスチェーンを遡る）
    - [SerializeField] フィールドの抽出
    - IsEditorClass 判定（名前空間に UnityEditor が含まれるか）

  IndexDatabase.UpsertScriptTypes(IEnumerable<ScriptTypeInfo>)
    - script_types テーブルへの書き込み（現状未実装）

  IndexCommand での呼び出し
    - AssetAnalyzer.Analyze() の後に SolutionLoader → TypeAnalyzer を実行
    - db.UpsertScriptTypes() を追加
```

**注意点**:
- MSBuildWorkspace は .NET 8 環境での動作に注意が必要（MSBuildLocator パッケージが必要）
- Unity プロジェクトの .sln は Unity Editor が生成したもの。パスを引数で受け取るか、
  `<project-root>/*.sln` を自動検出する

---

#### B. `impact` コマンド

スクリプト変更時に影響を受けるプレハブ・シーンを一覧表示する。

```
unity-indexer impact <script-path-or-guid>   [--json]

実装方針:
  - db.FindReferencingAssets(scriptGuid) で直接参照元を取得
  - さらに参照元のプレハブを参照しているシーンも再帰的に探索
  - 出力: 影響アセット一覧（種別 / パス / 参照の深さ）

実装場所:
  src/UnityIndexer.CLI/Commands/ImpactCommand.cs
  IndexDatabase.FindTransitiveReferencingAssets(guid, maxDepth) を追加
```

---

### 優先度: 中

#### C. 差分更新対応

現状は `index` コマンドを実行するたびにプロジェクト全体を再スキャンする。

```
実装方針:
  1. DB の assets.modified_at とファイルの LastWriteTimeUtc を比較
  2. 変更・追加ファイルのみ再解析
  3. 削除ファイルは DB から削除（孤立した参照のクリーンアップ）

影響クラス:
  AssetAnalyzer: フルスキャンと差分スキャンを切り替えるオプション追加
  IndexCommand:  --incremental フラグを追加
```

#### D. `IndexDatabase.UpsertComponents` の効率化

現状はアセット単位で全削除→再挿入。差分更新との組み合わせで非効率になる。

```
改善案:
  - component の hash（go_name + script_guid の組み合わせ）で差分検出
  - または変更アセットのみ削除→再挿入（アセット単位は現状でも可）
```

---

### 優先度: 低

#### E. 出力のリッチ化（`Spectre.Console`）

パッケージは追加済み。現状は `Console.WriteLine` 直接出力。

```
候補:
  - search / refs コマンドのテーブル表示を Spectre.Console.Table に変更
  - scene コマンドの階層表示を Tree ウィジェットに変更
  - index コマンドの進捗表示を ProgressBar に変更
```

#### F. MCP サーバー実装

CLI コマンドと同じロジックを再利用する形で実装。
CLI が充実した後に着手する。

---

## セッション記録

| セッション | 実施内容 |
|---|---|
| Session 1 | ソリューション構成・Core モデル・Analyzer (アセット解析)・Storage・CLI 基本コマンド・テスト |
| Session 2 | YamlAssetParser バグ修正・UpsertAssemblies 実装・scene コマンド・IMPLEMENTATION.md 作成 |
