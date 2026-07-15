using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper服务集合扩展
/// </summary>
public static partial class DapperServiceCollectionExtensions
{
    /// <summary>
    /// 注册数据库信息
    /// </summary>
    /// <typeparam name="TDatabase">数据库信息类型</typeparam>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddDatabase<TDatabase>(this IServiceCollection services)
        where TDatabase : class, IDatabase
    {
        return services.AddDatabase<IDatabase, TDatabase>();
    }

    /// <summary>
    /// 注册数据库信息
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddDatabase<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : IDatabase
        where TImplementation : class, TInterface
    {
        services.TryAddScoped(typeof(TInterface), typeof(TImplementation));
        services.TryAddScoped<ITableDatabase, DefaultTableDatabase>();
        services.TryAddSingleton(provider =>
        {
            var options = new SqlMetadataOptions();
            foreach (var configure in provider.GetServices<ISqlMetadataOptionsConfigure>())
                configure.Configure(options);
            return options;
        });
        services.TryAddSingleton<IDatabaseContextAccessor, AsyncLocalDatabaseContextAccessor>();
        services.TryAddScoped<IDatabaseScopeManager, DatabaseScopeManager>();
        services.TryAddSingleton<ISqlDataSourceResolver, DefaultSqlDataSourceResolver>();
        services.TryAddSingleton<ISqlDatabaseContextResolver, DefaultSqlDatabaseContextResolver>();
        services.TryAddSingleton<ITypeConverterResolver, DefaultTypeConverterResolver>();
        services.TryAddSingleton<IEntityMappingResolver, DefaultEntityMappingResolver>();
        services.TryAddSingleton<IFieldValueConverter, DefaultFieldValueConverter>();
        services.TryAddSingleton<IFieldValueConverterSelector, DefaultFieldValueConverterSelector>();
        services.TryAddSingleton<ISqlParameterFactory, DefaultSqlParameterFactory>();
        services.TryAddSingleton<ISqlParameterBinder, DefaultSqlParameterBinder>();
        services.TryAddSingleton<SqlImplementationTypeOptions>();
        services.TryAddSingleton<ISqlImplementationTypeResolver, DefaultSqlImplementationTypeResolver>();
        services.TryAddSingleton<ISqlQueryFactory, SqlQueryFactory>();
        services.TryAddSingleton<ISqlExecutorFactory, SqlExecutorFactory>();
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
            descriptor.Key = dataSourceKey;
            descriptor.DatabaseType = databaseType;
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
    /// 注册实体元数据
    /// </summary>
    /// <typeparam name="TEntityMetadata">实体元数据类型</typeparam>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddEntityMetadata<TEntityMetadata>(this IServiceCollection services)
        where TEntityMetadata : class, IEntityMetadata
    {
        return services.AddEntityMetadata<IEntityMetadata, TEntityMetadata>();
    }

    /// <summary>
    /// 注册实体元数据
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddEntityMetadata<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : IEntityMetadata
        where TImplementation : class, TInterface
    {
        services.TryAddSingleton(typeof(TInterface), typeof(TImplementation));
        return services;
    }

    /// <summary>
    /// 注册数据库类型转换器
    /// </summary>
    /// <typeparam name="TConverter">数据类型转换器类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="databaseType">数据库类型</param>
    /// <returns>服务集合</returns>
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
    /// 注册 SQL 实现类型映射
    /// </summary>
    /// <typeparam name="TService">服务类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="databaseType">数据库类型</param>
    /// <returns>服务集合</returns>
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
