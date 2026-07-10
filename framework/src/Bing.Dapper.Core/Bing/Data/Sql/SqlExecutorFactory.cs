using Bing.Data.Enums;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 执行器工厂
/// </summary>
public sealed class SqlExecutorFactory : SqlFactoryBase, ISqlExecutorFactory
{
    /// <summary>
    /// 初始化一个<see cref="SqlExecutorFactory"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="databaseDescriptorResolver">数据库描述解析器</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <param name="implementationTypeResolver">SQL 实现类型解析器</param>
    /// <param name="dataSourceResolver">SQL 数据源解析器</param>
    public SqlExecutorFactory(IServiceProvider serviceProvider,
        IDatabaseContextAccessor databaseContextAccessor = null,
        IDatabaseDescriptorResolver databaseDescriptorResolver = null,
        SqlMetadataOptions metadataOptions = null,
        ISqlImplementationTypeResolver implementationTypeResolver = null,
        ISqlDataSourceResolver dataSourceResolver = null)
        : base(serviceProvider, databaseContextAccessor, databaseDescriptorResolver, metadataOptions,
            implementationTypeResolver, dataSourceResolver)
    {
    }

    /// <inheritdoc />
    public TExecutor Create<TExecutor>(string dbKey) where TExecutor : class, ISqlExecutor =>
        CreateInstance<TExecutor>(CreateContext(dbKey));

    /// <inheritdoc />
    public TExecutor Create<TExecutor>(string dbKey, DatabaseType databaseType,
        DatabaseRole role = DatabaseRole.Default) where TExecutor : class, ISqlExecutor =>
        CreateInstance<TExecutor>(CreateContext(dbKey, databaseType, role));

    /// <inheritdoc />
    public TExecutor Create<TExecutor>() where TExecutor : class, ISqlExecutor =>
        CreateInstance<TExecutor>(GetCurrentContext(typeof(TExecutor)));
}