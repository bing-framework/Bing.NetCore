namespace Bing.Data.Enums;

/// <summary>
/// 标识 SQL 数据源使用的数据库类型。
/// </summary>
public enum DatabaseType
{
    /// <summary>
    /// SQL Server 数据库。
    /// </summary>
    SqlServer,

    /// <summary>
    /// MySQL 数据库。
    /// </summary>
    MySql,

    /// <summary>
    /// PostgreSQL 数据库。
    /// </summary>
    PgSql,

    /// <summary>
    /// Oracle 数据库。
    /// </summary>
    Oracle,

    /// <summary>
    /// SQLite 数据库。
    /// </summary>
    Sqlite,

    /// <summary>
    /// Apache Doris 数据库。
    /// </summary>
    Doris
}