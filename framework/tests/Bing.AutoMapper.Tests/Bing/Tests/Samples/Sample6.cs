namespace Bing.Tests.Samples;

/// <summary>
/// 样例6
/// </summary>
public class Sample6
{
    /// <summary>
    /// 获取或设置样例名称。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置关联的第七号源样例。
    /// </summary>
    public Sample7 Sample71 { get; set; }

    /// <summary>
    /// 获取或设置关联的第五号源样例。
    /// </summary>
    public Sample5 Sample51 { get; set; }
}

/// <summary>
/// 样例转换6
/// </summary>
public class Sample6to
{
    /// <summary>
    /// 获取或设置转换对象名称。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置关联的第七号转换样例。
    /// </summary>
    public Sample7to Sample71 { get; set; }

    /// <summary>
    /// 获取或设置关联的第五号转换样例。
    /// </summary>
    public Sample5to Sample51 { get; set; }
}
