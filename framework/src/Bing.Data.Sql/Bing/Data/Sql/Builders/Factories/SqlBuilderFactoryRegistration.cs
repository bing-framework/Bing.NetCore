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
    /// <param name="creator">使用查询级共享服务创建 Builder 的委托。</param>
    public SqlBuilderFactoryRegistration(ISqlProvider provider, Func<SqlBuilderServices, ISqlBuilder> creator)
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
    public Func<SqlBuilderServices, ISqlBuilder> Creator { get; }
}