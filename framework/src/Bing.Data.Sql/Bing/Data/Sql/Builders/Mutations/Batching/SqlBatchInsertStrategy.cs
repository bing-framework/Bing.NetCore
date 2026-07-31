namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 批量 Insert 的 SQL 生成策略。
/// </summary>
public enum SqlBatchInsertStrategy
{
    /// <summary>根据 Provider 能力自动选择组合或逐实体策略。</summary>
    Auto = 0,
    /// <summary>生成多行 Values 合并命令。</summary>
    MultiRowValues = 1,
    /// <summary>为每个实体生成独立命令。</summary>
    PerEntity = 3
}