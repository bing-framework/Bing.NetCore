using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
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
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType != typeof(ISqlProvider) || descriptor.ImplementationInstance is not ISqlProvider registeredProvider)
                continue;
            if (string.Equals(registeredProvider.Key?.Trim(), providerKey, StringComparison.OrdinalIgnoreCase))
            {
                if (ReferenceEquals(registeredProvider, provider))
                    return services;
                throw new InvalidOperationException($"SQL Provider Key '{providerKey}' 已注册。");
            }
        }
        services.AddSingleton<ISqlProvider>(provider);
        services.AddSingleton(new SqlBuilderFactoryRegistration(provider, creator));
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
        services.TryAddSingleton(provider =>
        {
            var options = new SqlMetadataOptions();
            foreach (var configure in provider.GetServices<ISqlMetadataOptionsConfigure>())
                configure.Configure(options);
            return options;
        });
        services.TryAddSingleton<IDatabaseContextAccessor, AsyncLocalDatabaseContextAccessor>();
        services.TryAddScoped<IDatabaseScopeManager, DatabaseScopeManager>();
        services.TryAddScoped<IReadPreferenceScopeManager, ReadPreferenceScopeManager>();
        services.TryAddSingleton<ISqlDataSourceResolver, DefaultSqlDataSourceResolver>();
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
        services.TryAddSingleton<ISqlMutationBuilderFactory, SqlMutationBuilderFactory>();
        services.TryAddSingleton<SqlImplementationTypeOptions>();
        services.TryAddSingleton<ISqlImplementationTypeResolver, DefaultSqlImplementationTypeResolver>();
        services.TryAddSingleton<ISqlQueryFactory, SqlQueryFactory>();
        services.TryAddSingleton<ISqlExecutorFactory, SqlExecutorFactory>();
        services.TryAddSingleton<ISqlMultipleQueryExecutorFactory, SqlMultipleQueryExecutorFactory>();
        services.TryAddSingleton<ISqlTransactionScopeFactory, SqlTransactionScopeFactory>();
        services.TryAddSingleton<ISqlDbConnectionFactoryResolver, DefaultSqlDbConnectionFactoryResolver>();
        return services;
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
    /// 注册 SQL 数据源。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="key">数据源标识。</param>
    /// <param name="databaseType">数据库类型。</param>
    /// <param name="connectionString">连接字符串。</param>
    /// <param name="connectionStringName">连接字符串配置名称。</param>
    /// <param name="setupAction">数据源配置操作。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqlDataSource(this IServiceCollection services, string key,
        DatabaseType databaseType, string connectionString = null, string connectionStringName = null,
        Action<SqlDataSourceDescriptor> setupAction = null)
    {
        return services.ConfigureSqlMetadata(options =>
        {
            var dataSourceKey = string.IsNullOrWhiteSpace(key) ? options.DataSources.DefaultDataSourceKey : key;
            if (string.IsNullOrWhiteSpace(options.DataSources.DefaultDataSourceKey))
                options.DataSources.DefaultDataSourceKey = dataSourceKey;
            if (!options.DataSources.DataSources.TryGetValue(dataSourceKey, out var descriptor))
            {
                descriptor = new SqlDataSourceDescriptor();
                options.DataSources.DataSources[dataSourceKey] = descriptor;
            }
            else if (string.IsNullOrWhiteSpace(key) && descriptor.DatabaseType != databaseType)
            {
                throw new InvalidOperationException(
                    $"默认 SQL 数据源 {dataSourceKey} 已注册为 {descriptor.DatabaseType}，不能使用无键注册覆盖为 {databaseType}。多 Provider 请使用具名数据源。");
            }
            descriptor.Key = dataSourceKey;
            descriptor.DatabaseType = databaseType;
            if (databaseType == DatabaseType.Doris)
                descriptor.SupportsTransactions = false;
            if (string.IsNullOrWhiteSpace(connectionString) == false)
                descriptor.ConnectionString = connectionString;
            if (string.IsNullOrWhiteSpace(connectionStringName) == false)
                descriptor.ConnectionStringName = connectionStringName;
            setupAction?.Invoke(descriptor);
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
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqlDataSource(this IServiceCollection services, IConfiguration configuration,
        string key, DatabaseType databaseType, string connectionStringName = null,
        Action<SqlDataSourceDescriptor> setupAction = null)
    {
        var name = string.IsNullOrWhiteSpace(connectionStringName) ? key : connectionStringName;
        var connectionString = configuration?.GetConnectionString(name);
        return services.AddSqlDataSource(key, databaseType, connectionString, name, setupAction);
    }

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
    /// 注册独立数据库连接工厂。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="databaseType">数据库类型。</param>
    /// <param name="factory">连接创建委托。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqlDbConnectionFactory(this IServiceCollection services,
        DatabaseType databaseType, Func<string, System.Data.IDbConnection> factory)
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));
        services.AddSingleton(new SqlDbConnectionFactoryRegistration
        {
            DatabaseType = databaseType,
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
    /// 注册指定数据库类型的 SQL 服务实现映射。
    /// </summary>
    /// <typeparam name="TService">服务契约类型。</typeparam>
    /// <typeparam name="TImplementation">具体实现类型。</typeparam>
    /// <param name="services">要注册服务的服务集合。</param>
    /// <param name="databaseType">映射适用的数据库类型。</param>
    /// <returns>当前服务集合，以支持链式注册。</returns>
    public static IServiceCollection AddSqlImplementationType<TService, TImplementation>(this IServiceCollection services,
        Bing.Data.Enums.DatabaseType databaseType)
        where TImplementation : TService
    {
        var options = GetOrCreateImplementationTypeOptions(services);
        options.Map(typeof(TService), typeof(TImplementation), databaseType);
        options.Map(typeof(TImplementation), typeof(TImplementation), databaseType);
        return services;
    }

    /// <summary>
    /// 获取或创建 SQL 实现类型配置
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>SQL 实现类型配置</returns>
    private static SqlImplementationTypeOptions GetOrCreateImplementationTypeOptions(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(t => t.ServiceType == typeof(SqlImplementationTypeOptions));
        if (descriptor?.ImplementationInstance is SqlImplementationTypeOptions options)
            return options;
        options = new SqlImplementationTypeOptions();
        services.RemoveAll<SqlImplementationTypeOptions>();
        services.AddSingleton(options);
        return options;
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
