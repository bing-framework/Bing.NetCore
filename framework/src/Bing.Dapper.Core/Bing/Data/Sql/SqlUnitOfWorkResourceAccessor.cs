using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Shares mapped ORM resources and creates the default Dapper resource for a SQL source.
/// </summary>
internal sealed class SqlUnitOfWorkResourceAccessor : ISqlUnitOfWorkResourceAccessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISqlDataSourceResolver _dataSourceResolver;
    private readonly ISqlConnectionStringResolver _connectionStringResolver;
    private readonly ISqlProviderResolver _providerResolver;
    private readonly ISqlDbConnectionFactoryResolver _connectionFactoryResolver;
    private readonly ISqlDatabaseIdentityResolver _databaseIdentityResolver;

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
