using Bing.Data.KeyGeneration;

namespace Bing.Data.Sql;

/// <summary>
/// 可选的稳定关联键写入能力。
/// </summary>
/// <remarks>已有 ISqlExecutor.Insert 返回类型不变。</remarks>
public interface ISqlKeyInsertExecutor
{
    /// <summary>
    /// 写入一个有界批次，Identity 按输入项逐条取键。
    /// </summary>
    /// <remarks>结果注明是否已最终提交。</remarks>
    /// <param name="rows">待写入并按输入关联返回主键的有界批次。</param>
    /// <param name="requireNativeBulk">为 true 时要求原生批量写入能力，否则允许逐条写入。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <typeparam name="TEntity">待写入的实体类型。</typeparam>
    /// <typeparam name="TKey">实体主键类型。</typeparam>
    /// <returns>按输入项关联的主键及提交状态。</returns>
    Task<InsertBatchResult<TKey>> InsertWithKeysAsync<TEntity, TKey>(IReadOnlyCollection<InsertRow<TEntity>> rows,
        bool requireNativeBulk = false, CancellationToken cancellationToken = default) where TEntity : class;
}
