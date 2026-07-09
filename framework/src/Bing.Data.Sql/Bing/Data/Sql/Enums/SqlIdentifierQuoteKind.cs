namespace Bing.Data.Sql;

/// <summary>
/// Sql标识符引用方式
/// </summary>
public enum SqlIdentifierQuoteKind
{
    /// <summary>
    /// 不引用
    /// </summary>
    None = 0,

    /// <summary>
    /// 反引号
    /// </summary>
    Backtick = 1,

    /// <summary>
    /// 方括号
    /// </summary>
    Bracket = 2,

    /// <summary>
    /// 双引号
    /// </summary>
    DoubleQuote = 3
}
