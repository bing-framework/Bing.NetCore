namespace Bing.Configuration;

/// <summary>
/// 指定配置类型绑定的配置节点名称。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class OptionsTypeAttribute : Attribute
{
    /// <summary>
    /// 获取配置节点名称；未指定时由使用方回退到目标类名称。
    /// </summary>
    public string SectionName { get; }

    /// <summary>
    /// 使用可选的配置节点名称初始化 <see cref="OptionsTypeAttribute"/> 的实例。
    /// </summary>
    /// <param name="sectionName">配置节点名称；为空时不覆盖默认节点名称推导规则。</param>
    public OptionsTypeAttribute(string sectionName = null)
    {
        SectionName = sectionName;
    }
}
