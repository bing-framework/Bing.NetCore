namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 批量 Mutation 的 SQL 生成策略。
/// </summary>
public enum SqlBatchStrategy
{
    /// <summary>
    /// 对 Insert 按 Provider 能力自动选择组合或逐实体策略；Update 和 Delete 使用逐实体策略。
    /// </summary>
    Auto,

    /// <summary>
    /// 生成多行 Values 或 IN 条件的合并命令。
    /// </summary>
    Combined,

    /// <summary>
    /// 为每个实体生成独立命令。
    /// </summary>
    PerEntity
}