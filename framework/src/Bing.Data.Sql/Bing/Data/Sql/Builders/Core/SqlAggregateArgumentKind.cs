namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 聚合参数的语义类型。
/// </summary>
internal enum SqlAggregateArgumentKind
{
    /// <summary>
    /// 结构化列标识符。
    /// </summary>
    Column,

    /// <summary>
    /// 可转换 SQL 表达式。
    /// </summary>
    Expression,

    /// <summary>
    /// 调用方提供的原始 SQL。
    /// </summary>
    Raw,

    /// <summary>
    /// 通配符参数。
    /// </summary>
    Wildcard
}