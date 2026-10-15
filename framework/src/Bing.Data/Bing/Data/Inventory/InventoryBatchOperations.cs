using System.Collections.Generic;
using System.Data.Common;

namespace Bing.Data.Inventory;

/// <summary>
/// 提供多 SKU 库存扣减的稳定排序入口。
/// </summary>
public static class InventoryBatchOperations
{
    /// <summary>
    /// 按固定键顺序在同一事务内扣减多项库存。
    /// </summary>
    /// <param name="transaction">与业务写入共享的数据库事务。</param>
    /// <param name="store">库存存储。</param>
    /// <param name="requests">库存请求集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按固定排序后的扣减结果。</returns>
    /// <remarks>
    /// 请求按租户、仓库、SKU 使用 Ordinal 顺序执行；任一项余额不足或处理中时抛出异常，调用方应回滚整个事务。
    /// </remarks>
    public static async Task<IReadOnlyList<InventoryDeductionResult>> DeductAsync(
        DbTransaction transaction, IInventoryStore store, IEnumerable<InventoryDeductionRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (store == null)
            throw new ArgumentNullException(nameof(store));
        if (requests == null)
            throw new ArgumentNullException(nameof(requests));

        var ordered = requests.OrderBy(request => request?.TenantId, StringComparer.Ordinal)
            .ThenBy(request => request?.WarehouseId, StringComparer.Ordinal)
            .ThenBy(request => request?.Sku, StringComparer.Ordinal)
            .ToArray();
        var requestIds = new HashSet<(string TenantId, string RequestId)>(RequestKeyComparer);
        var results = new List<InventoryDeductionResult>(ordered.Length);
        foreach (var request in ordered)
        {
            if (request == null)
                throw new ArgumentException("库存请求集合不能包含空值。", nameof(requests));
            var requestKey = (request.TenantId, request.RequestId);
            if (!requestIds.Add(requestKey))
                throw new InvalidOperationException($"库存请求集合包含同一租户的重复请求标识：{request.RequestId}。");

            cancellationToken.ThrowIfCancellationRequested();
            var result = await store.TryDeductAsync(transaction, request, cancellationToken);
            results.Add(result);
            if (!result.Applied)
                throw new InvalidOperationException(
                    $"库存扣减未完成，状态为 {result.Status}，租户 {request.TenantId}、仓库 {request.WarehouseId}、SKU {request.Sku}。");
        }
        return results;
    }

    /// <summary>
    /// 按 Ordinal 规则比较批次请求的租户和请求标识。
    /// </summary>
    private static readonly OrdinalRequestKeyComparer RequestKeyComparer = new();

    /// <summary>
    /// 提供库存批次二元请求键的 Ordinal 比较器。
    /// </summary>
    private sealed class OrdinalRequestKeyComparer : IEqualityComparer<(string TenantId, string RequestId)>
    {
        /// <summary>
        /// 初始化一个 <see cref="OrdinalRequestKeyComparer" /> 类型的实例。
        /// </summary>
        internal OrdinalRequestKeyComparer()
        {
        }

        /// <summary>
        /// 比较两个库存请求键。
        /// </summary>
        /// <param name="x">第一个库存请求键。</param>
        /// <param name="y">第二个库存请求键。</param>
        /// <returns>两个字段均按 Ordinal 规则相等时返回 true。</returns>
        public bool Equals((string TenantId, string RequestId) x, (string TenantId, string RequestId) y) =>
            StringComparer.Ordinal.Equals(x.TenantId, y.TenantId) &&
            StringComparer.Ordinal.Equals(x.RequestId, y.RequestId);

        /// <summary>
        /// 计算库存请求键的哈希值。
        /// </summary>
        /// <param name="obj">需要计算哈希值的库存请求键。</param>
        /// <returns>基于租户和请求标识的哈希值。</returns>
        public int GetHashCode((string TenantId, string RequestId) obj)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(obj.TenantId);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(obj.RequestId);
                return hash;
            }
        }
    }
}
