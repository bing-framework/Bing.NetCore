using Oracle.ManagedDataAccess.Client;
using System.ComponentModel;

namespace Bing.Data.Sql;

/// <summary>
/// Oracle数据库工厂
/// </summary>
[System.Obsolete("Dapper 连接创建已迁移至 ISqlDbConnectionFactoryResolver。")]
[EditorBrowsable(EditorBrowsableState.Never)]
public class OracleDatabaseFactory : IDatabaseFactory
{
    /// <inheritdoc />
    public IDatabase Create(string connection) => new DefaultDatabase(new OracleConnection(connection));
}
