using System;
using Bing.Data.Stores;
using Bing.Domain.Entities;
using IUnitOfWork = Bing.Uow.IUnitOfWork;

namespace Bing.Domain.Repositories
{
    /// <summary>
    /// 仓储
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public abstract class RepositoryBase<TEntity> : RepositoryBase<TEntity, Guid>, IRepository<TEntity>
        where TEntity : class, IAggregateRoot<TEntity, Guid>
    {
        /// <summary>
        /// 初始化一个<see cref="RepositoryBase{TEntity}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        protected RepositoryBase(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }
    }

    /// <summary>
    /// 仓储
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract class RepositoryBase<TEntity, TKey> : StoreBase<TEntity, TKey>, IRepository<TEntity, TKey>
        where TEntity : class, IAggregateRoot<TEntity, TKey>
    {
        /// <summary>
        /// 初始化一个<see cref="RepositoryBase{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        protected RepositoryBase(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        /// <summary>
        /// 获取工作单元
        /// </summary>
        public IUnitOfWork GetUnitOfWork() => UnitOfWork;
    }
}
