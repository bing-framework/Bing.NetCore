using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Domain.Repositories;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Properties;

namespace Bing.Domain.Entities;

/// <summary>
/// 树型路径更新服务
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">实体标识类型</typeparam>
/// <typeparam name="TParentId">父标识类型</typeparam>
public class UpdatePathManager<TEntity, TKey, TParentId>
    where TEntity : class, ITreeEntity<TEntity, TKey, TParentId>
{
    /// <summary>
    /// 仓储
    /// </summary>
    private readonly ITreeCompactRepository<TEntity, TKey, TParentId> _compactRepository;

    /// <summary>
    /// 仓储
    /// </summary>
    private readonly ITreeRepository<TEntity, TKey, TParentId> _repository;

    /// <summary>
    /// 初始化一个<see cref="UpdatePathManager{TEntity,TKey,TParentId}"/>类型的实例
    /// </summary>
    /// <param name="repository">仓储</param>
    public UpdatePathManager(ITreeCompactRepository<TEntity, TKey, TParentId> repository) => _compactRepository = repository;

    /// <summary>
    /// 初始化一个<see cref="UpdatePathManager{TEntity,TKey,TParentId}"/>类型的实例
    /// </summary>
    /// <param name="repository">仓储</param>
    public UpdatePathManager(ITreeRepository<TEntity, TKey, TParentId> repository) => _repository = repository;

    /// <summary>
    /// 更新实体级所有下级节点路径
    /// </summary>
    /// <param name="entity">实体</param>
    public async Task UpdatePathAsync(TEntity entity)
    {
        entity.CheckNotNull(nameof(entity));
        if (entity.ParentId.Equals(entity.Id))
            throw new Warning(LibraryResource.NotSupportMoveToChildren);
        if (_compactRepository != null)
        {
            await UpdatePathByCompactAsync(entity);
            return;
        }

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
    /// 更新路径来源于契约仓储
    /// </summary>
    /// <param name="entity">实体类型</param>
    private async Task UpdatePathByCompactAsync(TEntity entity)
    {
        var old = await _compactRepository.FindByIdNoTrackingAsync(entity.Id);
        if (old == null)
            return;
        if (entity.Path.Equals(old.ParentId))
            return;
        var children = await _compactRepository.GetAllChildrenAsync(entity);
        if (children.Exists(t => t.Id.Equals(entity.ParentId)))
            throw new Warning(LibraryResource.NotSupportMoveToChildren);
        var parent = await _compactRepository.FindAsync(entity.ParentId);
        entity.InitPath(parent);
        await UpdateChildrenPathAsync(entity, children);
        await _compactRepository.UpdateAsync(children);
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