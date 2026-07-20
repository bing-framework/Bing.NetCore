using System.ComponentModel;
using Microsoft.Data.SqlClient;

namespace Bing.Data.Sql;

/// <summary>
/// Sql Server数据库工厂
/// </summary>
[System.Obsolete("Dapper 连接创建已迁移至 ISqlDbConnectionFactoryResolver。")]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SqlServerDatabaseFactory : IDatabaseFactory
{
    /// <inheritdoc />
    public IDatabase Create(string connection) => new DefaultDatabase(new SqlConnection(connection));
}
