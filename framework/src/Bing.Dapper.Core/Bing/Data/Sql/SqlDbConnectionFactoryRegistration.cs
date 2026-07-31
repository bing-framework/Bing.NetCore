namespace Bing.Data.Sql;

/// <summary>
/// SQL Provider 独立连接工厂注册项。
/// </summary>
public sealed class SqlDbConnectionFactoryRegistration
{
    /// <summary>
    /// SQL Provider 唯一标识。
    /// </summary>
    public string ProviderKey { get; init; }

    /// <summary>
    /// 根据数据源连接字符串创建对应 Provider 独立 <see cref="IDbConnection"/> 的委托。
    /// </summary>
    public Func<string, IDbConnection> Factory { get; init; }
}