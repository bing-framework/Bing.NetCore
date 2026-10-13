using Bing.DependencyInjection;

namespace Bing.TextTemplating;

/// <summary>
/// 提供模板定义提供程序的默认生命周期实现。
/// </summary>
public abstract class TemplateDefinitionProviderBase : ITemplateDefinitionProvider, ITransientDependency
{
    /// <inheritdoc />
    /// <remarks>
    /// 在模板定义正式创建前执行预处理；默认不执行任何操作。
    /// </remarks>
    public virtual void PreDefine(ITemplateDefinitionContext context)
    {
    }

    /// <inheritdoc />
    public abstract void Define(ITemplateDefinitionContext context);

    /// <inheritdoc />
    public virtual void PostDefine(ITemplateDefinitionContext context)
    {
    }
}