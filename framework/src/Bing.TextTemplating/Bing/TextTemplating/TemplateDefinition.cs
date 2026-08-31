using Bing.Collections;

namespace Bing.TextTemplating;

/// <summary>
/// 描述已注册文本模板的名称、布局关系、渲染引擎和扩展属性。
/// </summary>
public class TemplateDefinition
{
    /// <summary>
    /// 获取模板的稳定注册名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取模板是否用作其他模板的布局，默认值为 <c>false</c>。
    /// </summary>
    public bool IsLayout { get; }

    /// <summary>
    /// 获取或设置应用于当前模板的布局模板名称；未使用布局时可以为 <c>null</c>。
    /// </summary>
    public string Layout { get; set; }

    /// <summary>
    /// 获取或设置当前模板使用的渲染引擎名称；未设置时由默认渲染引擎回退策略决定。
    /// </summary>
    public string RenderEngine { get; set; }

    /// <summary>
    /// 获取或设置扩展属性字典中的指定键值。
    /// </summary>
    /// <param name="name">扩展属性名称。</param>
    /// <value>键存在时返回对应值；键不存在时返回 <c>null</c>。设置会新增或覆盖该键的值。</value>
    public object this[string name]
    {
        get => Properties.GetOrDefault(name);
        set => Properties[name] = value;
    }

    /// <summary>
    /// 获取可供模板提供者和渲染器使用的扩展属性字典，默认初始化为空字典。
    /// </summary>
    public Dictionary<string, object> Properties { get; }

    /// <summary>
    /// 使用模板名称及可选布局信息初始化 <see cref="TemplateDefinition"/> 的实例。
    /// </summary>
    /// <param name="name">模板的稳定注册名称。</param>
    /// <param name="isLayout">是否将该模板标记为布局模板，默认值为 <c>false</c>。</param>
    /// <param name="layout">应用于当前模板的可选布局模板名称。</param>
    public TemplateDefinition(string name, bool isLayout = false, string layout = null)
    {
        Name = name;
        IsLayout = isLayout;
        Layout = layout;
        Properties = new Dictionary<string, object>();
    }

    /// <summary>
    /// 设置扩展属性并返回当前模板定义。
    /// </summary>
    /// <param name="key">要新增或覆盖的扩展属性名称。</param>
    /// <param name="value">要保存的扩展属性值。</param>
    /// <returns>当前模板定义，以支持链式配置。</returns>
    public virtual TemplateDefinition WithProperty(string key, object value)
    {
        Properties[key] = value;
        return this;
    }

    /// <summary>
    /// 设置渲染引擎名称并返回当前模板定义。
    /// </summary>
    /// <param name="renderEngine">要用于渲染当前模板的引擎名称。</param>
    /// <returns>当前模板定义，以支持链式配置。</returns>
    public virtual TemplateDefinition WithRenderEngine(string renderEngine)
    {
        RenderEngine = renderEngine;
        return this;
    }
}
