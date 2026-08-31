namespace Bing.TextTemplating;

/// <summary>
/// 查询已注册模板定义的管理器。
/// </summary>
public interface ITemplateDefinitionManager
{
    /// <summary>
    /// 按名称获取模板定义。
    /// </summary>
    /// <param name="name">已注册模板名称。</param>
    /// <returns>匹配的模板定义。</returns>
    TemplateDefinition Get(string name);

    /// <summary>
    /// 获取所有已注册模板定义。
    /// </summary>
    /// <returns>模板定义的只读列表。</returns>
    IReadOnlyList<TemplateDefinition> GetAll();

    /// <summary>
    /// 按名称尝试获取模板定义。
    /// </summary>
    /// <param name="name">已注册模板名称。</param>
    /// <returns>匹配的模板定义；未注册时返回 <c>null</c>。</returns>
    TemplateDefinition GetOrNull(string name);
}
