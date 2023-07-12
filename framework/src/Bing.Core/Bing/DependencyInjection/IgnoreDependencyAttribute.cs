namespace Bing.DependencyInjection;

/// <summary>
/// 标注了此特性的类，将忽略依赖注入自动映射
/// </summary>
/// <remarks>忽略注入，用于排除不被自动注入</remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class IgnoreDependencyAttribute : Attribute
{
    /// <summary>
    /// 是否忽略当前类以及子类
    /// </summary>
    /// <remarks>设置为true时，当前类以及子类都不再被自动注册，设置为false，仅当前类不背自动注册（默认False）</remarks>
    public bool Cascade { get; set; }

    /// <summary>
    /// 初始化一个<see cref="IgnoreDependencyAttribute"/>类型的实例
    /// </summary>
    /// <param name="cascade">是否忽略当前类以及子类</param>
    public IgnoreDependencyAttribute(bool cascade = false)
    {
        Cascade = cascade;
    }
}
