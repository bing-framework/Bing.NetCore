using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Data;
using Bing.Data.Queries;

namespace Bing.Application.Services
{
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
        TDto GetById(object id);

        /// <summary>
        /// 通过编号获取
        /// </summary>
        /// <param name="id">实体编号</param>
        Task<TDto> GetByIdAsync(object id);

        /// <summary>
        /// 通过编号列表获取
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表，范例："1,2"</param>
        List<TDto> GetByIds(string ids);

        /// <summary>
        /// 通过编号列表获取
        /// </summary>
        /// <param name="ids">用逗号分隔带额Id列表，范例："1,2"</param>
        Task<List<TDto>> GetByIdsAsync(string ids);

        /// <summary>
        /// 获取全部
        /// </summary>
        List<TDto> GetAll();

        /// <summary>
        /// 获取全部
        /// </summary>
        Task<List<TDto>> GetAllAsync();

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        List<TDto> Query(TQueryParameter parameter);

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        Task<List<TDto>> QueryAsync(TQueryParameter parameter);

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        PagerList<TDto> PagerQuery(TQueryParameter parameter);

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        Task<PagerList<TDto>> PagerQueryAsync(TQueryParameter parameter);
    }
}
