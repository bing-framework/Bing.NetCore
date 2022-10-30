using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bing.TextTemplating;

/// <summary>
/// 模板渲染引擎基类
/// </summary>
public abstract class TemplateRenderingEngineBase : ITemplateRenderingEngine
{
    /// <summary>
    /// 名称
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 模板定义管理器
    /// </summary>
    protected ITemplateDefinitionManager TemplateDefinitionManager { get; }

    /// <summary>
    /// 模板内容提供程序
    /// </summary>
    protected ITemplateContentProvider TemplateContentProvider { get; }

    /// <summary>
    /// 初始化一个<see cref="TemplateRenderingEngineBase"/>类型的实例
    /// </summary>
    /// <param name="templateDefinitionManager">模板定义管理器</param>
    /// <param name="templateContentProvider">模板内容提供程序</param>
    protected TemplateRenderingEngineBase(
        ITemplateDefinitionManager templateDefinitionManager, 
        ITemplateContentProvider templateContentProvider)
    {
        TemplateDefinitionManager = templateDefinitionManager;
        TemplateContentProvider = templateContentProvider;
    }

    /// <summary>
    /// 渲染
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="model">模型</param>
    /// <param name="cultureName">区域性名称</param>
    /// <param name="globalContext">全局上下文</param>
    public abstract Task<string> RenderAsync(string templateName, object model = null, string cultureName = null, Dictionary<string, object> globalContext = null);

    /// <summary>
    /// 获取模板内容
    /// </summary>
    /// <param name="templateDefinition">模板定义</param>
    protected virtual async Task<string> GetContentOrNullAsync(TemplateDefinition templateDefinition) => await TemplateContentProvider.GetContentOrNullAsync(templateDefinition);
}