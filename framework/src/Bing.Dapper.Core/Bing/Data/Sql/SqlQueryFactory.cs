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
    /// <param name="dataSourceResolver">SQL 数据源解析器</param>
    public SqlQueryFactory(IServiceProvider serviceProvider,
        IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions metadataOptions = null,
        ISqlDataSourceResolver dataSourceResolver = null)
        : base(serviceProvider, databaseContextAccessor, metadataOptions,
            dataSourceResolver: dataSourceResolver)
    {
    }

    /// <inheritdoc />
    public ISqlQuery Create(string dbKey = null) =>
        CreateInstance<ISqlQuery>(string.IsNullOrWhiteSpace(dbKey) ? GetCurrentContext() : CreateContext(dbKey));

    /// <summary>
    /// 创建固定在事务主库上下文中的查询对象。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <param name="context">已解析的事务数据库上下文。</param>
    /// <returns>查询对象。</returns>
    internal ISqlQuery CreateForTransaction(string dbKey, out DatabaseContext context)
    {
        context = CreateTransactionContext(dbKey);
        return CreateInstance<ISqlQuery>(context);
    }

    /// <summary>
    /// 使用固定事务数据库上下文创建查询对象。
    /// </summary>
    /// <param name="context">事务数据库上下文。</param>
    /// <returns>查询对象。</returns>
    internal ISqlQuery CreateForTransaction(DatabaseContext context) =>
        CreateInstance<ISqlQuery>(context ?? throw new ArgumentNullException(nameof(context)));
}