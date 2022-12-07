using System.Threading.Tasks;
using Bing.Data.Queries;

namespace Bing.Application.Services;

/// <summary>
/// 删除应用服务
/// </summary>
/// <typeparam name="TDto">数据传输对象类型</typeparam>
/// <typeparam name="TQueryParameter">查询参数类型</typeparam>
public interface IDeleteAppService<TDto, in TQueryParameter> : IQueryAppService<TDto, TQueryParameter>
    where TDto : new()
    where TQueryParameter : IQueryParameter
{
    /// <summary>
    /// 删除
    /// </summary>
    /// <param name="ids">用逗号分隔的Id列表，范例："1,2"</param>
    Task DeleteAsync(string ids);
}