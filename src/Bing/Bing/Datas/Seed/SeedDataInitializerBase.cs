using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Bing.Datas.UnitOfWorks;
using Bing.Domains.Entities;
using Bing.Domains.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Datas.Seed
{
    /// <summary>
    /// 种子数据初始化基类
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">键类型</typeparam>
    public abstract class SeedDataInitializerBase<TEntity, TKey> : ISeedDataInitializer
        where TEntity : class, IAggregateRoot, IKey<TKey>
        where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// 服务提供程序
        /// </summary>
        private readonly IServiceProvider _rootProvider;

        /// <summary>
        /// 种子数据初始化顺序
        /// </summary>
        public virtual int Order => 0;

        /// <summary>
        /// 初始化一个<see cref="SeedDataInitializerBase{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="rootProvider">服务提供程序</param>
        protected SeedDataInitializerBase(IServiceProvider rootProvider) => _rootProvider = rootProvider;

        /// <summary>
        /// 初始化种子数据
        /// </summary>
        public async Task InitializeAsync()
        {
            var entities = SeedData();
            await SyncToDatabaseAsync(entities);
        }

        /// <summary>
        /// 重写以提供要初始化的种子数据
        /// </summary>
        protected abstract TEntity[] SeedData();

        /// <summary>
        /// 重写以提供判断某个实体是否存在的表达式
        /// </summary>
        /// <param name="entity">要判断的实体</param>
        protected abstract Expression<Func<TEntity, bool>> ExistingExpression(TEntity entity);

        /// <summary>
        /// 将种子数据初始化到数据库
        /// </summary>
        /// <param name="entities">实体集合</param>
        protected virtual async Task SyncToDatabaseAsync(TEntity[] entities)
        {
            if (entities == null || entities.Length == 0)
                return;
            using var scope = _rootProvider.CreateScope();
            var scopeProvider = scope.ServiceProvider;
            using var unitOfWork = scopeProvider.GetService<IUnitOfWork>();
            var repository = scopeProvider.GetService<IRepository<TEntity, TKey>>();
            foreach (var entity in entities)
            {
                if (await repository.ExistsAsync(ExistingExpression(entity)))
                    continue;
                await repository.AddAsync(entity);
            }
            await unitOfWork.CommitAsync();
        }
    }
}
