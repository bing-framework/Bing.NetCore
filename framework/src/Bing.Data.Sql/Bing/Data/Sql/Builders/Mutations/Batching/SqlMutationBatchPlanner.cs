namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 默认 Mutation 批次规划器。
/// </summary>
public sealed class SqlMutationBatchPlanner
{
    /// <summary>
    /// 根据用户批量大小、Provider 参数上限和 SQL 长度上限计算批次计划。
    /// </summary>
    /// <param name="context">包含实体数量、每实体开销和批量选项的规划上下文。</param>
    /// <returns>按三类容量中最小值分片的批次计划；实体数量为零时返回空计划。</returns>
    public SqlMutationBatchPlan Plan(SqlMutationBatchPlanContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (context.EntityCount == 0)
            return new SqlMutationBatchPlan(0, 0, Array.Empty<int>());
        var parameterCapacity = GetParameterCapacity(context);
        var sqlLengthCapacity = GetSqlLengthCapacity(context);
        var userCapacity = context.Options.BatchSize ?? int.MaxValue;
        if (userCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(context), "批量大小必须大于零。");
        var batchSize = Math.Min(userCapacity, Math.Min(parameterCapacity, sqlLengthCapacity));
        if (batchSize <= 0)
            throw new InvalidOperationException("当前 Provider 参数或 SQL 长度上限无法容纳一个 Mutation 实体。");
        batchSize = Math.Min(batchSize, context.EntityCount);
        var batchCount = context.EntityCount / batchSize;
        if (context.EntityCount % batchSize != 0)
            batchCount++;
        return new SqlMutationBatchPlan(context.EntityCount, batchSize,
            new SqlMutationBatchSizeList(context.EntityCount, batchSize, batchCount));
    }

    /// <summary>
    /// 计算参数上限允许的实体数量。
    /// </summary>
    /// <param name="context">批次规划上下文。</param>
    /// <returns>参数容量允许的最大实体数；未配置参数上限时返回无界容量。</returns>
    private static int GetParameterCapacity(SqlMutationBatchPlanContext context)
    {
        if (context.MaxParameterCount == null)
            return int.MaxValue;
        return (context.MaxParameterCount.Value - context.ExistingParameterCount) / context.ParametersPerEntity;
    }

    /// <summary>
    /// 计算 SQL 长度上限允许的实体数量。
    /// </summary>
    /// <param name="context">批次规划上下文。</param>
    /// <returns>SQL 长度容量允许的最大实体数；未配置限制时返回无界容量。</returns>
    private static int GetSqlLengthCapacity(SqlMutationBatchPlanContext context)
    {
        if (context.MaxSqlLength == null || context.EstimatedSqlLengthPerEntity == 0)
            return int.MaxValue;
        return context.MaxSqlLength.Value / context.EstimatedSqlLengthPerEntity;
    }

    /// <summary>
    /// 按需计算每批实体数量，避免大规模小批次在规划阶段分配等同于实体数的内存。
    /// </summary>
    private sealed class SqlMutationBatchSizeList : IReadOnlyList<int>
    {
        /// <summary>
        /// 所有待处理实体数量。
        /// </summary>
        private readonly int _entityCount;

        /// <summary>
        /// 除最后一批外的固定批次大小。
        /// </summary>
        private readonly int _batchSize;

        /// <summary>
        /// 初始化一个 <see cref="SqlMutationBatchSizeList"/> 类型的实例。
        /// </summary>
        /// <param name="entityCount">待处理实体数量。</param>
        /// <param name="batchSize">除最后一批外的固定批次大小；最后一批可小于此值。</param>
        /// <param name="count">批次数量。</param>
        public SqlMutationBatchSizeList(int entityCount, int batchSize, int count)
        {
            _entityCount = entityCount;
            _batchSize = batchSize;
            Count = count;
        }

        /// <inheritdoc />
        public int Count { get; }

        /// <inheritdoc />
        public int this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                if (index < Count - 1)
                    return _batchSize;
                var remainder = _entityCount % _batchSize;
                return remainder == 0 ? _batchSize : remainder;
            }
        }

        /// <inheritdoc />
        public IEnumerator<int> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
                yield return this[index];
        }

        /// <inheritdoc />
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}