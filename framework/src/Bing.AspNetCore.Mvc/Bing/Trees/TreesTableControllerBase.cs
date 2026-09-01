using Bing.Data;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Trees;

/// <summary>
/// 树型表格控制器
/// </summary>
/// <typeparam name="TDto">数据传输对象类型</typeparam>
/// <typeparam name="TQuery">父标识类型</typeparam>
public abstract class TreesTableControllerBase<TDto, TQuery> : TreesTableControllerBase<TDto, TQuery, Guid?>
    where TDto : TreeDto<TDto>, new()
    where TQuery : class, ITreeQueryParameter, new()
{
    /// <summary>
    /// 初始化一个<see cref="TreesTableControllerBase{TDto,TQuery}"/>类型的实例
    /// </summary>
    /// <param name="service">树型服务</param>
    protected TreesTableControllerBase(ITreesAppService<TDto, TQuery, Guid?> service) : base(service)
    {
    }
}

/// <summary>
/// 树型表格控制器
/// </summary>
/// <typeparam name="TDto">数据传输对象类型</typeparam>
/// <typeparam name="TQuery">查询参数类型</typeparam>
/// <typeparam name="TParentId">父标识类型</typeparam>
public abstract class TreesTableControllerBase<TDto, TQuery, TParentId> : TreesControllerBase<PagerList<TDto>, TDto, TQuery, TParentId>
    where TDto : TreeDto<TDto>, new()
    where TQuery : class, ITreeQueryParameter<TParentId>, new()
{
    /// <summary>
    /// 树型服务
    /// </summary>
    private readonly ITreesAppService<TDto, TQuery, TParentId> _service;

    /// <summary>
    /// 初始化一个<see cref="TreesTableControllerBase{TDto,TQuery,TParentId}"/>类型的实例
    /// </summary>
    /// <param name="service">树型服务</param>
    protected TreesTableControllerBase(ITreesAppService<TDto, TQuery, TParentId> service) : base(service) => _service = service;

    /// <inheritdoc />
    [HttpGet]
    public override async Task<IActionResult> QueryAsync(TQuery query)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        QueryBefore(query);
        InitParam(query);
        PagerList<TDto> result;
        switch (GetOperation(query))
        {
            case LoadOperation.FirstLoad:
                result = await FirstLoad(query);
                break;
            case LoadOperation.LoadChild:
                result = await LoadChildren(query);
                break;
            default:
                result = await Search(query);
                break;
        }
        return Success(result);
    }

    /// <inheritdoc />
    protected override PagerList<TDto> ToResult(List<TDto> data, bool async = false) => new PagerList<TDto>(GetTreeTableResult(data, async).GetResult());

    /// <inheritdoc />
    protected override async Task<PagerList<TDto>> AsyncFirstLoad(TQuery query)
    {
        query.Level = 1;
        var data = await _service.PagerQueryAsync(query);
        ProcessData(data.Data, query);
        return data.Convert(GetTreeTableResult(data.Data, true).GetResult());
    }

    /// <summary>
    /// 获取树型表格结果。
    /// </summary>
    /// <param name="data">待转换的数据集合。</param>
    /// <param name="async">是否使用异步加载模式。</param>
    /// <returns>树型表格结果。</returns>
    protected abstract ITreeTableResult<TDto> GetTreeTableResult(IEnumerable<TDto> data, bool async);

    /// <inheritdoc />
    protected override async Task<TQuery> GetSyncLoadChildrenQuery(TQuery query)
    {
        var parent = await _service.GetByIdAsync(query.ParentId);
        query.Path = parent.Path;
        query.Level = null;
        query.ParentId = default;
        return query;
    }
}