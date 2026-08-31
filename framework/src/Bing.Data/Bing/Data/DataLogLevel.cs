namespace Bing.Data;

/// <summary>
/// 指定数据访问层输出的日志范围。
/// </summary>
public enum DataLogLevel
{
    /// <summary>
    /// 输出包括连接、事务和 SQL 在内的全部数据访问日志。
    /// </summary>
    All,

    /// <summary>
    /// 仅输出 SQL 相关日志。
    /// </summary>
    Sql,

    /// <summary>
    /// 关闭数据访问日志输出。
    /// </summary>
    Off
}