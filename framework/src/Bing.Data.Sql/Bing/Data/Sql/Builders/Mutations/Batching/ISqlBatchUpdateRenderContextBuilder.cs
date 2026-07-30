using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 为 Provider 批量 Update 渲染器创建结构化上下文。
/// </summary>
public interface ISqlBatchUpdateRenderContextBuilder
{
    /// <summary>
    /// 创建批量 Update 渲染上下文。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待更新实体集合。</param>
    /// <param name="options">更新列和并发选项。</param>
    /// <returns>包含映射和实体值的结构化上下文。</returns>
    SqlBatchUpdateRenderContext CreateUpdateRenderContext<TEntity>(IReadOnlyCollection<TEntity> entities,
        SqlUpdateOptions options = null) where TEntity : class;
}