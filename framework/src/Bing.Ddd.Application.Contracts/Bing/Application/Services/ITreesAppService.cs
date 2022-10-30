﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Application.Dtos;
using Bing.Data.Queries;

namespace Bing.Application.Services;

/// <summary>
/// 树型服务
/// </summary>
/// <typeparam name="TDto">数据传输对象类型</typeparam>
/// <typeparam name="TQueryParameter">查询参数类型</typeparam>
public interface ITreesAppService<TDto, in TQueryParameter> : ITreesAppService<TDto, TQueryParameter, Guid?>
    where TDto : class, ITreeNode, new()
    where TQueryParameter : class, ITreeQueryParameter
{
}

/// <summary>
/// 树型应用服务
/// </summary>
/// <typeparam name="TDto">数据传输对象类型</typeparam>
/// <typeparam name="TQueryParameter">查询参数类型</typeparam>
/// <typeparam name="TParentId">父标识类型</typeparam>
public interface ITreesAppService<TDto, in TQueryParameter, TParentId> :
    IDeleteAppService<TDto, TQueryParameter>
    where TDto : class, ITreeNode, new()
    where TQueryParameter : class, ITreeQueryParameter<TParentId>
{
    /// <summary>
    /// 通过标识查找列表
    /// </summary>
    /// <param name="ids">标识列表</param>
    Task<List<TDto>> FindByIdsAsync(string ids);

    /// <summary>
    /// 启用
    /// </summary>
    /// <param name="ids">标识列表</param>
    Task EnableAsync(string ids);

    /// <summary>
    /// 冻结
    /// </summary>
    /// <param name="ids">标识列表</param>
    Task DisableAsync(string ids);

    /// <summary>
    /// 交换排序
    /// </summary>
    /// <param name="id">标识</param>
    /// <param name="swapId">目标标识</param>
    Task SwapSortAsync(Guid id, Guid swapId);

    /// <summary>
    /// 修正排序
    /// </summary>
    /// <param name="parameter">查询参数</param>
    Task FixSortIdAsync(TQueryParameter parameter);
}