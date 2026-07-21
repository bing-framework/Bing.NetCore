using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认跨数据库查询校验器。
/// </summary>
public sealed class DefaultSqlCrossDatabaseQueryValidator : ISqlCrossDatabaseQueryValidator
{
	/// <summary>
	/// SQL 对象名称能力提供器。
	/// </summary>
	private readonly ISqlObjectNameCapabilityProvider _capabilityProvider;

	/// <summary>
	/// 初始化一个<see cref="DefaultSqlCrossDatabaseQueryValidator"/>类型的实例。
	/// </summary>
	/// <param name="capabilityProvider">SQL 对象名称能力提供器。</param>
	public DefaultSqlCrossDatabaseQueryValidator(ISqlObjectNameCapabilityProvider capabilityProvider = null)
	{
		_capabilityProvider = capabilityProvider ?? new DefaultSqlObjectNameCapabilityProvider();
	}

	/// <inheritdoc />
	public void Validate(DatabaseContext executionContext, SqlTableReference source, SqlTableReference target)
	{
		if (target == null)
			throw new ArgumentNullException(nameof(target));
		if (source == null)
		{
			ValidateTarget(executionContext, target);
			return;
		}
		ValidateCrossDatabase(executionContext, source, target);
	}

	/// <summary>
	/// 验证原始字符串 From 的结构化 Join 目标。
	/// </summary>
	/// <param name="executionContext">执行数据库上下文。</param>
	/// <param name="target">目标表引用。</param>
	public void ValidateTarget(DatabaseContext executionContext, SqlTableReference target)
	{
		if (target == null)
			throw new ArgumentNullException(nameof(target));
		var databaseType = ResolveDatabaseType(executionContext);
		if (databaseType == null)
			return;
		if (databaseType == DatabaseType.PgSql && HasValue(target.Database))
			throw new NotSupportedException("PostgreSQL 不支持普通跨 Database 查询。");
		if (databaseType == DatabaseType.Oracle && HasValue(target.Database))
			throw new NotSupportedException("Oracle 不支持普通跨 Database 查询。");
	}

	/// <summary>
	/// 验证同一连接中两个结构化表的关系。
	/// </summary>
	private void ValidateCrossDatabase(DatabaseContext executionContext, SqlTableReference source,
		SqlTableReference target)
	{
		var databaseType = ResolveDatabaseType(executionContext);
		if (databaseType == null)
			return;
		if (databaseType == DatabaseType.PgSql &&
			(HasValue(source.Database) || HasValue(target.Database)))
			throw new NotSupportedException("PostgreSQL 不支持普通跨 Database 查询。");
		if (databaseType == DatabaseType.Oracle &&
			(HasValue(source.Database) || HasValue(target.Database)))
			throw new NotSupportedException("Oracle 不支持普通跨 Database 查询。");
		if (databaseType == DatabaseType.Sqlite &&
			(HasValue(source.Database) || HasValue(target.Database) || HasValue(source.Schema) || HasValue(target.Schema)))
			throw new NotSupportedException("SQLite 核心映射不支持跨数据库结构化 Join。");
	}

	/// <summary>
	/// 解析跨数据库校验使用的数据库类型。
	/// </summary>
	private static DatabaseType? ResolveDatabaseType(DatabaseContext executionContext)
	{
		return executionContext?.DataSource?.DatabaseType;
	}

	/// <summary>
	/// 判断字符串是否包含有效值。
	/// </summary>
	private static bool HasValue(string value) => string.IsNullOrWhiteSpace(value) == false;
}
