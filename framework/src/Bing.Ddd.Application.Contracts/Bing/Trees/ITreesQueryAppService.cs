using Bing.Application.Services;

namespace Bing.Trees;

/// <summary>
/// 树型查询应用服务
/// </summary>
/// <typeparam name="TDto">数据传输对象类型</typeparam>
/// <typeparam name="TQueryParameter">查询参数类型</typeparam>
public interface ITreesQueryAppService<TDto, in TQueryParameter> : ITreesQueryAppService<TDto, TQueryParameter, Guid?>
    where TDto : class, ITreeNode, new()
    where TQueryParameter : class, ITreeQueryParameter
{
}

/// <summary>
/// 树型查询应用服务
/// </summary>
/// <typeparam name="TDto">数据传输对象类型</typeparam>
/// <typeparam name="TQueryParameter">查询参数类型</typeparam>
/// <typeparam name="TParentId">父标识类型</typeparam>
public interface ITreesQueryAppService<TDto, in TQueryParameter, TParentId> : IQueryAppService<TDto, TQueryParameter>
    where TDto : class, ITreeNode, new()
    where TQueryParameter : class, ITreeQueryParameter<TParentId>
{
    /// <summary>
    /// 通过标识查找列表
    /// </summary>
    /// <param name="ids">标识列表</param>
    Task<List<TDto>> FindByIdsAsync(string ids);
}
