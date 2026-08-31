using Bing.Data;
using Bing.Data.Queries;

namespace Bing.Application.Services;

/// <summary>
/// 查询应用服务
/// </summary>
/// <typeparam name="TDto">数据传输对象类型</typeparam>
/// <typeparam name="TQueryParameter">查询参数类型</typeparam>
public interface IQueryAppService<TDto, in TQueryParameter> : IAppService
    where TDto : new()
    where TQueryParameter : IQueryParameter
{
    /// <summary>
    /// 通过编号获取
    /// </summary>
    /// <param name="id">实体编号</param>
    /// <returns>表示实体查询结果的异步操作；未找到时结果为 null。</returns>
    Task<TDto> GetByIdAsync(object id);

    /// <summary>
    /// 通过编号列表获取
    /// </summary>
    /// <param name="ids">用逗号分隔带额Id列表，范例："1,2"</param>
    /// <returns>表示实体查询结果的异步操作，结果为数据传输对象列表。</returns>
    Task<List<TDto>> GetByIdsAsync(string ids);

    /// <summary>
    /// 获取全部
    /// </summary>
    /// <returns>表示查询结果的异步操作，结果为全部数据传输对象列表。</returns>
    Task<List<TDto>> GetAllAsync();

    /// <summary>
    /// 查询
    /// </summary>
    /// <param name="parameter">查询参数</param>
    /// <returns>表示查询结果的异步操作，结果为数据传输对象列表。</returns>
    Task<List<TDto>> QueryAsync(TQueryParameter parameter);

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <param name="parameter">查询参数</param>
    /// <returns>表示分页查询结果的异步操作。</returns>
    Task<PagerList<TDto>> PagerQueryAsync(TQueryParameter parameter);
}