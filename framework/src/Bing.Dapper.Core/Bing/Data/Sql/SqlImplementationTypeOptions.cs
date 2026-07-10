using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 实现类型配置
/// </summary>
public sealed class SqlImplementationTypeOptions
{
    /// <summary>
    /// 服务类型到实现类型映射
    /// </summary>
    public IDictionary<Type, Type> Mappings { get; } = new Dictionary<Type, Type>();

    /// <summary>
    /// 数据库类型维度的服务类型到实现类型映射
    /// </summary>
    public IDictionary<string, Type> DatabaseMappings { get; } = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 注册实现类型映射
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="implementationType">实现类型</param>
    /// <param name="databaseType">数据库类型</param>
    public void Map(Type serviceType, Type implementationType, DatabaseType? databaseType = null)
    {
        if (serviceType == null || implementationType == null)
            return;
        Mappings[serviceType] = implementationType;
        Mappings[implementationType] = implementationType;
        if (databaseType == null)
            return;
        DatabaseMappings[GetKey(serviceType, databaseType.Value)] = implementationType;
        DatabaseMappings[GetKey(implementationType, databaseType.Value)] = implementationType;
    }

    /// <summary>
    /// 获取数据库类型映射键
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="databaseType">数据库类型</param>
    /// <returns>映射键</returns>
    public static string GetKey(Type serviceType, DatabaseType databaseType) =>
        $"{serviceType?.AssemblyQualifiedName}:{databaseType}";
}
