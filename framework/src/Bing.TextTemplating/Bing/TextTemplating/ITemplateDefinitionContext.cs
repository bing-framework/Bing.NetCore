namespace Bing.TextTemplating;

/// <summary>
/// 模板定义上下文
/// </summary>
public interface ITemplateDefinitionContext
{
    /// <summary>
    /// 获取指定名称的模板定义列表
    /// </summary>
    /// <param name="name">模板名称</param>
    /// <returns>指定名称对应的模板定义列表。</returns>
    IReadOnlyList<TemplateDefinition> GetAll(string name);

    /// <summary>
    /// 获取指定名称的模板定义
    /// </summary>
    /// <param name="name">模板名称</param>
    /// <returns>指定名称对应的模板定义；未找到时返回 <see langword="null"/>。</returns>
    TemplateDefinition GetOrNull(string name);

    /// <summary>
    /// 添加
    /// </summary>
    /// <param name="definitions">模板定义数组</param>
    void Add(params TemplateDefinition[] definitions);
}
