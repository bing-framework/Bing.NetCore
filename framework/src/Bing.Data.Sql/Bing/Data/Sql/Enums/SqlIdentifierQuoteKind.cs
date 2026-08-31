namespace Bing.Data.Sql;

/// <summary>
/// 指定 SQL 标识符格式化时使用的引用字符。
/// </summary>
public enum SqlIdentifierQuoteKind
{
    /// <summary>
    /// 不为标识符添加引用字符。
    /// </summary>
    None = 0,

    /// <summary>
    /// 使用反引号引用标识符，常见于 MySQL 等方言。
    /// </summary>
    Backtick = 1,

    /// <summary>
    /// 使用方括号引用标识符，常见于 SQL Server 等方言。
    /// </summary>
    Bracket = 2,

    /// <summary>
    /// 使用双引号引用标识符，常见于 PostgreSQL、Oracle 等方言。
    /// </summary>
    DoubleQuote = 3
}
