namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 批量 Update 的 SQL 生成策略。
/// </summary>
public enum SqlBatchUpdateStrategy
{
    /// <summary>使用逐实体命令，直到 Provider 注册优化渲染器。</summary>
    Auto,
    /// <summary>为每个实体生成独立命令。</summary>
    PerEntity,
    /// <summary>要求使用 Provider 注册的优化批量 Update 渲染器。</summary>
    ProviderOptimized
}