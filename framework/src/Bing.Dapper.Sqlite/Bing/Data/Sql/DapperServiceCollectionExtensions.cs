using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Dapper.Sqlite;

/// <summary>
/// Sqlite 服务集合扩展
/// </summary>
public static class SqliteServiceCollectionExtensions
{
    /// <summary>
    /// 注册 SQLite Provider 能力，不配置默认数据源。
    /// 多 Provider 容器应通过具名数据源完成运行时路由。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqliteProvider(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddSqlCore();
        services.AddSqlBuilderProvider(SqliteSqlProvider.Instance, CreateBuilder);
        var queryOptions = new SqlOptions<SqliteSqlQuery> { DatabaseType = DatabaseType.Sqlite };
        var executorOptions = new SqlOptions<SqliteSqlExecutor> { DatabaseType = DatabaseType.Sqlite };
        var multipleQueryOptions = new SqlOptions<SqliteSqlMultipleQueryExecutor> { DatabaseType = DatabaseType.Sqlite };
        queryOptions.RegisterStringTypeHandler();
        executorOptions.RegisterStringTypeHandler();
        multipleQueryOptions.RegisterStringTypeHandler();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, SqliteDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(SqliteSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<SqliteTypeConverter>(DatabaseType.Sqlite);
        services.AddSqlImplementationType<ISqlQuery, SqliteSqlQuery>(SqliteSqlProvider.Instance.Key);
        services.AddSqlImplementationType<ISqlExecutor, SqliteSqlExecutor>(SqliteSqlProvider.Instance.Key);
        services.AddSqlImplementationType<ISqlMultipleQueryExecutor, SqliteSqlMultipleQueryExecutor>(SqliteSqlProvider.Instance.Key);
        services.TryAddTransient<ISqlQuery, SqliteSqlQuery>();
        services.TryAddTransient<ISqlExecutor, SqliteSqlExecutor>();
        services.TryAddTransient<ISqlMultipleQueryExecutor, SqliteSqlMultipleQueryExecutor>();
        services.TryAddSingleton(queryOptions);
        services.TryAddSingleton(executorOptions);
        services.TryAddSingleton(multipleQueryOptions);
        return services;
    }

    #region AddSqliteSqlQuery(注册Sqlite Sql查询对象)

    /// <summary>
    /// 注册Sqlite Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddSqliteSqlQuery(this IServiceCollection services)
    {
        services.AddSqliteSqlQuery("");
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddSqliteSqlQuery(this IServiceCollection services, string connection)
    {
        services.AddSqliteSqlQuery<ISqlQuery, SqliteSqlQuery>(connection);
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql查询对象
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddSqliteSqlQuery(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddSqliteSqlQuery<ISqlQuery, SqliteSqlQuery>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddSqliteSqlQuery<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlQuery
        where TImplementation : SqliteSqlQueryBase, TInterface
    {
        services.AddSqliteSqlQuery<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql查询对象
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    /// <returns>当前服务集合，以支持链式注册。</returns>
    public static IServiceCollection AddSqliteSqlQuery<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlQuery
        where TImplementation : SqliteSqlQueryBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation> { DatabaseType = DatabaseType.Sqlite };
        services.AddSqlBuilderProvider(SqliteSqlProvider.Instance, CreateBuilder);
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.Sqlite, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, SqliteDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(SqliteSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<SqliteTypeConverter>(DatabaseType.Sqlite);
        services.AddSqlImplementationType<TInterface, TImplementation>(SqliteSqlProvider.Instance.Key);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion

    #region AddSqliteSqlMultipleQueryExecutor(注册Sqlite多结果集查询执行器)

    /// <summary>
    /// 注册 SQLite 多结果集查询执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqliteSqlMultipleQueryExecutor(this IServiceCollection services)
    {
        services.AddSqliteSqlMultipleQueryExecutor("");
        return services;
    }

    /// <summary>
    /// 注册 SQLite 多结果集查询执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="connection">数据库连接字符串。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqliteSqlMultipleQueryExecutor(this IServiceCollection services,
        string connection)
    {
        return services.AddSqliteSqlMultipleQueryExecutor(options => options.ConnectionString(connection));
    }

    /// <summary>
    /// 注册 SQLite 多结果集查询执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="setupAction">SQL 配置操作。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddSqliteSqlMultipleQueryExecutor(this IServiceCollection services,
        Action<SqlOptions> setupAction)
    {
        var sqlOptions = new SqlOptions<SqliteSqlMultipleQueryExecutor> { DatabaseType = DatabaseType.Sqlite };
        services.AddSqlBuilderProvider(SqliteSqlProvider.Instance, CreateBuilder);
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.Sqlite, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, SqliteDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(SqliteSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<SqliteTypeConverter>(DatabaseType.Sqlite);
        services.AddSqlImplementationType<ISqlMultipleQueryExecutor, SqliteSqlMultipleQueryExecutor>(SqliteSqlProvider.Instance.Key);
        services.TryAddTransient<ISqlMultipleQueryExecutor, SqliteSqlMultipleQueryExecutor>();
        services.TryAddSingleton(sqlOptions);
        return services;
    }

    #endregion

    #region AddSqliteSqlExecutor(注册Sqlite Sql执行器)

    /// <summary>
    /// 注册Sqlite Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddSqliteSqlExecutor(this IServiceCollection services)
    {
        services.AddSqliteSqlExecutor("");
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddSqliteSqlExecutor(this IServiceCollection services, string connection)
    {
        services.AddSqliteSqlExecutor<ISqlExecutor, SqliteSqlExecutor>(connection);
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql执行器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static IServiceCollection AddSqliteSqlExecutor(this IServiceCollection services, Action<SqlOptions> setupAction)
    {
        services.AddSqliteSqlExecutor<ISqlExecutor, SqliteSqlExecutor>(setupAction);
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="connection">数据库连接字符串</param>
    public static IServiceCollection AddSqliteSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, string connection)
        where TInterface : ISqlExecutor
        where TImplementation : SqliteSqlExecutorBase, TInterface
    {
        services.AddSqliteSqlExecutor<TInterface, TImplementation>(t => t.ConnectionString(connection));
        return services;
    }

    /// <summary>
    /// 注册Sqlite Sql执行器
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    /// <returns>当前服务集合，以支持链式注册。</returns>
    public static IServiceCollection AddSqliteSqlExecutor<TInterface, TImplementation>(this IServiceCollection services, Action<SqlOptions> setupAction)
        where TInterface : ISqlExecutor
        where TImplementation : SqliteSqlExecutorBase, TInterface
    {
        var sqlOptions = new SqlOptions<TImplementation> { DatabaseType = DatabaseType.Sqlite };
        services.AddSqlBuilderProvider(SqliteSqlProvider.Instance, CreateBuilder);
        setupAction?.Invoke(sqlOptions);
        sqlOptions.RegisterStringTypeHandler();
        services.AddSqlDataSource(null, DatabaseType.Sqlite, sqlOptions.ConnectionString);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, SqliteDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(SqliteSqlProvider.Instance.Key, CreateConnection);
        services.AddDatabaseTypeConverter<SqliteTypeConverter>(DatabaseType.Sqlite);
        services.AddSqlImplementationType<TInterface, TImplementation>(SqliteSqlProvider.Instance.Key);
        services.TryAddTransient(typeof(TInterface), typeof(TImplementation));
        services.TryAddSingleton(typeof(SqlOptions<TImplementation>), _ => sqlOptions);
        return services;
    }

    #endregion
    /// <summary>
    /// 根据数据源连接字符串创建 SQLite 独立连接。
    /// </summary>
    /// <param name="connectionString">SQLite 数据源连接字符串。</param>
    /// <returns>尚未打开的 SQLite 数据库连接。</returns>
    private static SqliteConnection CreateConnection(string connectionString) => new(connectionString);

    /// <summary>
    /// 使用查询级共享服务创建 SQLite SQL Builder。
    /// </summary>
    /// <param name="services">当前查询的共享服务。</param>
    /// <returns>绑定该共享服务的 SQLite SQL Builder。</returns>
    private static ISqlBuilder CreateBuilder(SqlBuilderServices services) => new SqliteBuilder(services);
}
