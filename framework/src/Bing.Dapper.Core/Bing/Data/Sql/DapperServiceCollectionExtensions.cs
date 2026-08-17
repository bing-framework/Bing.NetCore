using Bing.Data.Enums;
using Bing.Data.Filters;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Filters;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Dapper;

/// <summary>
/// Dapper 核心服务集合扩展
/// </summary>
public static class DapperCoreServiceCollectionExtensions
{
    /// <summary>
    /// 注册 SQL Provider 及其 Builder 创建委托。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="provider">SQL Provider。</param>
    /// <param name="creator">使用查询级共享服务创建 Builder 的委托。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqlBuilderProvider(this IServiceCollection services, ISqlProvider provider,
        Func<SqlBuilderServices, ISqlBuilder> creator)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));
        if (creator == null)
            throw new ArgumentNullException(nameof(creator));
        var providerKey = provider.Key?.Trim();
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("SQL Provider Key 不能为空。", nameof(provider));
        var registeredProviders = services
            .Where(descriptor => descriptor.ServiceType == typeof(ISqlProvider) &&
                                 descriptor.ImplementationInstance is ISqlProvider registeredProvider &&
                                 string.Equals(registeredProvider.Key?.Trim(), providerKey,
                                     StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (registeredProviders.Count > 1)
            throw new InvalidOperationException($"SQL Provider Key '{providerKey}' 重复注册。");
        if (registeredProviders.Count == 1 &&
            ReferenceEquals((ISqlProvider)registeredProviders[0].ImplementationInstance, provider) == false)
            throw new InvalidOperationException($"SQL Provider Key '{providerKey}' 已注册。");

        var builderRegistrations = services
            .Where(descriptor => descriptor.ImplementationInstance is SqlBuilderFactoryRegistration registration &&
                                 string.Equals(registration.Provider.Key?.Trim(), providerKey,
                                     StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (builderRegistrations.Count > 1)
            throw new InvalidOperationException($"SQL Provider Key '{providerKey}' 的 SQL Builder 创建委托重复注册。");
        if (builderRegistrations.Count == 1)
        {
            var registration = (SqlBuilderFactoryRegistration)builderRegistrations[0].ImplementationInstance;
            if (ReferenceEquals(registration.Provider, provider) == false)
                throw new InvalidOperationException($"SQL Provider Key '{providerKey}' 已注册不同的 SQL Builder Provider。");
            if (Equals(registration.Creator, creator) == false)
                throw new InvalidOperationException($"SQL Provider Key '{providerKey}' 已注册不同的 SQL Builder 创建委托。");
        }
        else
            services.AddSingleton(new SqlBuilderFactoryRegistration(provider, creator));
        if (registeredProviders.Count == 0)
            services.AddSingleton<ISqlProvider>(provider);
        return services;
    }

    /// <summary>
    /// 注册 Dapper SQL 核心服务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    /// <remarks>
    /// 该方法不注册或解析跨 ORM 的 <c>IDatabase</c> 服务。Dapper 自有连接仅由
    /// <see cref="ISqlDbConnectionFactoryResolver"/> 创建，外部连接仅能由内部资源绑定器绑定。
    /// </remarks>
    public static IServiceCollection AddSqlCore(this IServiceCollection services)
    {
        NormalizeRegisteredDataSourceConstraints(services);
        services.TryAddSingleton(provider =>
        {
            var options = new SqlMetadataOptions();
            foreach (var configure in provider.GetServices<ISqlMetadataOptionsConfigure>())
                configure.Configure(options);
            NormalizeDataSourceConstraints(options);
            return options;
        });
        services.TryAddSingleton<IDatabaseContextAccessor, AsyncLocalDatabaseContextAccessor>();
        services.TryAddScoped<IDatabaseScopeManager, DatabaseScopeManager>();
        services.TryAddScoped<IDataFilter, DataFilter>();
        services.TryAddScoped<IReadPreferenceScopeManager, ReadPreferenceScopeManager>();
        services.TryAddSingleton<ISqlDataSourceResolver, DefaultSqlDataSourceResolver>();
        services.TryAddSingleton<ISqlProviderResolver, DefaultSqlProviderResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDatabaseIdentityContributor, DefaultSqlDatabaseIdentityContributor>());
        services.TryAddSingleton<ISqlDatabaseIdentityResolver, DefaultSqlDatabaseIdentityResolver>();
        services.TryAddSingleton<ISqlConnectionStringResolver, DefaultSqlConnectionStringResolver>();
        services.TryAddSingleton<ISqlDatabaseContextResolver, DefaultSqlDatabaseContextResolver>();
        services.TryAddSingleton<ITypeConverterResolver, DefaultTypeConverterResolver>();
        services.TryAddSingleton<IEntityModelMetadataProvider>(provider =>
            new CompositeEntityModelMetadataProvider(provider
                .GetServices<IEntityModelMetadataProviderRegistration>()
                .Select(registration => registration.Create(provider))));
        services.TryAddSingleton<ISqlObjectNameFormatter, DefaultSqlObjectNameFormatter>();
        services.TryAddSingleton<ISqlObjectNameCapabilityProvider, DefaultSqlObjectNameCapabilityProvider>();
        services.TryAddSingleton<ISqlTableReferenceValidator, DefaultSqlTableReferenceValidator>();
        services.TryAddSingleton<ISqlCrossDatabaseQueryValidator, DefaultSqlCrossDatabaseQueryValidator>();
        services.TryAddSingleton<IEntityMappingResolver, DefaultEntityMappingResolver>();
        services.TryAddSingleton<IFieldValueConverter, DefaultFieldValueConverter>();
        services.TryAddSingleton<IFieldValueConverterSelector, DefaultFieldValueConverterSelector>();
        services.TryAddSingleton<ISqlParameterFactory, DefaultSqlParameterFactory>();
        services.TryAddSingleton<IDapperParameterBinder, DefaultSqlParameterBinder>();
        services.TryAddSingleton<ISqlParameterBinder>(provider => provider.GetRequiredService<IDapperParameterBinder>());
        services.TryAddSingleton<ISqlBuilderFactory, SqlBuilderFactory>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlFilter, IsDeletedFilter>());
        services.TryAddSingleton<ISqlEntityMutationCommandBuilderFactory, SqlEntityMutationCommandBuilderFactory>();
        services.TryAddSingleton<ISqlQueryFactory, SqlQueryFactory>();
        services.TryAddTransient<ISqlQuery>(provider =>
            provider.GetRequiredService<ISqlQueryFactory>().Create());
        services.TryAddSingleton<ISqlExecutorFactory, SqlExecutorFactory>();
        services.TryAddTransient<ISqlExecutor>(provider =>
            provider.GetRequiredService<ISqlExecutorFactory>().Create());
        services.TryAddSingleton<ISqlMultipleQueryExecutorFactory, SqlMultipleQueryExecutorFactory>();
        services.TryAddTransient<ISqlMultipleQueryExecutor>(provider =>
            provider.GetRequiredService<ISqlMultipleQueryExecutorFactory>().Create());
        services.TryAddSingleton<ISqlTransactionScopeFactory, SqlTransactionScopeFactory>();
        services.TryAddSingleton<ISqlDbConnectionFactoryResolver, DefaultSqlDbConnectionFactoryResolver>();
        return services;
    }

    /// <summary>
    /// 归一化调用方预注册的 SQL 元数据选项。
    /// </summary>
    /// <param name="services">服务集合。</param>
    private static void NormalizeRegisteredDataSourceConstraints(IServiceCollection services)
    {
        for (var index = 0; index < services.Count; index++)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType != typeof(SqlMetadataOptions))
                continue;
            if (descriptor.ImplementationInstance is SqlMetadataOptions options)
            {
                NormalizeDataSourceConstraints(options);
                continue;
            }
            if (descriptor.ImplementationFactory != null)
            {
                var factory = descriptor.ImplementationFactory;
                services[index] = new ServiceDescriptor(typeof(SqlMetadataOptions), provider =>
                {
                    var options = factory(provider) as SqlMetadataOptions;
                    NormalizeDataSourceConstraints(options);
                    return options;
                }, descriptor.Lifetime);
                continue;
            }
            if (descriptor.ImplementationType != null)
            {
                var implementationType = descriptor.ImplementationType;
                services[index] = new ServiceDescriptor(typeof(SqlMetadataOptions), provider =>
                {
                    var options = ActivatorUtilities.CreateInstance(provider, implementationType) as SqlMetadataOptions;
                    NormalizeDataSourceConstraints(options);
                    return options;
                }, descriptor.Lifetime);
            }
        }
    }

    /// <summary>
    /// 配置 SQL 元数据选项。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="setupAction">配置操作。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection ConfigureSqlMetadata(this IServiceCollection services,
        Action<SqlMetadataOptions> setupAction)
    {
        if (setupAction == null)
            return services;
        services.AddSingleton<ISqlMetadataOptionsConfigure>(new DelegateSqlMetadataOptionsConfigure(setupAction));
        return services;
    }

    /// <summary>
    /// 归一化具有固定运行边界的数据源约束。
    /// </summary>
    /// <param name="options">已完成所有配置回调的 SQL 元数据选项。</param>
    private static void NormalizeDataSourceConstraints(SqlMetadataOptions options)
    {
        if (options == null)
            return;
        foreach (var descriptor in options.DataSources.DataSources.Values)
        {
            if (descriptor?.DatabaseType != DatabaseType.Doris)
                continue;
            descriptor.IsReadOnly = true;
            descriptor.SupportsTransactions = false;
        }
    }

    /// <summary>
    /// 注册 SQL 数据源。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="key">数据源标识。</param>
    /// <param name="databaseType">数据库类型。</param>
    /// <param name="connectionString">连接字符串。</param>
    /// <param name="connectionStringName">连接字符串配置名称。</param>
    /// <param name="setupAction">数据源配置操作。</param>
    /// <param name="providerKey">SQL Provider 唯一标识；未指定时使用官方数据库类型兼容映射。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqlDataSource(this IServiceCollection services, string key,
        DatabaseType databaseType, string connectionString = null, string connectionStringName = null,
        Action<SqlDataSourceDescriptor> setupAction = null, string providerKey = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        var requestedKey = NormalizeOptionalKey(key);
        var normalizedProviderKey = NormalizeOptionalKey(providerKey);
        var normalizedConnectionString = NormalizeOptionalValue(connectionString);
        var normalizedConnectionStringName = NormalizeOptionalValue(connectionStringName);
        return services.ConfigureSqlMetadata(options =>
        {
            var defaultDataSourceKey = NormalizeOptionalKey(options.DataSources.DefaultDataSourceKey);
            if (requestedKey == null && defaultDataSourceKey == null)
                throw new InvalidOperationException(
                    $"未配置默认 SQL 数据源 Key，无法使用无键方式注册数据源。请配置 {nameof(SqlDataSourceOptions.DefaultDataSourceKey)} 或指定数据源 Key。");
            var dataSourceKey = requestedKey ?? defaultDataSourceKey;
            if (options.DataSources.DataSources.TryGetValue(dataSourceKey, out var descriptor))
            {
                ValidateExistingDataSource(descriptor, dataSourceKey, requestedKey == null, databaseType,
                    normalizedProviderKey, normalizedConnectionString, normalizedConnectionStringName, setupAction);
                return;
            }

            descriptor = new SqlDataSourceDescriptor
            {
                Key = dataSourceKey,
                ProviderKey = normalizedProviderKey,
                DatabaseType = databaseType,
                ConnectionString = normalizedConnectionString,
                ConnectionStringName = normalizedConnectionStringName
            };
            if (databaseType == DatabaseType.Doris)
            {
                descriptor.IsReadOnly = true;
                descriptor.SupportsTransactions = false;
            }
            setupAction?.Invoke(descriptor);
            if (databaseType == DatabaseType.Doris)
            {
                // Doris 兼容标识始终保持保守的只读、无事务边界；可写 MySQL 端点必须显式使用 DatabaseType.MySql。
                descriptor.IsReadOnly = true;
                descriptor.SupportsTransactions = false;
            }
            options.DataSources.DataSources.Add(dataSourceKey, descriptor);
        });
    }

    /// <summary>
    /// 从配置注册 SQL 数据源。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">配置。</param>
    /// <param name="key">数据源标识。</param>
    /// <param name="databaseType">数据库类型。</param>
    /// <param name="connectionStringName">连接字符串配置名称。</param>
    /// <param name="setupAction">数据源配置操作。</param>
    /// <param name="providerKey">SQL Provider 唯一标识；未指定时使用官方数据库类型兼容映射。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqlDataSource(this IServiceCollection services, IConfiguration configuration,
        string key, DatabaseType databaseType, string connectionStringName = null,
        Action<SqlDataSourceDescriptor> setupAction = null, string providerKey = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        var name = string.IsNullOrWhiteSpace(connectionStringName) ? key : connectionStringName;
        var connectionString = configuration?.GetConnectionString(name);
        return services.AddSqlDataSource(key, databaseType, connectionString, name, setupAction, providerKey);
    }

    /// <summary>
    /// 验证已有数据源是否与本次注册完全一致。
    /// </summary>
    /// <param name="descriptor">已注册的数据源描述。</param>
    /// <param name="dataSourceKey">本次注册解析后的数据源唯一标识。</param>
    /// <param name="isDefaultRegistration">是否通过无键方式注册默认数据源。</param>
    /// <param name="databaseType">本次请求的数据库类型。</param>
    /// <param name="providerKey">本次请求的规范化 Provider Key。</param>
    /// <param name="connectionString">本次请求的规范化连接字符串。</param>
    /// <param name="connectionStringName">本次请求的规范化连接字符串配置名称。</param>
    /// <param name="setupAction">本次请求的数据源自定义配置；存在时不允许视为幂等重复注册。</param>
    private static void ValidateExistingDataSource(SqlDataSourceDescriptor descriptor, string dataSourceKey,
        bool isDefaultRegistration, DatabaseType databaseType, string providerKey, string connectionString,
        string connectionStringName, Action<SqlDataSourceDescriptor> setupAction)
    {
        if (isDefaultRegistration && descriptor.DatabaseType != databaseType)
            throw new InvalidOperationException(
                $"默认 SQL 数据源 {dataSourceKey} 已注册为 {descriptor.DatabaseType}，不能使用无键注册覆盖为 {databaseType}。多 Provider 请使用具名数据源。");
        if (descriptor.DatabaseType != databaseType ||
            string.Equals(NormalizeOptionalKey(descriptor.ProviderKey), providerKey, StringComparison.OrdinalIgnoreCase) == false ||
            string.Equals(NormalizeOptionalValue(descriptor.ConnectionString), connectionString, StringComparison.Ordinal) == false ||
            string.Equals(NormalizeOptionalValue(descriptor.ConnectionStringName), connectionStringName, StringComparison.Ordinal) == false ||
            setupAction != null)
            throw new InvalidOperationException($"SQL 数据源 Key '{dataSourceKey}' 重复注册且配置不一致。");
    }

    /// <summary>
    /// 规范化可选标识。
    /// </summary>
    /// <param name="value">可能为 null、空白或包含首尾空白的标识。</param>
    /// <returns>空白输入返回 <see langword="null"/>；非空输入返回去除首尾空白后的标识。</returns>
    private static string NormalizeOptionalKey(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// 规范化可选配置值。
    /// </summary>
    /// <param name="value">可能为 null、空白或有效内容的配置值。</param>
    /// <returns>空白输入返回 <see langword="null"/>；非空输入保留原始内容。</returns>
    private static string NormalizeOptionalValue(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// 注册默认实体模型元数据提供器。
    /// </summary>
    /// <typeparam name="TEntityMetadata">实体元数据提供器实现类型。</typeparam>
    /// <param name="services">要注册服务的服务集合。</param>
    /// <returns>当前服务集合，以支持链式注册。</returns>
    public static IServiceCollection AddEntityModelMetadataProvider<TEntityMetadata>(this IServiceCollection services)
        where TEntityMetadata : class, IEntityModelMetadataProvider
    {
        return services.AddEntityModelMetadataProvider<IEntityModelMetadataProvider, TEntityMetadata>();
    }

    /// <summary>
    /// 将实体模型元数据提供器注册到指定服务契约。
    /// </summary>
    /// <typeparam name="TInterface">元数据提供器服务契约类型。</typeparam>
    /// <typeparam name="TImplementation">元数据提供器实现类型。</typeparam>
    /// <param name="services">要注册服务的服务集合。</param>
    /// <returns>当前服务集合，以支持链式注册。</returns>
    public static IServiceCollection AddEntityModelMetadataProvider<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : IEntityModelMetadataProvider
        where TImplementation : class, TInterface
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddSingleton<TImplementation>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityModelMetadataProviderRegistration,
            EntityModelMetadataProviderRegistration<TImplementation>>());
        if (typeof(TInterface) != typeof(IEntityModelMetadataProvider))
            services.TryAddSingleton(typeof(TInterface), provider => provider.GetRequiredService<TImplementation>());
        return services;
    }

    /// <summary>
    /// 注册指定数据库类型的值转换器。
    /// </summary>
    /// <typeparam name="TConverter">数据库值转换器实现类型。</typeparam>
    /// <param name="services">要注册服务的服务集合。</param>
    /// <param name="databaseType">转换器适用的数据库类型。</param>
    /// <returns>当前服务集合，以支持链式注册。</returns>
    public static IServiceCollection AddDatabaseTypeConverter<TConverter>(this IServiceCollection services,
        Bing.Data.Enums.DatabaseType databaseType)
        where TConverter : class, Bing.Data.Metadata.ITypeConverter
    {
        services.TryAddSingleton<TConverter>();
        services.AddSingleton<DatabaseTypeConverterRegistration>(provider => new DatabaseTypeConverterRegistration
        {
            DatabaseType = databaseType,
            Converter = provider.GetRequiredService<TConverter>()
        });
        return services;
    }

    /// <summary>
    /// 注册指定 SQL Provider 的独立连接工厂。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="providerKey">SQL Provider 唯一标识。</param>
    /// <param name="factory">连接创建委托。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqlDbConnectionFactory(this IServiceCollection services,
        string providerKey, Func<string, System.Data.IDbConnection> factory)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("SQL Provider Key 不能为空。", nameof(providerKey));
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));
        var normalizedProviderKey = providerKey.Trim();
        foreach (var descriptor in services)
        {
            if (descriptor.ImplementationInstance is not SqlDbConnectionFactoryRegistration registration ||
                string.Equals(registration.ProviderKey, normalizedProviderKey, StringComparison.OrdinalIgnoreCase) == false)
                continue;
            if (Equals(registration.Factory, factory))
                return services;
            throw new InvalidOperationException($"Provider Key '{normalizedProviderKey}' 的独立连接工厂重复注册。");
        }
        services.AddSingleton(new SqlDbConnectionFactoryRegistration
        {
            ProviderKey = normalizedProviderKey,
            Factory = factory
        });
        return services;
    }

    /// <summary>
    /// SQL 元数据选项配置。
    /// </summary>
    private interface ISqlMetadataOptionsConfigure
    {
        /// <summary>
        /// 配置 SQL 元数据选项。
        /// </summary>
        /// <param name="options">SQL 元数据选项。</param>
        void Configure(SqlMetadataOptions options);
    }

    /// <summary>
    /// 委托 SQL 元数据选项配置。
    /// </summary>
    private sealed class DelegateSqlMetadataOptionsConfigure : ISqlMetadataOptionsConfigure
    {
        /// <summary>
        /// 配置操作。
        /// </summary>
        private readonly Action<SqlMetadataOptions> _setupAction;

        /// <summary>
        /// 初始化一个<see cref="DelegateSqlMetadataOptionsConfigure"/>类型的实例。
        /// </summary>
        /// <param name="setupAction">配置操作。</param>
        public DelegateSqlMetadataOptionsConfigure(Action<SqlMetadataOptions> setupAction) =>
            _setupAction = setupAction;

        /// <inheritdoc />
        public void Configure(SqlMetadataOptions options) => _setupAction(options);
    }

    /// <summary>
    /// 注册指定 Provider 的运行时服务实现。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="runtime">不可变的 Provider 运行时服务描述。</param>
    /// <returns>当前服务集合。</returns>
    public static IServiceCollection AddSqlProviderRuntime(this IServiceCollection services, SqlProviderRuntime runtime)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (runtime == null)
            throw new ArgumentNullException(nameof(runtime));
        var registrations = services
            .Where(item => item.ServiceType == typeof(SqlProviderRuntime))
            .Select(item => item.ImplementationInstance as SqlProviderRuntime)
            .Where(item => item != null && string.Equals(item.ProviderKey, runtime.ProviderKey,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (registrations.Count > 1)
            throw new InvalidOperationException($"SQL Provider Key '{runtime.ProviderKey}' 的运行时服务重复注册。");
        if (registrations.Count == 1)
        {
            var current = registrations[0];
            if (ReferenceEquals(current, runtime) == false &&
                (current.QueryType != runtime.QueryType || current.ExecutorType != runtime.ExecutorType ||
                 current.MultipleQueryExecutorType != runtime.MultipleQueryExecutorType))
                throw new InvalidOperationException($"SQL Provider Key '{runtime.ProviderKey}' 的运行时服务重复注册且配置不一致。");
            return services;
        }
        services.AddSingleton(runtime);
        return services;
    }

}

/// <summary>
/// 创建注册到组合元数据提供器前置链中的提供器。
/// </summary>
internal interface IEntityModelMetadataProviderRegistration
{
    /// <summary>
    /// 从服务提供器创建元数据提供器实例。
    /// </summary>
    /// <param name="serviceProvider">当前服务提供器。</param>
    /// <returns>实体模型元数据提供器。</returns>
    IEntityModelMetadataProvider Create(IServiceProvider serviceProvider);
}

/// <summary>
/// 通过依赖注入解析指定类型的元数据提供器。
/// </summary>
/// <typeparam name="TProvider">元数据提供器实现类型。</typeparam>
internal sealed class EntityModelMetadataProviderRegistration<TProvider> : IEntityModelMetadataProviderRegistration
    where TProvider : class, IEntityModelMetadataProvider
{
    /// <inheritdoc />
    public IEntityModelMetadataProvider Create(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<TProvider>();
}
