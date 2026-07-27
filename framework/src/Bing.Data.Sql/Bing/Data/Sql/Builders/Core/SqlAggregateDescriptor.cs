namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 结构化聚合列描述。
/// </summary>
internal sealed record SqlAggregateDescriptor
{
    /// <summary>
    /// 聚合函数。
    /// </summary>
    public SqlAggregateFunction Function { get; init; }

    /// <summary>
    /// 是否对聚合参数去重。
    /// </summary>
    public bool Distinct { get; init; }

    /// <summary>
    /// 聚合参数语义类型。
    /// </summary>
    public SqlAggregateArgumentKind ArgumentKind { get; init; }

    /// <summary>
    /// 结构化列的数据库名称。
    /// </summary>
    public string DatabaseName { get; init; }
}