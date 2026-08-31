namespace Bing.Data.Sql;

/// <summary>
/// 标识 SQL 执行使用的连接来源，以便确定连接生命周期和诊断信息。
/// </summary>
public enum SqlConnectionSource
{
    /// <summary>
    /// 无法确定连接来源。
    /// </summary>
    Unknown,

    /// <summary>
    /// 连接由 SQL 数据源配置解析或创建。
    /// </summary>
    DataSource,

    /// <summary>
    /// 连接由调用方或外部组件提供。
    /// </summary>
    External,

    /// <summary>
    /// 连接由 Entity Framework Core 上下文提供。
    /// </summary>
    EntityFrameworkCore
}