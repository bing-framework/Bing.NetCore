using Bing.DependencyInjection;

namespace Bing.TextTemplating;

/// <summary>
/// 模板定义提供程序基类
/// </summary>
public abstract class TemplateDefinitionProviderBase : ITemplateDefinitionProvider, ITransientDependency
{
    /// <summary>
    /// 定义之前
    /// </summary>
    /// <param name="context">模板定义上下文</param>
    public virtual void PreDefine(ITemplateDefinitionContext context)
    {
    }

    /// <summary>
    /// 定义
    /// </summary>
    /// <param name="context">模板定义上下文</param>
    public abstract void Define(ITemplateDefinitionContext context);

    /// <summary>
    /// 定义之后
    /// </summary>
    /// <param name="context">模板定义上下文</param>
    public virtual void PostDefine(ITemplateDefinitionContext context)
    {
    }
}