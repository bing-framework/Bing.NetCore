using Npgsql;

namespace Bing.Data.Sql;

/// <summary>
/// PostgreSql数据库工厂
/// </summary>
public class PostgreSqlDatabaseFactory : IDatabaseFactory
{
    /// <inheritdoc />
    public IDatabase Create(string connection) => new DefaultDatabase(new NpgsqlConnection(connection));
}
