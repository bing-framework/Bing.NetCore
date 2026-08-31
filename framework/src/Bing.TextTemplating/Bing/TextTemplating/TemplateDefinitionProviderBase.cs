using Bing.DependencyInjection;

namespace Bing.TextTemplating;

/// <summary>
/// 提供模板定义提供程序的默认生命周期实现。
/// </summary>
public abstract class TemplateDefinitionProviderBase : ITemplateDefinitionProvider, ITransientDependency
{
    /// <summary>
    /// 在模板定义正式创建前执行预处理；默认不执行任何操作。
    /// </summary>
    /// <param name="context">模板定义上下文。</param>
    public virtual void PreDefine(ITemplateDefinitionContext context)
    {
    }

    /// <summary>
    /// 注册或更新模板定义。
    /// </summary>
    /// <param name="context">模板定义上下文。</param>
    public abstract void Define(ITemplateDefinitionContext context);

    /// <summary>
    /// 在所有模板定义完成后执行收尾处理；默认不执行任何操作。
    /// </summary>
    /// <param name="context">模板定义上下文。</param>
    public virtual void PostDefine(ITemplateDefinitionContext context)
    {
    }
}