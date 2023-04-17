using Microsoft.Data.SqlClient;

namespace Bing.Data.Sql;

/// <summary>
/// Sql Server数据库工厂
/// </summary>
public class SqlServerDatabaseFactory : IDatabaseFactory
{
    /// <inheritdoc />
    public IDatabase Create(string connection) => new DefaultDatabase(new SqlConnection(connection));
}
