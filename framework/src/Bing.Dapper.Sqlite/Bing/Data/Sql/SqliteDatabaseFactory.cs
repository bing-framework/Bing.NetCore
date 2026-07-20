using System.ComponentModel;
using Microsoft.Data.Sqlite;

namespace Bing.Data.Sql;

/// <summary>
/// Sqlite数据库工厂
/// </summary>
[System.Obsolete("Dapper 连接创建已迁移至 ISqlDbConnectionFactoryResolver。")]
[EditorBrowsable(EditorBrowsableState.Never)]
public class SqliteDatabaseFactory : IDatabaseFactory
{
    /// <inheritdoc />
    public IDatabase Create(string connection) => new DefaultDatabase(new SqliteConnection(connection));
}
