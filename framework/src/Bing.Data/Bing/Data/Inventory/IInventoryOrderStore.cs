using System.Data.Common;

namespace Bing.Data.Inventory;

/// <summary>
/// 在业务事务中登记整单身份、执行库存扣减并保存首次结果。
/// </summary>
public interface IInventoryOrderStore
{
    /// <summary>
    /// 查询整单的已提交结果。
    /// </summary>
    /// <param name="connection">调用方创建并打开的独立查询连接。</param>
    /// <param name="request">需要核对内容的原整单请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已提交的首次结果；查询尚未看到结果时返回 null。</returns>
    /// <remarks>不登记请求、不扣减库存。null 不能证明旧操作已经回滚；Unknown 恢复需同时确认原操作已经结束。</remarks>
    Task<InventoryOrderResult> FindAsync(DbConnection connection, InventoryOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行整单库存请求。
    /// </summary>
    /// <param name="transaction">调用方拥有的活动业务事务。</param>
    /// <param name="request">整单请求快照。</param>
    /// <param name="onCompleted">首次完成时在同一事务内写入关键审计及出箱的回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>首次结果或完全相同请求的既有结果。</returns>
    /// <remarks>
    /// 不提交或释放调用方事务。余额不足会回滚本次整单的库存保存点并保存不足结果；
    /// 异常或取消必须由调用方回滚整笔事务并丢弃工作单元。回调只执行一次，不能执行非事务外部副作用。
    /// </remarks>
    Task<InventoryOrderResult> ExecuteAsync(DbTransaction transaction, InventoryOrderRequest request,
        Func<DbTransaction, InventoryOrderResult, CancellationToken, Task> onCompleted,
        CancellationToken cancellationToken = default);
}
