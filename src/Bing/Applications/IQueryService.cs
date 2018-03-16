using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Applications.Dtos;
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
    public interface IQueryService<TDto, in TQueryParameter> : IScopeDependency 
        where TDto : IDto, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 获取全部
        /// </summary>
        /// <returns></returns>
        List<TDto> GetAll();

        /// <summary>
        /// 获取全部
        /// </summary>
        /// <returns></returns>
        Task<List<TDto>> GetAllAsync();

        /// <summary>
        /// 通过编号获取
        /// </summary>
        /// <param name="id">实体编号</param>
        /// <returns></returns>
        TDto GetById(object id);

        /// <summary>
        /// 通过编号获取
        /// </summary>
        /// <param name="id">实体编号</param>
        /// <returns></returns>
        Task<TDto> GetByIdAsync(object id);

        /// <summary>
        /// 通过编号列表获取
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表，范例："1,2"</param>
        /// <returns></returns>
        List<TDto> GetByIds(string ids);

        /// <summary>
        /// 通过编号列表获取
        /// </summary>
        /// <param name="ids">用逗号分隔带额Id列表，范例："1,2"</param>
        /// <returns></returns>
        Task<List<TDto>> GetByIdsAsync(string ids);

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns></returns>
        List<TDto> Query(TQueryParameter parameter);

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns></returns>
        Task<List<TDto>> QueryAsync(TQueryParameter parameter);

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns></returns>
        PagerList<TDto> PagerQuery(TQueryParameter parameter);

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns></returns>
        Task<PagerList<TDto>> PagerQueryAsync(TQueryParameter parameter);
    }
}
