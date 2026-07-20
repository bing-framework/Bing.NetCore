using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 已格式化 SQL 项
/// </summary>
public sealed class FormattedSqlItem : SqlItem
{
    /// <summary>
    /// 已格式化 SQL
    /// </summary>
    private readonly string _sql;

    /// <summary>
    /// 初始化一个<see cref="FormattedSqlItem"/>类型的实例
    /// </summary>
    /// <param name="sql">已格式化 SQL</param>
    public FormattedSqlItem(string sql)
        : base("structured_reference") => _sql = sql;

    /// <inheritdoc />
    public override string ToSql(IDialect dialect = null, ITableDatabase tableDatabase = null) => _sql;
}