namespace Bing.TextTemplating;

/// <summary>
/// 定义模板定义生命周期中的扩展点。
/// </summary>
public interface ITemplateDefinitionProvider
{
    /// <summary>
    /// 在模板定义正式创建前修改或准备定义上下文。
    /// </summary>
    /// <param name="context">模板定义上下文。</param>
    void PreDefine(ITemplateDefinitionContext context);

    /// <summary>
    /// 向上下文中注册或更新模板定义。
    /// </summary>
    /// <param name="context">模板定义上下文。</param>
    void Define(ITemplateDefinitionContext context);

    /// <summary>
    /// 在所有模板定义完成后执行收尾处理。
    /// </summary>
    /// <param name="context">模板定义上下文。</param>
    void PostDefine(ITemplateDefinitionContext context);
}