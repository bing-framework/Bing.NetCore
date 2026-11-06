using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// 复用 ORM 映射资源或创建 Dapper 工作单元资源的访问器。
/// </summary>
internal sealed class SqlUnitOfWorkResourceAccessor : ISqlUnitOfWorkResourceAccessor
{
    /// <summary>
    /// 用于解析工作单元资源构建所需的依赖服务。
    /// </summary>
    private readonly IServiceProvider _serviceProvider;
    /// <summary>
    /// 解析工作单元使用的数据源描述。
    /// </summary>
    private readonly ISqlDataSourceResolver _dataSourceResolver;
    /// <summary>
    /// 解析数据源对应的连接字符串。
    /// </summary>
    private readonly ISqlConnectionStringResolver _connectionStringResolver;
    /// <summary>
    /// 解析数据库类型对应的 SQL 提供程序。
    /// </summary>
    private readonly ISqlProviderResolver _providerResolver;
    /// <summary>
    /// 解析用于创建数据库连接的工厂。
    /// </summary>
    private readonly ISqlDbConnectionFactoryResolver _connectionFactoryResolver;
    /// <summary>
    /// 解析连接对应的数据库标识。
    /// </summary>
    private readonly ISqlDatabaseIdentityResolver _databaseIdentityResolver;

    /// <summary>
    /// 初始化一个 <see cref="SqlUnitOfWorkResourceAccessor" /> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">用于解析当前测试服务的服务提供程序。</param>
    /// <param name="dataSourceResolver">用于解析 SQL 数据源配置的解析器。</param>
    /// <param name="connectionStringResolver">用于解析数据库连接字符串的解析器。</param>
    /// <param name="providerResolver">用于解析 SQL 提供程序的解析器。</param>
    /// <param name="connectionFactoryResolver">用于解析数据库连接工厂的解析器。</param>
    /// <param name="databaseIdentityResolver">用于解析数据库身份的解析器。</param>
    public SqlUnitOfWorkResourceAccessor(IServiceProvider serviceProvider,
        ISqlDataSourceResolver dataSourceResolver,
        ISqlConnectionStringResolver connectionStringResolver,
        ISqlProviderResolver providerResolver,
        ISqlDbConnectionFactoryResolver connectionFactoryResolver,
        ISqlDatabaseIdentityResolver databaseIdentityResolver)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _dataSourceResolver = dataSourceResolver ?? throw new ArgumentNullException(nameof(dataSourceResolver));
        _connectionStringResolver = connectionStringResolver ?? throw new ArgumentNullException(nameof(connectionStringResolver));
        _providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
        _connectionFactoryResolver = connectionFactoryResolver ?? throw new ArgumentNullException(nameof(connectionFactoryResolver));
        _databaseIdentityResolver = databaseIdentityResolver ?? throw new ArgumentNullException(nameof(databaseIdentityResolver));
    }

    /// <inheritdoc />
    public IUnitOfWorkConnectionResource GetResource(IUnitOfWorkScope scope, DatabaseContext context,
        string connectionStringOverride = null, bool createDapperResource = true,
        bool requireComparableIdentity = false)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));

        context ??= new DatabaseContext();
        var dataSource = ResolveDataSource(context);
        var resolvedContext = new DatabaseContext
        {
            DbKey = dataSource?.Key ?? context.DbKey,
            ProviderKey = context.ProviderKey,
            TenantId = context.TenantId,
            ReadPreference = context.ReadPreference,
            MappingProfile = dataSource?.MappingProfile ?? context.MappingProfile,
            DataSource = dataSource
        };
        if (dataSource == null)
            throw new InvalidOperationException($"未解析到 SQL 数据源 {context.DbKey ?? "<默认>"}。");
        var databaseType = dataSource.DatabaseType;
        var provider = _providerResolver.Resolve(resolvedContext, databaseType: databaseType);
        var connectionString = string.IsNullOrWhiteSpace(connectionStringOverride)
            ? _connectionStringResolver.Resolve(dataSource)
            : connectionStringOverride;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("数据库连接字符串不能为空。");

        IUnitOfWorkConnectionResource mappedResource = null;
        foreach (var resourceProvider in _serviceProvider.GetServices<IUnitOfWorkConnectionProvider>())
        {
            var resource = resourceProvider.GetResource(scope, resolvedContext.DbKey, provider.Key,
                resolvedContext.TenantId, connectionString);
            if (resource == null)
                continue;
            if (mappedResource != null && !ReferenceEquals(mappedResource, resource))
                throw new InvalidOperationException("多个 ORM 工作单元连接映射同时匹配当前 SQL 数据源。");
            mappedResource = resource;
        }
        if (mappedResource != null || !createDapperResource)
            return mappedResource;

        var identity = ResolveIdentity(dataSource.DatabaseType, connectionString);
        if (requireComparableIdentity && (identity?.IsComparable != true || identity.IsExclusiveMemory))
            throw new InvalidOperationException(
                $"SQL 数据源 {resolvedContext.DbKey} 的物理数据库身份无法安全比较，不能用于 CAP Outbox 事务共享。");

        SqlProviderTransactionCapabilities transactionCapabilities = null;
        if (scope.Options.IsTransactional)
        {
            if (provider is not ISqlProviderProfileProvider { Profile.Transaction: not null } profileProvider)
                throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.ProviderProfileMissing,
                    "Transaction", provider.Key,
                    $"Provider {provider.Key} 未声明事务能力 Profile。[ProviderProfileMissing]");
            transactionCapabilities = profileProvider.Profile.Transaction;
        }

        var canCompareConfiguration = identity?.IsComparable == true && !identity.IsExclusiveMemory;
        var resourceKey = new DapperSqlUnitOfWorkResourceKey(resolvedContext, dataSource,
            dataSource.DatabaseType, provider.Key, connectionString, _connectionFactoryResolver,
            canCompareConfiguration);
        return scope.GetOrAddResource(resourceKey, () => new DapperSqlUnitOfWorkConnectionResource(
            resourceKey, scope, dataSource, provider.Key, connectionString, _connectionFactoryResolver,
            transactionCapabilities));
    }

    /// <summary>
    /// 解析当前 SQL 上下文的数据源。
    /// </summary>
    /// <param name="context">当前 SQL 数据库上下文。</param>
    /// <returns>按上下文及主库读取策略解析的数据源描述信息。</returns>
    private SqlDataSourceDescriptor ResolveDataSource(DatabaseContext context)
    {
        var dataSource = context.DataSource ?? _dataSourceResolver.Resolve(context.DbKey,
            new DatabaseScopeOptions
            {
                DbKey = context.DbKey,
                TenantId = context.TenantId,
                ReadPreference = context.ReadPreference
            });
        if (context.ReadPreference != SqlReadPreference.Primary ||
            dataSource.PrimaryReadStrategy != PrimaryReadStrategy.PrimaryDataSource)
            return dataSource;
        return _dataSourceResolver.Resolve(dataSource.Key, new DatabaseScopeOptions
        {
            DbKey = dataSource.Key,
            TenantId = context.TenantId,
            ReadPreference = SqlReadPreference.Primary
        });
    }

    /// <summary>
    /// 尝试解析数据库的物理身份。
    /// </summary>
    /// <param name="databaseType">连接所使用的数据库类型。</param>
    /// <param name="connectionString">待解析物理数据库身份的连接字符串。</param>
    /// <returns>解析器返回的数据库身份；解析失败时为 null。</returns>
    private SqlDatabaseIdentity ResolveIdentity(DatabaseType databaseType, string connectionString)
    {
        try
        {
            return _databaseIdentityResolver.Resolve(databaseType, connectionString);
        }
        catch
        {
            return null;
        }
    }
}
