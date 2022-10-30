using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Extensions;

/// <summary>
/// Sql方言扩展
/// </summary>
public static partial class DialectExtensions
{
    /// <summary>
    /// 获取列
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="column">列名</param>
    /// <param name="columnAlias">列别名</param>
    public static string GetColumn(this IDialect dialect, string column, string columnAlias) => columnAlias.IsEmpty() ? column : $"{column} {GetAs(dialect)}{columnAlias}";

    /// <summary>
    /// 获取As关键字
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    private static string GetAs(IDialect dialect) => dialect == null ? null : dialect.SupportSelectAs() ? "As " : null;

    /// <summary>
    /// 获取安全名称
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="name">名称</param>
    public static string GetSafeName(this IDialect dialect, string name) => dialect == null ? name : dialect.SafeName(name);
}