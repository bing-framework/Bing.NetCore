using System.Data.Common;
using System.Linq.Expressions;
using Bing.Data;
using Bing.Data.Sql;
using System.Threading;
using Bing.Domain.Entities;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// 提供基于 EF Core 的实体存储能力。
/// </summary>
/// <typeparam name="TEntity">实体类型。</typeparam>
public abstract class StoreBase<TEntity> : StoreBase<TEntity, Guid>, IStore<TEntity>
    where TEntity : class, IKey<Guid>
{
    /// <summary>
    /// 初始化一个 <see cref="StoreBase{TEntity}"/> 类型的实例。
    /// </summary>
    /// <param name="unitOfWork">当前工作单元。</param>
    protected StoreBase(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}

/// <summary>
/// 提供基于 EF Core 的泛型实体存储能力。
/// </summary>
/// <typeparam name="TEntity">实体类型。</typeparam>
/// <typeparam name="TKey">实体标识类型。</typeparam>
public abstract class StoreBase<TEntity, TKey> : IStore<TEntity, TKey> where TEntity : class, IKey<TKey>
{
    #region 字段

    /// <summary>
    /// 当前存储器是否已释放。
    /// </summary>
    private int _disposed;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个 <see cref="StoreBase{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    /// <param name="unitOfWork">当前工作单元。</param>
    protected StoreBase(IUnitOfWork unitOfWork) => UnitOfWork = (UnitOfWorkBase)unitOfWork;

    #endregion

    #region 属性

    /// <summary>
    /// 获取当前工作单元。
    /// </summary>
    protected UnitOfWorkBase UnitOfWork { get; }

    /// <summary>
    /// 获取当前实体集。
    /// </summary>
    protected DbSet<TEntity> Set => UnitOfWork.Set<TEntity>();

    /// <summary>
    /// 获取或设置查询时是否跟踪实体。
    /// </summary>
    protected bool IsTracking { get; private set; } = true;

    /// <summary>
    /// SQL 查询对象缓存。
    /// </summary>
    private ISqlQuery _sqlQuery;

    /// <summary>
    /// 获取共享 EF Core 连接的 SQL 查询对象。
    /// </summary>
    protected virtual ISqlQuery Sql => _sqlQuery ??= CreateSqlQuery();

    /// <summary>
    /// 创建共享 EF Core 连接的 SQL 查询对象。
    /// </summary>
    /// <returns>基于共享 EF Core 连接创建的 SQL 查询对象。</returns>
    protected virtual ISqlQuery CreateSqlQuery()
    {
        return CreateEfCoreSqlQueryFactory().Create(UnitOfWork, EfCoreSqlConnectionMode.Shared);
    }

    /// <summary>
    /// 创建独立 EF Core 连接的 SQL 查询对象。
    /// </summary>
    /// <returns>基于独立 EF Core 连接创建的 SQL 查询对象。</returns>
    protected virtual ISqlQuery CreateIndependentSqlQuery()
    {
        return CreateEfCoreSqlQueryFactory().Create(UnitOfWork, EfCoreSqlConnectionMode.Independent);
    }

    /// <summary>
    /// 获取 EF Core SQL 查询工厂。
    /// </summary>
    /// <returns>已解析的 EF Core SQL 查询工厂。</returns>
    protected virtual IEfCoreSqlQueryFactory CreateEfCoreSqlQueryFactory()
    {
        var factory = UnitOfWork.ServiceProvider?.GetService(typeof(IEfCoreSqlQueryFactory)) as IEfCoreSqlQueryFactory;
        return factory ?? throw new InvalidOperationException("未注册 IEfCoreSqlQueryFactory，无法创建 EF Core SQL 查询对象。");
    }

    /// <summary>
    /// 获取当前工作单元的数据库连接。
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
    public virtual TEntity Find(object id) => UnitOfWork.ExecuteWithOperationCancellationBoundary(
        () => id.SafeString().IsEmpty() ? null : Set.Find(id));

    /// <inheritdoc />
    public virtual TEntity FindById(object id) => UnitOfWork.ExecuteWithOperationCancellationBoundary(
        () => id.SafeString().IsEmpty() ? null : Set.Find(id));

    #endregion

    #region FindByIdAsync(通过标识查找实体)

    /// <inheritdoc />
    [Obsolete("请使用 FindByIdAsync 方法")]
    public virtual async Task<TEntity> FindAsync(object id, CancellationToken cancellationToken = default)
    {
        return await WithOperationCancellationAsync(cancellationToken, async token =>
        {
            if (id.SafeString().IsEmpty())
                return null;
            return await Set.FindAsync(new[] { id }, token);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity> FindByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return await WithOperationCancellationAsync(cancellationToken, async token =>
        {
            if (id.SafeString().IsEmpty())
                return null;
            return await Set.FindAsync(new[] { id }, token);
        }).ConfigureAwait(false);
    }

    #endregion

    #region FindByIds(通过标识列表查找实体列表)

    /// <inheritdoc />
    public virtual List<TEntity> FindByIds(params TKey[] ids) => FindByIds((IEnumerable<TKey>)ids);

    /// <inheritdoc />
    public virtual List<TEntity> FindByIds(IEnumerable<TKey> ids)
    {
        return UnitOfWork.ExecuteWithOperationCancellationBoundary(
            () => ids == null ? null : Find(t => ids.Contains(t.Id)).ToList());
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
        return await WithOperationCancellationAsync(cancellationToken, token =>
            ids == null ? Task.FromResult<List<TEntity>>(null) : Find(t => ids.Contains(t.Id)).ToListAsync(token))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsAsync(string ids, CancellationToken cancellationToken = default)
    {
        return await WithOperationCancellationAsync(cancellationToken, async token =>
            await FindByIdsAsync(Conv.ToList<TKey>(ids), token).ConfigureAwait(false)).ConfigureAwait(false);
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
        return UnitOfWork.ExecuteWithOperationCancellationBoundary(
            () => ids == null ? null : FindAsNoTracking().Where(t => ids.Contains(t.Id)).ToList());
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
        return await WithOperationCancellationAsync(cancellationToken, async token =>
        {
            var entities = await FindByIdsNoTrackingAsync((IEnumerable<TKey>)new[] { id }, token)
                .ConfigureAwait(false);
            return entities == null || entities.Count == 0 ? null : entities[0];
        }).ConfigureAwait(false);
    }

    #endregion

    #region FindByIdsNoTrackingAsync(通过标识列表查找实体列表，不跟踪)

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsNoTrackingAsync(params TKey[] ids) => await FindByIdsNoTrackingAsync((IEnumerable<TKey>)ids);

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsNoTrackingAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        return await WithOperationCancellationAsync(cancellationToken, token =>
            ids == null ? Task.FromResult<List<TEntity>>(null) :
                FindAsNoTracking().Where(t => ids.Contains(t.Id)).ToListAsync(token)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsNoTrackingAsync(string ids, CancellationToken cancellationToken = default)
    {
        return await WithOperationCancellationAsync(cancellationToken, async token =>
            await FindByIdsNoTrackingAsync(Conv.ToList<TKey>(ids), token).ConfigureAwait(false)).ConfigureAwait(false);
    }

    #endregion

    #region Single(查找单个实体)

    /// <inheritdoc />
    public virtual TEntity Single(Expression<Func<TEntity, bool>> predicate) =>
        UnitOfWork.ExecuteWithOperationCancellationBoundary(() => Find().FirstOrDefault(predicate));

    /// <inheritdoc />
    public virtual TEntity Single(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>> action)
    {
        return UnitOfWork.ExecuteWithOperationCancellationBoundary(() =>
        {
            if (action == null)
                return Find().FirstOrDefault(predicate);
            return action(Find()).FirstOrDefault(predicate);
        });
    }

    #endregion

    #region SingleAsync(查找单个实体)

    /// <inheritdoc />
    public virtual async Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await WithOperationCancellationAsync(cancellationToken,
            token => Find().FirstOrDefaultAsync(predicate, token)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>> action, CancellationToken cancellationToken = default)
    {
        return await WithOperationCancellationAsync(cancellationToken, async token =>
        {
            if (action == null)
                return await SingleAsync(predicate, token).ConfigureAwait(false);
            return await action(Find()).FirstOrDefaultAsync(predicate, token).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    #endregion

    #region FindAll(查找实体列表)

    /// <inheritdoc />
    public virtual List<TEntity> FindAll(Expression<Func<TEntity, bool>> predicate = null)
    {
        return UnitOfWork.ExecuteWithOperationCancellationBoundary(
            () => predicate == null ? Find().ToList() : Find(predicate).ToList());
    }

    #endregion

    #region FindAllAsync(查找实体列表)

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindAllAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default)
    {
        return await WithOperationCancellationAsync(cancellationToken, token =>
            predicate == null ? Find().ToListAsync(token) : Find(predicate).ToListAsync(token)).ConfigureAwait(false);
    }

    #endregion

    #region FindAllNoTracking(查找实体列表，不跟踪)

    /// <inheritdoc />
    public virtual List<TEntity> FindAllNoTracking(Expression<Func<TEntity, bool>> predicate = null)
    {
        return UnitOfWork.ExecuteWithOperationCancellationBoundary(() =>
            predicate == null ? FindAsNoTracking().ToList() : FindAsNoTracking().Where(predicate).ToList());
    }

    #endregion

    #region FindAllNoTrackingAsync(查找实体列表，不跟踪)

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindAllNoTrackingAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default)
    {
        return await WithOperationCancellationAsync(cancellationToken, token =>
            predicate == null ? FindAsNoTracking().ToListAsync(token) :
                FindAsNoTracking().Where(predicate).ToListAsync(token)).ConfigureAwait(false);
    }

    #endregion

    #region Exists(判断是否存在)

    /// <inheritdoc />
    public bool Exists(TKey id)
    {
        return UnitOfWork.ExecuteWithOperationCancellationBoundary(() =>
        {
            if (id.SafeString().IsEmpty())
                return false;
            return Find().Any(t => Equals(id, t.Id));
        });
    }

    /// <inheritdoc />
    public virtual bool Exists(TKey[] ids)
    {
        return UnitOfWork.ExecuteWithOperationCancellationBoundary(
            () => ids != null && Find().Any(t => ids.Contains(t.Id)));
    }

    /// <inheritdoc />
    public virtual bool Exists(Expression<Func<TEntity, bool>> predicate)
    {
        return UnitOfWork.ExecuteWithOperationCancellationBoundary(
            () => predicate != null && Find().Any(predicate));
    }

    #endregion

    #region ExistsAsync(判断是否存在)

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(TKey id)
    {
        return await WithOperationCancellationAsync(CancellationToken.None, async token =>
        {
            if (id.SafeString().IsEmpty())
                return false;
            return await ExistsAsync(t => Equals(id, t.Id), token).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(params TKey[] ids)
    {
        return await WithOperationCancellationAsync(CancellationToken.None, async token =>
            ids != null && await ExistsAsync(t => ids.Contains(t.Id), token).ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await WithOperationCancellationAsync(cancellationToken, token =>
            predicate == null ? Task.FromResult(false) : Find().AnyAsync(predicate, token)).ConfigureAwait(false);
    }

    #endregion

    #region Count(查找数量)

    /// <inheritdoc />
    public virtual int Count(Expression<Func<TEntity, bool>> predicate = null)
    {
        return UnitOfWork.ExecuteWithOperationCancellationBoundary(
            () => predicate == null ? Find().Count() : Find().Count(predicate));
    }

    #endregion

    #region CountAsync(查找数量)

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default)
    {
        return await WithOperationCancellationAsync(cancellationToken, token =>
            predicate == null ? Find().CountAsync(token) : Find().CountAsync(predicate, token)).ConfigureAwait(false);
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
        await WithOperationCancellationAsync(cancellationToken, async token =>
        {
            ThrowIfDisposed();
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            await Set.AddAsync(entity, token).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task AddAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        await WithOperationCancellationAsync(cancellationToken, async token =>
        {
            ThrowIfDisposed();
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));
            await Set.AddRangeAsync(entities, token).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    #endregion

    #region Update(修改实体)

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">实体为空时抛出。</exception>
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
        if (oldVersion == null || current.Version.Length != oldVersion.Length)
        {
            ThrowConcurrencyException(entity);
            return;
        }
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
    /// <exception cref="ConcurrencyException">实体版本号与持久化版本不一致时抛出。</exception>
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

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">实体集合为空时抛出。</exception>
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
    /// 删除实体。
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
    /// 删除实体。
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
        await WithOperationCancellationAsync(cancellationToken, async token =>
        {
            ThrowIfDisposed();
            var entity = await FindByIdAsync(id, token).ConfigureAwait(false);
            Delete(entity);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await WithOperationCancellationAsync(cancellationToken, async token =>
        {
            if (entity != null)
                await RemoveAsync(entity.Id, token).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task RemoveAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        await WithOperationCancellationAsync(cancellationToken, async token =>
        {
            ThrowIfDisposed();
            if (ids == null)
                return;
            var entities = await FindByIdsAsync(ids, token).ConfigureAwait(false);
            Delete(entities);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task RemoveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        await WithOperationCancellationAsync(cancellationToken, async token =>
        {
            if (entities != null)
                await RemoveAsync(entities.Select(t => t.Id), token).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    #endregion

    private async Task<TResult> WithOperationCancellationAsync<TResult>(CancellationToken callerToken,
        Func<CancellationToken, Task<TResult>> action)
    {
        using var lease = UnitOfWork.CreateOperationCancellationLease(callerToken);
        using var cancellationRegistration = UnitOfWork.RegisterOperationCancellation(lease.Token);
        lease.Token.ThrowIfCancellationRequested();
        return await action(lease.Token).ConfigureAwait(false);
    }

    private async Task WithOperationCancellationAsync(CancellationToken callerToken,
        Func<CancellationToken, Task> action)
    {
        using var lease = UnitOfWork.CreateOperationCancellationLease(callerToken);
        using var cancellationRegistration = UnitOfWork.RegisterOperationCancellation(lease.Token);
        lease.Token.ThrowIfCancellationRequested();
        await action(lease.Token).ConfigureAwait(false);
    }

    #region ThrowIfDisposed(已释放则抛出异常)

    /// <summary>
    /// 已释放则抛出异常
    /// </summary>
    /// <exception cref="ObjectDisposedException">当前存储对象已释放时抛出。</exception>
    protected void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(GetType().Name);
    }

    #endregion

    #region Dispose(释放)

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        var query = _sqlQuery;
        _sqlQuery = null;
        query?.Dispose();
    }

    #endregion
}
