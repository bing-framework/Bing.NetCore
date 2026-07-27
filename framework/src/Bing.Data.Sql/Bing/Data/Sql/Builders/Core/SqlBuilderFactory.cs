using Bing.Data.Enums;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// SQL Builder 工厂注册项。
/// </summary>
public sealed class SqlBuilderFactoryRegistration
{
    /// <summary>
    /// 初始化一个 <see cref="SqlBuilderFactoryRegistration"/> 类型的实例。
    /// </summary>
    /// <param name="provider">SQL 提供程序。</param>
    /// <param name="creator">Builder 创建委托。</param>
    public SqlBuilderFactoryRegistration(ISqlProvider provider, Func<ISqlBuilder> creator)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Creator = creator ?? throw new ArgumentNullException(nameof(creator));
    }

    /// <summary>
    /// SQL 提供程序。
    /// </summary>
    public ISqlProvider Provider { get; }

    /// <summary>
    /// Builder 创建委托。
    /// </summary>
    public Func<ISqlBuilder> Creator { get; }
}

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
    private readonly IReadOnlyDictionary<ISqlProvider, Func<ISqlBuilder>> _providers;

    /// <summary>
    /// 数据库类型注册映射。
    /// </summary>
    private readonly IReadOnlyDictionary<DatabaseType, Func<ISqlBuilder>> _databaseTypes;

    /// <summary>
    /// 初始化一个 <see cref="SqlBuilderFactory"/> 类型的实例。
    /// </summary>
    /// <param name="registrations">Provider Builder 注册项。</param>
    public SqlBuilderFactory(IEnumerable<SqlBuilderFactoryRegistration> registrations)
    {
        if (registrations == null)
            throw new ArgumentNullException(nameof(registrations));
        var providers = new Dictionary<ISqlProvider, Func<ISqlBuilder>>();
        var databaseTypes = new Dictionary<DatabaseType, Func<ISqlBuilder>>();
        foreach (var registration in registrations)
        {
            if (registration == null)
                throw new ArgumentException("SQL Builder 工厂注册项不能为空。", nameof(registrations));
            if (providers.ContainsKey(registration.Provider))
                throw new ArgumentException("同一 SQL Provider 只能注册一个 Builder 创建委托。", nameof(registrations));
            if (databaseTypes.ContainsKey(registration.Provider.DatabaseType))
                throw new ArgumentException("同一数据库类型只能注册一个 SQL Builder。", nameof(registrations));
            providers.Add(registration.Provider, registration.Creator);
            databaseTypes.Add(registration.Provider.DatabaseType, registration.Creator);
        }
        _providers = providers;
        _databaseTypes = databaseTypes;
    }

    /// <inheritdoc />
    public ISqlBuilder Create(ISqlProvider provider)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));
        if (_providers.TryGetValue(provider, out var creator) == false)
            throw new NotSupportedException($"未注册 SQL Provider '{provider.GetType().FullName}' 对应的 Builder 创建委托。");
        return CreateBuilder(creator, provider.DatabaseType);
    }

    /// <inheritdoc />
    public ISqlBuilder Create(DatabaseType databaseType)
    {
        if (_databaseTypes.TryGetValue(databaseType, out var creator) == false)
            throw new NotSupportedException($"未注册数据库类型 '{databaseType}' 对应的 SQL Builder。");
        return CreateBuilder(creator, databaseType);
    }

    /// <summary>
    /// 创建并验证 Builder。
    /// </summary>
    /// <param name="creator">Builder 创建委托。</param>
    /// <param name="databaseType">目标数据库类型。</param>
    /// <returns>已创建的 Builder。</returns>
    private static ISqlBuilder CreateBuilder(Func<ISqlBuilder> creator, DatabaseType databaseType)
    {
        var builder = creator();
        if (builder == null)
            throw new InvalidOperationException($"数据库类型 '{databaseType}' 的 SQL Builder 创建委托返回了 null。");
        return builder;
    }
}