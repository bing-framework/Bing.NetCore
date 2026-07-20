using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 跨数据库标识符语法
/// </summary>
public sealed class CrossDatabaseSyntax
{
    /// <summary>
    /// 初始化一个<see cref="CrossDatabaseSyntax"/>类型的实例
    /// </summary>
    /// <param name="databaseType">数据库类型</param>
    /// <param name="openingIdentifier">起始标识符</param>
    /// <param name="closingIdentifier">结束标识符</param>
    /// <param name="supportsSchema">是否支持架构</param>
    /// <param name="supportsTransactions">是否支持事务</param>
    public CrossDatabaseSyntax(DatabaseType databaseType, char openingIdentifier, char closingIdentifier,
        bool supportsSchema = true, bool supportsTransactions = true)
    {
        DatabaseType = databaseType;
        OpeningIdentifier = openingIdentifier;
        ClosingIdentifier = closingIdentifier;
        SupportsSchema = supportsSchema;
        SupportsTransactions = supportsTransactions;
    }

    /// <summary>
    /// SQL Server 语法
    /// </summary>
    public static CrossDatabaseSyntax SqlServer { get; } = new(DatabaseType.SqlServer, '[', ']');

    /// <summary>
    /// MySql 语法
    /// </summary>
    public static CrossDatabaseSyntax MySql { get; } = new(DatabaseType.MySql, '`', '`');

    /// <summary>
    /// PostgreSql 语法
    /// </summary>
    public static CrossDatabaseSyntax PostgreSql { get; } = new(DatabaseType.PgSql, '"', '"');

    /// <summary>
    /// Oracle 语法
    /// </summary>
    public static CrossDatabaseSyntax Oracle { get; } = new(DatabaseType.Oracle, '"', '"');

    /// <summary>
    /// Sqlite 语法
    /// </summary>
    public static CrossDatabaseSyntax Sqlite { get; } = new(DatabaseType.Sqlite, '`', '`');

    /// <summary>
    /// Doris 语法
    /// </summary>
    public static CrossDatabaseSyntax Doris { get; } = new(DatabaseType.Doris, '`', '`', false, false);

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; }

    /// <summary>
    /// 起始标识符
    /// </summary>
    public char OpeningIdentifier { get; }

    /// <summary>
    /// 结束标识符
    /// </summary>
    public char ClosingIdentifier { get; }

    /// <summary>
    /// 是否支持架构
    /// </summary>
    public bool SupportsSchema { get; }

    /// <summary>
    /// 是否支持事务
    /// </summary>
    public bool SupportsTransactions { get; }
}