using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 实现类型解析器
/// </summary>
public interface ISqlImplementationTypeResolver
{
    /// <summary>
    /// 解析服务类型对应的实现类型
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="databaseType">数据库类型</param>
    /// <returns>实现类型</returns>
    Type Resolve(Type serviceType, DatabaseType? databaseType = null);
}
