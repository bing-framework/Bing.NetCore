using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认 SQL 对象名称能力提供器。
/// </summary>
public sealed class DefaultSqlObjectNameCapabilityProvider : ISqlObjectNameCapabilityProvider
{
    /// <summary>
    /// MySql 与 Doris 对象名称能力。
    /// </summary>
    private static readonly SqlObjectNameCapabilities MySqlCapabilities = new()
    {
        SupportsCatalog = true,
        SupportsCrossCatalogQuery = true,
        MaximumNameParts = 2
    };

    /// <summary>
    /// SQL Server 对象名称能力。
    /// </summary>
    private static readonly SqlObjectNameCapabilities SqlServerCapabilities = new()
    {
        SupportsCatalog = true,
        SupportsPhysicalSchema = true,
        SupportsCrossCatalogQuery = true,
        MaximumNameParts = 3
    };

    /// <summary>
    /// PostgreSql 对象名称能力。
    /// </summary>
    private static readonly SqlObjectNameCapabilities PostgreSqlCapabilities = new()
    {
        SupportsPhysicalSchema = true,
        MaximumNameParts = 2
    };

    /// <summary>
    /// Oracle 对象名称能力。
    /// </summary>
    private static readonly SqlObjectNameCapabilities OracleCapabilities = new()
    {
        SupportsPhysicalSchema = true,
        SupportsDatabaseLink = true,
        MaximumNameParts = 2
    };

    /// <summary>
    /// SQLite 对象名称能力。
    /// </summary>
    private static readonly SqlObjectNameCapabilities SqliteCapabilities = new()
    {
        SupportsCatalog = true,
        SupportsCrossCatalogQuery = true,
        SupportsAttachedAlias = true,
        MaximumNameParts = 2
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