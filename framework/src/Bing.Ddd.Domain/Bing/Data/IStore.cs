﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bing.Domain.Entities;
using Bing.Validation;

namespace Bing.Data;

/// <summary>
/// 存储器
/// </summary>
/// <typeparam name="TEntity">对象类型</typeparam>
public interface IStore<TEntity> : IStore<TEntity, Guid> where TEntity : class, IKey<Guid>, IVersion { }

/// <summary>
/// 存储器
/// </summary>
/// <typeparam name="TEntity">对象类型</typeparam>
/// <typeparam name="TKey">对象标识类型</typeparam>
public interface IStore<TEntity, in TKey> : IQueryStore<TEntity, TKey>
    where TEntity : class, IKey<TKey>
{
    #region Add(添加)

    /// <summary>
    /// 添加实体
    /// </summary>
    /// <param name="entity">实体</param>
    void Add([Valid] TEntity entity);

    /// <summary>
    /// 添加实体集合
    /// </summary>
    /// <param name="entities">实体集合</param>
    void Add([Valid] IEnumerable<TEntity> entities);

    /// <summary>
    /// 添加实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync([Valid] TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加实体集合
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    #endregion

    #region Update(修改)

    /// <summary>
    /// 修改实体
    /// </summary>
    /// <param name="entity">实体</param>
    void Update([Valid] TEntity entity);

    /// <summary>
    /// 修改实体集合
    /// </summary>
    /// <param name="entities">实体集合</param>
    void Update([Valid] IEnumerable<TEntity> entities);

    /// <summary>
    /// 修改实体
    /// </summary>
    /// <param name="entity">实体</param>
    Task UpdateAsync([Valid] TEntity entity);

    /// <summary>
    /// 修改实体集合
    /// </summary>
    /// <param name="entities">实体集合</param>
    Task UpdateAsync([Valid] IEnumerable<TEntity> entities);

    #endregion

    #region Remove(移除)

    /// <summary>
    /// 移除实体
    /// </summary>
    /// <param name="id">标识</param>
    void Remove(object id);

    /// <summary>
    /// 移除实体
    /// </summary>
    /// <param name="entity">实体</param>
    void Remove(TEntity entity);

    /// <summary>
    /// 移除实体集合
    /// </summary>
    /// <param name="ids">标识集合</param>
    void Remove(IEnumerable<TKey> ids);

    /// <summary>
    /// 移除实体集合
    /// </summary>
    /// <param name="entities">实体集合</param>
    void Remove(IEnumerable<TEntity> entities);

    /// <summary>
    /// 移除实体
    /// </summary>
    /// <param name="id">标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除实体集合
    /// </summary>
    /// <param name="ids">标识集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除实体集合
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    #endregion
}