using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
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
        services.TryAddSingleton<SqlMetadataOptions>();
        services.TryAddSingleton<IDatabaseContextAccessor, AsyncLocalDatabaseContextAccessor>();
        services.TryAddScoped<IDatabaseScopeManager, DatabaseScopeManager>();
        services.TryAddSingleton<IDatabaseDescriptorResolver, DefaultDatabaseDescriptorResolver>();
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
        return services;
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
