using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bing.Resilience.Operations.Relational;

public sealed partial class RelationalOperationStore
{
    /// <summary>当前 Journal 结构版本。版本历史由宿主迁移工具管理。</summary>
    public const int SchemaVersion = 1;

    /// <summary>获取已替换安全表名前缀的 v1 脚本；只返回文本，不执行 DDL。</summary>
    public string GetMigrationScript()
    {
        using var stream = typeof(RelationalOperationStore).Assembly.GetManifestResourceStream(
            $"Bing.Resilience.Migrations.{_dialect}.v1.sql")!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd().Replace("{{prefix}}", _prefix, StringComparison.Ordinal);
    }

    /// <summary>只读检查必需列、空值约束、主键、幂等唯一键及认领索引；不会创建或修复结构。</summary>
    /// <remarks>适用于宿主发布/启动检查。它不替代宿主的迁移版本历史，也不检查业务表。</remarks>
    public async ValueTask ValidateSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var errors = new List<string>();
        foreach (var (suffix, columns, primary) in new[]
        {
            ("operations", "id:64 kind:64 scope_hash:64 key_hash:64 fingerprint:64 state:64 version:# data:* attempts:int created_at:# updated_at:# next_at:# deadline:# lease_token:64? lease_until:#?", "id"),
            ("attempts", "operation_id:64 attempt_no:int kind:64 lease_token:64 started_at:# completed_at:#? outcome:64? generation:#? endpoint:256?", "operation_id,attempt_no"),
            ("audit", "operation_id:64 version:# event_type:64 actor:128 reason:* at_utc:#", "operation_id,version,event_type")
        })
        {
            var table = _prefix + "_" + suffix;
            if (_dialect == ResilienceRelationalDialect.Sqlite)
                await ValidateSqliteCollationsAsync(connection, table, errors, cancellationToken).ConfigureAwait(false);
            var actual = await ReadColumnsAsync(connection, table, cancellationToken).ConfigureAwait(false);
            foreach (var spec in columns.Split(' '))
            {
                var pair = spec.Split(':'); var nullable = pair[1].EndsWith('?'); var type = pair[1].TrimEnd('?');
                if (!actual.TryGetValue(pair[0], out var column)) { errors.Add(table + "." + pair[0] + " 缺失"); continue; }
                var isInteger = type is "#" or "int";
                var validType = _dialect == ResilienceRelationalDialect.Sqlite
                    ? isInteger ? column.Type.Contains("INT", StringComparison.OrdinalIgnoreCase) :
                        new[] { "CHAR", "TEXT", "CLOB" }.Any(t => column.Type.Contains(t, StringComparison.OrdinalIgnoreCase))
                    : isInteger ? column.Type == (type == "#" ? "bigint" : "int") :
                        type == "*" ? column.Type is "longtext" :
                        (column.Type is "varchar" or "char") && column.Length >= int.Parse(type, CultureInfo.InvariantCulture);
                if (!validType) errors.Add(table + "." + pair[0] + " 类型不兼容");
                // SQLite 的非 INTEGER PRIMARY KEY 可将 notnull 标为 0；已有 v1 结构保持兼容。
                if (column.Nullable != nullable && !(_dialect == ResilienceRelationalDialect.Sqlite && primary.Split(',').Contains(pair[0])))
                    errors.Add(table + "." + pair[0] + " 空值约束不兼容");
                if (_dialect == ResilienceRelationalDialect.MySql && !isInteger && column.Collation != "utf8mb4_bin")
                    errors.Add(table + "." + pair[0] + " 必须使用 utf8mb4_bin");
            }
            var indexes = await ReadIndexesAsync(connection, table, cancellationToken).ConfigureAwait(false);
            if (!indexes.Any(x => x.Primary && x.Columns == primary)) errors.Add(table + " 主键不兼容");
            if (suffix == "operations")
            {
                if (!indexes.Any(x => x.Unique && x.Columns == "kind,scope_hash,key_hash")) errors.Add(table + " 幂等唯一键缺失");
                var hasClaimIndex = _dialect == ResilienceRelationalDialect.MySql
                    ? indexes.Any(x => string.Equals(x.Name, _prefix + "_due", StringComparison.OrdinalIgnoreCase) && x.Columns == "kind,next_at,id")
                    : indexes.Any(x => x.Columns == "kind,next_at,id");
                if (!hasClaimIndex) errors.Add(table + " 认领索引缺失或名称/列不兼容");
            }
            if (_dialect == ResilienceRelationalDialect.MySql)
            {
                await using var command = MetadataCommand(connection,
                    "SELECT engine FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name=@table", table);
                if (!string.Equals(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string, "InnoDB", StringComparison.OrdinalIgnoreCase))
                    errors.Add(table + " 必须使用 InnoDB");
            }
        }
        if (errors.Count > 0) throw new ResilienceConfigurationException("Journal 结构校验失败：" + string.Join("；", errors));
    }

    private sealed record SchemaColumn(string Type, bool Nullable, long Length, string? Collation);
    private sealed record SchemaIndex(string Name, bool Primary, bool Unique, string Columns);
    private static async Task ValidateSqliteCollationsAsync(DbConnection connection, string table, List<string> errors, CancellationToken token)
    {
        await using var command = MetadataCommand(connection, "SELECT sql FROM sqlite_master WHERE type='table' AND name=@table", table);
        var ddl = await command.ExecuteScalarAsync(token).ConfigureAwait(false) as string ?? "";
        // SQLite 不通过 table_info 暴露列排序规则。按词法读取 COLLATE 后的名称，
        // 保留单引号名称；注释、默认字符串和带引号标识符中的 COLLATE 不作为关键字。
        // 同时检查未索引的 state/lease_token，避免 CAS 比较改变语义。
        var tokens = Regex.Matches(ddl, @"--[^\r\n]*|/\*[\s\S]*?\*/|'(?:''|[^'])*'|""(?:""""|[^""])*""|`(?:``|[^`])*`|\[[^\]]*\]|\w+", RegexOptions.CultureInvariant);
        var expectCollation = false;
        foreach (Match match in tokens)
        {
            var value = match.Value;
            if (value.StartsWith("--", StringComparison.Ordinal) || value.StartsWith("/*", StringComparison.Ordinal)) continue;
            if (expectCollation)
            {
                if (value[0] is '\'' or '"' or '`' or '[') value = value[1..^1];
                if (!string.Equals(value, "BINARY", StringComparison.OrdinalIgnoreCase))
                { errors.Add(table + " 只能使用 BINARY 排序规则"); return; }
            }
            expectCollation = string.Equals(match.Value, "COLLATE", StringComparison.OrdinalIgnoreCase);
        }
    }
    private static DbCommand MetadataCommand(DbConnection connection, string sql, string table)
    {
        var command = connection.CreateCommand(); command.CommandText = sql;
        var parameter = command.CreateParameter(); parameter.ParameterName = "@table"; parameter.Value = table; command.Parameters.Add(parameter);
        return command;
    }
    private async Task<Dictionary<string, SchemaColumn>> ReadColumnsAsync(DbConnection connection, string table, CancellationToken token)
    {
        await using var command = MetadataCommand(connection, _dialect == ResilienceRelationalDialect.Sqlite
            ? $"PRAGMA table_info('{table}')"
            : "SELECT column_name,data_type,is_nullable,character_maximum_length,collation_name FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name=@table", table);
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var result = new Dictionary<string, SchemaColumn>(StringComparer.Ordinal);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            if (_dialect == ResilienceRelationalDialect.Sqlite)
                result.Add(reader.GetString(1), new(reader.GetString(2), reader.GetInt32(3) == 0, 0, null));
            else result.Add(reader.GetString(0), new(reader.GetString(1), reader.GetString(2) == "YES",
                reader.IsDBNull(3) ? 0 : Convert.ToInt64(reader[3], CultureInfo.InvariantCulture), reader.IsDBNull(4) ? null : reader.GetString(4)));
        }
        return result;
    }
    private async Task<List<SchemaIndex>> ReadIndexesAsync(DbConnection connection, string table, CancellationToken token)
    {
        var result = new List<SchemaIndex>();
        if (_dialect == ResilienceRelationalDialect.MySql)
        {
            await using var command = MetadataCommand(connection,
                "SELECT index_name,non_unique,GROUP_CONCAT(column_name ORDER BY seq_in_index SEPARATOR ',') FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name=@table GROUP BY index_name,non_unique HAVING MAX(COALESCE(sub_part,0))=0", table);
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                var name = reader.GetString(0);
                result.Add(new(name, string.Equals(name, "PRIMARY", StringComparison.OrdinalIgnoreCase),
                    Convert.ToInt32(reader[1], CultureInfo.InvariantCulture) == 0, reader.GetString(2)));
            }
            return result;
        }
        var indexes = new List<(string Name, bool Unique, bool Primary)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"PRAGMA index_list('{table}')";
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                if (reader.GetInt32(4) == 0) indexes.Add((reader.GetString(1), reader.GetInt32(2) == 1, reader.GetString(3) == "pk"));
        }
        foreach (var index in indexes)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA index_xinfo('" + index.Name.Replace("'", "''", StringComparison.Ordinal) + "')";
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            var names = new SortedDictionary<int, string>();
            var binary = true;
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (reader.GetInt32(5) == 0) continue;
                names[reader.GetInt32(0)] = reader.IsDBNull(2) ? "" : reader.GetString(2);
                binary &= string.Equals(reader.GetString(4), "BINARY", StringComparison.OrdinalIgnoreCase);
            }
            if (!binary) continue; // 非 BINARY 键不能承担 Journal 的身份或认领约束。
            result.Add(new(index.Name, index.Primary, index.Unique, string.Join(",", names.Values)));
        }
        return result;
    }
}
