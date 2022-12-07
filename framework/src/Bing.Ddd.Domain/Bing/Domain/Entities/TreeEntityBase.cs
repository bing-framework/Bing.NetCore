﻿using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Validation;

namespace Bing.Domain.Entities;

/// <summary>
/// 树型实体
/// </summary>
/// <typeparam name="TEntity">树型实体类型</typeparam>
public abstract class TreeEntityBase<TEntity> : TreeEntityBase<TEntity, Guid, Guid?>
    where TEntity : class, ITreeEntity<TEntity, Guid, Guid?>, IVerifyModel<TEntity>
{
    /// <summary>
    /// 初始化一个<see cref="TreeEntityBase{TEntity}"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    /// <param name="path">路径</param>
    /// <param name="level">级数</param>
    protected TreeEntityBase(Guid id, string path, int level) : base(id, path, level)
    {
    }
}

/// <summary>
/// 树型实体
/// </summary>
/// <typeparam name="TEntity">树型实体类型</typeparam>
/// <typeparam name="TKey">标识类型</typeparam>
/// <typeparam name="TParentId">父编号类型</typeparam>
public abstract class TreeEntityBase<TEntity, TKey, TParentId> : AggregateRoot<TEntity, TKey>,
    ITreeEntity<TEntity, TKey, TParentId>
    where TEntity : class, ITreeEntity<TEntity, TKey, TParentId>, IVerifyModel<TEntity>
{
    /// <summary>
    /// 父标识
    /// </summary>
    public TParentId ParentId { get; set; }

    /// <summary>
    /// 路径
    /// </summary>
    public string Path { get; protected set; }

    /// <summary>
    /// 级数
    /// </summary>
    public int Level { get; protected set; }

    /// <summary>
    /// 排序号
    /// </summary>
    public int? SortId { get; set; }

    /// <summary>
    /// 启用
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 初始化一个<see cref="TreeEntityBase{TEntity,TKey,TParentId}"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    /// <param name="path">路径</param>
    /// <param name="level">级数</param>
    protected TreeEntityBase(TKey id, string path, int level) : base(id)
    {
        Path = path;
        Level = level;
    }

    /// <summary>
    /// 初始化路径
    /// </summary>
    public virtual void InitPath() => InitPath(default(TEntity));

    /// <summary>
    /// 初始化路径
    /// </summary>
    /// <param name="parent">父节点</param>
    public void InitPath(TEntity parent)
    {
        if (Equals(parent, null))
        {
            Level = 1;
            Path = $"{Id},";
            return;
        }

        Level = parent.Level + 1;
        Path = $"{parent.Path}{Id},";
    }

    /// <summary>
    /// 从路径中获取所有上级节点编号
    /// </summary>
    /// <param name="excludeSelf">是否排除当前节点，默认排除自身</param>
    public List<TKey> GetParentIdsFromPath(bool excludeSelf = true)
    {
        if (string.IsNullOrWhiteSpace(Path))
            return new List<TKey>();
        var result = Path.Split(',').Where(id => !string.IsNullOrWhiteSpace(id) && id != ",").ToList();
        if (excludeSelf)
            result = result.Where(id => id.SafeString().ToLower() != Id.SafeString().ToLower()).ToList();
        return result.Select(Conv.To<TKey>).ToList();
    }
}