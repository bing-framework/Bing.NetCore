using System.Data.Common;
using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// 默认 SQL 数据库物理身份解析器。
/// </summary>
public sealed class DefaultSqlDatabaseIdentityResolver : ISqlDatabaseIdentityResolver
{
    /// <summary>
    /// 身份解析贡献者。
    /// </summary>
    private readonly IReadOnlyList<ISqlDatabaseIdentityContributor> _contributors;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlDatabaseIdentityResolver"/>类型的实例。
    /// </summary>
    /// <param name="contributors">身份解析贡献者。</param>
    public DefaultSqlDatabaseIdentityResolver(IEnumerable<ISqlDatabaseIdentityContributor> contributors = null)
    {
        _contributors = contributors?.ToList() ?? new List<ISqlDatabaseIdentityContributor>
        {
            new DefaultSqlDatabaseIdentityContributor()
        };
    }

    /// <inheritdoc />
    public SqlDatabaseIdentity Resolve(DatabaseType databaseType, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("数据库连接字符串不能为空，无法解析物理数据库身份。");
        var contributor = _contributors.FirstOrDefault(item => item is not DefaultSqlDatabaseIdentityContributor &&
            item.CanResolve(databaseType)) ?? _contributors.FirstOrDefault(item => item != null && item.CanResolve(databaseType));
        if (contributor == null)
            throw new NotSupportedException($"数据库类型 {databaseType} 不支持物理数据库身份比较。");
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        return contributor.Resolve(databaseType, builder) ??
            throw new InvalidOperationException($"数据库类型 {databaseType} 的物理身份解析结果不能为空。");
    }
}