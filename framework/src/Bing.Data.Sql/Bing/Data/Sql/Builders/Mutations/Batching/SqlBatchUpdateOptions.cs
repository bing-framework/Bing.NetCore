using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 批量 Update 选项。
/// </summary>
public sealed class SqlBatchUpdateOptions : SqlMutationBatchOptions
{
	/// <summary>
	/// 应用于每个实体的 Update 列筛选和并发选项。
	/// </summary>
	public SqlUpdateOptions UpdateOptions { get; set; }
}