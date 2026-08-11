using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// 多结果集查询执行器工厂。
/// </summary>
internal sealed class SqlMultipleQueryExecutorFactory : SqlFactoryBase, ISqlMultipleQueryExecutorFactory
{
    /// <summary>
    /// 初始化一个<see cref="SqlMultipleQueryExecutorFactory"/>类型的实例。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
    /// <param name="metadataOptions">SQL 元数据配置。</param>
    /// <param name="dataSourceResolver">SQL 数据源解析器。</param>
    public SqlMultipleQueryExecutorFactory(IServiceProvider serviceProvider,
        IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions metadataOptions = null,
        ISqlDataSourceResolver dataSourceResolver = null)
        : base(serviceProvider, databaseContextAccessor, metadataOptions, dataSourceResolver: dataSourceResolver)
    {
    }

    /// <inheritdoc />
    public ISqlMultipleQueryExecutor Create(string dbKey = null) =>
        CreateInstance<ISqlMultipleQueryExecutor>(string.IsNullOrWhiteSpace(dbKey) ? GetCurrentContext() : CreateContext(dbKey));
}