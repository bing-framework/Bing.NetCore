using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Data.Inventory;

/// <summary>
/// 注册库存一致性基础设施。
/// </summary>
public static class InventoryServiceCollectionExtensions
{
    /// <summary>
    /// 注册数据库库存存储。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="database">数据库语法。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>事务仍由调用方创建；注册不会隐式创建数据库连接或作用域。</remarks>
    public static IServiceCollection AddBingInventoryStore(this IServiceCollection services,
        InventoryDatabase database)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddSingleton<IInventoryStore>(new DbInventoryStore(database));
        return services;
    }

    /// <summary>
    /// 注册整单库存存储。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="database">数据库语法。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>默认使用当前作用域的库存存储，保留调用方已有注册；业务事务与关键审计、出箱回调由宿主提供。</remarks>
    public static IServiceCollection AddBingInventoryOrders(this IServiceCollection services,
        InventoryDatabase database)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (database != InventoryDatabase.MySql && database != InventoryDatabase.Sqlite)
            throw new ArgumentOutOfRangeException(nameof(database));
        services.AddBingInventoryStore(database);
        services.TryAddScoped<IInventoryOrderStore>(provider =>
            new DbInventoryOrderStore(database, provider.GetRequiredService<IInventoryStore>()));
        services.TryAddSingleton<IInventoryOrderProjectionStore>(
            new DbInventoryOrderProjectionStore(database));
        return services;
    }
}
