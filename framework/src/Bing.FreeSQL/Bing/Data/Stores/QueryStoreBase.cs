using System.Data;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Bing.Data.Queries;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.DependencyInjection;
using Bing.Domain.Entities;
using Bing.Extensions;
using Bing.FreeSQL;
using Bing.FreeSQL.Extensions;
using Bing.Helpers;
using Bing.Uow;
using FreeSql;
using IUnitOfWork = Bing.Uow.IUnitOfWork;

namespace Bing.Data.Stores;

/// <summary>
/// 查询存储器
/// </summary>
/// <typeparam name="TEntity">对象类型</typeparam>
public abstract class QueryStoreBase<TEntity> : QueryStoreBase<TEntity, Guid>, IQueryStore<TEntity>
    where TEntity : class, IKey<Guid>
{
    /// <summary>
    /// 初始化一个 <see cref="QueryStoreBase{TEntity}"/> 类型的实例。
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="sqlQueryFactory">SQL 查询对象工厂</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">SQL 元数据配置</param>
    /// <param name="typeConverterResolver">数据类型转换器解析器</param>
    protected QueryStoreBase(IUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
        IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
        ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory, databaseContextAccessor,
        metadataOptions, typeConverterResolver)
    {
    }
}

/// <summary>
/// 查询存储器
/// </summary>
/// <typeparam name="TEntity">对象类型</typeparam>
/// <typeparam name="TKey">对象标识类型</typeparam>
public abstract class QueryStoreBase<TEntity,TKey> : IQueryStore<TEntity, TKey> where TEntity : class, IKey<TKey>
{
    #region 属性

    /// <summary>
    /// 获取工作单元
    /// </summary>
    protected UnitOfWorkBase UnitOfWork { get; }

    /// <summary>
    /// SQL 查询对象工厂。
    /// </summary>
    private readonly ISqlQueryFactory _sqlQueryFactory;

    /// <summary>
    /// 数据库上下文访问器。
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// SQL 元数据配置。
    /// </summary>
    private readonly SqlMetadataOptions _metadataOptions;

    /// <summary>
    /// 数据类型转换器解析器。
    /// </summary>
    private readonly ITypeConverterResolver _typeConverterResolver;

    /// <summary>
    /// 获取实体集
    /// </summary>
    protected DbSet<TEntity> Set => UnitOfWork.Set<TEntity>();

    /// <summary>
    /// Sql查询对象
    /// </summary>
    private ISqlQuery _sqlQuery;

    /// <summary>
    /// 获取Sql查询对象
    /// </summary>
    protected virtual ISqlQuery Sql => _sqlQuery ??= CreateSqlQuery();

    /// <summary>
    /// 创建Sql查询对象
    /// </summary>
    /// <returns>绑定当前工作单元和实体映射解析器的 SQL 查询对象。</returns>
    protected virtual ISqlQuery CreateSqlQuery()
    {
        var result = _sqlQueryFactory.Create();
        var metadataProvider = new CompositeEntityModelMetadataProvider(new IEntityModelMetadataProvider[]
        {
            new FreeSqlEntityModelMetadataProvider(UnitOfWork.Orm)
        });
        SqlQueryRuntimeBinding.BindEntityMappingResolver(result, new DefaultEntityMappingResolver(
            _databaseContextAccessor, _metadataOptions, _typeConverterResolver, metadataProvider));
        return result;
    }

    /// <summary>
    /// 获取数据库连接
    /// </summary>
    protected IDbConnection Connection => UnitOfWork.GetConnection();

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个 <see cref="QueryStoreBase{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="sqlQueryFactory">SQL 查询对象工厂</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">SQL 元数据配置</param>
    /// <param name="typeConverterResolver">数据类型转换器解析器</param>
    protected QueryStoreBase(IUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
        IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
        ITypeConverterResolver typeConverterResolver)
    {
        UnitOfWork = (UnitOfWorkBase)unitOfWork;
        _sqlQueryFactory = sqlQueryFactory ?? throw new ArgumentNullException(nameof(sqlQueryFactory));
        _databaseContextAccessor = databaseContextAccessor;
        _metadataOptions = metadataOptions ?? throw new ArgumentNullException(nameof(metadataOptions));
        _typeConverterResolver = typeConverterResolver ?? throw new ArgumentNullException(nameof(typeConverterResolver));
    }

    #endregion

    /// <inheritdoc />
    public IQueryable<TEntity> FindAsNoTracking() => Set.Select.NoTracking().AsQueryable();

    /// <inheritdoc />
    public IQueryable<TEntity> Find() => Set.Select.AsQueryable();

    /// <inheritdoc />
    public IQueryable<TEntity> Find(ICondition<TEntity> criteria) => Set.Select.AsQueryable().Where(criteria);

    /// <inheritdoc />
    public IQueryable<TEntity> Find(Expression<Func<TEntity, bool>> predicate) => Set.Select.AsQueryable().Where(predicate);

    /// <inheritdoc />
    public virtual TEntity Find(object id) => id.SafeString().IsEmpty() ? null : Set.Select.WhereDynamic(id).ToOne();

    /// <inheritdoc />
    public virtual TEntity FindById(object id) => id.SafeString().IsEmpty() ? null : Set.Select.WhereDynamic(id).ToOne();

    /// <inheritdoc />
    public virtual async Task<TEntity> FindAsync(object id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (id.SafeString().IsEmpty())
            return null;
        return await Set.Select.WhereDynamic(id).ToOneAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity> FindByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (id.SafeString().IsEmpty())
            return null;
        return await Set.Select.WhereDynamic(id).ToOneAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual List<TEntity> FindByIds(params TKey[] ids) => FindByIds((IEnumerable<TKey>)ids);

    /// <inheritdoc />
    public virtual List<TEntity> FindByIds(IEnumerable<TKey> ids)
    {
        if (ids == null)
            return null;
        return Set.Select.WhereDynamic(ids.ToArray()).ToList();
    }

    /// <inheritdoc />
    public virtual List<TEntity> FindByIds(string ids)
    {
        var idList = Conv.ToList<TKey>(ids);
        return FindByIds(idList);
    }

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsAsync(params TKey[] ids) => await FindByIdsAsync((IEnumerable<TKey>)ids);

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ids == null)
            return null;
        return await Set.Select.WhereDynamic(ids.ToArray()).ToListAsync(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsAsync(string ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var idList = Conv.ToList<TKey>(ids);
        return await FindByIdsAsync(idList, cancellationToken);
    }

    /// <inheritdoc />
    public virtual TEntity FindByIdNoTracking(TKey id)
    {
        if (id == null)
            return null;
        return Set.Select.NoTracking().WhereDynamic(id).ToOne();
    }

    /// <inheritdoc />
    public virtual async Task<TEntity> FindByIdNoTrackingAsync(TKey id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (id == null)
            return null;
        return await Set.Select.NoTracking().WhereDynamic(id).ToOneAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual List<TEntity> FindByIdsNoTracking(params TKey[] ids) => FindByIdsNoTracking((IEnumerable<TKey>)ids);

    /// <inheritdoc />
    public virtual List<TEntity> FindByIdsNoTracking(IEnumerable<TKey> ids)
    {
        if (ids == null)
            return null;
        return Set.Select.NoTracking().WhereDynamic(ids.ToArray()).ToList();
    }

    /// <inheritdoc />
    public virtual List<TEntity> FindByIdsNoTracking(string ids)
    {
        var idList = Conv.ToList<TKey>(ids);
        return FindByIdsNoTracking(idList);
    }

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsNoTrackingAsync(params TKey[] ids) => await FindByIdsNoTrackingAsync((IEnumerable<TKey>)ids);

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsNoTrackingAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ids == null)
            return null;
        return await Set.Select.NoTracking().WhereDynamic(ids.ToArray()).ToListAsync(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindByIdsNoTrackingAsync(string ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var idList = Conv.ToList<TKey>(ids);
        return await FindByIdsNoTrackingAsync(idList, cancellationToken);
    }

    /// <inheritdoc />
    public virtual TEntity Single(Expression<Func<TEntity, bool>> predicate) => Set.Select.Where(predicate).ToOne();

    /// <inheritdoc />
    public TEntity Single(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>> action)
    {
        if (action == null)
            return Single(predicate);
        return action(Find()).Where(predicate).RestoreToSelect().ToOne();
    }

    /// <inheritdoc />
    public virtual async Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Set.Select.Where(predicate).ToOneAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IQueryable<TEntity>> action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (action == null)
            return await SingleAsync(predicate, cancellationToken);
        return await action(Find()).Where(predicate).RestoreToSelect().ToOneAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual List<TEntity> FindAll(Expression<Func<TEntity, bool>> predicate = null)
    {
        if (predicate == null)
            return Set.Select.ToList();
        return Set.Select.Where(predicate).ToList();
    }

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindAllAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (predicate == null)
            return await Set.Select.ToListAsync(cancellationToken);
        return await Set.Select.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual List<TEntity> FindAllNoTracking(Expression<Func<TEntity, bool>> predicate = null)
    {
        if (predicate == null)
            return Set.Select.NoTracking().ToList();
        return Set.Select.NoTracking().Where(predicate).ToList();
    }

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindAllNoTrackingAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (predicate == null)
            return await Set.Select.NoTracking().ToListAsync(cancellationToken);
        return await Set.Select.NoTracking().Where(predicate).ToListAsync(cancellationToken);
    }


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
        if (ids == null)
            return false;
        return Exists(t => ids.Contains(t.Id));
    }

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
        if (ids == null)
            return false;
        return await ExistsAsync(t => ids.Contains(t.Id));
    }

    /// <inheritdoc />
    public virtual bool Exists(Expression<Func<TEntity, bool>> predicate)
    {
        if (predicate == null)
            return false;
        return Set.Select.Any(predicate);
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (predicate == null)
            return false;
        return await Set.Select.AnyAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int Count(Expression<Func<TEntity, bool>> predicate = null)
    {
        if (predicate == null)
            return (int)Set.Select.Count();
        return (int)Set.Select.Where(predicate).Count();
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (predicate == null)
            return (int)await Set.Select.CountAsync(cancellationToken);
        return (int)await Set.Select.Where(predicate).CountAsync(cancellationToken);
    }

    /// <summary>
    /// 查询。
    /// </summary>
    /// <param name="query">查询对象</param>
    /// <returns>符合查询条件和排序规则的实体列表。</returns>
    public virtual List<TEntity> Query(IQueryBase<TEntity> query) => Query(Set.Select.AsQueryable(), query).ToList();

    /// <summary>
    /// 获取查询结果
    /// </summary>
    /// <param name="queryable">数据源</param>
    /// <param name="query">查询对象</param>
    /// <returns>应用查询条件和排序规则后的查询对象。</returns>
    private IQueryable<TEntity> Query(IQueryable<TEntity> queryable, IQueryBase<TEntity> query)
    {
        queryable = queryable.Where(query);
        var order = query.GetOrder();
        if (string.IsNullOrWhiteSpace(order))
            return queryable;
        return queryable.OrderBy(order);
    }

    /// <summary>
    /// 查询未跟踪的实体。
    /// </summary>
    /// <param name="query">查询对象</param>
    /// <returns>符合查询条件和排序规则的未跟踪实体列表。</returns>
    public virtual List<TEntity> QueryAsNoTracking(IQueryBase<TEntity> query) => Query(FindAsNoTracking(), query).ToList();

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <param name="query">查询对象</param>
    /// <returns>符合查询条件的分页实体列表。</returns>
    public virtual PagerList<TEntity> PagerQuery(IQueryBase<TEntity> query) => Set.Select.AsQueryable().Where(query).ToPagerList(query.GetPager());

    /// <summary>
    /// 分页查询 - 返回未跟踪的实体
    /// </summary>
    /// <param name="query">查询对象</param>
    /// <returns>符合查询条件的分页未跟踪实体列表。</returns>
    public virtual PagerList<TEntity> PagerQueryAsNoTracking(IQueryBase<TEntity> query) => FindAsNoTracking().Where(query).ToPagerList(query.GetPager());

    /// <summary>
    /// 查询。
    /// </summary>
    /// <param name="query">查询对象</param>
    /// <returns>包含查询结果列表的异步任务。</returns>
    public virtual Task<List<TEntity>> QueryAsync(IQueryBase<TEntity> query) =>
        QueryAsync(query, CancellationToken.None);

    /// <summary>
    /// 查询。
    /// </summary>
    /// <param name="query">查询对象。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果列表的异步任务。</returns>
    public virtual async Task<List<TEntity>> QueryAsync(IQueryBase<TEntity> query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Query(Set.Select.AsQueryable(), query).RestoreToSelect().ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查询未跟踪的实体。
    /// </summary>
    /// <param name="query">查询对象</param>
    /// <returns>包含未跟踪查询结果列表的异步任务。</returns>
    public virtual Task<List<TEntity>> QueryAsNoTrackingAsync(IQueryBase<TEntity> query) =>
        QueryAsNoTrackingAsync(query, CancellationToken.None);

    /// <summary>
    /// 查询未跟踪的实体。
    /// </summary>
    /// <param name="query">查询对象。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含未跟踪查询结果列表的异步任务。</returns>
    public virtual async Task<List<TEntity>> QueryAsNoTrackingAsync(IQueryBase<TEntity> query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Query(FindAsNoTracking(), query).RestoreToSelect().ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 分页查询。
    /// </summary>
    /// <param name="query">查询对象</param>
    /// <returns>包含分页查询结果的异步任务。</returns>
    public virtual Task<PagerList<TEntity>> PagerQueryAsync(IQueryBase<TEntity> query) =>
        PagerQueryAsync(query, CancellationToken.None);

    /// <summary>
    /// 分页查询。
    /// </summary>
    /// <param name="query">查询对象。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含分页查询结果的异步任务。</returns>
    public virtual async Task<PagerList<TEntity>> PagerQueryAsync(IQueryBase<TEntity> query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Set.Select.AsQueryable().Where(query).ToPagerListAsync(query.GetPager(), cancellationToken);
    }

    /// <summary>
    /// 分页查询未跟踪的实体。
    /// </summary>
    /// <param name="query">查询对象</param>
    /// <returns>包含分页未跟踪查询结果的异步任务。</returns>
    public virtual Task<PagerList<TEntity>> PagerQueryAsNoTrackingAsync(IQueryBase<TEntity> query) =>
        PagerQueryAsNoTrackingAsync(query, CancellationToken.None);

    /// <summary>
    /// 分页查询未跟踪的实体。
    /// </summary>
    /// <param name="query">查询对象。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含分页未跟踪查询结果的异步任务。</returns>
    public virtual async Task<PagerList<TEntity>> PagerQueryAsNoTrackingAsync(IQueryBase<TEntity> query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await FindAsNoTracking().Where(query).ToPagerListAsync(query.GetPager(), cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        var sqlQuery = Interlocked.Exchange(ref _sqlQuery, null);
        sqlQuery?.Dispose();
    }
}
