using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认 SQL 对象名称能力提供器。
/// </summary>
public sealed class DefaultSqlObjectNameCapabilityProvider : ISqlObjectNameCapabilityProvider
{
    /// <summary>
    /// MySQL 与 Doris 共用的对象名称能力：支持架构名，最多允许两段对象名称。
    /// </summary>
    private static readonly SqlObjectNameCapabilities MySqlCapabilities = new()
    {
        SupportsSchema = true,
        MaximumNameParts = 2
    };

    /// <summary>
    /// SQL Server 对象名称能力：支持数据库名和架构名，最多允许三段对象名称。
    /// </summary>
    private static readonly SqlObjectNameCapabilities SqlServerCapabilities = new()
    {
        SupportsDatabase = true,
        SupportsSchema = true,
        MaximumNameParts = 3
    };

    /// <summary>
    /// PostgreSQL 对象名称能力：支持架构名，最多允许两段对象名称。
    /// </summary>
    private static readonly SqlObjectNameCapabilities PostgreSqlCapabilities = new()
    {
        SupportsSchema = true,
        MaximumNameParts = 2
    };

    /// <summary>
    /// Oracle 对象名称能力：支持架构名，最多允许两段对象名称。
    /// </summary>
    private static readonly SqlObjectNameCapabilities OracleCapabilities = new()
    {
        SupportsSchema = true,
        MaximumNameParts = 2
    };

    /// <summary>
    /// SQLite 对象名称能力：不支持数据库名和架构名限定，最多允许单段对象名称。
    /// </summary>
    private static readonly SqlObjectNameCapabilities SqliteCapabilities = new()
    {
        MaximumNameParts = 1
    };

    /// <inheritdoc />
    public SqlObjectNameCapabilities GetCapabilities(DatabaseType? databaseType) => databaseType switch
    {
        DatabaseType.MySql or DatabaseType.Doris => MySqlCapabilities,
        DatabaseType.SqlServer => SqlServerCapabilities,
        DatabaseType.PgSql => PostgreSqlCapabilities,
        DatabaseType.Oracle => OracleCapabilities,
        DatabaseType.Sqlite => SqliteCapabilities,
        _ => throw new NotSupportedException("未配置数据库类型的 SQL 对象名称能力。")
    };
}