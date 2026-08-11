using Bing.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文快照帮助器。
/// </summary>
public static class DatabaseContextSnapshot
{
    /// <summary>
    /// 创建默认深快照。
    /// </summary>
    /// <param name="source">源数据库上下文。</param>
    /// <returns>独立的数据库上下文快照。</returns>
    public static DatabaseContext Create(DatabaseContext source)
    {
        if (source == null)
            return null;
        return new DatabaseContext
        {
            DbKey = source.DbKey,
            ProviderKey = source.ProviderKey,
            TenantId = source.TenantId,
            ReadPreference = source.ReadPreference,
            MappingProfile = source.MappingProfile,
            DataSource = Create(source.DataSource)
        };
    }

    /// <summary>
    /// 创建独立的 SQL 数据源描述深快照。
    /// </summary>
    /// <param name="source">源 SQL 数据源描述。</param>
    /// <returns>独立的 SQL 数据源描述快照。</returns>
    private static SqlDataSourceDescriptor Create(SqlDataSourceDescriptor source)
    {
        if (source == null)
            return null;
        return new SqlDataSourceDescriptor
        {
            Key = source.Key,
            ProviderKey = source.ProviderKey,
            DatabaseType = source.DatabaseType,
            ConnectionStringName = source.ConnectionStringName,
            ConnectionString = source.ConnectionString,
            IsReadOnly = source.IsReadOnly,
            MappingProfile = source.MappingProfile,
            PrimaryReadStrategy = source.PrimaryReadStrategy,
            PrimaryDataSourceKey = source.PrimaryDataSourceKey,
            SupportsTransactions = source.SupportsTransactions,
            QueryCapabilities = CloneQueryCapabilities(source.QueryCapabilities)
        };
    }

    /// <summary>
    /// 克隆查询语法能力配置。
    /// </summary>
    /// <param name="capabilities">源能力配置。</param>
    /// <returns>独立能力配置副本。</returns>
    private static SqlQueryCapabilities CloneQueryCapabilities(SqlQueryCapabilities capabilities) => capabilities == null
        ? null
        : new SqlQueryCapabilities
        {
            Cte = capabilities.Cte,
            Union = capabilities.Union,
            UnionAll = capabilities.UnionAll,
            Intersect = capabilities.Intersect,
            Except = capabilities.Except,
            RightJoin = capabilities.RightJoin,
            FullJoin = capabilities.FullJoin,
            Pagination = capabilities.Pagination
        };
}