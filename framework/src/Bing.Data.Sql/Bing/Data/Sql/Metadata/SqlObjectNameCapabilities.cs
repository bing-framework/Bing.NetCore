namespace Bing.Data.Sql.Metadata;

/// <summary>
/// SQL 对象名称能力。
/// </summary>
public sealed record SqlObjectNameCapabilities
{
	/// <summary>
	/// 是否支持数据库限定。
	/// </summary>
	public bool SupportsDatabase { get; init; }

	/// <summary>
	/// 是否支持架构限定。
	/// </summary>
	public bool SupportsSchema { get; init; }

	/// <summary>
	/// 最大对象名称段数。
	/// </summary>
	public int MaximumNameParts { get; init; }
}
