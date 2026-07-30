using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 渲染 Provider 专用批量 Update 命令。
/// </summary>
public interface ISqlBatchUpdateRenderer
{
    /// <summary>
    /// 适用的 Provider 唯一标识。
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// 判断当前结构化上下文是否适合由该渲染器生成优化批量命令。
    /// </summary>
    /// <param name="context">批量 Update 的结构化上下文。</param>
    /// <returns>可安全生成优化命令时返回 <c>true</c>。</returns>
    bool CanRender(SqlBatchUpdateRenderContext context);

    /// <summary>
    /// 将结构化批量 Update 上下文渲染为可执行命令。
    /// </summary>
    /// <param name="context">批量 Update 的结构化上下文。</param>
    /// <returns>可执行 SQL 命令快照。</returns>
    SqlMutationCommand Render(SqlBatchUpdateRenderContext context);
}