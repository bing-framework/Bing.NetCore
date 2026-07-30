namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 批量 Insert 的 SQL 生成策略。
/// </summary>
public enum SqlBatchInsertStrategy
{
    /// <summary>根据 Provider 能力自动选择组合或逐实体策略。</summary>
    Auto,
    /// <summary>生成多行 Values 合并命令。</summary>
    MultiRowValues,
    /// <summary>使用 Provider 专用批量 Insert 命令。</summary>
    ProviderOptimized,
    /// <summary>为每个实体生成独立命令。</summary>
    PerEntity
}