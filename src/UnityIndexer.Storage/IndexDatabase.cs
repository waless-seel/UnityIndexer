using Microsoft.Data.Sqlite;
using UnityIndexer.Core.Models;

namespace UnityIndexer.Storage;

/// <summary>
/// UnityIndexer の SQLite データベース管理クラス。
/// マイグレーション・CRUD・全文検索を担当する。
/// </summary>
public sealed class IndexDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public IndexDatabase(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        ApplyMigrations();
    }

    // -------------------------------------------------------
    // マイグレーション
    // -------------------------------------------------------

    private void ApplyMigrations()
    {
        ExecuteNonQuery("PRAGMA journal_mode=WAL;");
        ExecuteNonQuery("PRAGMA foreign_keys=ON;");

        ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS assets (
                guid        TEXT PRIMARY KEY,
                path        TEXT NOT NULL,
                name        TEXT NOT NULL,
                type        TEXT NOT NULL,
                file_size   INTEGER NOT NULL DEFAULT 0,
                modified_at TEXT NOT NULL,
                indexed_at  TEXT NOT NULL
            );
            """);

        ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS asset_refs (
                from_guid   TEXT NOT NULL,
                to_guid     TEXT NOT NULL,
                ref_type    TEXT NOT NULL,
                go_name     TEXT,
                PRIMARY KEY (from_guid, to_guid, ref_type)
            );
            """);

        ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS components (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                asset_guid      TEXT NOT NULL,
                go_name         TEXT NOT NULL,
                script_guid     TEXT,
                builtin_class   TEXT,
                enabled         INTEGER NOT NULL DEFAULT 1,
                hierarchy_path  TEXT
            );
            """);

        ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS script_types (
                asset_guid      TEXT PRIMARY KEY,
                namespace       TEXT,
                class_name      TEXT NOT NULL,
                base_type       TEXT,
                kind            TEXT NOT NULL,
                is_mono         INTEGER NOT NULL DEFAULT 0,
                is_so           INTEGER NOT NULL DEFAULT 0,
                is_editor       INTEGER NOT NULL DEFAULT 0
            );
            """);

        ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS assemblies (
                asset_guid      TEXT PRIMARY KEY,
                assembly_name   TEXT NOT NULL,
                references_json TEXT NOT NULL DEFAULT '[]',
                allow_unsafe    INTEGER NOT NULL DEFAULT 0,
                auto_referenced INTEGER NOT NULL DEFAULT 1
            );
            """);

        // 既存 script_types テーブルへの列追加マイグレーション（列が既に存在する場合は無視）
        TryExecuteNonQuery("ALTER TABLE script_types ADD COLUMN interfaces TEXT;");
        TryExecuteNonQuery("ALTER TABLE script_types ADD COLUMN serialized_fields TEXT;");

        // FTS5 全文検索テーブル
        ExecuteNonQuery("""
            CREATE VIRTUAL TABLE IF NOT EXISTS fts_assets
            USING fts5(guid UNINDEXED, name, path, type UNINDEXED, content='assets', content_rowid='rowid');
            """);

        // FTS 自動更新トリガー
        ExecuteNonQuery("""
            CREATE TRIGGER IF NOT EXISTS assets_ai AFTER INSERT ON assets BEGIN
                INSERT INTO fts_assets(rowid, guid, name, path, type)
                VALUES (new.rowid, new.guid, new.name, new.path, new.type);
            END;
            """);
        ExecuteNonQuery("""
            CREATE TRIGGER IF NOT EXISTS assets_ad AFTER DELETE ON assets BEGIN
                INSERT INTO fts_assets(fts_assets, rowid, guid, name, path, type)
                VALUES ('delete', old.rowid, old.guid, old.name, old.path, old.type);
            END;
            """);
        ExecuteNonQuery("""
            CREATE TRIGGER IF NOT EXISTS assets_au AFTER UPDATE ON assets BEGIN
                INSERT INTO fts_assets(fts_assets, rowid, guid, name, path, type)
                VALUES ('delete', old.rowid, old.guid, old.name, old.path, old.type);
                INSERT INTO fts_assets(rowid, guid, name, path, type)
                VALUES (new.rowid, new.guid, new.name, new.path, new.type);
            END;
            """);
    }

    // -------------------------------------------------------
    // アセット CRUD
    // -------------------------------------------------------

    public void UpsertAssets(IEnumerable<AssetInfo> assets)
    {
        using var tx = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO assets (guid, path, name, type, file_size, modified_at, indexed_at)
            VALUES ($guid, $path, $name, $type, $size, $mod, $idx)
            ON CONFLICT(guid) DO UPDATE SET
                path=$path, name=$name, type=$type, file_size=$size,
                modified_at=$mod, indexed_at=$idx;
            """;

        var pGuid = cmd.Parameters.Add("$guid", SqliteType.Text);
        var pPath = cmd.Parameters.Add("$path", SqliteType.Text);
        var pName = cmd.Parameters.Add("$name", SqliteType.Text);
        var pType = cmd.Parameters.Add("$type", SqliteType.Text);
        var pSize = cmd.Parameters.Add("$size", SqliteType.Integer);
        var pMod  = cmd.Parameters.Add("$mod",  SqliteType.Text);
        var pIdx  = cmd.Parameters.Add("$idx",  SqliteType.Text);

        foreach (var a in assets)
        {
            pGuid.Value = a.Guid;
            pPath.Value = a.RelativePath;
            pName.Value = a.Name;
            pType.Value = a.Type.ToString();
            pSize.Value = a.FileSizeBytes;
            pMod.Value  = a.LastModified.ToString("O");
            pIdx.Value  = a.IndexedAt.ToString("O");
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public void UpsertReferences(IEnumerable<AssetReference> refs)
    {
        using var tx = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO asset_refs (from_guid, to_guid, ref_type, go_name)
            VALUES ($from, $to, $ref, $go);
            """;

        var pFrom = cmd.Parameters.Add("$from", SqliteType.Text);
        var pTo   = cmd.Parameters.Add("$to",   SqliteType.Text);
        var pRef  = cmd.Parameters.Add("$ref",  SqliteType.Text);
        var pGo   = cmd.Parameters.Add("$go",   SqliteType.Text);

        foreach (var r in refs)
        {
            pFrom.Value = r.FromGuid;
            pTo.Value   = r.ToGuid;
            pRef.Value  = r.RefType.ToString();
            pGo.Value   = (object?)r.GameObjectName ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public void UpsertAssemblies(IEnumerable<AssemblyDefinitionInfo> assemblies)
    {
        using var tx = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO assemblies (asset_guid, assembly_name, references_json, allow_unsafe, auto_referenced)
            VALUES ($guid, $name, $refs, $unsafe, $auto)
            ON CONFLICT(asset_guid) DO UPDATE SET
                assembly_name=$name, references_json=$refs,
                allow_unsafe=$unsafe, auto_referenced=$auto;
            """;

        var pGuid   = cmd.Parameters.Add("$guid",   SqliteType.Text);
        var pName   = cmd.Parameters.Add("$name",   SqliteType.Text);
        var pRefs   = cmd.Parameters.Add("$refs",   SqliteType.Text);
        var pUnsafe = cmd.Parameters.Add("$unsafe", SqliteType.Integer);
        var pAuto   = cmd.Parameters.Add("$auto",   SqliteType.Integer);

        foreach (var a in assemblies)
        {
            pGuid.Value   = a.AssetGuid;
            pName.Value   = a.AssemblyName;
            pRefs.Value   = System.Text.Json.JsonSerializer.Serialize(a.References);
            pUnsafe.Value = a.AllowUnsafeCode ? 1 : 0;
            pAuto.Value   = a.AutoReferenced  ? 1 : 0;
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public void UpsertScriptTypes(IEnumerable<ScriptTypeInfo> types)
    {
        using var tx = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO script_types
                (asset_guid, namespace, class_name, base_type, kind,
                 is_mono, is_so, is_editor, interfaces, serialized_fields)
            VALUES ($guid, $ns, $cls, $base, $kind, $mono, $so, $ed, $ifaces, $fields)
            ON CONFLICT(asset_guid) DO UPDATE SET
                namespace=$ns, class_name=$cls, base_type=$base, kind=$kind,
                is_mono=$mono, is_so=$so, is_editor=$ed,
                interfaces=$ifaces, serialized_fields=$fields;
            """;

        var pGuid   = cmd.Parameters.Add("$guid",   SqliteType.Text);
        var pNs     = cmd.Parameters.Add("$ns",     SqliteType.Text);
        var pCls    = cmd.Parameters.Add("$cls",    SqliteType.Text);
        var pBase   = cmd.Parameters.Add("$base",   SqliteType.Text);
        var pKind   = cmd.Parameters.Add("$kind",   SqliteType.Text);
        var pMono   = cmd.Parameters.Add("$mono",   SqliteType.Integer);
        var pSo     = cmd.Parameters.Add("$so",     SqliteType.Integer);
        var pEd     = cmd.Parameters.Add("$ed",     SqliteType.Integer);
        var pIfaces = cmd.Parameters.Add("$ifaces", SqliteType.Text);
        var pFields = cmd.Parameters.Add("$fields", SqliteType.Text);

        foreach (var t in types)
        {
            pGuid.Value   = t.AssetGuid;
            pNs.Value     = (object?)t.Namespace ?? DBNull.Value;
            pCls.Value    = t.ClassName;
            pBase.Value   = (object?)t.BaseTypeName ?? DBNull.Value;
            pKind.Value   = t.Kind.ToString();
            pMono.Value   = t.IsMonoBehaviour   ? 1 : 0;
            pSo.Value     = t.IsScriptableObject ? 1 : 0;
            pEd.Value     = t.IsEditorClass      ? 1 : 0;
            pIfaces.Value = System.Text.Json.JsonSerializer.Serialize(t.Interfaces);
            pFields.Value = System.Text.Json.JsonSerializer.Serialize(t.SerializedFields);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public void UpsertComponents(IEnumerable<ComponentInfo> components)
    {
        using var tx = _connection.BeginTransaction();
        // コンポーネントはアセット単位で全削除して再挿入
        var grouped = components.GroupBy(c => c.AssetGuid);
        foreach (var group in grouped)
        {
            using var del = _connection.CreateCommand();
            del.CommandText = "DELETE FROM components WHERE asset_guid=$g";
            del.Parameters.AddWithValue("$g", group.Key);
            del.ExecuteNonQuery();

            using var ins = _connection.CreateCommand();
            ins.CommandText = """
                INSERT INTO components (asset_guid, go_name, script_guid, builtin_class, enabled, hierarchy_path)
                VALUES ($ag, $go, $sg, $bc, $en, $hp);
                """;
            foreach (var c in group)
            {
                ins.Parameters.Clear();
                ins.Parameters.AddWithValue("$ag", c.AssetGuid);
                ins.Parameters.AddWithValue("$go", c.GameObjectName);
                ins.Parameters.AddWithValue("$sg", (object?)c.ScriptGuid ?? DBNull.Value);
                ins.Parameters.AddWithValue("$bc", (object?)c.BuiltinComponentClass ?? DBNull.Value);
                ins.Parameters.AddWithValue("$en", c.Enabled ? 1 : 0);
                ins.Parameters.AddWithValue("$hp", (object?)c.HierarchyPath ?? DBNull.Value);
                ins.ExecuteNonQuery();
            }
        }
        tx.Commit();
    }

    // -------------------------------------------------------
    // 検索
    // -------------------------------------------------------

    /// <summary>全文検索（FTS5）でアセットを検索する</summary>
    public IReadOnlyList<AssetSearchResult> SearchAssets(string query, AssetType? typeFilter = null, int limit = 50)
    {
        using var cmd = _connection.CreateCommand();

        if (string.IsNullOrWhiteSpace(query))
        {
            cmd.CommandText = typeFilter is null
                ? "SELECT guid, path, name, type FROM assets LIMIT $limit"
                : "SELECT guid, path, name, type FROM assets WHERE type=$type LIMIT $limit";
        }
        else
        {
            cmd.CommandText = typeFilter is null
                ? """
                  SELECT a.guid, a.path, a.name, a.type
                  FROM fts_assets f JOIN assets a ON f.guid=a.guid
                  WHERE fts_assets MATCH $q
                  ORDER BY rank
                  LIMIT $limit
                  """
                : """
                  SELECT a.guid, a.path, a.name, a.type
                  FROM fts_assets f JOIN assets a ON f.guid=a.guid
                  WHERE fts_assets MATCH $q AND a.type=$type
                  ORDER BY rank
                  LIMIT $limit
                  """;
            cmd.Parameters.AddWithValue("$q", EscapeFtsQuery(query));
        }

        if (typeFilter is not null)
            cmd.Parameters.AddWithValue("$type", typeFilter.Value.ToString());
        cmd.Parameters.AddWithValue("$limit", limit);

        var results = new List<AssetSearchResult>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new AssetSearchResult(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }
        return results;
    }

    /// <summary>指定アセットを参照しているアセットを返す</summary>
    public IReadOnlyList<AssetSearchResult> FindReferencingAssets(string toGuid)
        => QueryAssetsByGuid("""
            SELECT a.guid, a.path, a.name, a.type
            FROM asset_refs r JOIN assets a ON r.from_guid=a.guid
            WHERE r.to_guid=$guid
            """, toGuid);

    /// <summary>指定アセットが参照しているアセットを返す</summary>
    public IReadOnlyList<AssetSearchResult> FindReferencedAssets(string fromGuid)
        => QueryAssetsByGuid("""
            SELECT a.guid, a.path, a.name, a.type
            FROM asset_refs r JOIN assets a ON r.to_guid=a.guid
            WHERE r.from_guid=$guid
            """, fromGuid);

    /// <summary>GUID または相対パスでアセットを検索する</summary>
    public AssetSearchResult? FindAsset(string guidOrPath)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT guid, path, name, type FROM assets WHERE guid=$v OR path=$v LIMIT 1";
        cmd.Parameters.AddWithValue("$v", guidOrPath);
        using var reader = cmd.ExecuteReader();
        return reader.Read()
            ? new AssetSearchResult(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3))
            : null;
    }

    /// <summary>指定アセット (プレハブ/シーン) 内のコンポーネント一覧を返す</summary>
    public IReadOnlyList<ComponentRow> GetComponents(string assetGuid)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT c.go_name, c.script_guid, c.builtin_class, c.enabled, c.hierarchy_path,
                   a.path as script_path, a.name as script_name
            FROM components c
            LEFT JOIN assets a ON c.script_guid = a.guid
            WHERE c.asset_guid = $guid
            ORDER BY c.go_name
            """;
        cmd.Parameters.AddWithValue("$guid", assetGuid);

        var results = new List<ComponentRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new ComponentRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetInt32(3) != 0,
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }
        return results;
    }

    public int CountAssets()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM assets";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int CountScriptTypes()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM script_types";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>GUID でスクリプト型情報を検索する</summary>
    public (string ClassName, bool IsMono, bool IsSo, string? Namespace)? GetScriptType(string guid)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT class_name, is_mono, is_so, namespace FROM script_types WHERE asset_guid=$guid";
        cmd.Parameters.AddWithValue("$guid", guid);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return (reader.GetString(0), reader.GetInt32(1) != 0, reader.GetInt32(2) != 0,
                reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    // -------------------------------------------------------
    // ヘルパー
    // -------------------------------------------------------

    private IReadOnlyList<AssetSearchResult> QueryAssetsByGuid(string sql, string guid)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$guid", guid);
        var results = new List<AssetSearchResult>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add(new AssetSearchResult(
                reader.GetString(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3)));
        return results;
    }

    private static string EscapeFtsQuery(string query)
    {
        // FTS5 の特殊文字をエスケープして prefix search に変換
        var escaped = query.Replace("\"", "\"\"");
        return $"\"{escaped}\"*";
    }

    private void ExecuteNonQuery(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>SQL を実行し、例外は無視する（ALTER TABLE IF NOT EXISTS の代替）</summary>
    private void TryExecuteNonQuery(string sql)
    {
        try { ExecuteNonQuery(sql); }
        catch { /* 列が既に存在する場合など、エラーは無視 */ }
    }

    public void Dispose() => _connection.Dispose();
}

/// <summary>アセット検索結果の軽量 DTO</summary>
public sealed record AssetSearchResult(string Guid, string Path, string Name, string Type);

/// <summary>コンポーネント行の DTO</summary>
public sealed record ComponentRow(
    string GameObjectName,
    string? ScriptGuid,
    string? BuiltinClass,
    bool Enabled,
    string? HierarchyPath,
    string? ScriptPath,
    string? ScriptName);
