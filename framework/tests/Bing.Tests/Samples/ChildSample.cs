using System;

namespace Bing.Tests.Samples;

/// <summary>
/// 深度克隆测试使用的子级样例对象。
/// </summary>
[Serializable]
public class ChildSample
{
    /// <summary>
    /// 获取或设置第一个样例对象。
    /// </summary>
    public OneSample One { get; set; }

    /// <summary>
    /// 获取或设置第二个样例对象。
    /// </summary>
    public OneSample Two { get; set; }

    /// <summary>
    /// 获取或设置子级对象名称。
    /// </summary>
    public string Name { get; set; }
}
