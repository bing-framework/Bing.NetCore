using Bing.Data.Enums;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 默认 SQL Provider 解析器。
/// </summary>
public sealed class DefaultSqlProviderResolver : ISqlProviderResolver
{
    /// <summary>
    /// 已注册的 Provider Key 映射。
    /// </summary>
    private readonly IReadOnlyDictionary<string, ISqlProvider> _providers;

    /// <summary>
    /// 官方 Provider 的数据库类型兼容映射。
    /// </summary>
    private static readonly IReadOnlyDictionary<DatabaseType, string> OfficialProviderKeys =
        new Dictionary<DatabaseType, string>
        {
            [DatabaseType.MySql] = "bing.mysql",
            [DatabaseType.Doris] = "bing.mysql",
            [DatabaseType.PgSql] = "bing.postgresql",
            [DatabaseType.SqlServer] = "bing.sqlserver",
            [DatabaseType.Sqlite] = "bing.sqlite",
            [DatabaseType.Oracle] = "bing.oracle"
        };

    /// <summary>
    /// 初始化一个 <see cref="DefaultSqlProviderResolver"/> 类型的实例。
    /// </summary>
    /// <param name="providers">已注册的 SQL Provider。</param>
    public DefaultSqlProviderResolver(IEnumerable<ISqlProvider> providers)
    {
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        var result = new Dictionary<string, ISqlProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            if (provider == null)
                throw new ArgumentException("SQL Provider 注册项不能为空。", nameof(providers));
            var key = NormalizeProviderKey(provider.Key, nameof(providers));
            if (result.ContainsKey(key))
                throw new ArgumentException($"SQL Provider Key '{key}' 已注册。", nameof(providers));
            result.Add(key, provider);
        }
        _providers = result;
    }

    /// <inheritdoc />
    public ISqlProvider Resolve(string providerKey)
    {
        var key = NormalizeProviderKey(providerKey, nameof(providerKey));
        if (_providers.TryGetValue(key, out var provider))
            return provider;
        throw new NotSupportedException($"未注册 Provider Key '{key}' 对应的 SQL Provider。");
    }

    /// <inheritdoc />
    public bool TryResolve(string providerKey, out ISqlProvider provider)
    {
        provider = null;
        if (string.IsNullOrWhiteSpace(providerKey))
            return false;
        return _providers.TryGetValue(providerKey.Trim(), out provider);
    }

    /// <inheritdoc />
    public ISqlProvider Resolve(DatabaseContext context, ISqlProvider provider = null, DatabaseType? databaseType = null)
    {
        var dataSourceProviderKey = context?.DataSource?.ProviderKey;
        if (string.IsNullOrWhiteSpace(dataSourceProviderKey) == false)
            return Resolve(dataSourceProviderKey);
        if (string.IsNullOrWhiteSpace(context?.ProviderKey) == false)
            return Resolve(context.ProviderKey);
        if (provider != null)
            return Resolve(provider.Key);
        var resolvedDatabaseType = context?.DataSource?.DatabaseType ?? databaseType;
        if (resolvedDatabaseType != null && OfficialProviderKeys.TryGetValue(resolvedDatabaseType.Value, out var key))
            return Resolve(key);
        var databaseTypeText = resolvedDatabaseType?.ToString() ?? "<未指定>";
        throw new NotSupportedException($"未能解析数据库类型 '{databaseTypeText}' 对应的官方 SQL Provider。请为数据源配置 {nameof(SqlDataSourceDescriptor.ProviderKey)}。");
    }

    /// <summary>
    /// 规范化 Provider Key。
    /// </summary>
    /// <param name="providerKey">Provider Key。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>移除首尾空白后的 Provider Key。</returns>
    private static string NormalizeProviderKey(string providerKey, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("SQL Provider Key 不能为空。", parameterName);
        return providerKey.Trim();
    }
}