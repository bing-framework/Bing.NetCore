using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认 SQL 表引用验证器。
/// </summary>
public sealed class DefaultSqlTableReferenceValidator : ISqlTableReferenceValidator
{
    /// <summary>
    /// SQL 对象名称能力提供器。
    /// </summary>
    private readonly ISqlObjectNameCapabilityProvider _capabilityProvider;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlTableReferenceValidator"/>类型的实例。
    /// </summary>
    /// <param name="capabilityProvider">SQL 对象名称能力提供器。</param>
    public DefaultSqlTableReferenceValidator(ISqlObjectNameCapabilityProvider capabilityProvider = null)
    {
        _capabilityProvider = capabilityProvider ?? new DefaultSqlObjectNameCapabilityProvider();
    }

    /// <inheritdoc />
    public void Validate(SqlTableReference table, DatabaseType databaseType)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));
        var capabilities = _capabilityProvider.GetCapabilities(databaseType);
        ValidateCapabilities(table, capabilities);
        ValidateNameParts(table, capabilities);
        ValidateIdentifiers(table);
    }

    /// <summary>
    /// 验证 Provider 支持的限定字段。
    /// </summary>
    /// <param name="table">结构化表引用。</param>
    /// <param name="capabilities">对象名称能力。</param>
    private static void ValidateCapabilities(SqlTableReference table, SqlObjectNameCapabilities capabilities)
    {
        if (string.IsNullOrWhiteSpace(table.TableName))
            throw new ArgumentException("表名不能为空。", nameof(table));
        if (HasValue(table.Database) && capabilities.SupportsDatabase == false)
            throw new NotSupportedException("当前数据库 Provider 不支持 Database 限定。");
        if (HasValue(table.Schema) && capabilities.SupportsSchema == false)
            throw new NotSupportedException("当前数据库 Provider 不支持 Schema 限定。");
    }

    /// <summary>
    /// 验证对象名称段数。
    /// </summary>
    /// <param name="table">结构化表引用。</param>
    /// <param name="capabilities">对象名称能力。</param>
    private static void ValidateNameParts(SqlTableReference table, SqlObjectNameCapabilities capabilities)
    {
        var nameParts = 1;
        if (HasValue(table.Database))
            nameParts++;
        if (HasValue(table.Schema))
            nameParts++;
        if (capabilities.MaximumNameParts > 0 && nameParts > capabilities.MaximumNameParts)
            throw new InvalidOperationException("SQL 对象名称段数超过当前数据库 Provider 支持的上限。");
    }

    /// <summary>
    /// 验证所有动态标识符。
    /// </summary>
    /// <param name="table">结构化表引用。</param>
    private static void ValidateIdentifiers(SqlTableReference table)
    {
        ValidateIdentifier(table.Database);
        ValidateIdentifier(table.Schema);
        ValidateIdentifier(table.TableName);
        ValidateIdentifier(table.Alias);
    }

    /// <summary>
    /// 验证单个动态标识符。
    /// </summary>
    /// <param name="identifier">标识符。</param>
    private static void ValidateIdentifier(string identifier)
    {
        if (HasValue(identifier) && identifier.IndexOfAny(new[] { '\r', '\n', ';' }) >= 0)
            throw new ArgumentException("表引用包含无效标识符字符。", nameof(identifier));
    }

    /// <summary>
    /// 判断字符串是否包含有效值。
    /// </summary>
    /// <param name="value">字符串值。</param>
    /// <returns>包含有效值时返回 <see langword="true"/>。</returns>
    private static bool HasValue(string value) => string.IsNullOrWhiteSpace(value) == false;
}