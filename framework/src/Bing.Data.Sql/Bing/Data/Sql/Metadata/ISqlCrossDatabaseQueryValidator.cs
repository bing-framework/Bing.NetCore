namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 跨数据库查询校验器。
/// </summary>
public interface ISqlCrossDatabaseQueryValidator
{
	/// <summary>
	/// 验证源表与目标表能否在当前执行上下文中连接。
	/// </summary>
	/// <param name="source">源表引用。</param>
	/// <param name="target">目标表引用。</param>
	/// <param name="executionContext">执行数据库上下文。</param>
	void Validate(SqlTableReference source, SqlTableReference target, DatabaseContext executionContext);
}
