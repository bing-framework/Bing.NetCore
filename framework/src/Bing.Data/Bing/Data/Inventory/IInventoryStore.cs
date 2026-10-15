using System.Data.Common;

namespace Bing.Data.Inventory;

/// <summary>
/// 在调用方数据库事务内执行库存扣减。
/// </summary>
/// <remarks>库存、请求结果和后续业务写入必须使用同一个事务，由调用方负责提交或回滚。</remarks>
public interface IInventoryStore
{
    /// <summary>
    /// 尝试执行一次幂等库存扣减。
    /// </summary>
    /// <param name="transaction">与业务写入共享的数据库事务。</param>
    /// <param name="request">库存扣减请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>扣减结果；重复请求返回第一次已提交的结果。</returns>
    Task<InventoryDeductionResult> TryDeductAsync(DbTransaction transaction,
        InventoryDeductionRequest request, CancellationToken cancellationToken = default);
}
