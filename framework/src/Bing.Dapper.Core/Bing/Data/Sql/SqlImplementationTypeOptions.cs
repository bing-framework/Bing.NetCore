namespace Bing.Data.Sql;

/// <summary>
/// SQL 实现类型配置
/// </summary>
internal sealed class SqlImplementationTypeOptions
{
    /// <summary>
    /// Provider Key 维度的服务类型到实现类型映射。
    /// </summary>
    public IDictionary<string, Type> ProviderMappings { get; } =
        new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 注册实现类型映射
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="implementationType">实现类型</param>
    /// <param name="providerKey">Provider 唯一标识。</param>
    public void Map(Type serviceType, Type implementationType, string providerKey)
    {
        if (serviceType == null)
            throw new ArgumentNullException(nameof(serviceType));
        if (implementationType == null)
            throw new ArgumentNullException(nameof(implementationType));
        var normalizedProviderKey = NormalizeProviderKey(providerKey);
        Map(serviceType, implementationType, normalizedProviderKey, providerKey);
        Map(implementationType, implementationType, normalizedProviderKey, providerKey);
    }

    /// <summary>
    /// 根据服务类型和规范化 Provider Key 生成实现映射键。
    /// </summary>
    /// <param name="serviceType">待解析实现的服务契约类型。</param>
    /// <param name="providerKey">Provider 唯一标识。</param>
    /// <returns>大小写无关 Provider 映射字典使用的复合键。</returns>
    public static string GetKey(Type serviceType, string providerKey) =>
        $"{serviceType?.AssemblyQualifiedName}:{NormalizeProviderKey(providerKey)}";

    /// <summary>
    /// 添加单个映射并拒绝同一 Provider 下的不同实现。
    /// </summary>
    /// <param name="serviceType">服务契约或具体实现类型。</param>
    /// <param name="implementationType">要注册的具体实现类型。</param>
    /// <param name="normalizedProviderKey">已去除首尾空白的 Provider Key。</param>
    /// <param name="providerKey">调用方传入的原始 Provider Key。</param>
    private void Map(Type serviceType, Type implementationType, string normalizedProviderKey, string providerKey)
    {
        var key = GetKey(serviceType, normalizedProviderKey);
        if (ProviderMappings.TryGetValue(key, out var currentImplementationType))
        {
            if (currentImplementationType == implementationType)
                return;
            throw new InvalidOperationException(
                $"Provider Key '{normalizedProviderKey}' 已为服务类型 {serviceType.FullName} 注册实现 {currentImplementationType.FullName}，不能注册 {implementationType.FullName}。");
        }
        ProviderMappings.Add(key, implementationType);
    }

    /// <summary>
    /// 规范化 Provider Key。
    /// </summary>
    /// <param name="providerKey">调用方提供的 Provider Key。</param>
    /// <returns>去除首尾空白后可用于大小写无关映射的 Provider Key。</returns>
    private static string NormalizeProviderKey(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("SQL Provider Key 不能为空。", nameof(providerKey));
        return providerKey.Trim();
    }
}
