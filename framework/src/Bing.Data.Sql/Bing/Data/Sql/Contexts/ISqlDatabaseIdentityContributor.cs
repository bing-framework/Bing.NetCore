using System.Data.Common;
using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据库物理身份解析贡献者。
/// </summary>
public interface ISqlDatabaseIdentityContributor
{
    /// <summary>
    /// 是否支持指定数据库类型。
    /// </summary>
    /// <param name="databaseType">数据库类型。</param>
    /// <returns>是否支持。</returns>
    bool CanResolve(DatabaseType databaseType);

    /// <summary>
    /// 解析数据库物理身份。
    /// </summary>
    /// <param name="databaseType">数据库类型。</param>
    /// <param name="builder">连接字符串构建器。</param>
    /// <returns>不含凭据的物理身份。</returns>
    SqlDatabaseIdentity Resolve(DatabaseType databaseType, DbConnectionStringBuilder builder);
}