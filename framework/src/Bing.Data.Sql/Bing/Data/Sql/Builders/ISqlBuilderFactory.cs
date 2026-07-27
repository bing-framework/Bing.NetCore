using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL Builder 工厂。
/// </summary>
public interface ISqlBuilderFactory
{
    /// <summary>
    /// 根据 Provider 唯一标识创建 Builder。
    /// </summary>
    ISqlBuilder Create(string providerKey);

    /// <summary>
    /// 根据 SQL 提供程序创建 Builder。
    /// </summary>
    ISqlBuilder Create(ISqlProvider provider);

    /// <summary>
    /// 根据 SQL 提供程序和查询级共享服务创建 Builder。
    /// </summary>
    ISqlBuilder Create(ISqlProvider provider, SqlBuilderServices services);

    /// <summary>
    /// 根据数据库类型创建 Builder。
    /// </summary>
    ISqlBuilder Create(DatabaseType databaseType);
}