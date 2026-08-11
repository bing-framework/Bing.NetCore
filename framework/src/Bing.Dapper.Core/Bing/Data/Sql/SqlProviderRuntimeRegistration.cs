namespace Bing.Data.Sql;

/// <summary>
/// Provider 运行时服务实现注册。
/// </summary>
internal sealed class SqlProviderRuntimeRegistration
{
    /// <summary>
    /// 初始化 Provider 运行时服务实现注册。
    /// </summary>
    /// <param name="providerKey">Provider 唯一标识。</param>
    public SqlProviderRuntimeRegistration(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("SQL Provider Key 不能为空。", nameof(providerKey));
        ProviderKey = providerKey.Trim();
    }

    /// <summary>
    /// Provider 唯一标识。
    /// </summary>
    public string ProviderKey { get; }

    /// <summary>
    /// 服务实现映射。
    /// </summary>
    private readonly Dictionary<Type, Type> _implementations = new();

    /// <summary>
    /// 注册服务实现。
    /// </summary>
    /// <param name="serviceType">服务契约类型。</param>
    /// <param name="implementationType">具体实现类型。</param>
    public void Map(Type serviceType, Type implementationType)
    {
        if (serviceType == null)
            throw new ArgumentNullException(nameof(serviceType));
        if (implementationType == null)
            throw new ArgumentNullException(nameof(implementationType));
        if (serviceType.IsAssignableFrom(implementationType) == false)
            throw new ArgumentException($"类型 {implementationType.FullName} 未实现 {serviceType.FullName}。",
                nameof(implementationType));
        if (_implementations.TryGetValue(serviceType, out var current))
        {
            if (current == implementationType)
                return;
            throw new InvalidOperationException(
                $"Provider Key '{ProviderKey}' 已为服务类型 {serviceType.FullName} 注册实现 {current.FullName}，不能注册 {implementationType.FullName}。");
        }
        _implementations.Add(serviceType, implementationType);
    }

    /// <summary>
    /// 解析服务实现类型。
    /// </summary>
    /// <param name="serviceType">服务契约类型。</param>
    /// <returns>已注册的具体实现类型；未注册时返回 null。</returns>
    public Type Resolve(Type serviceType) => serviceType != null && _implementations.TryGetValue(serviceType,
        out var implementationType) ? implementationType : null;
}