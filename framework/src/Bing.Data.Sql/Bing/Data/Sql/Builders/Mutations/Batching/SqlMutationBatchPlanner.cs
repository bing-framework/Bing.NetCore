namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 默认 Mutation 批次规划器。
/// </summary>
public sealed class SqlMutationBatchPlanner : ISqlMutationBatchPlanner
{
    /// <inheritdoc />
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
        var sizes = new List<int>(batchCount);
        var remaining = context.EntityCount;
        while (remaining > 0)
        {
            var current = Math.Min(batchSize, remaining);
            sizes.Add(current);
            remaining -= current;
        }
        return new SqlMutationBatchPlan(context.EntityCount, batchSize, sizes);
    }

    /// <summary>
    /// 计算参数上限允许的实体数量。
    /// </summary>
    private static int GetParameterCapacity(SqlMutationBatchPlanContext context)
    {
        if (context.MaxParameterCount == null)
            return int.MaxValue;
        return (context.MaxParameterCount.Value - context.ExistingParameterCount) / context.ParametersPerEntity;
    }

    /// <summary>
    /// 计算 SQL 长度上限允许的实体数量。
    /// </summary>
    private static int GetSqlLengthCapacity(SqlMutationBatchPlanContext context)
    {
        if (context.MaxSqlLength == null || context.EstimatedSqlLengthPerEntity == 0)
            return int.MaxValue;
        return context.MaxSqlLength.Value / context.EstimatedSqlLengthPerEntity;
    }
}