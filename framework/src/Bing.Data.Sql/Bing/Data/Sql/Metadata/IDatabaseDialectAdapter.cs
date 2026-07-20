using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 数据库方言适配器
/// </summary>
public interface IDatabaseDialectAdapter
{
    /// <summary>
    /// 获取数据库语法
    /// </summary>
    /// <param name="databaseType">数据库类型</param>
    CrossDatabaseSyntax GetSyntax(DatabaseType? databaseType);

    /// <summary>
    /// 格式化表名
    /// </summary>
    /// <param name="table">表标识符</param>
    /// <param name="databaseType">数据库类型</param>
    string FormatTable(TableIdentifier table, DatabaseType? databaseType);

    /// <summary>
    /// 格式化列名
    /// </summary>
    /// <param name="column">列标识符</param>
    /// <param name="databaseType">数据库类型</param>
    string FormatColumn(ColumnIdentifier column, DatabaseType? databaseType);
}