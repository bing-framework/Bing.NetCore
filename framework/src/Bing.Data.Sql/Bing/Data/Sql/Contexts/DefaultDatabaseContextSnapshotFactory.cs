namespace Bing.Data.Sql;

/// <summary>
/// 默认数据库上下文快照工厂。
/// </summary>
public sealed class DefaultDatabaseContextSnapshotFactory : IDatabaseContextSnapshotFactory
{
    /// <summary>
    /// 创建独立的数据库上下文深快照。
    /// </summary>
    /// <param name="source">源数据库上下文。</param>
    /// <returns>独立的数据库上下文快照。</returns>
    public DatabaseContext Create(DatabaseContext source)
    {
        if (source == null)
            return null;
        return new DatabaseContext
        {
            DbKey = source.DbKey,
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
            DatabaseType = source.DatabaseType,
            ConnectionStringName = source.ConnectionStringName,
            ConnectionString = source.ConnectionString,
            IsReadOnly = source.IsReadOnly,
            MappingProfile = source.MappingProfile,
            PrimaryReadStrategy = source.PrimaryReadStrategy,
            PrimaryDataSourceKey = source.PrimaryDataSourceKey,
            SupportsTransactions = source.SupportsTransactions
        };
    }
}