using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 查询对象工厂
/// </summary>
public interface ISqlQueryFactory
{
    /// <summary>
    /// 创建 Sql 查询对象
    /// </summary>
    /// <typeparam name="TQuery">Sql 查询对象类型</typeparam>
    /// <param name="dbKey">数据库键</param>
    /// <param name="databaseType">数据库类型</param>
    /// <param name="role">数据库角色</param>
    /// <returns>Sql 查询对象</returns>
    TQuery Create<TQuery>(string dbKey, DatabaseType databaseType, DatabaseRole role = DatabaseRole.Default)
        where TQuery : class, ISqlQuery;

    /// <summary>
    /// 基于当前数据库上下文创建 Sql 查询对象
    /// </summary>
    /// <typeparam name="TQuery">Sql 查询对象类型</typeparam>
    /// <returns>Sql 查询对象</returns>
    TQuery Create<TQuery>() where TQuery : class, ISqlQuery;
}