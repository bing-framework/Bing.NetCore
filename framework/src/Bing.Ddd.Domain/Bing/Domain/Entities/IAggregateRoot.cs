﻿namespace Bing.Domain.Entities;

/// <summary>
/// 聚合根
/// </summary>
public interface IAggregateRoot : IEntity;

/// <summary>
/// 聚合根
/// </summary>
/// <typeparam name="TKey">标识类型</typeparam>
public interface IAggregateRoot<out TKey> : IEntity<TKey>, IAggregateRoot;

/// <summary>
/// 聚合根
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">标识类型</typeparam>
public interface IAggregateRoot<in TEntity, out TKey> : IEntity<TEntity, TKey>, IAggregateRoot<TKey>
    where TEntity : IAggregateRoot;
