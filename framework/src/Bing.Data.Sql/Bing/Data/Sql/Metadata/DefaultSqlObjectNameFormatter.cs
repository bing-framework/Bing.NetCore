using Bing.Data.Enums;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认 SQL 对象名格式化器
/// </summary>
public sealed class DefaultSqlObjectNameFormatter : ISqlObjectNameFormatter
{
    /// <summary>
    /// SQL 对象名称能力提供器。
    /// </summary>
    private readonly ISqlObjectNameCapabilityProvider _capabilityProvider;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlObjectNameFormatter"/>类型的实例。
    /// </summary>
    /// <param name="capabilityProvider">SQL 对象名称能力提供器。</param>
    public DefaultSqlObjectNameFormatter(ISqlObjectNameCapabilityProvider capabilityProvider = null) =>
        _capabilityProvider = capabilityProvider ?? new DefaultSqlObjectNameCapabilityProvider();

    /// <inheritdoc />
    public string Format(SqlTableReference reference, IDialect dialect, DatabaseType? databaseType)
    {
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));
        // 旧通用 Builder 未绑定数据源时一直使用 SQL Server 风格方言，保留该兼容默认值。
        var type = databaseType ?? reference.DatabaseType ?? DatabaseType.SqlServer;
        var capabilities = _capabilityProvider.GetCapabilities(type);
        Validate(reference, capabilities);
        return type switch
        {
            DatabaseType.MySql or DatabaseType.Doris => Join(dialect, reference.Catalog, reference.ResolvedTableName),
            DatabaseType.SqlServer => Join(dialect, reference.Catalog, reference.PhysicalSchema,
                reference.ResolvedTableName),
            DatabaseType.PgSql => Join(dialect, reference.PhysicalSchema, reference.ResolvedTableName),
            DatabaseType.Oracle => FormatOracle(reference, dialect),
            DatabaseType.Sqlite => Join(dialect, reference.AttachedAlias ?? reference.Catalog,
                reference.ResolvedTableName),
            _ => throw new NotSupportedException("未配置数据库类型的 SQL 对象名称格式化规则。")
        };
    }

    /// <summary>
    /// 格式化 Oracle 表引用
    /// </summary>
    private static string FormatOracle(SqlTableReference reference, IDialect dialect)
    {
        var result = Join(dialect, reference.PhysicalSchema, reference.ResolvedTableName);
        return string.IsNullOrWhiteSpace(reference.DatabaseLink) ? result :
            $"{result}@{Quote(dialect, reference.DatabaseLink)}";
    }

    /// <summary>
    /// 拼接逐段转义的标识符。
    /// </summary>
    /// <param name="dialect">SQL 方言。</param>
    /// <param name="parts">名称段。</param>
    /// <returns>拼接后的 SQL 对象名称。</returns>
    private static string Join(IDialect dialect, params string[] parts) => string.Join(".", parts
        .Where(part => string.IsNullOrWhiteSpace(part) == false)
        .Select(part => Quote(dialect, part)));

    /// <summary>
    /// 转义单个动态标识符。
    /// </summary>
    /// <param name="dialect">SQL 方言。</param>
    /// <param name="identifier">标识符。</param>
    /// <returns>方言安全的标识符。</returns>
    private static string Quote(IDialect dialect, string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("标识符不能为空。", nameof(identifier));
        if (identifier.IndexOfAny(new[] { '\r', '\n', ';' }) >= 0)
            throw new ArgumentException("表引用包含无效标识符字符。", nameof(identifier));
        var escaped = identifier.Replace(dialect.ClosingIdentifier.ToString(), new string(dialect.ClosingIdentifier, 2));
        return $"{dialect.OpeningIdentifier}{escaped}{dialect.ClosingIdentifier}";
    }

    /// <summary>
    /// 验证表引用字段是否受到 Provider 支持。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    /// <param name="capabilities">SQL 对象名称能力。</param>
    private static void Validate(SqlTableReference reference, SqlObjectNameCapabilities capabilities)
    {
        if (string.IsNullOrWhiteSpace(reference.ResolvedTableName))
            throw new ArgumentException("解析后的表名不能为空。", nameof(reference));
        if (string.IsNullOrWhiteSpace(reference.Catalog) == false && capabilities.SupportsCatalog == false)
            throw new NotSupportedException("当前数据库 Provider 不支持 Catalog 限定。");
        if (string.IsNullOrWhiteSpace(reference.PhysicalSchema) == false && capabilities.SupportsPhysicalSchema == false)
            throw new NotSupportedException("当前数据库 Provider 不支持 PhysicalSchema 限定。");
        if (string.IsNullOrWhiteSpace(reference.DatabaseLink) == false && capabilities.SupportsDatabaseLink == false)
            throw new NotSupportedException("当前数据库 Provider 不支持 DatabaseLink 限定。");
        if (string.IsNullOrWhiteSpace(reference.AttachedAlias) == false && capabilities.SupportsAttachedAlias == false)
            throw new NotSupportedException("当前数据库 Provider 不支持已附加数据库别名。");
        if (string.IsNullOrWhiteSpace(reference.Catalog) == false && string.IsNullOrWhiteSpace(reference.AttachedAlias) == false &&
            string.Equals(reference.Catalog, reference.AttachedAlias, StringComparison.OrdinalIgnoreCase) == false)
            throw new ArgumentException("SQLite Catalog 必须与已附加数据库别名一致。", nameof(reference));
        var values = new[] { reference.Catalog, reference.PhysicalSchema, reference.ResolvedTableName,
            reference.DatabaseLink, reference.AttachedAlias };
        if (values.Any(value => string.IsNullOrWhiteSpace(value) == false &&
                                value.IndexOfAny(new[] { '\r', '\n', ';' }) >= 0))
            throw new ArgumentException("表引用包含无效标识符字符。", nameof(reference));
    }
}