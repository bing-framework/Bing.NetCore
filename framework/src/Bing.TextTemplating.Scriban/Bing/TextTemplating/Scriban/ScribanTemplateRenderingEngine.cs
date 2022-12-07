using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Bing.DependencyInjection;
using Bing.Helpers;
using Scriban;
using Scriban.Runtime;

namespace Bing.TextTemplating.Scriban;

/// <summary>
/// Scriban模板渲染引擎
/// </summary>
public class ScribanTemplateRenderingEngine : TemplateRenderingEngineBase, ITransientDependency
{
    /// <summary>
    /// 引擎名称
    /// </summary>
    public const string EngineName = "Scriban";

    /// <summary>
    /// 名称
    /// </summary>
    public override string Name => EngineName;

    /// <summary>
    /// 初始化一个<see cref="ScribanTemplateRenderingEngine"/>类型的实例
    /// </summary>
    /// <param name="templateDefinitionManager">模板定义管理器</param>
    /// <param name="templateContentProvider">模板内容提供程序</param>
    public ScribanTemplateRenderingEngine(
        ITemplateDefinitionManager templateDefinitionManager, 
        ITemplateContentProvider templateContentProvider) 
        : base(templateDefinitionManager, templateContentProvider)
    {
    }

    /// <summary>
    /// 渲染
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="model">模型</param>
    /// <param name="cultureName">区域性名称</param>
    /// <param name="globalContext">全局上下文</param>
    public override async Task<string> RenderAsync(string templateName, object model = null, string cultureName = null, Dictionary<string, object> globalContext = null)
    {
        Check.NotNullOrEmpty(templateName, nameof(templateName));
        globalContext ??= new Dictionary<string, object>();
        return await RenderInternalAsync(templateName, globalContext, model);
    }

    /// <summary>
    /// 内部渲染
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="globalContext">全局上下文</param>
    /// <param name="model">模型</param>
    protected virtual async Task<string> RenderInternalAsync(string templateName, Dictionary<string, object> globalContext, object model = null)
    {
        var templateDefinition = TemplateDefinitionManager.Get(templateName);
        var renderedContent = await RenderSingleTemplateAsync(templateDefinition, globalContext, model);
        if (templateDefinition.Layout != null)
        {
            globalContext["context"] = renderedContent;
            renderedContent = await RenderInternalAsync(templateDefinition.Layout, globalContext);
        }

        return renderedContent;
    }

    /// <summary>
    /// 渲染单个模板
    /// </summary>
    /// <param name="templateDefinition">模板定义</param>
    /// <param name="globalContext">全局上下文</param>
    /// <param name="model">模型</param>
    protected virtual async Task<string> RenderSingleTemplateAsync(TemplateDefinition templateDefinition, Dictionary<string, object> globalContext, object model = null)
    {
        var rawTemplateContent = await GetContentOrNullAsync(templateDefinition);
        return await RenderTemplateContentWithScribanAsync(templateDefinition, rawTemplateContent, globalContext, model);
    }

    /// <summary>
    /// 渲染模板内容
    /// </summary>
    /// <param name="templateDefinition">模板定义</param>
    /// <param name="templateContent">模板内容</param>
    /// <param name="globalContext">全局上下文</param>
    /// <param name="model">模型</param>
    protected virtual async Task<string> RenderTemplateContentWithScribanAsync(TemplateDefinition templateDefinition, string templateContent, Dictionary<string, object> globalContext, object model = null)
    {
        var context = CreateScribanTemplateContext(templateDefinition, globalContext, model);
        return await Template.Parse(templateContent).RenderAsync(context);
    }

    /// <summary>
    /// 创建Scriban模板上下文
    /// </summary>
    /// <param name="templateDefinition">模板定义</param>
    /// <param name="globalContext">全局上下文</param>
    /// <param name="model">模型</param>
    protected virtual TemplateContext CreateScribanTemplateContext(TemplateDefinition templateDefinition, Dictionary<string, object> globalContext, object model = null)
    {
        var context = new TemplateContext();
        var scriptObject = new ScriptObject();
        scriptObject.Import(globalContext);
        if (model != null)
            scriptObject["model"] = model;
        context.PushGlobal(scriptObject);
        context.PushCulture(CultureInfo.CurrentUICulture);
        return context;
    }
}