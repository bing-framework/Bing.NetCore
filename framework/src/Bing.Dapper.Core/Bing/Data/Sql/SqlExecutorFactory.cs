using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 执行器工厂
/// </summary>
internal sealed class SqlExecutorFactory : SqlFactoryBase, ISqlExecutorFactory
{
    /// <summary>
    /// 初始化一个<see cref="SqlExecutorFactory"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <param name="dataSourceResolver">SQL 数据源解析器</param>
    public SqlExecutorFactory(IServiceProvider serviceProvider,
        IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions metadataOptions = null,
        ISqlDataSourceResolver dataSourceResolver = null)
        : base(serviceProvider, databaseContextAccessor, metadataOptions,
            dataSourceResolver: dataSourceResolver)
    {
    }

    /// <inheritdoc />
    public ISqlExecutor Create(string dbKey = null) =>
        CreateInstance<ISqlExecutor>(string.IsNullOrWhiteSpace(dbKey) ? GetCurrentContext() : CreateContext(dbKey));

    /// <summary>
    /// 使用固定事务数据库上下文创建执行器对象。
    /// </summary>
    /// <param name="context">事务数据库上下文。</param>
    /// <returns>执行器对象。</returns>
    internal ISqlExecutor CreateForTransaction(DatabaseContext context) =>
        CreateInstance<ISqlExecutor>(context ?? throw new ArgumentNullException(nameof(context)));
}