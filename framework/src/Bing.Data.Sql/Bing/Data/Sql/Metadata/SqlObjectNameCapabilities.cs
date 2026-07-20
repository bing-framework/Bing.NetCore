namespace Bing.Data.Sql.Metadata;

/// <summary>
/// SQL 对象名称能力。
/// </summary>
public sealed class SqlObjectNameCapabilities
{
    /// <summary>
    /// 是否支持 Catalog 限定。
    /// </summary>
    public bool SupportsCatalog { get; init; }

    /// <summary>
    /// 是否支持物理架构限定。
    /// </summary>
    public bool SupportsPhysicalSchema { get; init; }

    /// <summary>
    /// 是否支持数据库链接限定。
    /// </summary>
    public bool SupportsDatabaseLink { get; init; }

    /// <summary>
    /// 是否支持同一连接的跨 Catalog 查询。
    /// </summary>
    public bool SupportsCrossCatalogQuery { get; init; }

    /// <summary>
    /// 是否支持 SQLite 已附加数据库别名。
    /// </summary>
    public bool SupportsAttachedAlias { get; init; }

    /// <summary>
    /// 支持的最大名称段数。
    /// </summary>
    public int MaximumNameParts { get; init; }
}