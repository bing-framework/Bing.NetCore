namespace Bing.Auditing.Persistence;

/// <summary>
/// 审计日志存储使用的数据库语法。
/// </summary>
public enum AuditLogDatabase
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
