namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 基于已注册 SQL 提供程序创建 Builder 的工厂。
/// </summary>
/// <remarks>
/// 工厂不反向依赖任何具体 Provider 程序集；调用方在组合根注册对应 Provider 与 Builder 创建委托。
/// </remarks>
public sealed class SqlBuilderFactory : ISqlBuilderFactory
{
    /// <summary>
    /// Provider 注册映射。
    /// </summary>
    private readonly IReadOnlyDictionary<string, Func<SqlBuilderServices, ISqlBuilder>> _providers;

    /// <summary>
    /// 初始化一个 <see cref="SqlBuilderFactory"/> 类型的实例。
    /// </summary>
    /// <param name="registrations">Provider Builder 注册项。</param>
    public SqlBuilderFactory(IEnumerable<SqlBuilderFactoryRegistration> registrations)
    {
        if (registrations == null)
            throw new ArgumentNullException(nameof(registrations));
        var providers = new Dictionary<string, Func<SqlBuilderServices, ISqlBuilder>>(StringComparer.OrdinalIgnoreCase);
        foreach (var registration in registrations)
        {
            if (registration == null)
                throw new ArgumentException("SQL Builder 工厂注册项不能为空。", nameof(registrations));
            var providerKey = NormalizeProviderKey(registration.Provider.Key, nameof(registrations));
            if (providers.ContainsKey(providerKey))
                throw new ArgumentException($"SQL Provider Key '{providerKey}' 已注册。", nameof(registrations));
            providers.Add(providerKey, registration.Creator);
        }
        _providers = providers;
    }

    /// <inheritdoc />
    public ISqlBuilder Create(string providerKey)
    {
        var normalizedKey = NormalizeProviderKey(providerKey, nameof(providerKey));
        if (_providers.TryGetValue(normalizedKey, out var creator) == false)
            throw new NotSupportedException($"未注册 Provider Key '{normalizedKey}' 对应的 SQL Builder。");
        return CreateBuilder(creator, normalizedKey, null);
    }

    /// <inheritdoc />
    public ISqlBuilder Create(ISqlProvider provider)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));
        return Create(provider.Key);
    }

    /// <inheritdoc />
    public ISqlBuilder Create(ISqlProvider provider, SqlBuilderServices services)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        var providerKey = NormalizeProviderKey(provider.Key, nameof(provider));
        if (_providers.TryGetValue(providerKey, out var creator) == false)
            throw new NotSupportedException($"未注册 Provider Key '{providerKey}' 对应的 SQL Builder。");
        return CreateBuilder(creator, providerKey, services);
    }

    /// <summary>
    /// 创建并验证 Builder。
    /// </summary>
    /// <param name="creator">Builder 创建委托。</param>
    /// <param name="providerIdentity">Provider 标识。</param>
    /// <param name="services">查询级共享服务；兼容创建时为 null。</param>
    /// <returns>已创建的 Builder。</returns>
    private static ISqlBuilder CreateBuilder(Func<SqlBuilderServices, ISqlBuilder> creator, string providerIdentity,
        SqlBuilderServices services)
    {
        var builder = creator(services);
        if (builder == null)
            throw new InvalidOperationException($"Provider '{providerIdentity}' 的 SQL Builder 创建委托返回了 null。");
        return builder;
    }

    /// <summary>
    /// 规范化 Provider Key。
    /// </summary>
    /// <param name="providerKey">Provider 标识。</param>
    /// <param name="parameterName">发生参数错误时使用的参数名。</param>
    /// <returns>去除首尾空白后的 Provider 标识。</returns>
    private static string NormalizeProviderKey(string providerKey, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("SQL Provider Key 不能为空。", parameterName);
        return providerKey.Trim();
    }
}