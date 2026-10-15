using System.Data.Common;
using Bing.Data.Inventory;
using FreeSql;

namespace Bing.FreeSQL.Inventory;

/// <summary>
/// 提供 FreeSQL 工作单元中的库存扣减适配。
/// </summary>
public static class FreeSqlInventoryExtensions
{
    /// <summary>
    /// 在当前 FreeSQL 事务内执行库存扣减。
    /// </summary>
    /// <param name="unitOfWork">当前 FreeSQL 工作单元。</param>
    /// <param name="store">库存存储。</param>
    /// <param name="request">库存请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>库存扣减结果。</returns>
    /// <remarks>方法不会创建、提交或释放事务，调用方必须先开启事务。</remarks>
    public static Task<InventoryDeductionResult> TryDeductInventoryAsync(this IUnitOfWork unitOfWork,
        IInventoryStore store, InventoryDeductionRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = GetTransaction(unitOfWork);
        return store?.TryDeductAsync(transaction, request, cancellationToken) ??
               throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// 在当前 FreeSQL 事务内按稳定顺序扣减多项库存。
    /// </summary>
    public static Task<IReadOnlyList<InventoryDeductionResult>> DeductInventoryBatchAsync(
        this IUnitOfWork unitOfWork, IInventoryStore store, IEnumerable<InventoryDeductionRequest> requests,
        CancellationToken cancellationToken = default) =>
        InventoryBatchOperations.DeductAsync(GetTransaction(unitOfWork), store, requests, cancellationToken);

    /// <summary>
    /// 获取当前工作单元已创建的数据库事务。
    /// </summary>
    private static DbTransaction GetTransaction(IUnitOfWork unitOfWork)
    {
        if (unitOfWork == null)
            throw new ArgumentNullException(nameof(unitOfWork));
        return unitOfWork.GetOrBeginTransaction(false) ??
               throw new InvalidOperationException("FreeSQL 当前没有可复用的数据库事务。");
    }
}
