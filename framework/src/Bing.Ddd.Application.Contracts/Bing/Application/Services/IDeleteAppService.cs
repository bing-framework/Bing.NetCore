using Bing.Data.Queries;

namespace Bing.Application.Services;

/// <summary>
/// 提供数据删除应用服务。
/// </summary>
/// <typeparam name="TDto">数据传输对象类型。</typeparam>
/// <typeparam name="TQueryParameter">查询参数类型。</typeparam>
public interface IDeleteAppService<TDto, in TQueryParameter> : IQueryAppService<TDto, TQueryParameter>
    where TDto : new()
    where TQueryParameter : IQueryParameter
{
    /// <summary>
    /// 根据编号列表删除数据。
    /// </summary>
    /// <param name="ids">以逗号分隔的实体编号列表，例如：<c>1,2</c>。</param>
    Task DeleteAsync(string ids);
}