using System;

namespace Bing.Tests.Samples;

/// <summary>
/// 深度克隆测试使用的基础样例对象。
/// </summary>
[Serializable]
public class OneSample
{
    /// <summary>
    /// 获取或设置对象名称。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置对象说明。
    /// </summary>
    public string Desc { get; set; }

    /// <summary>
    /// 获取或设置对象年龄。
    /// </summary>
    public int Age { get; set; }
}
