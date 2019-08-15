using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Domains.Repositories;
using Bing.Exceptions;
using Bing.Properties;
using Bing.Utils.Extensions;

namespace Bing.Domains.Entities.Trees
{
    /// <summary>
    /// 树型路径更新服务
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    /// <typeparam name="TParentId">父标识类型</typeparam>
    public class UpdatePathManager<TEntity, TKey, TParentId> where TEntity : class, ITreeEntity<TEntity, TKey, TParentId>
    {
        /// <summary>
        /// 仓储
        /// </summary>
        private readonly ITreeCompactRepository<TEntity, TKey, TParentId> _repository;

        /// <summary>
        /// 初始化一个<see cref="UpdatePathManager{TEntity,TKey,TParentId}"/>类型的实例
        /// </summary>
        /// <param name="repository">仓储</param>
        public UpdatePathManager(ITreeCompactRepository<TEntity, TKey, TParentId> repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// 更新实体级所有下级节点路径
        /// </summary>
        /// <param name="entity">实体</param>
        public async Task UpdatePathAsync(TEntity entity)
        {
            entity.CheckNotNull(nameof(entity));
            if (entity.ParentId.Equals(entity.Id))
                throw new Warning(LibraryResource.NotSupportMoveToChildren);
            var old = await _repository.FindByIdNoTrackingAsync(entity.Id);
            if (old == null)
                return;
            if (entity.Path.Equals(old.ParentId))
                return;
            var children = await _repository.GetAllChildrenAsync(entity);
            if (children.Exists(t => t.Id.Equals(entity.ParentId)))
                throw new Warning(LibraryResource.NotSupportMoveToChildren);
            var parent = await _repository.FindAsync(entity.ParentId);
            entity.InitPath(parent);
            await UpdateChildrenPathAsync(entity, children);
            await _repository.UpdateAsync(children);
        }

        /// <summary>
        /// 更新下级节点路径
        /// </summary>
        /// <param name="parent">父节点</param>
        /// <param name="children">子节点</param>
        private async Task UpdateChildrenPathAsync(TEntity parent, List<TEntity> children)
        {
            if (parent == null || children == null)
                return;
            var list = children.Where(t => t.ParentId.Equals(parent.Id)).ToList();
            foreach (var child in list)
            {
                child.InitPath(parent);
                await UpdateChildrenPathAsync(child, children);
            }
        }
    }
}
