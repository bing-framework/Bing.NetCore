using System;
using System.Collections.Generic;
using Bing.Collections;

namespace Bing.TextTemplating;

/// <summary>
/// 文本模板选项配置
/// </summary>
public class BingTextTemplatingOptions
{
    /// <summary>
    /// 模板定义提供程序 类型列表
    /// </summary>
    public ITypeList<ITemplateDefinitionProvider> DefinitionProviders { get; }

    /// <summary>
    /// 模板内容构造者 类型列表
    /// </summary>
    public ITypeList<ITemplateContentContributor> ContentContributors { get; }

    /// <summary>
    /// 渲染引擎字典
    /// </summary>
    public IDictionary<string, Type> RenderingEngines { get; }

    /// <summary>
    /// 默认渲染引擎
    /// </summary>
    public string DefaultRenderingEngine { get; set; }

    /// <summary>
    /// 初始化一个<see cref="BingTextTemplatingOptions"/>类型的实例
    /// </summary>
    public BingTextTemplatingOptions()
    {
        DefinitionProviders = new TypeList<ITemplateDefinitionProvider>();
        ContentContributors = new TypeList<ITemplateContentContributor>();
        RenderingEngines = new Dictionary<string, Type>();
    }
}