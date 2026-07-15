using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据库连接工厂注册项
/// </summary>
public sealed class SqlDbConnectionFactoryRegistration
{
    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; init; }

    /// <summary>
    /// 连接创建委托
    /// </summary>
    public Func<string, IDbConnection> Factory { get; init; }
}

/// <summary>
/// 默认 SQL 数据库连接工厂解析器
/// </summary>
public sealed class DefaultSqlDbConnectionFactoryResolver : ISqlDbConnectionFactoryResolver
{
    /// <summary>
    /// 已注册的连接工厂
    /// </summary>
    private readonly IReadOnlyDictionary<DatabaseType, Func<string, IDbConnection>> _factories;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlDbConnectionFactoryResolver"/>类型的实例
    /// </summary>
    /// <param name="registrations">连接工厂注册项</param>
    public DefaultSqlDbConnectionFactoryResolver(IEnumerable<SqlDbConnectionFactoryRegistration> registrations)
    {
        _factories = (registrations ?? Array.Empty<SqlDbConnectionFactoryRegistration>())
            .Where(t => t?.Factory != null)
            .GroupBy(t => t.DatabaseType)
            .ToDictionary(t => t.Key, t => t.Last().Factory);
    }

    /// <inheritdoc />
    public IDbConnection Create(DatabaseType databaseType, string connectionString)
    {
        if (_factories.TryGetValue(databaseType, out var factory) == false)
            throw new InvalidOperationException($"未注册数据库类型 {databaseType} 的独立连接工厂。");
        return factory(connectionString) ?? throw new InvalidOperationException($"数据库类型 {databaseType} 的独立连接工厂返回了空连接。");
    }
}
