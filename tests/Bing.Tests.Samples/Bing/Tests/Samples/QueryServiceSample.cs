﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Bing.Application.Dtos;
using Bing.Application.Services;
using Bing.Auditing;
using Bing.Data.Queries;

namespace Bing.Tests.Samples;

/// <summary>
/// 数据传输对象样例
/// </summary>
public class DtoSample : DtoBase, IAuditedObjectWithName
{
    /// <summary>
    /// 名称
    /// </summary>
    [Required(ErrorMessage = "名称不能为空")]
    public string Name { get; set; }

    /// <summary>
    /// 忽略值
    /// </summary>
    [IgnoreMap]
    public string IgnoreValue { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime? CreationTime { get; set; }

    /// <summary>
    /// 创建人标识
    /// </summary>
    public Guid? CreatorId { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string Creator { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// 最后修改人标识
    /// </summary>
    public Guid? LastModifierId { get; set; }

    /// <summary>
    /// 最后修改人
    /// </summary>
    public string LastModifier { get; set; }

    /// <summary>
    /// 创建空集合
    /// </summary>
    public static List<DtoSample> EmptyList()
    {
        return new List<DtoSample>();
    }
}

/// <summary>
/// 查询服务样例
/// </summary>
public class QueryServiceSample : QueryAppServiceBase<EntitySample, DtoSample, QueryParameterSample>
{
    /// <summary>
    /// 初始化一个<see cref="QueryServiceSample"/>类型的实例
    /// </summary>
    public QueryServiceSample(IRepositorySample repository) : base(repository)
    {
    }

    /// <summary>
    /// 创建查询对象
    /// </summary>
    /// <param name="parameter">查询参数</param>
    protected override IQueryBase<EntitySample> CreateQuery(QueryParameterSample parameter) => new Query<EntitySample>(parameter).WhereIfNotEmpty(t => t.Name == parameter.Name);

    /// <summary>
    /// 查询时是否跟踪对象
    /// </summary>
    protected override bool IsTracking => true;
}