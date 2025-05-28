using System.Data.Common;
using System.Linq.Expressions;
using Bing.Data;
using Bing.Data.Sql;
using Bing.DependencyInjection;
using Bing.Domain.Entities;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// 存储器
/// </summary>
/// <typeparam name="TEntity">对象类型</typeparam>
public abstract class StoreBase<TEntity> : StoreBase<TEntity, Guid>, IStore<TEntity>
    where TEntity : class, IKey<Guid>
{
    /// <summary>
    /// 初始化一个<see cref="StoreBase{TEntity}"/>类型的实例
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    protected StoreBase(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}

/// <summary>
/// 存储器
/// </summary>
/// <typeparam name="TEntity">对象类型</typeparam>
/// <typeparam name="TKey">对象标识类型</typeparam>
public abstract class StoreBase<TEntity, TKey> : IStore<TEntity, TKey> where TEntity : class, IKey<TKey>
{
    #region 字段

    /// <summary>
    /// 是否已释放
    /// </summary>
    private bool _disposed;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="StoreBase{TEntity,TKey}"/>类型的实例
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    protected StoreBase(IUnitOfWork unitOfWork) => UnitOfWork = (UnitOfWorkBase)unitOfWork;

    #endregion

    #region 属性

    /// <summary>
    /// 工作单元
    /// </summary>
    protected UnitOfWorkBase UnitOfWork { get; }

    /// <summary>
    /// 实体集
    /// </summary>
    protected DbSet<TEntity> Set => UnitOfWork.Set<TEntity>();

    /// <summary>
    /// 查询时是否跟踪对象
    /// </summary>
    protected bool IsTracking { get; private set; } = true;

    /// <summary>
    /// Sql查询对象
    /// </summary>
    private ISqlQuery _sqlQuery;

    /// <summary>
    /// Sql查询对象
    /// </summary>
    protected virtual ISqlQuery Sql => _sqlQuery ??= CreateSqlQuery();

    /// <summary>
    /// 创建Sql查询对象
    /// </summary>
    protected virtual ISqlQuery CreateSqlQuery()
    {
        var result = ServiceLocator.Instance.GetService<ISqlQuery>();
        result.SetConnection(Connection);
        return result;
    }

    /// <summary>
    /// 数据库连接
    /// </summary>
    protected DbConnection Connection => UnitOfWork.Database.GetDbConnection();

    #endregion

    #region Find(查找实体)

    /// <inheritdoc />
    public IQueryable<TEntity> FindAsNoTracking() => Set.AsNoTracking();

    /// <inheritdoc />
    public IQueryable<TEntity> Find()
    {
        ThrowIfDisposed();
        return Set;
    }

    /// <inheritdoc />
    public IQueryable<TEntity> Find(ICondition<TEntity> criteria) => Find().Where(criteria);

    /// <inheritdoc />
    public IQueryable<TEntity> Find(Expression<Func<TEntity, bool>> predicate) => Find().Where(predicate);

    #endregion

    #region FindById(通过标识查找实体)

    /// <inheritdoc />
    [Obsolete("请使用 FindById 方法")]
    public virtual TEntity Find(object id) => id.SafeString().IsEmpty() ? null : Set.Find(id);

    /// <inheritdoc />
    public virtual TEntity FindById(object id) => id.SafeString().IsEmpty() ? null : Set.Find(id);

    #endregion

    #region FindByIdAsync(通过标识查找实体)

    /// <inheritdoc />
    [Obsolete("请使用 FindByIdAsync 方法")]
    public virtual async Task<TEntity> FindAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id.SafeString().IsEmpty())
            return null;
        return await Set.FindAsync(new[] { id }, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity> FindByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (id.SafeString().IsEmpty())
            return null;
        return await Set.FindAsync(new[] { id }, cancellationToken);
    }

    #endregion

    #region FindByIds(通过标识列表查找实体列表)

    /// <inheritdoc />
    public virtual List<TEntity> FindByIds(params TKey[] ids) => FindByIds((IEnumerable<TKey>)ids);

    /// <inheritdoc />
    public virtual List<TEntity> FindByIds(IEnumerable<TKey> ids)
    {
        return ids == null ? null : Find(t => ids.Contains(t.Id)).ToList();
    }

    /// <inheritdoc />
    public virtual List<TEntity> FindByIds(string ids)
    {
        return FindByIds(Conv.ToList<TKey>(ids));
    }

    #endregion

    #region FindByIdsAsync(通过标识列表查找实体列表)

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsAsync(params TKey[] ids) => await FindByIdsAsync((IEnumerable<TKey>)ids);

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ids == null ? null : await Find(t => ids.Contains(t.Id)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsAsync(string ids, CancellationToken cancellationToken = default)
    {
        return await FindByIdsAsync(Conv.ToList<TKey>(ids), cancellationToken);
    }

    #endregion

    #region FindByIdNoTracking(通过标识查找实体，不跟踪)

    /// <inheritdoc />
    public virtual TEntity FindByIdNoTracking(TKey id)
    {
        var entities = FindByIdsNoTracking(id);
        return entities == null || entities.Count == 0 ? null : entities[0];
    }

    #endregion

    #region FindByIdsNoTracking(通过标识列表查找实体列表，不跟踪)

    /// <inheritdoc />
    public virtual List<TEntity> FindByIdsNoTracking(params TKey[] ids) => FindByIdsNoTracking((IEnumerable<TKey>)ids);

    /// <inheritdoc />
    public virtual List<TEntity> FindByIdsNoTracking(IEnumerable<TKey> ids)
    {
        return ids == null ? null : FindAsNoTracking().Where(t => ids.Contains(t.Id)).ToList();
    }

    /// <inheritdoc />
    public virtual List<TEntity> FindByIdsNoTracking(string ids)
    {
        return FindByIdsNoTracking(Conv.ToList<TKey>(ids));
    }

    #endregion

    #region FindByIdNoTrackingAsync(通过标识查找实体，不跟踪)

    /// <inheritdoc />
    public virtual async Task<TEntity> FindByIdNoTrackingAsync(TKey id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entities = await FindByIdsNoTrackingAsync(id);
        return entities == null || entities.Count == 0 ? null : entities[0];
    }

    #endregion

    #region FindByIdsNoTrackingAsync(通过标识列表查找实体列表，不跟踪)

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsNoTrackingAsync(params TKey[] ids) => await FindByIdsNoTrackingAsync((IEnumerable<TKey>)ids);

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsNoTrackingAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ids == null ? null : await FindAsNoTracking().Where(t => ids.Contains(t.Id)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsNoTrackingAsync(string ids, CancellationToken cancellationToken = default)
    {
        return await FindByIdsNoTrackingAsync(Conv.ToList<TKey>(ids), cancellationToken);
    }

    #endregion

    #region Single(查找单个实体)

    /// <inheritdoc />
    public virtual TEntity Single(Expression<Func<TEntity, bool>> predicate) => Find().FirstOrDefault(predicate);

    /// <inheritdoc />
    public virtual TEntity Single(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>> action)
    {
        if (action == null)
            return Single(predicate);
        return action(Find()).FirstOrDefault(predicate);
    }

    #endregion

    #region SingleAsync(查找单个实体)

    /// <inheritdoc />
    public virtual async Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Find().FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>> action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (action == null)
            return await SingleAsync(predicate, cancellationToken);
        return await action(Find()).FirstOrDefaultAsync(predicate, cancellationToken);
    }

    #endregion

    #region FindAll(查找实体列表)

    /// <inheritdoc />
    public virtual List<TEntity> FindAll(Expression<Func<TEntity, bool>> predicate = null)
    {
        return predicate == null ? Find().ToList() : Find(predicate).ToList();
    }

    #endregion

    #region FindAllAsync(查找实体列表)

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindAllAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (predicate == null)
            return await Find().ToListAsync(cancellationToken);
        return await Find(predicate).ToListAsync(cancellationToken);
    }

    #endregion

    #region FindAllNoTracking(查找实体列表，不跟踪)

    /// <inheritdoc />
    public virtual List<TEntity> FindAllNoTracking(Expression<Func<TEntity, bool>> predicate = null)
    {
        return predicate == null ? FindAsNoTracking().ToList() : FindAsNoTracking().Where(predicate).ToList();
    }

    #endregion

    #region FindAllNoTrackingAsync(查找实体列表，不跟踪)

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindAllNoTrackingAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (predicate == null)
            return await FindAsNoTracking().ToListAsync(cancellationToken);
        return await FindAsNoTracking().Where(predicate).ToListAsync(cancellationToken);
    }

    #endregion

    #region Exists(判断是否存在)

    /// <inheritdoc />
    public bool Exists(TKey id)
    {
        if (id.SafeString().IsEmpty())
            return false;
        return Exists(t => Equals(id, t.Id));
    }

    /// <inheritdoc />
    public virtual bool Exists(TKey[] ids)
    {
        return ids != null && Exists(t => ids.Contains(t.Id));
    }

    /// <inheritdoc />
    public virtual bool Exists(Expression<Func<TEntity, bool>> predicate)
    {
        return predicate != null && Find().Any(predicate);
    }

    #endregion

    #region ExistsAsync(判断是否存在)

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(TKey id)
    {
        if (id.SafeString().IsEmpty())
            return false;
        return await ExistsAsync(t => Equals(id, t.Id));
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(params TKey[] ids)
    {
        return ids != null && await ExistsAsync(t => ids.Contains(t.Id));
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return predicate != null && await Find().AnyAsync(predicate, cancellationToken);
    }

    #endregion

    #region Count(查找数量)

    /// <inheritdoc />
    public virtual int Count(Expression<Func<TEntity, bool>> predicate = null)
    {
        return predicate == null ? Find().Count() : Find().Count(predicate);
    }

    #endregion

    #region CountAsync(查找数量)

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return predicate == null
            ? await Find().CountAsync(cancellationToken)
            : await Find().CountAsync(predicate, cancellationToken);
    }

    #endregion

    #region Add(添加实体)

    /// <inheritdoc />
    public virtual void Add(TEntity entity)
    {
        ThrowIfDisposed();
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        Set.Add(entity);
    }

    /// <inheritdoc />
    public virtual void Add(IEnumerable<TEntity> entities)
    {
        ThrowIfDisposed();
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        Set.AddRange(entities);
    }

    #endregion

    #region AddAsync(添加实体)

    /// <inheritdoc />
    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        await Set.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task AddAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        await Set.AddRangeAsync(entities, cancellationToken);
    }

    #endregion

    #region Update(修改实体)

    /// <summary>
    /// 修改实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <exception cref="ArgumentNullException"></exception>
    public virtual void Update(TEntity entity)
    {
        ThrowIfDisposed();
        entity.CheckNull(nameof(entity));
        var entry = UnitOfWork.Entry(entity);
        ValidateVersion(entry, entity);
        UpdateEntity(entry, entity);
    }

    /// <summary>
    /// 验证版本号
    /// </summary>
    /// <param name="entry">输入实体</param>
    /// <param name="entity">实体</param>
    protected void ValidateVersion(EntityEntry<TEntity> entry, TEntity entity)
    {
        if (entity is not IVersion current)
            return;
        if (current.Version == null || current.Version.Length == 0)
        {
            ThrowConcurrencyException(entity);
            return;
        }

        var oldVersion = entry.OriginalValues.GetValue<byte[]>(nameof(IVersion.Version));
        for (var i = 0; i < oldVersion.Length; i++)
        {
            if (current.Version[i] != oldVersion[i])
                ThrowConcurrencyException(entity);
        }
    }

    /// <summary>
    /// 抛出并发异常
    /// </summary>
    /// <param name="entity">实体</param>
    /// <exception cref="ConcurrencyException"></exception>
    private static void ThrowConcurrencyException(TEntity entity) => throw new ConcurrencyException($"Type:{typeof(TEntity)}, Id:{entity.Id}");

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="entry">输入实体</param>
    /// <param name="entity">实体</param>
    protected void UpdateEntity(EntityEntry<TEntity> entry, TEntity entity)
    {
        var oldEntry = UnitOfWork.ChangeTracker.Entries<TEntity>().FirstOrDefault(x => x.Entity.Equals(entity));
        if (oldEntry != null)
        {
            oldEntry.CurrentValues.SetValues(entity);
            return;
        }
        if (entry.State == EntityState.Detached)
            UnitOfWork.Update(entity);
    }

    /// <summary>
    /// 修改实体集合
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <exception cref="ArgumentNullException"></exception>
    public virtual void Update(IEnumerable<TEntity> entities)
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        foreach (var entity in entities)
            Update(entity);
    }

    #endregion

    #region UpdateAsync(修改实体)

    /// <inheritdoc />
    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Update(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task UpdateAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        foreach (var entity in entities)
            Update(entity);
        return Task.CompletedTask;
    }

    #endregion

    #region Remove(移除实体)

    /// <inheritdoc />
    public virtual void Remove(object id)
    {
        ThrowIfDisposed();
        var entity = FindById(id);
        Delete(entity);
    }

    /// <inheritdoc />
    public virtual void Remove(TEntity entity)
    {
        if (entity == null)
            return;
        Remove(entity.Id);
    }

    /// <inheritdoc />
    public virtual void Remove(IEnumerable<TKey> ids)
    {
        ThrowIfDisposed();
        if (ids == null)
            return;
        var list = FindByIds(ids);
        Delete(list);
    }

    /// <inheritdoc />
    public virtual void Remove(IEnumerable<TEntity> entities)
    {
        if (entities == null)
            return;
        Remove(entities.Select(t => t.Id));
    }

    /// <summary>
    /// 删除
    /// </summary>
    /// <param name="entity">实体</param>
    private void Delete(TEntity entity)
    {
        if (entity == null)
            return;
        if (entity is ISoftDelete model)
        {
            model.IsDeleted = true;
            return;
        }

        Set.Remove(entity);
    }

    /// <summary>
    /// 删除实体集合
    /// </summary>
    /// <param name="list">实体集合</param>
    private void Delete(List<TEntity> list)
    {
        if (list == null)
            return;
        if (!list.Any())
            return;
        if (list[0] is ISoftDelete)
        {
            foreach (var entity in list.Select(t => (ISoftDelete)t))
                entity.IsDeleted = true;
            return;
        }
        Set.RemoveRange(list);
    }

    #endregion

    #region RemoveAsync(移除实体)

    /// <inheritdoc />
    public virtual async Task RemoveAsync(object id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var entity = await FindByIdAsync(id, cancellationToken);
        Delete(entity);
    }

    /// <inheritdoc />
    public virtual async Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity == null)
            return;
        await RemoveAsync(entity.Id, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task RemoveAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (ids == null)
            return;
        var entities = await FindByIdsAsync(ids, cancellationToken);
        Delete(entities);
    }

    /// <inheritdoc />
    public virtual async Task RemoveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        if (entities == null)
            return;
        await RemoveAsync(entities.Select(t => t.Id), cancellationToken);
    }

    #endregion

    #region ThrowIfDisposed(已释放则抛出异常)

    /// <summary>
    /// 已释放则抛出异常
    /// </summary>
    /// <exception cref="ObjectDisposedException"></exception>
    protected void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    #endregion

    #region Dispose(释放)

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
    }

    #endregion
}
