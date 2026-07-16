namespace Bing.Data.Sql;

/// <summary>
/// 基于 <see cref="AsyncLocal{T}"/> 的数据库上下文访问器
/// </summary>
public sealed class AsyncLocalDatabaseContextAccessor : IDatabaseContextAccessor
{
    /// <summary>
    /// 当前数据库上下文。
    /// </summary>
    private readonly AsyncLocal<DatabaseContext> _current = new();

    /// <summary>
    /// 当前数据库上下文
    /// </summary>
    public DatabaseContext Current
    {
        get => Clone(_current.Value);
        set => _current.Value = Clone(value);
    }

    /// <summary>
    /// 创建数据库上下文快照。
    /// </summary>
    /// <param name="context">数据库上下文。</param>
    /// <returns>数据库上下文快照。</returns>
    private static DatabaseContext Clone(DatabaseContext context)
    {
        if (context == null)
            return null;
        return new DatabaseContext
        {
            DbKey = context.DbKey,
            TenantId = context.TenantId,
            ReadPreference = context.ReadPreference,
            MappingProfile = context.MappingProfile,
            DataSource = Clone(context.DataSource)
        };
    }

    /// <summary>
    /// 创建 SQL 数据源描述快照。
    /// </summary>
    /// <param name="dataSource">SQL 数据源描述。</param>
    /// <returns>SQL 数据源描述快照。</returns>
    private static SqlDataSourceDescriptor Clone(SqlDataSourceDescriptor dataSource)
    {
        if (dataSource == null)
            return null;
        return new SqlDataSourceDescriptor
        {
            Key = dataSource.Key,
            DatabaseType = dataSource.DatabaseType,
            ConnectionStringName = dataSource.ConnectionStringName,
            ConnectionString = dataSource.ConnectionString,
            IsReadOnly = dataSource.IsReadOnly,
            MappingProfile = dataSource.MappingProfile,
            PrimaryReadStrategy = dataSource.PrimaryReadStrategy,
            PrimaryDataSourceKey = dataSource.PrimaryDataSourceKey,
            SupportsTransactions = dataSource.SupportsTransactions
        };
    }
}
