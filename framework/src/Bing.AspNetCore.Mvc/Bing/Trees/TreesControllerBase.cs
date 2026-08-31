using Bing.AspNetCore.Mvc;
using Bing.Exceptions;
using Bing.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Trees;

/// <summary>
/// 树型控制器基类
/// </summary>
/// <typeparam name="TTreeResult">树型结果</typeparam>
/// <typeparam name="TDto">数据传输对象类型</typeparam>
/// <typeparam name="TQuery">父标识类型</typeparam>
public abstract class TreesControllerBase<TTreeResult, TDto, TQuery> : TreesControllerBase<TTreeResult, TDto, TQuery, Guid?>
    where TTreeResult : class, new()
    where TDto : class, ITreeNode, new()
    where TQuery : class, ITreeQueryParameter, new()
{
    /// <summary>
    /// 初始化一个<see cref="TreesControllerBase{TTreeResult,TDto,TQuery}"/>类型的实例
    /// </summary>
    /// <param name="service">树型服务</param>
    protected TreesControllerBase(ITreesAppService<TDto, TQuery, Guid?> service) : base(service)
    {
    }
}

/// <summary>
/// 树型控制器基类
/// </summary>
/// <typeparam name="TTreeResult">树型结果</typeparam>
/// <typeparam name="TDto">数据传输对象类型</typeparam>
/// <typeparam name="TQuery">查询参数类型</typeparam>
/// <typeparam name="TParentId">父标识类型</typeparam>
public abstract class TreesControllerBase<TTreeResult, TDto, TQuery, TParentId> : ApiControllerBase
    where TTreeResult : class, new()
    where TDto : class, ITreeNode, new()
    where TQuery : class, ITreeQueryParameter<TParentId>, new()
{
    /// <summary>
    /// 树型服务
    /// </summary>
    private readonly ITreesAppService<TDto, TQuery, TParentId> _service;

    /// <summary>
    /// 初始化一个<see cref="TreesControllerBase{TTreeResult,TDto,TQuery,TParentId}"/>类型的实例
    /// </summary>
    /// <param name="service">树型服务</param>
    protected TreesControllerBase(ITreesAppService<TDto, TQuery, TParentId> service) => _service = service;

    /// <summary>
    /// 获取加载模式
    /// </summary>
    /// <returns>当前树控制器使用的加载模式。</returns>
    protected virtual LoadMode GetLoadMode() => LoadMode.Sync;

    /// <summary>
    /// 获取单个实例
    /// </summary>
    /// <remarks>
    /// 调用范例：
    /// GET
    /// /api/customer/1
    /// </remarks>
    /// <param name="id">标识</param>
    /// <returns>表示查询结果的异步操作。</returns>
    [HttpGet("{id}")]
    public virtual async Task<IActionResult> GetAsync(string id)
    {
        var result = await _service.GetByIdAsync(id);
        return Success(result);
    }

    /// <summary>
    /// 删除，注意：该方法用于删除单个实体，批量删除请使用POST提交，否则可能失败
    /// </summary>
    /// <remarks>
    /// 调用范例：
    /// DELETE
    /// /api/customer/1
    /// </remarks>
    /// <param name="id">标识</param>
    /// <returns>表示删除结果的异步操作。</returns>
    [HttpDelete("{id}")]
    public virtual async Task<IActionResult> DeleteAsync(string id)
    {
        await _service.DeleteAsync(id);
        return Success();
    }

    /// <summary>
    /// 批量删除，注意：body参数需要添加引号，"'1,2,3'"而不是"1,2,3"
    /// </summary>
    /// <remarks>
    /// 调用范例：
    /// POST
    /// /api/customers/delete
    /// body："'1,2,3'"
    /// </remarks>
    /// <param name="ids">标识列表，多个Id用逗号分隔，范例：1,2,3</param>
    /// <returns>表示批量删除结果的异步操作。</returns>
    [HttpPost("delete")]
    public virtual async Task<IActionResult> BatchDeleteAsync([FromBody] string ids)
    {
        await _service.DeleteAsync(ids);
        return Success();
    }

    /// <summary>
    /// 批量启用指定标识对应的树节点，并返回更新后的节点。
    /// </summary>
    /// <param name="ids">以逗号分隔的节点标识列表。</param>
    /// <returns>表示启用结果的异步操作。</returns>
    [HttpPost("enable")]
    public virtual async Task<IActionResult> EnableAsync([FromBody] string ids)
    {
        await _service.EnableAsync(ids);
        var result = await _service.FindByIdsAsync(ids);
        return Success(result);
    }

    /// <summary>
    /// 批量禁用指定标识对应的树节点，并返回更新后的节点。
    /// </summary>
    /// <param name="ids">以逗号分隔的节点标识列表。</param>
    /// <returns>表示禁用结果的异步操作。</returns>
    [HttpPost("disable")]
    public virtual async Task<IActionResult> DisableAsync([FromBody] string ids)
    {
        await _service.DisableAsync(ids);
        var result = await _service.FindByIdsAsync(ids);
        return Success(result);
    }

    /// <summary>
    /// 交换两个树节点的排序号。
    /// </summary>
    /// <param name="ids">两个节点标识组成的逗号分隔字符串，例如 <c>1,2</c>。</param>
    /// <remarks>
    /// 调用范例:
    /// POST
    /// /api/customer/SwapSort
    /// body: "'1,2'"
    /// </remarks>
    /// <returns>表示交换排序结果的异步操作。</returns>
    [HttpPost("SwapSort")]
    public virtual async Task<IActionResult> SwapSortAsync([FromBody] string ids)
    {
        var idList = ids.ToGuidList();
        if (idList.Count < 2)
            return Fail("交换排序失败");
        await _service.SwapSortAsync(idList[0], idList[1]);
        return Success();
    }

    /// <summary>
    /// 修正排序
    /// </summary>
    /// <param name="parameter">查询参数</param>
    /// <returns>表示修正排序结果的异步操作。</returns>
    [HttpPost("fix")]
    public virtual async Task<IActionResult> FixAsync([FromBody] TQuery parameter)
    {
        if (parameter == null)
            return Fail("查询参数不能为空");
        await _service.FixSortIdAsync(parameter);
        return Success();
    }

    /// <summary>
    /// 查询
    /// </summary>
    /// <remarks>
    /// 调用范例:
    /// GET
    /// /api/role?name=a
    /// </remarks>
    /// <param name="query">查询参数</param>
    /// <returns>表示树查询结果的异步操作。</returns>
    [HttpGet]
    public virtual async Task<IActionResult> QueryAsync([FromQuery] TQuery query)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }
        QueryBefore(query);
        InitParam(query);
        TTreeResult result;
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

    /// <summary>
    /// 查询前操作
    /// </summary>
    /// <param name="query">查询参数</param>
    protected virtual void QueryBefore(TQuery query) { }

    /// <summary>
    /// 初始化参数
    /// </summary>
    /// <param name="query">查询参数</param>
    protected virtual void InitParam(TQuery query)
    {
        if (query.Order.IsEmpty())
            query.Order = "SortId";
        query.Path = null;
        if (GetOperation(query) == LoadOperation.LoadChild)
            return;
        query.ParentId = default;
    }

    /// <summary>
    /// 获取操作
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>当前请求对应的树加载操作。</returns>
    protected LoadOperation? GetOperation(TQuery query)
    {
        var operation = Request.Query["operation"].SafeString().ToLower();
        if (operation == "loadchild")
            return LoadOperation.LoadChild;
        return query.IsSearch() ? LoadOperation.Search : LoadOperation.FirstLoad;
    }

    /// <summary>
    /// 首次加载
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>表示首次加载结果的异步操作。</returns>
    protected virtual async Task<TTreeResult> FirstLoad(TQuery query)
    {
        if (GetLoadMode() == LoadMode.Sync)
            return await SyncFirstLoad(query);
        return await AsyncFirstLoad(query);
    }

    /// <summary>
    /// 同步首次查询
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>表示同步首次查询结果的异步操作。</returns>
    protected virtual async Task<TTreeResult> SyncFirstLoad(TQuery query)
    {
        var data = await Query(query);
        return ToResult(data);
    }

    /// <summary>
    /// 查询
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>表示查询结果的异步操作，结果为数据传输对象列表。</returns>
    private async Task<List<TDto>> Query(TQuery query)
    {
        var data = await _service.QueryAsync(query);
        ProcessData(data, query);
        return data;
    }

    /// <summary>
    /// 数据处理
    /// </summary>
    /// <param name="data">数据列表</param>
    /// <param name="query">查询参数</param>
    protected virtual void ProcessData(List<TDto> data, TQuery query)
    {
    }

    /// <summary>
    /// 转换为树型结果
    /// </summary>
    /// <param name="data">数据列表</param>
    /// <param name="async">是否异步</param>
    /// <returns>转换后的树型结果。</returns>
    protected abstract TTreeResult ToResult(List<TDto> data, bool async = false);

    /// <summary>
    /// 异步首次加载
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>表示异步首次加载结果的异步操作。</returns>
    protected virtual async Task<TTreeResult> AsyncFirstLoad(TQuery query)
    {
        query.Level = 1;
        var data = await Query(query);
        return ToResult(data, true);
    }

    /// <summary>
    /// 加载子节点
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>表示子节点加载结果的异步操作。</returns>
    protected virtual async Task<TTreeResult> LoadChildren(TQuery query)
    {
        if (query.ParentId == null)
            throw new Warning("父节点标识为空，加载节点失败");
        if (GetLoadMode() == LoadMode.Async)
            return await AsyncLoadChildren(query);
        return await SyncLoadChildren(query);
    }

    /// <summary>
    /// 异步加载子节点
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>表示异步子节点加载结果的异步操作。</returns>
    protected virtual async Task<TTreeResult> AsyncLoadChildren(TQuery query)
    {
        var queryParam = await GetAsyncLoadChildrenQuery(query);
        var data = await Query(queryParam);
        return ToResult(data, true);
    }

    /// <summary>
    /// 获取异步加载子节点查询参数
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>表示异步子节点查询参数结果的异步操作。</returns>
    protected virtual Task<TQuery> GetAsyncLoadChildrenQuery(TQuery query)
    {
        query.Level = null;
        query.Path = null;
        return Task.FromResult(query);
    }

    /// <summary>
    /// 同步加载子节点
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>表示同步子节点加载结果的异步操作。</returns>
    protected virtual async Task<TTreeResult> SyncLoadChildren(TQuery query)
    {
        var parentId = query.ParentId.SafeString();
        var queryParam = await GetSyncLoadChildrenQuery(query);
        var data = await _service.QueryAsync(queryParam);
        data.RemoveAll(t => t.Id == parentId);
        ProcessData(data, query);
        return ToResult(data);
    }

    /// <summary>
    /// 获取同步加载子节点查询参数
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>表示同步子节点查询参数结果的异步操作。</returns>
    protected virtual async Task<TQuery> GetSyncLoadChildrenQuery(TQuery query)
    {
        var parent = await _service.GetByIdAsync(query.ParentId);
        query.Path = parent.Path;
        query.Level = null;
        query.ParentId = default;
        return query;
    }

    /// <summary>
    /// 搜索
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>表示树搜索结果的异步操作。</returns>
    protected virtual async Task<TTreeResult> Search(TQuery query)
    {
        var data = await _service.QueryAsync(query);
        var ids = data.GetMissingParentIds();
        var list = await _service.GetByIdsAsync(ids.Join());
        data.AddRange(list);
        ProcessData(data, query);
        if (GetLoadMode() == LoadMode.Async)
            return ToResult(data, true);
        return ToResult(data);
    }
}
