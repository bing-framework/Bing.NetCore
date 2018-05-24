using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Applications.Dtos;
using Bing.Applications.Operations;
using Bing.Datas.Queries;
using Bing.Dependency;
using Bing.Domains.Repositories;

namespace Bing.Applications
{
    /// <summary>
    /// 查询服务
    /// </summary>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    public interface IQueryService<TDto, in TQueryParameter> : IService,
        IGetAll<TDto>, IGetById<TDto>, 
        IGetAllAsync<TDto>, IGetByIdAsync<TDto>, 
        IPagerQuery<TDto, TQueryParameter>, IPagerQueryAsync<TDto, TQueryParameter>
        where TDto : IResponse, new()
        where TQueryParameter : IQueryParameter
    {
    }
}
