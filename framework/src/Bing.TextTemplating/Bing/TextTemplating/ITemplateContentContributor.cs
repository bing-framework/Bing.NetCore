using System.Threading.Tasks;

namespace Bing.TextTemplating;

/// <summary>
/// 模板内容构造者
/// </summary>
public interface ITemplateContentContributor
{
    /// <summary>
    /// 获取模板内容
    /// </summary>
    /// <param name="context">模板内容构造者上下文</param>
    Task<string> GetOrNullAsync(TemplateContentContributorContext context);
}