using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 批量 Insert 选项。
/// </summary>
public sealed class SqlBatchInsertOptions : SqlMutationBatchOptions
{
    /// <summary>批量 Insert 的 SQL 生成策略。</summary>
    public SqlBatchInsertStrategy Strategy { get; set; } = SqlBatchInsertStrategy.Auto;

    /// <summary>应用于每个实体的 Insert 列筛选选项。</summary>
    public SqlInsertOptions InsertOptions { get; set; }
}