using Bing.Data.Enums;
using Bing.Data;
using Bing.Data.Sql.Configs;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 工厂基类。
/// </summary>
internal abstract class SqlFactoryBase
{
    /// <summary>
    /// 服务提供程序。
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 数据库上下文访问器。
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// SQL 元数据配置。
    /// </summary>
    private readonly SqlMetadataOptions _metadataOptions;

    /// <summary>
    /// SQL Provider 解析器。
    /// </summary>
    private readonly ISqlProviderResolver _providerResolver;

    /// <summary>
    /// SQL 数据源解析器。
    /// </summary>
    private readonly ISqlDataSourceResolver _dataSourceResolver;

    /// <summary>
    /// SQL 连接字符串解析器。
    /// </summary>
    private readonly ISqlConnectionStringResolver _connectionStringResolver;

    /// <summary>
    /// 初始化一个<see cref="SqlFactoryBase"/>类型的实例。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
    /// <param name="metadataOptions">SQL 元数据配置。</param>
    /// <param name="dataSourceResolver">SQL 数据源解析器。</param>
    /// <param name="providerResolver">SQL Provider 解析器。</param>
    /// <param name="connectionStringResolver">SQL 连接字符串解析器。</param>
    protected SqlFactoryBase(IServiceProvider serviceProvider,
        IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions metadataOptions = null,
        ISqlDataSourceResolver dataSourceResolver = null,
        ISqlProviderResolver providerResolver = null,
        ISqlConnectionStringResolver connectionStringResolver = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _databaseContextAccessor = databaseContextAccessor;
        _metadataOptions = metadataOptions ?? new SqlMetadataOptions();
        _providerResolver = providerResolver ?? _serviceProvider.GetService<ISqlProviderResolver>();
        _dataSourceResolver = dataSourceResolver ?? new DefaultSqlDataSourceResolver(_metadataOptions);
        _connectionStringResolver = connectionStringResolver ??
                                    _serviceProvider.GetService<ISqlConnectionStringResolver>() ??
                                    new DefaultSqlConnectionStringResolver(
                                        _serviceProvider.GetService<ConnectionStringCollection>());
    }

    /// <summary>
    /// 创建数据库上下文。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>数据库上下文。</returns>
    protected DatabaseContext CreateContext(string dbKey)
    {
        var current = _databaseContextAccessor?.Current ?? _metadataOptions.DefaultDatabaseContext;
        var options = new DatabaseScopeOptions
        {
            DbKey = dbKey,
            TenantId = current?.TenantId,
            ReadPreference = current?.ReadPreference ?? SqlReadPreference.Default
        };
        var dataSource = _dataSourceResolver.Resolve(dbKey, options);
        return CreateContext(dataSource, options.TenantId, current?.MappingProfile,
            options.ReadPreference ?? SqlReadPreference.Default);
    }

    /// <summary>
    /// 获取当前数据库上下文。
    /// </summary>
    /// <returns>数据库上下文。</returns>
    protected DatabaseContext GetCurrentContext()
    {
        var current = _databaseContextAccessor?.Current ?? _metadataOptions.DefaultDatabaseContext;
        if (current?.DataSource != null)
        {
            var resolvedDataSource = ResolveCurrentDataSource(current);
            return CreateContext(resolvedDataSource, current.TenantId, current.MappingProfile,
                current.ReadPreference);
        }
        var options = new DatabaseScopeOptions
        {
            DbKey = current?.DbKey,
            TenantId = current?.TenantId,
            ReadPreference = current?.ReadPreference ?? SqlReadPreference.Default
        };
        var dataSource = _dataSourceResolver.Resolve(current?.DbKey, options);
        return CreateContext(dataSource, options.TenantId, current?.MappingProfile,
            options.ReadPreference ?? SqlReadPreference.Default);
    }

    /// <summary>
    /// 解析当前上下文在读取偏好下实际使用的数据源。
    /// </summary>
    /// <param name="context">当前数据库上下文。</param>
    /// <returns>用于冻结 Query 或 Executor 上下文的数据源。</returns>
    private SqlDataSourceDescriptor ResolveCurrentDataSource(DatabaseContext context)
    {
        var dataSource = context?.DataSource;
        if (context?.ReadPreference != SqlReadPreference.Primary ||
            dataSource?.PrimaryReadStrategy != PrimaryReadStrategy.PrimaryDataSource)
            return dataSource;
        return _dataSourceResolver.Resolve(dataSource.Key, new DatabaseScopeOptions
        {
            DbKey = dataSource.Key,
            TenantId = context.TenantId,
            ReadPreference = SqlReadPreference.Primary
        });
    }

    /// <summary>
    /// 创建事务使用的主库数据库上下文。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>数据库上下文。</returns>
    protected DatabaseContext CreateTransactionContext(string dbKey)
    {
        var current = _databaseContextAccessor?.Current ?? _metadataOptions.DefaultDatabaseContext;
        var requestedDbKey = string.IsNullOrWhiteSpace(dbKey) ? current?.DbKey : dbKey;
        var options = new DatabaseScopeOptions
        {
            DbKey = requestedDbKey,
            TenantId = current?.TenantId,
            ReadPreference = SqlReadPreference.Primary
        };
        var dataSource = _dataSourceResolver.Resolve(requestedDbKey, options);
        return CreateContext(dataSource, options.TenantId, current?.MappingProfile,
            options.ReadPreference ?? SqlReadPreference.Default);
    }

    /// <summary>
    /// 创建实例。
    /// </summary>
    /// <typeparam name="TService">服务类型。</typeparam>
    /// <param name="context">数据库上下文。</param>
    /// <returns>服务实例。</returns>
    protected TService CreateInstance<TService>(DatabaseContext context) where TService : class
    {
        var providerKey = ResolveProviderKey(context);
        var implementationType = GetImplementationType(typeof(TService), providerKey);
        var options = CreateOptions(implementationType, context);
        return (TService)ActivatorUtilities.CreateInstance(_serviceProvider, implementationType, _serviceProvider,
            options);
    }

    /// <summary>
    /// 获取实现类型。
    /// </summary>
    /// <param name="serviceType">服务类型。</param>
    /// <param name="providerKey">Provider 唯一标识。</param>
    /// <returns>实现类型。</returns>
    protected Type GetImplementationType(Type serviceType, string providerKey)
    {
        var implementationType = _serviceProvider.GetServices<SqlProviderRuntime>()
            .SingleOrDefault(item => string.Equals(item.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase))
            ?.Resolve(serviceType);
        if (implementationType != null)
            return implementationType;
        if (serviceType.IsAbstract == false && serviceType.IsInterface == false)
            return serviceType;
        throw new InvalidOperationException($"未注册类型 {serviceType.FullName} 在 Provider Key {providerKey ?? "<未指定>"} 下的 SQL 实现类型。");
    }

    /// <summary>
    /// 创建 SQL 配置。
    /// </summary>
    /// <param name="implementationType">实现类型。</param>
    /// <param name="context">数据库上下文。</param>
    /// <returns>SQL 配置。</returns>
    protected SqlOptions CreateOptions(Type implementationType, DatabaseContext context)
    {
        var template = GetTemplateOptions(implementationType);
        var dataSource = context?.DataSource ?? _dataSourceResolver.Resolve(context?.DbKey);
        var optionsType = typeof(SqlOptions<>).MakeGenericType(implementationType);
        var options = (SqlOptions)Activator.CreateInstance(optionsType);
        CopyOptions(template, options);
        options.DatabaseType = dataSource.DatabaseType;
        options.SetDatabaseContext(CreateContext(dataSource, context?.TenantId, context?.MappingProfile,
            context?.ReadPreference ?? SqlReadPreference.Default));
        var connectionString = ResolveConnectionString(dataSource, template);
        if (string.IsNullOrWhiteSpace(connectionString) == false)
        {
            options.ConnectionString = connectionString;
            options.Connection = null;
        }
        return options;
    }

    /// <summary>
    /// 获取模板 SQL 配置。
    /// </summary>
    /// <param name="implementationType">实现类型。</param>
    /// <returns>模板 SQL 配置。</returns>
    protected SqlOptions GetTemplateOptions(Type implementationType) =>
        _serviceProvider.GetService(typeof(SqlOptions<>).MakeGenericType(implementationType)) as SqlOptions;

    /// <summary>
    /// 创建数据库上下文。
    /// </summary>
    /// <param name="dataSource">数据源描述。</param>
    /// <param name="tenantId">租户标识。</param>
    /// <param name="mappingProfile">映射配置名称。</param>
    /// <param name="readPreference">读取偏好。</param>
    /// <returns>数据库上下文。</returns>
    private DatabaseContext CreateContext(SqlDataSourceDescriptor dataSource, string tenantId,
        string mappingProfile, SqlReadPreference readPreference)
    {
        if (dataSource == null)
            throw new InvalidOperationException("SQL 数据源描述不能为空");
        return DatabaseContextSnapshot.Create(new DatabaseContext
        {
            DbKey = dataSource.Key,
            ProviderKey = dataSource.ProviderKey,
            TenantId = tenantId,
            MappingProfile = string.IsNullOrWhiteSpace(dataSource.MappingProfile) ? mappingProfile : dataSource.MappingProfile,
            ReadPreference = readPreference,
            DataSource = dataSource
        });
    }

    /// <summary>
    /// 从冻结上下文解析 Provider Key。
    /// </summary>
    private string ResolveProviderKey(DatabaseContext context)
    {
        if (_providerResolver == null)
            throw new InvalidOperationException("未注册 SQL Provider 解析器，无法解析 SQL 实现类型。");
        return _providerResolver.Resolve(context, databaseType: context?.DataSource?.DatabaseType).Key;
    }

    /// <summary>
    /// 解析连接字符串。
    /// </summary>
    /// <param name="dataSource">数据源描述。</param>
    /// <param name="template">模板 SQL 配置。</param>
    /// <returns>连接字符串。</returns>
    private string ResolveConnectionString(SqlDataSourceDescriptor dataSource, SqlOptions template)
    {
        if (string.IsNullOrWhiteSpace(dataSource?.ConnectionString) == false ||
            string.IsNullOrWhiteSpace(dataSource?.ConnectionStringName) == false)
            return _connectionStringResolver.Resolve(dataSource);
        if (template?.Connection != null)
            return null;
        return _connectionStringResolver.Resolve(dataSource);
    }

    /// <summary>
    /// 复制 SQL 配置。
    /// </summary>
    /// <param name="source">源配置。</param>
    /// <param name="target">目标配置。</param>
    private static void CopyOptions(SqlOptions source, SqlOptions target)
    {
        if (source == null || target == null)
            return;
        foreach (var property in target.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(t => t.CanWrite))
        {
            var sourceProperty = source.GetType().GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public);
            if (sourceProperty == null || sourceProperty.CanRead == false)
                continue;
            property.SetValue(target, sourceProperty.GetValue(source));
        }
        target.QueryCapabilities = CloneQueryCapabilities(source.QueryCapabilities);
    }

    /// <summary>
    /// 克隆查询语法能力配置。
    /// </summary>
    /// <param name="capabilities">源能力配置。</param>
    /// <returns>独立能力配置副本。</returns>
    private static SqlQueryCapabilities CloneQueryCapabilities(SqlQueryCapabilities capabilities) => capabilities == null
        ? null
        : new SqlQueryCapabilities
        {
            Cte = capabilities.Cte,
            Union = capabilities.Union,
            UnionAll = capabilities.UnionAll,
            Intersect = capabilities.Intersect,
            Except = capabilities.Except,
            RightJoin = capabilities.RightJoin,
            FullJoin = capabilities.FullJoin,
            Pagination = capabilities.Pagination
        };
}
