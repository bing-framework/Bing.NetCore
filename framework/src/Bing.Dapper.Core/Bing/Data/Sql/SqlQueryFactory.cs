using Bing.Data.Enums;
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
    /// <param name="databaseDescriptorResolver">数据库描述解析器</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <param name="implementationTypeResolver">SQL 实现类型解析器</param>
    public SqlQueryFactory(IServiceProvider serviceProvider,
        IDatabaseContextAccessor databaseContextAccessor = null,
        IDatabaseDescriptorResolver databaseDescriptorResolver = null,
        SqlMetadataOptions metadataOptions = null,
        ISqlImplementationTypeResolver implementationTypeResolver = null)
        : base(serviceProvider, databaseContextAccessor, databaseDescriptorResolver, metadataOptions,
            implementationTypeResolver)
    {
    }

    /// <inheritdoc />
    public TQuery Create<TQuery>(string dbKey, DatabaseType databaseType, DatabaseRole role = DatabaseRole.Default)
        where TQuery : class, ISqlQuery => CreateInstance<TQuery>(CreateContext(dbKey, databaseType, role));

    /// <inheritdoc />
    public TQuery Create<TQuery>() where TQuery : class, ISqlQuery =>
        CreateInstance<TQuery>(GetCurrentContext(typeof(TQuery)));
}