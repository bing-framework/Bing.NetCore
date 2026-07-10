using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// 默认数据库描述解析器
/// </summary>
public sealed class DefaultDatabaseDescriptorResolver : IDatabaseDescriptorResolver
{
    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _options;

    /// <summary>
    /// SQL 数据源解析器
    /// </summary>
    private readonly ISqlDataSourceResolver _dataSourceResolver;

    /// <summary>
    /// 初始化一个<see cref="DefaultDatabaseDescriptorResolver"/>类型的实例
    /// </summary>
    /// <param name="options">Sql 元数据配置</param>
    /// <param name="dataSourceResolver">SQL 数据源解析器</param>
    public DefaultDatabaseDescriptorResolver(SqlMetadataOptions options = null,
        ISqlDataSourceResolver dataSourceResolver = null)
    {
        _options = options ?? new SqlMetadataOptions();
        _dataSourceResolver = dataSourceResolver ?? new DefaultSqlDataSourceResolver(_options);
    }

    /// <summary>
    /// 解析数据库描述信息
    /// </summary>
    /// <param name="context">数据库上下文</param>
    /// <returns>数据库描述信息</returns>
    public DatabaseDescriptor Resolve(DatabaseContext context)
    {
        var databaseContext = NormalizeContext(context);
        var dataSource = _dataSourceResolver.Resolve(databaseContext.DataSourceKey ?? databaseContext.DbKey,
            new DatabaseScopeOptions
            {
                DbKey = databaseContext.DbKey,
                DatabaseType = databaseContext.DatabaseType,
                Role = databaseContext.Role,
                ReadOnly = databaseContext.ReadOnly ? true : null,
                MappingProfile = databaseContext.MappingProfile,
                ReadPreference = databaseContext.ReadPreference
            }) ?? databaseContext.DataSource;
        if (dataSource != null)
            return Merge(databaseContext, dataSource);
        if (_options.Databases.TryGetValue(
                SqlMetadataOptions.GetDatabaseDescriptorKey(databaseContext.DbKey, databaseContext.DatabaseType,
                    databaseContext.Role), out var descriptor))
        {
            return Merge(databaseContext, descriptor);
        }

        return new DatabaseDescriptor
        {
            DbKey = databaseContext.DbKey,
            DatabaseType = databaseContext.DatabaseType,
            Role = databaseContext.Role,
            ReadOnly = databaseContext.ReadOnly
        };
    }

    /// <summary>
    /// 标准化数据库上下文
    /// </summary>
    /// <param name="context">数据库上下文</param>
    /// <returns>标准化后的数据库上下文</returns>
    private DatabaseContext NormalizeContext(DatabaseContext context)
    {
        if (context != null)
            return context;
        if (_options.DefaultDatabaseContext != null)
            return _options.DefaultDatabaseContext;
        return new DatabaseContext
        {
            DbKey = ConnectionStringCollection.DefaultConnectionStringName,
            DatabaseType = DatabaseType.SqlServer,
            Role = DatabaseRole.Default
        };
    }

    /// <summary>
    /// 合并数据库上下文与数据库描述信息
    /// </summary>
    /// <param name="context">数据库上下文</param>
    /// <param name="descriptor">数据库描述信息</param>
    /// <returns>合并后的数据库描述信息</returns>
    private static DatabaseDescriptor Merge(DatabaseContext context, DatabaseDescriptor descriptor) => new()
    {
        DbKey = string.IsNullOrWhiteSpace(descriptor?.DbKey) ? context?.DbKey : descriptor.DbKey,
        DatabaseType = descriptor?.DatabaseType ?? context?.DatabaseType ?? DatabaseType.SqlServer,
        Role = descriptor?.Role ?? context?.Role ?? DatabaseRole.Default,
        ConnectionString = descriptor?.ConnectionString,
        ReadOnly = descriptor?.ReadOnly ?? context?.ReadOnly ?? false
    };

    /// <summary>
    /// 合并数据库上下文与 SQL 数据源描述信息
    /// </summary>
    /// <param name="context">数据库上下文</param>
    /// <param name="descriptor">SQL 数据源描述信息</param>
    /// <returns>合并后的数据库描述信息</returns>
    private static DatabaseDescriptor Merge(DatabaseContext context, SqlDataSourceDescriptor descriptor) => new()
    {
        DbKey = string.IsNullOrWhiteSpace(descriptor?.DbKey) ? context?.DbKey : descriptor.DbKey,
        DatabaseType = descriptor?.DatabaseType ?? context?.DatabaseType ?? DatabaseType.SqlServer,
        Role = context?.Role ?? DatabaseRole.Default,
        ConnectionString = descriptor?.ConnectionString,
        ReadOnly = descriptor?.IsReadOnly ?? context?.ReadOnly ?? false
    };
}