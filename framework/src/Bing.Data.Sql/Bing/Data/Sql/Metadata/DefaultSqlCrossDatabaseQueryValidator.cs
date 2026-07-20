namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认类型化跨数据库查询校验器
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
    public DefaultSqlCrossDatabaseQueryValidator(ISqlObjectNameCapabilityProvider capabilityProvider = null) =>
        _capabilityProvider = capabilityProvider ?? new DefaultSqlObjectNameCapabilityProvider();

    /// <inheritdoc />
    public void Validate(SqlTableReference source, SqlTableReference target, DatabaseContext executionContext)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        var executionDbKey = executionContext?.DbKey ?? source?.DbKey;
        ValidateDbKey(executionDbKey, target.DbKey);
        if (source != null)
            ValidateDbKey(source.DbKey, target.DbKey);
        // 旧通用 Builder 未绑定数据源时一直使用 SQL Server 风格方言，保留该兼容默认值。
        var databaseType = executionContext?.DataSource?.DatabaseType ?? source?.DatabaseType ?? target.DatabaseType ??
                   Bing.Data.Enums.DatabaseType.SqlServer;
        var capabilities = _capabilityProvider.GetCapabilities(databaseType);
        ValidateCapabilities(target, capabilities);
        if (source != null)
            ValidateCapabilities(source, capabilities);
        if (source != null && string.Equals(source.Catalog, target.Catalog, StringComparison.OrdinalIgnoreCase) == false &&
            (string.IsNullOrWhiteSpace(source.Catalog) == false || string.IsNullOrWhiteSpace(target.Catalog) == false) &&
            capabilities.SupportsCrossCatalogQuery == false)
            throw new NotSupportedException("当前数据库 Provider 不支持同一连接中的跨 Catalog 查询。");
    }

    /// <inheritdoc />
    public void ValidateJoin(string executionDbKey, SqlTableReference reference)
    {
        if (reference == null)
            return;
        Validate(new SqlTableReference { DbKey = executionDbKey, DatabaseType = reference.DatabaseType }, reference,
            new DatabaseContext { DbKey = executionDbKey });
    }

    /// <summary>
    /// 验证两个数据源标识是否一致。
    /// </summary>
    /// <param name="sourceDbKey">源数据源标识。</param>
    /// <param name="targetDbKey">目标数据源标识。</param>
    private static void ValidateDbKey(string sourceDbKey, string targetDbKey)
    {
        if (string.IsNullOrWhiteSpace(sourceDbKey) || string.IsNullOrWhiteSpace(targetDbKey) ||
            string.Equals(sourceDbKey, targetDbKey, StringComparison.OrdinalIgnoreCase))
            return;
        throw new InvalidOperationException(
            $"类型化 Join 不支持跨 DbKey 查询。执行数据源为 {sourceDbKey}，连接表数据源为 {targetDbKey}。");
    }

    /// <summary>
    /// 验证表引用是否符合 Provider 对象名称能力。
    /// </summary>
    /// <param name="reference">表引用。</param>
    /// <param name="capabilities">对象名称能力。</param>
    private static void ValidateCapabilities(SqlTableReference reference, SqlObjectNameCapabilities capabilities)
    {
        if (string.IsNullOrWhiteSpace(reference.Catalog) == false && capabilities.SupportsCatalog == false)
            throw new NotSupportedException("当前数据库 Provider 不支持 Catalog 限定。");
        if (string.IsNullOrWhiteSpace(reference.PhysicalSchema) == false && capabilities.SupportsPhysicalSchema == false)
            throw new NotSupportedException("当前数据库 Provider 不支持 PhysicalSchema 限定。");
        if (string.IsNullOrWhiteSpace(reference.DatabaseLink) == false && capabilities.SupportsDatabaseLink == false)
            throw new NotSupportedException("当前数据库 Provider 不支持 DatabaseLink 限定。");
        if (string.IsNullOrWhiteSpace(reference.AttachedAlias) == false && capabilities.SupportsAttachedAlias == false)
            throw new NotSupportedException("当前数据库 Provider 不支持已附加数据库别名。");
    }
}