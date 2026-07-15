using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 查询对象工厂
/// </summary>
public sealed class SqlQueryFactory : SqlFactoryBase, ISqlQueryFactory
{
    /// <summary>
    /// 初始化一个<see cref="SqlQueryFactory"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <param name="implementationTypeResolver">SQL 实现类型解析器</param>
    /// <param name="dataSourceResolver">SQL 数据源解析器</param>
    public SqlQueryFactory(IServiceProvider serviceProvider,
        IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions metadataOptions = null,
        ISqlImplementationTypeResolver implementationTypeResolver = null,
        ISqlDataSourceResolver dataSourceResolver = null)
        : base(serviceProvider, databaseContextAccessor, metadataOptions,
            implementationTypeResolver, dataSourceResolver)
    {
    }

    /// <inheritdoc />
    public TQuery Create<TQuery>(string dbKey) where TQuery : class, ISqlQuery =>
        CreateInstance<TQuery>(CreateContext(dbKey));

    /// <inheritdoc />
    public TQuery Create<TQuery>() where TQuery : class, ISqlQuery =>
        CreateInstance<TQuery>(GetCurrentContext(typeof(TQuery)));
}