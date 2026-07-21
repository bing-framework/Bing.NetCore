namespace Bing.Data.Sql.Metadata;

/// <summary>
/// SQL 表引用。
/// </summary>
public sealed record SqlTableReference
{
    /// <summary>
    /// 实体类型。
    /// </summary>
    public Type EntityType { get; init; }

    /// <summary>
    /// 数据库名称。主要用于 SQL Server 三段式表名。
    /// </summary>
    public string Database { get; init; }

    /// <summary>
    /// 数据库架构。MySQL 和 Doris 中表示数据库名称。
    /// </summary>
    public string Schema { get; init; }

    /// <summary>
    /// 最终物理表名。
    /// </summary>
    public string TableName { get; init; }

    /// <summary>
    /// 表别名。
    /// </summary>
    public string Alias { get; init; }
}