using System.Text.Json.Serialization.Metadata;
﻿using Bing.Dapper;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Bing.Dapper.PostgreSql;

/// <summary>
/// PostgreSql 服务集合扩展
/// </summary>
public static class PostgreSqlServiceCollectionExtensions
{
    /// <summary>
    /// 注册 PostgreSQL Provider 能力，不配置默认数据源。
    /// </summary>
    /// <remarks>
    /// 多 Provider 容器应通过具名数据源完成运行时路由。
    /// </remarks>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddPostgreSqlProvider(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddSqlCore();
        services.AddSqlBuilderProvider(PostgreSqlSqlProvider.Instance, CreateBuilder);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlBatchUpdateRenderer, PostgreSqlBatchUpdateRenderer>());
        var queryOptions = new SqlOptions<PostgreSqlQuery> { DatabaseType = DatabaseType.PgSql };
        var executorOptions = new SqlOptions<PostgreSqlExecutor> { DatabaseType = DatabaseType.PgSql };
        var multipleQueryOptions = new SqlOptions<PostgreSqlMultipleQueryExecutor> { DatabaseType = DatabaseType.PgSql };
        queryOptions.RegisterStringTypeHandler();
        executorOptions.RegisterStringTypeHandler();
        multipleQueryOptions.RegisterStringTypeHandler();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, PostgreSqlDbParameterCustomizer>());
        services.AddSqlDbConnectionFactory(PostgreSqlSqlProvider.Instance.Key, CreateConnection);
        // 静态创建保留转换器构造函数，并保持调用方预注册实例的优先级。
        services.TryAddSingleton(new PostgreSqlTypeConverter());
        services.AddSingleton<DatabaseTypeConverterRegistration>(provider => new DatabaseTypeConverterRegistration
        {
            DatabaseType = DatabaseType.PgSql,
            Converter = provider.GetRequiredService<PostgreSqlTypeConverter>()
        });
        services.AddSqlProviderRuntime(SqlProviderRuntime.CreateWithFactories(PostgreSqlSqlProvider.Instance.Key,
            typeof(PostgreSqlQuery), typeof(PostgreSqlExecutor), typeof(PostgreSqlMultipleQueryExecutor),
            (provider, options) => new PostgreSqlQuery(provider, (SqlOptions<PostgreSqlQuery>)options),
            (provider, options) => new PostgreSqlExecutor(provider, (SqlOptions<PostgreSqlExecutor>)options),
            (provider, options) => new PostgreSqlMultipleQueryExecutor(provider, (SqlOptions<PostgreSqlMultipleQueryExecutor>)options),
            serviceType => serviceType == typeof(ISqlQuery) ? new SqlOptions<PostgreSqlQuery>() :
                serviceType == typeof(ISqlExecutor) ? new SqlOptions<PostgreSqlExecutor>() : new SqlOptions<PostgreSqlMultipleQueryExecutor>(),
            (provider, serviceType) => serviceType == typeof(ISqlQuery) ? provider.GetService<SqlOptions<PostgreSqlQuery>>() :
                serviceType == typeof(ISqlExecutor) ? provider.GetService<SqlOptions<PostgreSqlExecutor>>() : provider.GetService<SqlOptions<PostgreSqlMultipleQueryExecutor>>()));
        services.TryAddSingleton(queryOptions);
        services.TryAddSingleton(executorOptions);
        services.TryAddSingleton(multipleQueryOptions);
        return services;
    }

    /// <summary>
    /// 为映射到 PostgreSQL json 或 jsonb 的指定 CLR 类型注册源生成 JSON 转换。
    /// </summary>
    /// <remarks>字符串映射继续读写原始 JSON 文本。可为不同 CLR 类型分别注册。</remarks>
    /// <typeparam name="T">JSON 列的 CLR 类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <param name="typeInfo">由应用的 JsonSerializerContext 生成的类型元数据。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddPostgreSqlJson<T>(this IServiceCollection services, JsonTypeInfo<T> typeInfo)
    {
        if (typeInfo == null)
            throw new ArgumentNullException(nameof(typeInfo));
        if (typeof(T) == typeof(string))
            return services;

        services.AddSqlCore();
        var descriptor = ServiceDescriptor.Singleton<IFieldValueConverter>(new PostgreSqlJsonFieldValueConverter<T>(typeInfo));
        // 保持应用已有转换器的顺序，将 JSON 转换插在内置默认转换之前。
        for (var i = 0; i < services.Count; i++)
        {
            if (services[i].ServiceType != typeof(IFieldValueConverter) ||
                services[i].ImplementationType != typeof(DefaultFieldValueConverter))
                continue;
            services.Insert(i, descriptor);
            return services;
        }
        services.Add(descriptor);
        return services;
    }

    /// <summary>
    /// 根据数据源连接字符串创建 PostgreSQL 独立连接。
    /// </summary>
    /// <param name="connectionString">PostgreSQL 数据源连接字符串。</param>
    /// <returns>尚未打开的 PostgreSQL 数据库连接。</returns>
    private static NpgsqlConnection CreateConnection(string connectionString) => new(connectionString);

    /// <summary>
    /// 使用查询级共享服务创建 PostgreSQL SQL Builder。
    /// </summary>
    /// <param name="services">当前查询的共享服务。</param>
    /// <returns>绑定该共享服务的 PostgreSQL SQL Builder。</returns>
    private static ISqlBuilder CreateBuilder(SqlBuilderServices services) => new PostgreSqlBuilder(services);
}
