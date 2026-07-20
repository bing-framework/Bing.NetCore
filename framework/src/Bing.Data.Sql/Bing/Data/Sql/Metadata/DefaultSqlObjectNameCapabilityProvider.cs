using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认 SQL 对象名称能力提供器。
/// </summary>
public sealed class DefaultSqlObjectNameCapabilityProvider : ISqlObjectNameCapabilityProvider
{
    /// <inheritdoc />
    public SqlObjectNameCapabilities GetCapabilities(DatabaseType? databaseType) => databaseType switch
    {
        DatabaseType.MySql or DatabaseType.Doris => new SqlObjectNameCapabilities
        {
            SupportsCatalog = true,
            SupportsCrossCatalogQuery = true,
            MaximumNameParts = 2
        },
        DatabaseType.SqlServer => new SqlObjectNameCapabilities
        {
            SupportsCatalog = true,
            SupportsPhysicalSchema = true,
            SupportsCrossCatalogQuery = true,
            MaximumNameParts = 3
        },
        DatabaseType.PgSql => new SqlObjectNameCapabilities
        {
            SupportsPhysicalSchema = true,
            MaximumNameParts = 2
        },
        DatabaseType.Oracle => new SqlObjectNameCapabilities
        {
            SupportsPhysicalSchema = true,
            SupportsDatabaseLink = true,
            MaximumNameParts = 2
        },
        DatabaseType.Sqlite => new SqlObjectNameCapabilities
        {
            SupportsCatalog = true,
            SupportsCrossCatalogQuery = true,
            SupportsAttachedAlias = true,
            MaximumNameParts = 2
        },
        _ => throw new NotSupportedException("未配置数据库类型的 SQL 对象名称能力。")
    };
}