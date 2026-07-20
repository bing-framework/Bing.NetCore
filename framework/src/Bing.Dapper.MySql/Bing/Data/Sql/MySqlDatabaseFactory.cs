using System.ComponentModel;
using MySqlConnector;

namespace Bing.Data.Sql;

/// <summary>
/// MySql数据库工厂
/// </summary>
[System.Obsolete("Dapper 连接创建已迁移至 ISqlDbConnectionFactoryResolver。")]
[EditorBrowsable(EditorBrowsableState.Never)]
public class MySqlDatabaseFactory : IDatabaseFactory
{
    /// <summary>
    /// 创建数据库信息
    /// </summary>
    /// <param name="connection">数据库连接字符串</param>
    public IDatabase Create(string connection) => new DefaultDatabase(new MySqlConnection(connection));
}
