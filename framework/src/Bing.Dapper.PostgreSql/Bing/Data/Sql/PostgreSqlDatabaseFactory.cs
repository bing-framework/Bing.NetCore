using System.ComponentModel;
using Npgsql;

namespace Bing.Data.Sql;

/// <summary>
/// PostgreSql数据库工厂
/// </summary>
[System.Obsolete("Dapper 连接创建已迁移至 ISqlDbConnectionFactoryResolver。")]
[EditorBrowsable(EditorBrowsableState.Never)]
public class PostgreSqlDatabaseFactory : IDatabaseFactory
{
    /// <inheritdoc />
    public IDatabase Create(string connection) => new DefaultDatabase(new NpgsqlConnection(connection));
}
