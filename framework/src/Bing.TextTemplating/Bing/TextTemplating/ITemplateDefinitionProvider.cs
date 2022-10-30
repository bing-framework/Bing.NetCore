namespace Bing.TextTemplating;

/// <summary>
/// 模板定义提供程序
/// </summary>
public interface ITemplateDefinitionProvider
{
    /// <summary>
    /// 定义之前
    /// </summary>
    /// <param name="context">模板定义上下文</param>
    void PreDefine(ITemplateDefinitionContext context);

    /// <summary>
    /// 定义
    /// </summary>
    /// <param name="context">模板定义上下文</param>
    void Define(ITemplateDefinitionContext context);

    /// <summary>
    /// 定义之后
    /// </summary>
    /// <param name="context">模板定义上下文</param>
    void PostDefine(ITemplateDefinitionContext context);
}