using System.Collections.Generic;
using Bing.Collections;

namespace Bing.TextTemplating;

/// <summary>
/// 模板定义
/// </summary>
public class TemplateDefinition
{
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 是否布局
    /// </summary>
    public bool IsLayout { get; }

    /// <summary>
    /// 布局
    /// </summary>
    public string Layout { get; set; }

    /// <summary>
    /// 渲染引擎
    /// </summary>
    public string RenderEngine { get; set; }

    /// <summary>
    /// 获取或设置 <see cref="Properties"/> 上的键值
    /// </summary>
    /// <param name="name">属性名</param>
    public object this[string name]
    {
        get => Properties.GetOrDefault(name);
        set => Properties[name] = value;
    }

    /// <summary>
    /// 自定义属性
    /// </summary>
    public Dictionary<string, object> Properties { get; }

    /// <summary>
    /// 初始化一个<see cref="TemplateDefinition"/>类型的实例
    /// </summary>
    /// <param name="name">名称</param>
    /// <param name="isLayout">是否布局</param>
    /// <param name="layout">布局</param>
    public TemplateDefinition(string name, bool isLayout = false, string layout = null)
    {
        Name = name;
        IsLayout = isLayout;
        Layout = layout;
        Properties = new Dictionary<string, object>();
    }

    /// <summary>
    /// 设置属性
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    public virtual TemplateDefinition WithProperty(string key, object value)
    {
        Properties[key] = value;
        return this;
    }

    /// <summary>
    /// 设置渲染引擎
    /// </summary>
    /// <param name="renderEngine">渲染引擎</param>
    public virtual TemplateDefinition WithRenderEngine(string renderEngine)
    {
        RenderEngine = renderEngine;
        return this;
    }
}