using Bing.Data.Enums;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Events.Cap;

/// <summary>
/// 配置按数据源隔离的 CAP 运行时。
/// </summary>
public sealed class CapUnitOfWorkBuilder
{
    private readonly CapUnitOfWorkRegistrationRegistry _registry;

    internal CapUnitOfWorkBuilder(CapUnitOfWorkRegistrationRegistry registry) =>
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    /// <summary>
    /// 添加一个数据源专属的 CAP runtime。
    /// </summary>
    /// <param name="dataSourceKey">与 SQL Uow 数据源一致的键。</param>
    /// <param name="databaseType">该 SQL 数据源的数据库类型。</param>
    /// <param name="storageConnectionString">CAP Outbox 应使用的数据库连接字符串。</param>
    /// <param name="configureCap">使用给定的 Outbox 连接字符串配置 CAP 存储和传输。</param>
    /// <param name="configureServices">可选的 runtime 专属服务注册，包括该 runtime 的消息处理器。</param>
    /// <param name="publisherOnly">仅运行发布端时设为 true；消费者由其他进程启动。</param>
    /// <returns>当前 builder。</returns>
    public CapUnitOfWorkBuilder AddDataSource(string dataSourceKey, DatabaseType databaseType,
        string storageConnectionString, Action<CapOptions, string> configureCap,
        Action<IServiceCollection> configureServices = null, bool publisherOnly = false)
    {
        _registry.Add(new CapUnitOfWorkDataSourceRegistration(dataSourceKey, databaseType,
            storageConnectionString, configureCap, configureServices, publisherOnly));
        return this;
    }
}
