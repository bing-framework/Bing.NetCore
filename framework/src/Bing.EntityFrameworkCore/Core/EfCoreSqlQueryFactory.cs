using Bing.Data.Sql;
using Bing.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// EF Core SQL 查询工厂
/// </summary>
public sealed class EfCoreSqlQueryFactory : IEfCoreSqlQueryFactory
{
    /// <summary>
    /// 初始化一个<see cref="EfCoreSqlQueryFactory"/>类型的实例
    /// </summary>
    public EfCoreSqlQueryFactory()
    {
    }

    /// <inheritdoc />
    public ISqlQuery Create(UnitOfWorkBase unitOfWork, EfCoreSqlConnectionMode mode = EfCoreSqlConnectionMode.Shared)
    {
        if (unitOfWork == null)
            throw new ArgumentNullException(nameof(unitOfWork));
        var query = CreateQuery();
        if (mode == EfCoreSqlConnectionMode.Independent)
            return query;
        query.SetConnection(unitOfWork.Database.GetDbConnection());
        var transaction = unitOfWork.Database.CurrentTransaction?.GetDbTransaction();
        if (transaction != null)
            query.SetTransaction(transaction);
        return query;
    }

    /// <summary>
    /// 创建 SQL 查询对象
    /// </summary>
    private ISqlQuery CreateQuery()
    {
        var query = ServiceLocator.Instance.GetService<ISqlQuery>();
        if (query == null)
            throw new InvalidOperationException("未注册 ISqlQuery，无法创建 EF Core SQL 查询对象");
        return query;
    }
}