using System.Data;
namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据库连接工厂解析器
/// </summary>
public interface ISqlDbConnectionFactoryResolver
{
    /// <summary>
    /// 创建指定 Provider 的连接。
    /// </summary>
    /// <param name="providerKey">SQL Provider 唯一标识。</param>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>数据库连接</returns>
    IDbConnection Create(string providerKey, string connectionString);
}
