using System;
using Bing.Extensions;

namespace Bing.Tests.Samples;

/// <summary>
/// 深度克隆测试使用的父级样例对象。
/// </summary>
[Serializable]
public class ParentSample
{
    /// <summary>
    /// 获取或设置子级样例对象。
    /// </summary>
    public ChildSample Child { get; set; }

    /// <summary>
    /// 获取或设置对象名称。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 存储由 SetName 设置的名称。
    /// </summary>
    private string _name;

    /// <summary>
    /// 初始化一个 <see cref="ParentSample"/> 类型的实例。
    /// </summary>
    public ParentSample()
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="ParentSample"/> 类型的实例。
    /// </summary>
    /// <param name="sample">要复制的父级样例对象。</param>
    public ParentSample(ParentSample sample)
    {
        this.Child = sample.Child;
        this.Name = sample.Name;
        this._name = sample._name;
    }

    //public ParentSample Clone()
    //{
    //    return this.DeepClone();
    //}

    /// <summary>
    /// 设置对象的私有名称。
    /// </summary>
    /// <param name="name">要保存的名称。</param>
    public void SetName(string name)
    {
        _name = name;
    }

    /// <summary>
    /// 获取对象的私有名称。
    /// </summary>
    /// <returns>已保存的名称。</returns>
    public string GetName()
    {
        return _name;
    }
}
