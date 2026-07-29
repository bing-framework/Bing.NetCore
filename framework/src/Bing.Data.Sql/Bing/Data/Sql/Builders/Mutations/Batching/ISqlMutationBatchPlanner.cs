namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 规划 Mutation 参数和 SQL 长度分片。
/// </summary>
public interface ISqlMutationBatchPlanner
{
    /// <summary>
    /// 根据 Provider 限制和用户选项创建批次计划。
    /// </summary>
    /// <param name="context">批次规划输入。</param>
    /// <returns>可直接用于分组执行的批次计划。</returns>
    SqlMutationBatchPlan Plan(SqlMutationBatchPlanContext context);
}