using System;
using Bing.Domain.Entities;

namespace Bing.Tests.Samples;

/// <summary>
/// string聚合根测试样例
/// </summary>
public class StringAggregateRootSample : AggregateRoot<StringAggregateRootSample, string>
{
    /// <summary>
    /// 初始化一个<see cref="StringAggregateRootSample"/>类型的实例
    /// </summary>
    public StringAggregateRootSample() : this(Guid.NewGuid().ToString()) { }

    /// <summary>
    /// 初始化一个<see cref="StringAggregateRootSample"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    public StringAggregateRootSample(string id) : base(id) { }

    /// <summary>
    /// 姓名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 添加描述
    /// </summary>
    protected override void AddDescriptions()
    {
        AddDescription($"{nameof(Id)}:{Id},");
        AddDescription("姓名", Name);
    }

    /// <summary>
    /// 添加变更列表
    /// </summary>
    /// <param name="newObj">新对象</param>
    protected override void AddChanges(StringAggregateRootSample newObj)
    {
        AddChange(nameof(Name), "StringSampleName", Name, newObj.Name);
    }
}