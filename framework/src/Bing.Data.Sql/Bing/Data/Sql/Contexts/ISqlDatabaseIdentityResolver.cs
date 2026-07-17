using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据库物理身份解析器。
/// </summary>
public interface ISqlDatabaseIdentityResolver
{
    /// <summary>
    /// 解析数据库连接字符串对应的物理身份。
    /// </summary>
    /// <param name="databaseType">数据库类型。</param>
    /// <param name="connectionString">连接字符串。</param>
    /// <returns>不含凭据的数据库物理身份。</returns>
    SqlDatabaseIdentity Resolve(DatabaseType databaseType, string connectionString);
}