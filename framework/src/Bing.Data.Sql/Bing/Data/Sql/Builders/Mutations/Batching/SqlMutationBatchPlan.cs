namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// Mutation 分片计算结果。
/// </summary>
public sealed class SqlMutationBatchPlan
{
    /// <summary>
    /// 初始化一个 <see cref="SqlMutationBatchPlan"/> 类型的实例。
    /// </summary>
    /// <param name="entityCount">待处理实体数量。</param>
    /// <param name="effectiveBatchSize">实际每批最大实体数量。</param>
    /// <param name="batchSizes">按顺序保存的每批实体数量。</param>
    public SqlMutationBatchPlan(int entityCount, int effectiveBatchSize, IReadOnlyList<int> batchSizes)
    {
        EntityCount = entityCount;
        EffectiveBatchSize = effectiveBatchSize;
        BatchSizes = batchSizes ?? throw new ArgumentNullException(nameof(batchSizes));
    }

    /// <summary>
    /// 待处理实体数量。
    /// </summary>
    public int EntityCount { get; }

    /// <summary>
    /// 实际每批最大实体数量。
    /// </summary>
    public int EffectiveBatchSize { get; }

    /// <summary>
    /// 按顺序保存的每批实体数量。
    /// </summary>
    public IReadOnlyList<int> BatchSizes { get; }
}