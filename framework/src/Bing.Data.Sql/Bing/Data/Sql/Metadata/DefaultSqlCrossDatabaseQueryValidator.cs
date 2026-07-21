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
	public void Validate(SqlTableReference source, SqlTableReference target, DatabaseContext executionContext)
	{
		if (target == null)
			throw new ArgumentNullException(nameof(target));
		var executionDbKey = Normalize(executionContext?.DbKey);
		var sourceDbKey = Normalize(source?.DbKey) ?? executionDbKey;
		var targetDbKey = Normalize(target.DbKey) ?? executionDbKey;
		ValidateDbKey(sourceDbKey, targetDbKey);
		if (string.IsNullOrWhiteSpace(source?.Catalog) || string.IsNullOrWhiteSpace(target.Catalog) ||
			string.Equals(source.Catalog, target.Catalog, StringComparison.OrdinalIgnoreCase))
			return;
		var databaseType = ResolveDatabaseType(executionContext, source, target);
		if (_capabilityProvider.GetCapabilities(databaseType).SupportsCrossCatalogQuery == false)
			throw new NotSupportedException("当前数据库 Provider 不支持同一连接中的跨 Catalog 查询。");
	}

	/// <summary>
	/// 验证源表和目标表的数据源标识。
	/// </summary>
	/// <param name="sourceDbKey">源数据源标识。</param>
	/// <param name="targetDbKey">目标数据源标识。</param>
	private static void ValidateDbKey(string sourceDbKey, string targetDbKey)
	{
		if (string.IsNullOrWhiteSpace(sourceDbKey) || string.IsNullOrWhiteSpace(targetDbKey))
			return;
		if (string.Equals(sourceDbKey, targetDbKey, StringComparison.OrdinalIgnoreCase) == false)
			throw new InvalidOperationException("不同 DbKey 的表不能直接执行 Join 查询。");
	}

	/// <summary>
	/// 解析跨 Catalog 校验使用的数据库类型。
	/// </summary>
	/// <param name="executionContext">执行数据库上下文。</param>
	/// <param name="source">源表引用。</param>
	/// <param name="target">目标表引用。</param>
	/// <returns>数据库类型。</returns>
	private static DatabaseType ResolveDatabaseType(DatabaseContext executionContext, SqlTableReference source,
		SqlTableReference target)
	{
		var databaseType = executionContext?.DataSource?.DatabaseType ?? source?.DatabaseType ?? target.DatabaseType;
		if (databaseType == null)
			throw new InvalidOperationException("无法确定跨 Catalog 查询的数据库类型。");
		return databaseType.Value;
	}

	/// <summary>
	/// 规范化数据源标识。
	/// </summary>
	/// <param name="dbKey">数据源标识。</param>
	/// <returns>规范化后的数据源标识。</returns>
	private static string Normalize(string dbKey) => string.IsNullOrWhiteSpace(dbKey) ? null : dbKey.Trim();
}
