using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 支持将同构实体删除合并为单条命令的 Mutation Builder。
/// </summary>
/// <remarks>
/// 该契约为可选能力。实现必须保持主键与并发条件按实体配对，禁止生成会交叉匹配的独立条件集合。
/// </remarks>
public interface ISqlCombinedDeleteMutationBuilder
{
    /// <summary>
    /// 生成删除多个实体的单条 SQL 命令。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待删除且不能为空的实体集合。</param>
    /// <param name="options">删除选项。</param>
    /// <param name="strategy">组合条件策略。</param>
    /// <returns>可执行的批量 Delete SQL 命令快照。</returns>
    SqlWriteCommand DeleteCombined<TEntity>(IReadOnlyCollection<TEntity> entities,
        SqlDeleteOptions options = null, SqlBatchDeleteStrategy strategy = SqlBatchDeleteStrategy.Auto) where TEntity : class;
}