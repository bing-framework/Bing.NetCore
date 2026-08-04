namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// SQLite 结构化表引用样例实体。
/// </summary>
public sealed class SqliteStructuredTableSample
{
    /// <summary>
    /// 标识。
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 名称。
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// SQLite 结构化订单样例实体。
/// </summary>
public sealed class SqliteStructuredOrderSample
{
    /// <summary>
    /// 标识。
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 租户标识。
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// 名称。
    /// </summary>
    public string Name { get; set; }
}