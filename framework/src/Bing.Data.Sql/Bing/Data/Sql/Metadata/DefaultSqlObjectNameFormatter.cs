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
        if (databaseType == null)
            throw new InvalidOperationException("无法确定 SQL 对象名称格式化使用的数据库类型。");
        return databaseType.Value switch
        {
            DatabaseType.MySql or DatabaseType.Doris => Join(dialect, reference.Schema, reference.TableName),
            DatabaseType.SqlServer => Join(dialect, reference.Database, reference.Schema, reference.TableName),
            DatabaseType.PgSql or DatabaseType.Oracle => Join(dialect, reference.Schema, reference.TableName),
            DatabaseType.Sqlite => Join(dialect, reference.TableName),
            _ => throw new NotSupportedException("未配置数据库类型的 SQL 对象名称格式化规则。")
        };
    }

    /// <summary>
    /// 拼接逐段转义的标识符。
    /// </summary>
    /// <param name="dialect">SQL 方言。</param>
    /// <param name="first">第一名称段。</param>
    /// <param name="second">第二名称段。</param>
    /// <param name="third">第三名称段。</param>
    /// <returns>拼接后的 SQL 对象名称。</returns>
    private static string Join(IDialect dialect, string first = null, string second = null, string third = null)
    {
        var builder = new StringBuilder(64);
        AppendPart(builder, dialect, first);
        AppendPart(builder, dialect, second);
        AppendPart(builder, dialect, third);
        return builder.ToString();
    }

    /// <summary>
    /// 追加单个名称段。
    /// </summary>
    private static void AppendPart(StringBuilder builder, IDialect dialect, string part)
    {
        if (string.IsNullOrWhiteSpace(part))
            return;
        if (builder.Length > 0)
            builder.Append('.');
        builder.Append(dialect.SafeName(part));
    }
}