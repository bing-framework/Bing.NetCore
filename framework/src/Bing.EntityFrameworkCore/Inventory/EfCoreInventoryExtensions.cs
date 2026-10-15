using Bing.Data.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bing.EntityFrameworkCore.Inventory;

/// <summary>
/// 提供 EFCore 工作单元中的库存扣减适配。
/// </summary>
public static class EfCoreInventoryExtensions
{
    /// <summary>
    /// 在当前 EFCore 事务内执行库存扣减。
    /// </summary>
    /// <param name="context">当前数据库上下文。</param>
    /// <param name="store">库存存储。</param>
    /// <param name="request">库存请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>库存扣减结果。</returns>
    /// <remarks>方法不会创建、提交或释放事务，调用方必须先开启事务。</remarks>
    public static Task<InventoryDeductionResult> TryDeductInventoryAsync(this DbContext context,
        IInventoryStore store, InventoryDeductionRequest request, CancellationToken cancellationToken = default)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        var transaction = context.Database.CurrentTransaction?.GetDbTransaction() ??
            throw new InvalidOperationException("EFCore 当前没有可复用的数据库事务。");
        return store?.TryDeductAsync(transaction, request, cancellationToken) ??
               throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// 在当前 EFCore 事务内按稳定顺序扣减多项库存。
    /// </summary>
    public static Task<IReadOnlyList<InventoryDeductionResult>> DeductInventoryBatchAsync(this DbContext context,
        IInventoryStore store, IEnumerable<InventoryDeductionRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        var transaction = context.Database.CurrentTransaction?.GetDbTransaction() ??
            throw new InvalidOperationException("EFCore 当前没有可复用的数据库事务。");
        return InventoryBatchOperations.DeductAsync(transaction, store, requests, cancellationToken);
    }
}
