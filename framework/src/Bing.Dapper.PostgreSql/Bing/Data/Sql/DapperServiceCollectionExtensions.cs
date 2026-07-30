using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Dapper;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Dapper.PostgreSql;

/// <summary>
/// PostgreSql 服务集合扩展
/// </summary>
public static class PostgreSqlServiceCollectionExtensions
{
    /// <summary>
    /// 注册 PostgreSQL Provider 能力，不配置默认数据源。
    /// 多 Provider 容器应通过具名数据源完成运行时路由。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddPostgreSqlProvider(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddSqlBuilderProvider(PostgreSqlSqlProvider.Instance, services => new PostgreSqlBuilder(services));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlBatchUpdateRenderer, PostgreSqlBatchUpdateRenderer>());
        var queryOptions = new SqlOptions<PostgreSqlQuery> { DatabaseType = DatabaseType.PgSql };
        var executorOptions = new SqlOptions<PostgreSqlExecutor> { DatabaseType = DatabaseType.PgSql };
        var multipleQueryOptions = new SqlOptions<PostgreSqlMultipleQueryExecutor> { DatabaseType = DatabaseType.PgSql };
        queryOptions.RegisterStringTypeHandler();
        executorOptions.RegisterStringTypeHandler();
        multipleQueryOptions.RegisterStringTypeHandler();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, PostgreSqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(PostgreSqlSqlProvider.Instance.Key, connection => new NpgsqlConnection(connection));
        services.AddDatabaseTypeConverter<PostgreSqlTypeConverter>(DatabaseType.PgSql);
        services.AddSqlImplementationType<ISqlQuery, PostgreSqlQuery>(DatabaseType.PgSql);
        services.AddSqlImplementationType<ISqlExecutor, PostgreSqlExecutor>(DatabaseType.PgSql);
        services.AddSqlImplementationType<ISqlMultipleQueryExecutor, PostgreSqlMultipleQueryExecutor>(DatabaseType.PgSql);
        services.TryAddTransient<ISqlQuery, PostgreSqlQuery>();
        services.TryAddTransient<ISqlExecutor, PostgreSqlExecutor>();
        services.TryAddTransient<ISqlMultipleQueryExecutor, PostgreSqlMultipleQueryExecutor>();
        services.TryAddSingleton(queryOptions);
        services.TryAddSingleton(executorOptions);
        services.TryAddSingleton(multipleQueryOptions);
        return services;
    }

    #region AddPostgreSqlQuery(注册PostgreSql Sql查询对象)

    /// <summary>
    /// 注册PostgreSql Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddPostgreSqlQuery(this IServiceCollection services)
    {
        services.AddPostgreSqlQuery("");
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddPostgreSqlQuery(this IServiceCollection services, string connection)
    {
        services.AddPostgreSqlQuery<ISqlQuery, PostgreSqlQuery>(connection);
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddPostgreSqlQuery(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddPostgreSqlQuery<ISqlQuery, PostgreSqlQuery>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddPostgreSqlQuery<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlQuery
        where TImplementation : PostgreSqlQueryBase, TInterface
    {
        services.AddPostgreSqlQuery<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddPostgreSqlQuery<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlQuery
        where TImplementation : PostgreSqlQueryBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation> { DatabaseType = DatabaseType.PgSql };
        services.AddSqlBuilderProvider(PostgreSqlSqlProvider.Instance, services => new PostgreSqlBuilder(services));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlBatchUpdateRenderer, PostgreSqlBatchUpdateRenderer>());
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.PgSql, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, PostgreSqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(PostgreSqlSqlProvider.Instance.Key, connection => new NpgsqlConnection(connection));
        services.AddDatabaseTypeConverter<PostgreSqlTypeConverter>(DatabaseType.PgSql);
        services.AddSqlImplementationType<TInterface, TImplementation>(DatabaseType.PgSql);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion

    #region AddPostgreSqlMultipleQueryExecutor(注册 PostgreSQL 多结果集查询执行器)

    /// <summary>
    /// 注册 PostgreSQL 多结果集查询执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddPostgreSqlMultipleQueryExecutor(this IServiceCollection services)
    {
        return services.AddPostgreSqlMultipleQueryExecutor("");
    }

    /// <summary>
    /// 注册 PostgreSQL 多结果集查询执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="connection">数据库连接字符串。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddPostgreSqlMultipleQueryExecutor(this IServiceCollection services,
        string connection)
    {
        return services.AddPostgreSqlMultipleQueryExecutor(options => options.ConnectionString(connection));
    }

    /// <summary>
    /// 注册 PostgreSQL 多结果集查询执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="setupAction">SQL 配置操作。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddPostgreSqlMultipleQueryExecutor(this IServiceCollection services,
        Action<SqlOptions> setupAction)
    {
        var sqlOptions = new SqlOptions<PostgreSqlMultipleQueryExecutor> { DatabaseType = DatabaseType.PgSql };
        services.AddSqlBuilderProvider(PostgreSqlSqlProvider.Instance,
            serviceProvider => new PostgreSqlBuilder(serviceProvider));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlBatchUpdateRenderer, PostgreSqlBatchUpdateRenderer>());
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.PgSql, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, PostgreSqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(PostgreSqlSqlProvider.Instance.Key, connection => new NpgsqlConnection(connection));
        services.AddDatabaseTypeConverter<PostgreSqlTypeConverter>(DatabaseType.PgSql);
        services.AddSqlImplementationType<ISqlMultipleQueryExecutor, PostgreSqlMultipleQueryExecutor>(DatabaseType.PgSql);
        services.TryAddTransient<ISqlMultipleQueryExecutor, PostgreSqlMultipleQueryExecutor>();
        services.TryAddSingleton(sqlOptions);
        return services;
    }

    #endregion

    #region AddPostgreSqlExecutor(注册PostgreSql Sql执行器)

    /// <summary>
    /// 注册PostgreSql Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddPostgreSqlExecutor(this IServiceCollection services)
    {
        services.AddPostgreSqlExecutor("");
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddPostgreSqlExecutor(this IServiceCollection services, string connection)
    {
        services.AddPostgreSqlExecutor<ISqlExecutor, PostgreSqlExecutor>(connection);
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddPostgreSqlExecutor(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddPostgreSqlExecutor<ISqlExecutor, PostgreSqlExecutor>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddPostgreSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlExecutor
        where TImplementation : PostgreSqlExecutorBase, TInterface
    {
        services.AddPostgreSqlExecutor<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册PostgreSql Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddPostgreSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlExecutor
        where TImplementation : PostgreSqlExecutorBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation> { DatabaseType = DatabaseType.PgSql };
        services.AddSqlBuilderProvider(PostgreSqlSqlProvider.Instance, services => new PostgreSqlBuilder(services));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlBatchUpdateRenderer, PostgreSqlBatchUpdateRenderer>());
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.PgSql, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, PostgreSqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(PostgreSqlSqlProvider.Instance.Key, connection => new NpgsqlConnection(connection));
        services.AddDatabaseTypeConverter<PostgreSqlTypeConverter>(DatabaseType.PgSql);
        services.AddSqlImplementationType<TInterface, TImplementation>(DatabaseType.PgSql);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion
}
