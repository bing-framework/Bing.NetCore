using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Applications.Dtos;
using Bing.Applications.Operations;
using Bing.Datas.Queries.Trees;

namespace Bing.Applications.Trees
{
    /// <summary>
    /// 树型服务
    /// </summary>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    public interface ITreeService<TDto, in TQueryParameter> : ITreeService<TDto, TQueryParameter,Guid?>        
        where TDto : class, IResponse, ITreeNode, new()
        where TQueryParameter : class, ITreeQueryParameter
    {

    }

    /// <summary>
    /// 树型服务
    /// </summary>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TParentId">父标识类型</typeparam>
    public interface ITreeService<TDto, in TQueryParameter, TParentId> : IDeleteService<TDto, TQueryParameter>
        where TDto : class, IResponse, ITreeNode, new()
        where TQueryParameter : class, ITreeQueryParameter<TParentId>
    {
        /// <summary>
        /// 通过标识查找列表
        /// </summary>
        /// <param name="ids">标识列表</param>
        /// <returns></returns>
        Task<List<TDto>> FindByIdsAsync(string ids);

        /// <summary>
        /// 启用
        /// </summary>
        /// <param name="ids">标识列表</param>
        /// <returns></returns>
        Task EnableAsync(string ids);

        /// <summary>
        /// 冻结
        /// </summary>
        /// <param name="ids">标识列表</param>
        /// <returns></returns>
        Task DisableAsync(string ids);

        /// <summary>
        /// 交换排序
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="swapId">目标标识</param>
        /// <returns></returns>
        Task SwapSortAsync(Guid id, Guid swapId);

        /// <summary>
        /// 修正排序
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns></returns>
        Task FixSortIdAsync(TQueryParameter parameter);
    }
}
