namespace Bing.Data.Sql;

/// <summary>
/// Sql 查询对象工厂
/// </summary>
public interface ISqlQueryFactory
{
    /// <summary>
    /// 创建 Sql 查询对象
    /// </summary>
    /// <param name="dbKey">数据库键</param>
    /// <returns>Sql 查询对象</returns>
    ISqlQuery Create(string dbKey = null);
}