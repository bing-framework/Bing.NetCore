namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 跨数据库查询校验器。
/// </summary>
public interface ISqlCrossDatabaseQueryValidator
{
	/// <summary>
	/// 验证跨数据库查询。
	/// </summary>
	/// <param name="executionContext">执行数据库上下文。</param>
	/// <param name="source">源表引用。</param>
	/// <param name="target">目标表引用。</param>
	void Validate(DatabaseContext executionContext, SqlTableReference source, SqlTableReference target);

	/// <summary>
	/// 验证原始字符串 From 的结构化 Join 目标。
	/// </summary>
	/// <param name="executionContext">执行数据库上下文。</param>
	/// <param name="target">目标表引用。</param>
	void ValidateTarget(DatabaseContext executionContext, SqlTableReference target);
}
