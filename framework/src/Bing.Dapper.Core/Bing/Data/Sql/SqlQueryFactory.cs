using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 查询对象工厂
/// </summary>
internal sealed class SqlQueryFactory : SqlFactoryBase, ISqlQueryFactory
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

    /// <summary>
    /// 创建固定在事务主库上下文中的查询对象。
    /// </summary>
    /// <typeparam name="TQuery">查询类型。</typeparam>
    /// <param name="dbKey">数据源标识。</param>
    /// <param name="context">已解析的事务数据库上下文。</param>
    /// <returns>查询对象。</returns>
    internal TQuery CreateForTransaction<TQuery>(string dbKey, out DatabaseContext context)
        where TQuery : class, ISqlQuery
    {
        context = CreateTransactionContext(dbKey);
        return CreateInstance<TQuery>(context);
    }

    /// <summary>
    /// 使用固定事务数据库上下文创建查询对象。
    /// </summary>
    /// <typeparam name="TQuery">查询类型。</typeparam>
    /// <param name="context">事务数据库上下文。</param>
    /// <returns>查询对象。</returns>
    internal TQuery CreateForTransaction<TQuery>(DatabaseContext context) where TQuery : class, ISqlQuery =>
        CreateInstance<TQuery>(context ?? throw new ArgumentNullException(nameof(context)));
}