using Oracle.ManagedDataAccess.Client;

namespace Bing.Data.Sql;

/// <summary>
/// Oracle数据库工厂
/// </summary>
public class OracleDatabaseFactory : IDatabaseFactory
{
    /// <inheritdoc />
    public IDatabase Create(string connection) => new DefaultDatabase(new OracleConnection(connection));
}
