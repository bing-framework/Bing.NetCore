using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using System.Text;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认 SQL 对象名格式化器
/// </summary>
public sealed class DefaultSqlObjectNameFormatter : ISqlObjectNameFormatter
{
    /// <inheritdoc />
    public string Format(SqlTableReference reference, IDialect dialect, DatabaseType? databaseType)
    {
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        if (dialect == null)
            throw new ArgumentNullException(nameof(dialect));
        var type = databaseType ?? reference.DatabaseType;
        if (type == null)
            throw new InvalidOperationException("无法确定 SQL 对象名称格式化使用的数据库类型。");
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
    private static string Join(IDialect dialect, params string[] parts)
    {
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
                continue;
            if (builder.Length > 0)
                builder.Append('.');
            builder.Append(Quote(dialect, part));
        }
        return builder.ToString();
    }

    /// <summary>
    /// 转义单个动态标识符。
    /// </summary>
    /// <param name="dialect">SQL 方言。</param>
    /// <param name="identifier">标识符。</param>
    /// <returns>方言安全的标识符。</returns>
    private static string Quote(IDialect dialect, string identifier)
    {
        var escaped = identifier.Replace(dialect.ClosingIdentifier.ToString(), new string(dialect.ClosingIdentifier, 2));
        return $"{dialect.OpeningIdentifier}{escaped}{dialect.ClosingIdentifier}";
    }
}