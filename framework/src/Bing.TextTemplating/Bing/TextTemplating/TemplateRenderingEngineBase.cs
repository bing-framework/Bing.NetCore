namespace Bing.TextTemplating;

/// <summary>
/// 提供模板渲染引擎的共享依赖和模板内容读取能力。
/// </summary>
public abstract class TemplateRenderingEngineBase : ITemplateRenderingEngine
{
    /// <summary>
    /// 获取当前渲染引擎的唯一名称。
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 获取模板定义管理器。
    /// </summary>
    protected ITemplateDefinitionManager TemplateDefinitionManager { get; }

    /// <summary>
    /// 获取模板内容提供程序。
    /// </summary>
    protected ITemplateContentProvider TemplateContentProvider { get; }

    /// <summary>
    /// 初始化一个 <see cref="TemplateRenderingEngineBase"/> 实例。
    /// </summary>
    /// <param name="templateDefinitionManager">模板定义管理器。</param>
    /// <param name="templateContentProvider">模板内容提供程序。</param>
    protected TemplateRenderingEngineBase(
        ITemplateDefinitionManager templateDefinitionManager, 
        ITemplateContentProvider templateContentProvider)
    {
        TemplateDefinitionManager = templateDefinitionManager;
        TemplateContentProvider = templateContentProvider;
    }

    /// <summary>
    /// 异步渲染指定模板并返回生成的文本内容。
    /// </summary>
    /// <param name="templateName">要渲染的模板名称。</param>
    /// <param name="model">传递给模板的模型，可为空。</param>
    /// <param name="cultureName">渲染使用的区域性名称，可为空。</param>
    /// <param name="globalContext">传递给模板的全局上下文，可为空。</param>
    /// <returns>表示异步渲染操作的任务，结果为生成的文本内容。</returns>
    public abstract Task<string> RenderAsync(string templateName, object model = null, string cultureName = null, Dictionary<string, object> globalContext = null);

    /// <summary>
    /// 异步获取指定模板定义对应的模板内容。
    /// </summary>
    /// <param name="templateDefinition">模板定义。</param>
    /// <returns>表示异步读取操作的任务，结果为模板内容；内容不存在时返回 <see langword="null"/>。</returns>
    protected virtual async Task<string> GetContentOrNullAsync(TemplateDefinition templateDefinition) => await TemplateContentProvider.GetContentOrNullAsync(templateDefinition);
}