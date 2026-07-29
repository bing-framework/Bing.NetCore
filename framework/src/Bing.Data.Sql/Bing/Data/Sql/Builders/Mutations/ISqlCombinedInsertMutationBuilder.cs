using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 支持将同构实体插入合并为多行 Values 命令的 Mutation Builder。
/// </summary>
/// <remarks>
/// 该契约为可选能力。仅当 Provider 支持标准多行 Values 语法时，批量执行器才会使用此能力。
/// </remarks>
public interface ISqlCombinedInsertMutationBuilder
{
    /// <summary>
    /// 生成插入多个实体的单条多行 Values SQL 命令。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待插入且不能为空的实体集合。</param>
    /// <param name="options">插入选项。</param>
    /// <returns>可执行的多行 Insert SQL 命令快照。</returns>
    SqlMutationCommand InsertCombined<TEntity>(IReadOnlyCollection<TEntity> entities,
        SqlInsertOptions options = null) where TEntity : class;
}