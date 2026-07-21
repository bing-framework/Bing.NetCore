namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// SQLite 附加数据库结构化表引用样例实体。
/// </summary>
public sealed class SqliteAttachedTableSample
{
    /// <summary>
    /// 标识。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 名称。
    /// </summary>
    public string Name { get; set; }
}
