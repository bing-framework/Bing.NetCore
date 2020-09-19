using System;
using Bing.Data;
using Bing.Domain.Entities;
using Bing.Uow;

namespace Bing.Domain.Repositories
{
    /// <summary>
    /// 仓储
    /// </summary>
    public interface IRepository
    {
        #region GetUnitOfWork(获取工作单元)

        /// <summary>
        /// 获取工作单元
        /// </summary>
        IUnitOfWork GetUnitOfWork();

        #endregion
    }

    /// <summary>
    /// 仓储
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public interface IRepository<TEntity> : IRepository<TEntity, Guid>
        where TEntity : class, IAggregateRoot, IKey<Guid>
    {
    }

    /// <summary>
    /// 仓储
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public interface IRepository<TEntity, in TKey> : IRepository, IQueryRepository<TEntity, TKey>, IStore<TEntity, TKey>
        where TEntity : class, IAggregateRoot, IKey<TKey>
    {
    }
}
