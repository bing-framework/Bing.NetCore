using System;
using Bing.Application.Dtos;
using Bing.Domain.Entities;

namespace Bing.SampleClasses;

/// <summary>
/// 树型实体测试样例
/// </summary>
public class TreeEntitySample : TreeEntityBase<TreeEntitySample>
{
    /// <summary>
    /// 初始化一个<see cref="TreeEntitySample"/>类型的实例
    /// </summary>
    public TreeEntitySample() : this(Guid.Empty)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="TreeEntitySample"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    public TreeEntitySample(Guid id) : base(id, "", 0)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="TreeEntitySample"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    /// <param name="path">路径</param>
    public TreeEntitySample(Guid id, string path) : base(id, path, 0)
    {
    }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// 树形Dto测试样例 
/// </summary>
public class TreeEntityDto : TreeDtoBase
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; }
}