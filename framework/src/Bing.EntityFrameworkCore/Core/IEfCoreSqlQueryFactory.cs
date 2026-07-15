using Bing.Data.Sql;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// EF Core SQL 查询工厂
/// </summary>
public interface IEfCoreSqlQueryFactory
{
    /// <summary>
    /// 创建 SQL 查询对象
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="mode">连接模式</param>
    /// <param name="dbKey">数据源标识</param>
    /// <returns>SQL 查询对象</returns>
    ISqlQuery Create(UnitOfWorkBase unitOfWork, EfCoreSqlConnectionMode mode = EfCoreSqlConnectionMode.Shared,
        string dbKey = null);
}