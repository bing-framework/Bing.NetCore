using Bing.Applications.Operations;
using Bing.Datas.Queries;
using Bing.Dependency;

namespace Bing.Applications
{
    /// <summary>
    /// 查询服务
    /// </summary>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    [IgnoreDependency]
    public interface IQueryService<TDto, in TQueryParameter> : IService,
        IGetAll<TDto>, IGetById<TDto>,
        IGetAllAsync<TDto>, IGetByIdAsync<TDto>,
        IPageQuery<TDto, TQueryParameter>, IPageQueryAsync<TDto, TQueryParameter>
        where TDto : new()
        where TQueryParameter : IQueryParameter
    {
    }
}
