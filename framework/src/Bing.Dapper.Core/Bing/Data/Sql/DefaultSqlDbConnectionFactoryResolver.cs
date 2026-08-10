namespace Bing.Data.Sql;

/// <summary>
/// 默认 SQL Provider 独立连接工厂解析器。
/// </summary>
internal sealed class DefaultSqlDbConnectionFactoryResolver : ISqlDbConnectionFactoryResolver
{
    /// <summary>
    /// 按 Provider Key 保存的连接工厂。
    /// </summary>
    private readonly IReadOnlyDictionary<string, Func<string, IDbConnection>> _factories;

    /// <summary>
    /// 初始化一个 <see cref="DefaultSqlDbConnectionFactoryResolver"/> 类型的实例。
    /// </summary>
    /// <param name="registrations">连接工厂注册项</param>
    public DefaultSqlDbConnectionFactoryResolver(IEnumerable<SqlDbConnectionFactoryRegistration> registrations)
    {
        var result = new Dictionary<string, Func<string, IDbConnection>>(StringComparer.OrdinalIgnoreCase);
        foreach (var registration in (registrations ?? Array.Empty<SqlDbConnectionFactoryRegistration>())
            .Where(registration => registration?.Factory != null &&
                string.IsNullOrWhiteSpace(registration.ProviderKey) == false))
        {
            var providerKey = registration.ProviderKey.Trim();
            if (result.ContainsKey(providerKey))
                throw new InvalidOperationException($"Provider Key '{providerKey}' 的独立连接工厂重复注册。");
            result.Add(providerKey, registration.Factory);
        }
        _factories = result;
    }

    /// <inheritdoc />
    public IDbConnection Create(string providerKey, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("SQL Provider Key 不能为空。", nameof(providerKey));
        var normalizedKey = providerKey.Trim();
        if (_factories.TryGetValue(normalizedKey, out var factory) == false)
            throw new InvalidOperationException($"未注册 Provider Key '{normalizedKey}' 的独立连接工厂。");
        return factory(connectionString) ??
            throw new InvalidOperationException($"Provider Key '{normalizedKey}' 的独立连接工厂返回了空连接。");
    }
}
