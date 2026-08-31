using System.Globalization;
using Bing.DependencyInjection;
using Bing.Helpers;
using Scriban;
using Scriban.Runtime;

namespace Bing.TextTemplating.Scriban;

/// <summary>
/// 使用 Scriban 引擎渲染文本模板。
/// </summary>
public class ScribanTemplateRenderingEngine : TemplateRenderingEngineBase, ITransientDependency
{
    /// <summary>
    /// Scriban 渲染引擎的稳定名称。
    /// </summary>
    public const string EngineName = "Scriban";

    /// <summary>
    /// 获取 Scriban 渲染引擎的名称。
    /// </summary>
    public override string Name => EngineName;

    /// <summary>
    /// 初始化一个 <see cref="ScribanTemplateRenderingEngine"/> 实例。
    /// </summary>
    /// <param name="templateDefinitionManager">模板定义管理器。</param>
    /// <param name="templateContentProvider">模板内容提供程序。</param>
    public ScribanTemplateRenderingEngine(
        ITemplateDefinitionManager templateDefinitionManager, 
        ITemplateContentProvider templateContentProvider) 
        : base(templateDefinitionManager, templateContentProvider)
    {
    }

    /// <summary>
    /// 使用 Scriban 异步渲染指定模板。
    /// </summary>
    /// <param name="templateName">要渲染的模板名称。</param>
    /// <param name="model">传递给模板的模型，可为空。</param>
    /// <param name="cultureName">渲染使用的区域性名称；当前实现保留该参数以兼容统一渲染契约。</param>
    /// <param name="globalContext">传递给模板的全局上下文，可为空。</param>
    /// <returns>表示异步渲染操作的任务，结果为生成的模板文本。</returns>
    public override async Task<string> RenderAsync(string templateName, object model = null, string cultureName = null, Dictionary<string, object> globalContext = null)
    {
        Check.NotNullOrEmpty(templateName, nameof(templateName));
        globalContext ??= new Dictionary<string, object>();
        return await RenderInternalAsync(templateName, globalContext, model);
    }

    /// <summary>
    /// 递归渲染模板及其布局模板。
    /// </summary>
    /// <param name="templateName">要渲染的模板名称。</param>
    /// <param name="globalContext">模板共享的全局上下文。</param>
    /// <param name="model">传递给模板的模型，可为空。</param>
    /// <returns>表示异步渲染操作的任务，结果为模板及布局生成的文本。</returns>
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
    /// 读取并渲染单个模板定义，不处理其布局模板。
    /// </summary>
    /// <param name="templateDefinition">待渲染的模板定义。</param>
    /// <param name="globalContext">模板共享的全局上下文。</param>
    /// <param name="model">传递给模板的模型，可为空。</param>
    /// <returns>表示异步渲染操作的任务，结果为单个模板生成的文本。</returns>
    protected virtual async Task<string> RenderSingleTemplateAsync(TemplateDefinition templateDefinition, Dictionary<string, object> globalContext, object model = null)
    {
        var rawTemplateContent = await GetContentOrNullAsync(templateDefinition);
        return await RenderTemplateContentWithScribanAsync(templateDefinition, rawTemplateContent, globalContext, model);
    }

    /// <summary>
    /// 使用 Scriban 解析并异步渲染模板内容。
    /// </summary>
    /// <param name="templateDefinition">当前模板定义。</param>
    /// <param name="templateContent">待解析的模板内容。</param>
    /// <param name="globalContext">模板共享的全局上下文。</param>
    /// <param name="model">传递给模板的模型，可为空。</param>
    /// <returns>表示异步渲染操作的任务，结果为生成的文本。</returns>
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
    /// <returns>已创建并填充模板全局变量的 Scriban 上下文。</returns>
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