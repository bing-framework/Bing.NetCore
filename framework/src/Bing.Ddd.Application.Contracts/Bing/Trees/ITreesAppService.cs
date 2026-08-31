namespace Bing.Trees;

/// <summary>
/// 定义使用默认 GUID 父节点标识的树形应用服务契约。
/// </summary>
/// <typeparam name="TDto">树形数据传输对象类型。</typeparam>
/// <typeparam name="TQueryParameter">树形查询参数类型。</typeparam>
public interface ITreesAppService<TDto, in TQueryParameter> : ITreesAppService<TDto, TQueryParameter, Guid?>
    where TDto : class, ITreeNode, new()
    where TQueryParameter : class, ITreeQueryParameter
{
}

/// <summary>
/// 定义树形数据的删除、启用、禁用和排序操作契约。
/// </summary>
/// <typeparam name="TDto">树形数据传输对象类型。</typeparam>
/// <typeparam name="TQueryParameter">树形查询参数类型。</typeparam>
/// <typeparam name="TParentId">父节点标识类型。</typeparam>
public interface ITreesAppService<TDto, in TQueryParameter, TParentId> : ITreesQueryAppService<TDto, TQueryParameter, TParentId>
    where TDto : class, ITreeNode, new()
    where TQueryParameter : class, ITreeQueryParameter<TParentId>
{
    /// <summary>
    /// 异步删除指定标识对应的树节点。
    /// </summary>
    /// <param name="ids">以逗号分隔的节点标识列表，例如 <c>1,2</c>。</param>
    Task DeleteAsync(string ids);

    /// <summary>
    /// 异步启用指定标识对应的树节点。
    /// </summary>
    /// <param name="ids">以逗号分隔的节点标识列表。</param>
    Task EnableAsync(string ids);

    /// <summary>
    /// 异步禁用指定标识对应的树节点。
    /// </summary>
    /// <param name="ids">以逗号分隔的节点标识列表。</param>
    Task DisableAsync(string ids);

    /// <summary>
    /// 异步交换两个树节点的排序号。
    /// </summary>
    /// <param name="id">第一个节点标识。</param>
    /// <param name="swapId">要交换排序号的目标节点标识。</param>
    Task SwapSortAsync(Guid id, Guid swapId);

    /// <summary>
    /// 异步按查询结果重新修正树节点的排序号。
    /// </summary>
    /// <param name="parameter">用于筛选待修正节点的查询参数。</param>
    Task FixSortIdAsync(TQueryParameter parameter);
}
