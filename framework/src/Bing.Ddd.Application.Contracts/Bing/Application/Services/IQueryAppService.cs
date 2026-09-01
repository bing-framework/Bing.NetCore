using Bing.Data;
using Bing.Data.Queries;

namespace Bing.Application.Services;

/// <summary>
/// 提供数据查询应用服务。
/// </summary>
/// <typeparam name="TDto">数据传输对象类型。</typeparam>
/// <typeparam name="TQueryParameter">查询参数类型。</typeparam>
public interface IQueryAppService<TDto, in TQueryParameter> : IAppService
    where TDto : new()
    where TQueryParameter : IQueryParameter
{
    /// <summary>
    /// 根据编号获取数据。
    /// </summary>
    /// <param name="id">实体编号。</param>
    /// <returns>表示实体查询结果的异步操作；未找到时结果为 null。</returns>
    Task<TDto> GetByIdAsync(object id);

    /// <summary>
    /// 根据编号列表获取数据。
    /// </summary>
    /// <param name="ids">以逗号分隔的实体编号列表，例如：<c>1,2</c>。</param>
    /// <returns>表示实体查询结果的异步操作，结果为数据传输对象列表。</returns>
    Task<List<TDto>> GetByIdsAsync(string ids);

    /// <summary>
    /// 获取全部数据。
    /// </summary>
    /// <returns>表示查询结果的异步操作，结果为全部数据传输对象列表。</returns>
    Task<List<TDto>> GetAllAsync();

    /// <summary>
    /// 根据查询参数获取数据。
    /// </summary>
    /// <param name="parameter">查询参数。</param>
    /// <returns>表示查询结果的异步操作，结果为数据传输对象列表。</returns>
    Task<List<TDto>> QueryAsync(TQueryParameter parameter);

    /// <summary>
    /// 根据查询参数分页获取数据。
    /// </summary>
    /// <param name="parameter">查询参数。</param>
    /// <returns>表示分页查询结果的异步操作。</returns>
    Task<PagerList<TDto>> PagerQueryAsync(TQueryParameter parameter);
}