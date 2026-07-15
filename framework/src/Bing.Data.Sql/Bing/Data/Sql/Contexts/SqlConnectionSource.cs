namespace Bing.Data.Sql;

/// <summary>
/// SQL 连接来源
/// </summary>
public enum SqlConnectionSource
{
    /// <summary>
    /// 未知来源
    /// </summary>
    Unknown,

    /// <summary>
    /// SQL 数据源配置
    /// </summary>
    DataSource,

    /// <summary>
    /// 外部提供的连接
    /// </summary>
    External,

    /// <summary>
    /// Entity Framework Core 连接
    /// </summary>
    EntityFrameworkCore
}