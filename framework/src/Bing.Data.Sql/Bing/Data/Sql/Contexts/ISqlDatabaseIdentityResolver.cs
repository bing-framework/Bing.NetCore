using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据库物理身份解析器。
/// </summary>
public interface ISqlDatabaseIdentityResolver
{
    /// <summary>
    /// 从数据库连接字符串解析可比较且不包含凭据的物理数据库身份。
    /// </summary>
    /// <param name="databaseType">用于选择身份解析规则的数据库类型。</param>
    /// <param name="connectionString">要解析的连接字符串，不能为 <c>null</c> 或空字符串。</param>
    /// <returns>不含凭据的数据库物理身份。</returns>
    /// <exception cref="InvalidOperationException">连接字符串为空时引发。</exception>
    /// <exception cref="NotSupportedException">没有支持指定数据库类型的解析贡献者时引发。</exception>
    SqlDatabaseIdentity Resolve(DatabaseType databaseType, string connectionString);
}