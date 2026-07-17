using System.Data.Common;
using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// 默认 SQL 数据库物理身份解析器。
/// </summary>
public sealed class DefaultSqlDatabaseIdentityResolver : ISqlDatabaseIdentityResolver
{
    /// <inheritdoc />
    public SqlDatabaseIdentity Resolve(DatabaseType databaseType, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("数据库连接字符串不能为空，无法解析物理数据库身份。");
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        return databaseType switch
        {
            DatabaseType.Sqlite => ResolveSqlite(builder),
            DatabaseType.SqlServer => ResolveSqlServer(builder),
            DatabaseType.MySql => ResolveServerDatabase(databaseType, builder, "Server", "Data Source", "Host"),
            DatabaseType.PgSql => ResolveServerDatabase(databaseType, builder, "Host", "Server", "Data Source"),
            DatabaseType.Oracle => ResolveOracle(builder),
            _ => throw new NotSupportedException($"数据库类型 {databaseType} 不支持物理数据库身份比较。")
        };
    }

    /// <summary>
    /// 解析 SQL Server 数据库物理身份。
    /// </summary>
    /// <param name="builder">连接字符串构建器。</param>
    /// <returns>SQL Server 数据库物理身份。</returns>
    private static SqlDatabaseIdentity ResolveSqlServer(DbConnectionStringBuilder builder)
    {
        var endpoint = GetValue(builder, "Server", "Data Source", "DataSource", "Address", "Addr", "Network Address");
        var separatorIndex = endpoint?.IndexOf('\\') ?? -1;
        var server = separatorIndex > -1 ? endpoint.Substring(0, separatorIndex) : endpoint;
        var instance = separatorIndex > -1 ? endpoint.Substring(separatorIndex + 1) : null;
        return new SqlDatabaseIdentity
        {
            DatabaseType = DatabaseType.SqlServer,
            Server = Normalize(server),
            Instance = Normalize(instance),
            Port = ParsePort(GetValue(builder, "Port")),
            Database = Normalize(GetValue(builder, "Database", "Initial Catalog"))
        };
    }

    /// <summary>
    /// 解析服务器与数据库名称构成的物理身份。
    /// </summary>
    /// <param name="databaseType">数据库类型。</param>
    /// <param name="builder">连接字符串构建器。</param>
    /// <param name="serverKeys">服务器字段名称。</param>
    /// <returns>数据库物理身份。</returns>
    private static SqlDatabaseIdentity ResolveServerDatabase(DatabaseType databaseType,
        DbConnectionStringBuilder builder, params string[] serverKeys)
    {
        return new SqlDatabaseIdentity
        {
            DatabaseType = databaseType,
            Server = Normalize(GetValue(builder, serverKeys)),
            Port = ParsePort(GetValue(builder, "Port")),
            Database = Normalize(GetValue(builder, "Database", "Initial Catalog"))
        };
    }

    /// <summary>
    /// 解析 Oracle 数据库物理身份。
    /// </summary>
    /// <param name="builder">连接字符串构建器。</param>
    /// <returns>Oracle 数据库物理身份。</returns>
    private static SqlDatabaseIdentity ResolveOracle(DbConnectionStringBuilder builder)
    {
        var dataSource = Normalize(GetValue(builder, "Data Source", "DataSource", "Server"));
        return new SqlDatabaseIdentity
        {
            DatabaseType = DatabaseType.Oracle,
            Server = dataSource,
            ServiceName = Normalize(GetValue(builder, "Service Name", "SID")) ?? dataSource
        };
    }

    /// <summary>
    /// 解析 SQLite 数据库物理身份。
    /// </summary>
    /// <param name="builder">连接字符串构建器。</param>
    /// <returns>SQLite 数据库物理身份。</returns>
    private static SqlDatabaseIdentity ResolveSqlite(DbConnectionStringBuilder builder)
    {
        var dataSource = GetValue(builder, "Data Source", "DataSource", "Filename");
        if (string.IsNullOrWhiteSpace(dataSource))
            throw new InvalidOperationException("SQLite 连接字符串缺少 Data Source 或 Filename，无法解析物理数据库身份。");
        var mode = Normalize(GetValue(builder, "Mode"));
        var cache = Normalize(GetValue(builder, "Cache"));
        var normalizedSource = dataSource.Trim();
        var isMemory = string.Equals(normalizedSource, ":memory:", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(mode, "memory", StringComparison.OrdinalIgnoreCase);
        if (isMemory)
        {
            var name = normalizedSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                ? normalizedSource
                : $"{normalizedSource}|{mode}|{cache}";
            return new SqlDatabaseIdentity
            {
                DatabaseType = DatabaseType.Sqlite,
                FilePath = Normalize(name)
            };
        }
        if (normalizedSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return new SqlDatabaseIdentity
            {
                DatabaseType = DatabaseType.Sqlite,
                FilePath = Normalize(normalizedSource)
            };
        }
        return new SqlDatabaseIdentity
        {
            DatabaseType = DatabaseType.Sqlite,
            FilePath = Path.GetFullPath(normalizedSource)
        };
    }

    /// <summary>
    /// 获取连接字符串字段值。
    /// </summary>
    /// <param name="builder">连接字符串构建器。</param>
    /// <param name="keys">字段名称。</param>
    /// <returns>第一个非空字段值。</returns>
    private static string GetValue(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value) && string.IsNullOrWhiteSpace(value?.ToString()) == false)
                return value.ToString();
        }
        return null;
    }

    /// <summary>
    /// 解析端口。
    /// </summary>
    /// <param name="value">端口字符串。</param>
    /// <returns>端口号。</returns>
    private static int? ParsePort(string value) => int.TryParse(value, out var port) ? port : null;

    /// <summary>
    /// 规范化身份字段。
    /// </summary>
    /// <param name="value">待规范化的字段值。</param>
    /// <returns>规范化后的字段值。</returns>
    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}