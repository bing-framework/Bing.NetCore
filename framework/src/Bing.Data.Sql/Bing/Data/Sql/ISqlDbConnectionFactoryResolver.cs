using System.Data;
using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据库连接工厂解析器
/// </summary>
public interface ISqlDbConnectionFactoryResolver
{
    /// <summary>
    /// 创建指定数据库类型的连接
    /// </summary>
    /// <param name="databaseType">数据库类型</param>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>数据库连接</returns>
    IDbConnection Create(DatabaseType databaseType, string connectionString);
}
