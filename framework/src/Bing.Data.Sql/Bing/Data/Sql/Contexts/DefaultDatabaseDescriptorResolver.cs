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
    /// 初始化一个<see cref="DefaultDatabaseDescriptorResolver"/>类型的实例
    /// </summary>
    /// <param name="options">Sql 元数据配置</param>
    public DefaultDatabaseDescriptorResolver(SqlMetadataOptions options = null) =>
        _options = options ?? new SqlMetadataOptions();

    /// <summary>
    /// 解析数据库描述信息
    /// </summary>
    /// <param name="context">数据库上下文</param>
    /// <returns>数据库描述信息</returns>
    public DatabaseDescriptor Resolve(DatabaseContext context)
    {
        var databaseContext = NormalizeContext(context);
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
}