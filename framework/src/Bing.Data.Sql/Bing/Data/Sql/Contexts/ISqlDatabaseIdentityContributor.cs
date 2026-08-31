using System.Data.Common;
using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据库物理身份解析贡献者。
/// </summary>
public interface ISqlDatabaseIdentityContributor
{
    /// <summary>
    /// 确定当前贡献者是否支持解析指定数据库类型。
    /// </summary>
    /// <param name="databaseType">要判断的数据库类型。</param>
    /// <returns>支持该数据库类型时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    bool CanResolve(DatabaseType databaseType);

    /// <summary>
    /// 使用连接字符串构建器解析数据库物理身份。
    /// </summary>
    /// <param name="databaseType">要解析的数据库类型，应已通过 <see cref="CanResolve"/> 验证支持。</param>
    /// <param name="builder">包含连接参数的构建器；实现不得将用户名、密码等凭据写入结果。</param>
    /// <returns>非空且不含凭据的物理数据库身份。</returns>
    SqlDatabaseIdentity Resolve(DatabaseType databaseType, DbConnectionStringBuilder builder);
}