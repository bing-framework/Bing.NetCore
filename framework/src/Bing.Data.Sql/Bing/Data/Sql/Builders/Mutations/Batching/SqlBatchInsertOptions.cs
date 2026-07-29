using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 批量 Insert 选项。
/// </summary>
public sealed class SqlBatchInsertOptions : SqlMutationBatchOptions
{
	/// <summary>
	/// 应用于每个实体的 Insert 列筛选选项。
	/// </summary>
	public SqlInsertOptions InsertOptions { get; set; }
}