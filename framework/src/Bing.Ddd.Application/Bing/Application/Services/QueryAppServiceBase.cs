using System.Linq.Dynamic.Core;
using Bing.Data;
using Bing.Data.Queries;
using Bing.Domain.Entities;
using Bing.Helpers;
using Bing.ObjectMapping;

namespace Bing.Application.Services;

/// <summary>
/// 查询应用服务基类
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TDto">数据传输对象类型</typeparam>
/// <typeparam name="TQueryParameter">查询参数类型</typeparam>
public abstract class QueryAppServiceBase<TEntity, TDto, TQueryParameter> : QueryAppServiceBase<TEntity, TDto, TQueryParameter, Guid>
    where TEntity : class, IKey<Guid>
    where TDto : new()
    where TQueryParameter : IQueryParameter
{
    /// <summary>
    /// 初始化一个<see cref="QueryAppServiceBase{TEntity,TDto,TQueryParameter}"/>类型的实例
    /// </summary>
    /// <param name="store">查询存储器</param>
    protected QueryAppServiceBase(IQueryStore<TEntity, Guid> store) : base(store)
    {
    }
}

/// <summary>
/// 查询应用服务基类
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TDto">数据传输对象类型</typeparam>
/// <typeparam name="TQueryParameter">查询参数类型</typeparam>
/// <typeparam name="TKey">实体标识类型</typeparam>
public abstract class QueryAppServiceBase<TEntity, TDto, TQueryParameter, TKey> : AppServiceBase, IQueryAppService<TDto, TQueryParameter>
    where TEntity : class, IKey<TKey>
    where TDto : new()
    where TQueryParameter : IQueryParameter
{
    /// <summary>
    /// 查询存储器
    /// </summary>
    private readonly IQueryStore<TEntity, TKey> _store;

    /// <summary>
    /// 查询时是否跟踪对象
    /// </summary>
    protected virtual bool IsTracking => false;

    /// <summary>
    /// 初始化一个<see cref="QueryAppServiceBase{TEntity,TDto,TQueryParameter,TKey}"/>类型的实例
    /// </summary>
    /// <param name="store">查询存储器</param>
    protected QueryAppServiceBase(IQueryStore<TEntity, TKey> store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// 转换为数据传输对象
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>转换后的数据传输对象。</returns>
    protected virtual TDto ToDto(TEntity entity) => entity.MapTo<TDto>();

    #region GetByIdAsync(通过编号获取)

    /// <summary>
    /// 通过编号获取
    /// </summary>
    /// <param name="id">实体编号</param>
    /// <returns>实体存在时返回转换后的数据传输对象；不存在时返回 <see langword="null"/>。</returns>
    public virtual async Task<TDto> GetByIdAsync(object id)
    {
        var key = Conv.To<TKey>(id);
        return ToDto(await _store.FindByIdAsync(key));
    }

    #endregion

    #region GetByIdsAsync(通过编号列表获取)

    /// <summary>
    /// 通过编号列表获取
    /// </summary>
    /// <param name="ids">用逗号分隔带额Id列表，范例："1,2"</param>
    /// <returns>按标识查询并转换后的数据传输对象列表。</returns>
    public virtual async Task<List<TDto>> GetByIdsAsync(string ids)
    {
        var entities = await _store.FindByIdsAsync(ids);
        return entities.Select(ToDto).ToList();
    }

    #endregion

    #region GetAll(获取全部)

    /// <summary>
    /// 获取全部
    /// </summary>
    /// <returns>全部实体转换后的数据传输对象列表。</returns>
    public virtual async Task<List<TDto>> GetAllAsync()
    {
        var entities = await _store.FindAllAsync();
        return entities.Select(ToDto).ToList();
    }

    #endregion

    #region Query(查询)

    /// <summary>
    /// 查询
    /// </summary>
    /// <param name="parameter">查询参数</param>
    /// <returns>符合查询参数的数据传输对象列表；参数为空时返回空列表。</returns>
    public virtual List<TDto> Query(TQueryParameter parameter)
    {
        if (parameter == null)
            return new List<TDto>();
        return ExecuteQuery(parameter).ToList().Select(ToDto).ToList();
    }

    /// <summary>
    /// 查询
    /// </summary>
    /// <param name="parameter">查询参数</param>
    /// <returns>异步查询得到的数据传输对象列表；参数为空时返回空列表。</returns>
    public virtual async Task<List<TDto>> QueryAsync(TQueryParameter parameter)
    {
        if (parameter == null)
            return new List<TDto>();
        return (await AsyncExecuter.ToListAsync(ExecuteQuery(parameter))).Select(ToDto).ToList();
    }

    /// <summary>
    /// 执行查询
    /// </summary>
    /// <param name="parameter">查询参数</param>
    /// <returns>应用查询参数过滤和排序后的实体查询对象。</returns>
    private IQueryable<TEntity> ExecuteQuery(TQueryParameter parameter)
    {
        var query = CreateQuery(parameter);
        var queryable = Filter(query);
        queryable = Filter(queryable, parameter);
        var order = query.GetOrder();
        return string.IsNullOrWhiteSpace(order) ? queryable : queryable.OrderBy(order);
    }

    /// <summary>
    /// 创建查询对象
    /// </summary>
    /// <param name="parameter">查询参数</param>
    /// <returns>根据查询参数创建的查询对象。</returns>
    protected virtual IQueryBase<TEntity> CreateQuery(TQueryParameter parameter) => new Query<TEntity>(parameter);

    /// <summary>
    /// 过滤
    /// </summary>
    /// <param name="query">查询条件</param>
    /// <returns>应用基础查询条件后的实体查询对象。</returns>
    private IQueryable<TEntity> Filter(IQueryBase<TEntity> query) => IsTracking ? _store.Find().Where(query) : _store.FindAsNoTracking().Where(query);

    /// <summary>
    /// 过滤
    /// </summary>
    /// <param name="queryable">查询条件</param>
    /// <param name="parameter">查询参数</param>
    /// <returns>应用自定义查询条件后的实体查询对象。</returns>
    protected virtual IQueryable<TEntity> Filter(IQueryable<TEntity> queryable, TQueryParameter parameter) => queryable;

    #endregion

    #region PagerQuery(分页查询)

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <param name="parameter">查询参数</param>
    /// <returns>分页查询结果；参数为空时返回空分页结果。</returns>
    public virtual PagerList<TDto> PagerQuery(TQueryParameter parameter)
    {
        if (parameter == null)
            return new PagerList<TDto>();
        var query = CreateQuery(parameter);
        var queryable = Filter(query);
        queryable = Filter(queryable, parameter);
        return queryable.ToPagerList(query.GetPager()).Convert(ToDto);
    }

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <param name="parameter">查询参数</param>
    /// <returns>异步分页查询结果；参数为空时返回空分页结果。</returns>
    public virtual async Task<PagerList<TDto>> PagerQueryAsync(TQueryParameter parameter)
    {
        if (parameter == null)
            return new PagerList<TDto>();
        var query = CreateQuery(parameter);
        var queryable = Filter(query);
        queryable = Filter(queryable, parameter);
        var pager = query.GetPager();
        Bing.Data.Queries.Internal.Helper.InitOrder(queryable, pager);
        if (pager.TotalCount <= 0)
            pager.TotalCount = await AsyncExecuter.CountAsync(queryable);
        var orderedQueryable = Bing.Data.Queries.Internal.Helper.GetOrderedQueryable(queryable, pager);
        if (orderedQueryable == null)
            throw new ArgumentException("必须设置排序字段");
        queryable = orderedQueryable.Skip(pager.GetSkipCount()).Take(pager.PageSize);
        var pagerList = new PagerList<TEntity>(pager, await AsyncExecuter.ToListAsync(queryable));
        return pagerList.Convert(ToDto);
    }

    #endregion
}
