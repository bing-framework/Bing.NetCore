namespace Bing.TextTemplating;

/// <summary>
/// 为模板定义和区域性提供内容的贡献者。
/// </summary>
public interface ITemplateContentContributor
{
    /// <summary>
    /// 异步获取模板内容。
    /// </summary>
    /// <param name="context">包含模板定义、服务提供程序和区域性的内容构造上下文。</param>
    /// <returns>可用的模板内容；当前贡献者未提供内容时返回 <c>null</c>。</returns>
    Task<string> GetOrNullAsync(TemplateContentContributorContext context);
}
