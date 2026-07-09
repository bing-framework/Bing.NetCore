namespace Bing.Data.Sql;

/// <summary>
/// 数据库角色
/// </summary>
public enum DatabaseRole
{
    /// <summary>
    /// 默认角色
    /// </summary>
    Default = 0,

    /// <summary>
    /// 主库
    /// </summary>
    Master = 1,

    /// <summary>
    /// 从库
    /// </summary>
    Slave = 2,

    /// <summary>
    /// 只读库
    /// </summary>
    ReadOnly = 3,

    /// <summary>
    /// 报表库
    /// </summary>
    Reporting = 4,

    /// <summary>
    /// 归档库
    /// </summary>
    Archive = 5
}
