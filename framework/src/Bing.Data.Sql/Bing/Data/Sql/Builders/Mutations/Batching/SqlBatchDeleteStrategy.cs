namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 批量 Delete 的 SQL 生成策略。
/// </summary>
public enum SqlBatchDeleteStrategy
{
    /// <summary>优先使用 Provider 支持的合并删除命令。</summary>
    Auto,
    /// <summary>生成单主键 IN 条件批量命令。</summary>
    InPredicate,
    /// <summary>生成复合主键或并发列的配对条件批量命令。</summary>
    CompositePredicate,
    /// <summary>使用 Provider 专用批量 Delete 命令。</summary>
    ProviderOptimized,
    /// <summary>为每个实体生成独立命令。</summary>
    PerEntity
}