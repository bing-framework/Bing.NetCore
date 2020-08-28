using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Application.Aspects;
using Bing.Application.Dtos;
using Bing.Datas.Queries;
using Bing.Validations.Aspects;

namespace Bing.Application.Services
{
    /// <summary>
    /// 增删改查应用服务
    /// </summary>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    public interface ICrudAppService<TDto, in TQueryParameter> : ICrudAppService<TDto, TDto, TQueryParameter>
        where TDto : IDto, new()
        where TQueryParameter : IQueryParameter
    {
    }

    /// <summary>
    /// 增删改查应用服务
    /// </summary>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TRequest">请求参数类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    public interface ICrudAppService<TDto, TRequest, in TQueryParameter> : ICrudAppService<TDto, TRequest, TRequest, TRequest, TQueryParameter>
        where TDto : IResponse, new()
        where TRequest : IRequest, IKey, new()
        where TQueryParameter : IQueryParameter
    {
    }

    /// <summary>
    /// 增删改查应用服务
    /// </summary>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TCreateRequest">创建参数类型</typeparam>
    /// <typeparam name="TUpdateRequest">修改参数类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    public interface ICrudAppService<TDto, in TCreateRequest, in TUpdateRequest, in TQueryParameter> : IDeleteAppService<TDto, TQueryParameter>
        where TDto : IResponse, new()
        where TCreateRequest : IRequest, new()
        where TUpdateRequest : IRequest, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="request">请求参数</param>
        [UnitOfWork]
        string Create([Valid] TCreateRequest request);

        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="request">请求参数</param>
        [UnitOfWork]
        Task<string> CreateAsync([Valid] TCreateRequest request);

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="request">请求参数</param>
        [UnitOfWork]
        void Update([Valid] TUpdateRequest request);

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="request">请求参数</param>
        [UnitOfWork]
        Task UpdateAsync([Valid] TUpdateRequest request);
    }

    /// <summary>
    /// 增删改查应用服务
    /// </summary>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TRequest">请求参数类型</typeparam>
    /// <typeparam name="TCreateRequest">创建参数类型</typeparam>
    /// <typeparam name="TUpdateRequest">修改参数类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    public interface ICrudAppService<TDto, TRequest, in TCreateRequest, in TUpdateRequest, in TQueryParameter> : ICrudAppService<TDto, TCreateRequest, TUpdateRequest, TQueryParameter>
        where TDto : IResponse, new()
        where TRequest : IRequest, IKey, new()
        where TCreateRequest : IRequest, new()
        where TUpdateRequest : IRequest, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="request">请求参数</param>
        [UnitOfWork]
        void Save([Valid] TRequest request);

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="request">请求参数</param>
        [UnitOfWork]
        Task SaveAsync([Valid] TRequest request);

        /// <summary>
        /// 批量保存
        /// </summary>
        /// <param name="addList">新增列表</param>
        /// <param name="updateList">修改列表</param>
        /// <param name="deleteList">删除列表</param>
        List<TDto> Save(List<TRequest> addList, List<TRequest> updateList, List<TRequest> deleteList);

        /// <summary>
        /// 批量保存
        /// </summary>
        /// <param name="addList">新增列表</param>
        /// <param name="updateList">修改列表</param>
        /// <param name="deleteList">删除列表</param>
        Task<List<TDto>> SaveAsync(List<TRequest> addList, List<TRequest> updateList, List<TRequest> deleteList);
    }
}
