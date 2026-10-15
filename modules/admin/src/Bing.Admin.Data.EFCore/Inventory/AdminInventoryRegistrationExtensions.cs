using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Auditing.Persistence;
using Bing.Data.Inventory;
using Bing.Data.Transaction;
using Bing.Events.Messages;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Admin.Data.Inventory;

/// <summary>
/// 注册 EFCore Admin 的库存事务业务。
/// </summary>
public static class AdminInventoryRegistrationExtensions
{
    /// <summary>
    /// 注册整单库存业务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>库存及审计表由部署迁移显式创建，不在注册时操作数据库。</remarks>
    public static IServiceCollection AddAdminInventory(this IServiceCollection services)
    {
        services.AddBingInventoryOrders(InventoryDatabase.MySql);
        services.TryAddSingleton<ITransactionalAuditingStore>(new DbAuditingStore(AuditLogDatabase.MySql));
        services.TryAddScoped<AdminInventoryService>(provider => new AdminInventoryService(
            provider.GetRequiredService<AdminUnitOfWork>(), provider.GetRequiredService<ITransactionActionManager>(),
            () => provider.GetRequiredService<AdminUnitOfWork>().Database.CurrentTransaction?.GetDbTransaction(),
            provider.GetRequiredService<IInventoryOrderStore>(), provider.GetRequiredService<ITransactionalAuditingStore>(),
            provider.GetRequiredService<IMessageEventBus>(), provider.GetService<Bing.Events.Ordering.OrderedEventPublisher>()));
        services.TryAddSingleton<AdminInventoryOrderExecutor>();
        return services;
    }
}
