namespace Bing.Events.Consumption;

/// <summary>
/// 指定事件消费记录使用的数据库语法。
/// </summary>
public enum EventConsumptionDatabase
{
    /// <summary>
    /// MySQL 数据库。
    /// </summary>
    MySql,

    /// <summary>
    /// SQLite 数据库。
    /// </summary>
    Sqlite
}
