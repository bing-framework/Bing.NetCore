namespace Bing.Data.Sql;

/// <summary>
/// 默认 SQL 实现类型解析器
/// </summary>
public sealed class DefaultSqlImplementationTypeResolver : ISqlImplementationTypeResolver
{
    /// <summary>
    /// SQL 实现类型配置
    /// </summary>
    private readonly SqlImplementationTypeOptions _options;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlImplementationTypeResolver"/>类型的实例
    /// </summary>
    /// <param name="options">SQL 实现类型配置</param>
    public DefaultSqlImplementationTypeResolver(SqlImplementationTypeOptions options = null) =>
        _options = options ?? new SqlImplementationTypeOptions();

    /// <inheritdoc />
    public Type Resolve(Type serviceType, string providerKey)
    {
        if (serviceType == null)
            return null;
        if (string.IsNullOrWhiteSpace(providerKey) == false &&
            _options.ProviderMappings.TryGetValue(SqlImplementationTypeOptions.GetKey(serviceType, providerKey),
                out var implementationType))
            return implementationType;
        if (serviceType.IsAbstract == false && serviceType.IsInterface == false)
            return serviceType;
        return null;
    }
}
