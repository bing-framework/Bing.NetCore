namespace Bing.Data.Sql;

/// <summary>
/// SQL 实现类型解析器
/// </summary>
internal interface ISqlImplementationTypeResolver
{
    /// <summary>
    /// 解析服务类型对应的实现类型
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="providerKey">Provider 唯一标识。</param>
    /// <returns>实现类型</returns>
    Type Resolve(Type serviceType, string providerKey);
}
