using Microsoft.Data.Sqlite;

namespace Bing.Data.Sql;

/// <summary>
/// Sqlite数据库工厂
/// </summary>
public class SqliteDatabaseFactory : IDatabaseFactory
{
    /// <inheritdoc />
    public IDatabase Create(string connection) => new DefaultDatabase(new SqliteConnection(connection));
}
