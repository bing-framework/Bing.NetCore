using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 批量 Delete 选项。
/// </summary>
public sealed class SqlBatchDeleteOptions : SqlMutationBatchOptions
{
	/// <summary>
	/// 应用于每个实体的 Delete 安全和并发选项。
	/// </summary>
	public SqlDeleteOptions DeleteOptions { get; set; }
}