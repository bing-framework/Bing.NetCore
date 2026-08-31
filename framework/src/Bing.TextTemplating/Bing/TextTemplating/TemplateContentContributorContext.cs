namespace Bing.TextTemplating;

/// <summary>
/// 向模板内容贡献者提供模板定义、服务和区域性信息的运行上下文。
/// </summary>
public class TemplateContentContributorContext
{
    /// <summary>
    /// 获取正在构造内容的模板定义，始终非 <c>null</c>。
    /// </summary>
    public TemplateDefinition TemplateDefinition { get; }

    /// <summary>
    /// 获取用于解析模板相关服务的服务提供程序，始终非 <c>null</c>。
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 获取请求的区域性名称；未显式指定区域性时可以为 <c>null</c>。
    /// </summary>
    public string Culture { get; }

    /// <summary>
    /// 使用模板定义、服务提供程序和可选区域性初始化 <see cref="TemplateContentContributorContext"/> 的实例。
    /// </summary>
    /// <param name="templateDefinition">正在构造内容的模板定义。</param>
    /// <param name="serviceProvider">用于解析模板相关服务的服务提供程序。</param>
    /// <param name="culture">可选的区域性名称。</param>
    /// <exception cref="ArgumentNullException"><paramref name="templateDefinition"/> 或 <paramref name="serviceProvider"/> 为 <c>null</c> 时抛出。</exception>
    public TemplateContentContributorContext(
        TemplateDefinition templateDefinition,
        IServiceProvider serviceProvider, 
        string culture)
    {
        TemplateDefinition = templateDefinition ?? throw new ArgumentNullException(nameof(templateDefinition));
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Culture = culture;
    }
}
