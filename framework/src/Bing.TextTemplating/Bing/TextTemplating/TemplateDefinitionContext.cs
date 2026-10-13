using System.Collections.Immutable;
using Bing.Collections;

namespace Bing.TextTemplating;

/// <summary>
/// 模板定义上下文
/// </summary>
public class TemplateDefinitionContext : ITemplateDefinitionContext
{
    /// <summary>
    /// 获取模板字典
    /// </summary>
    protected Dictionary<string, TemplateDefinition> Templates { get; }

    /// <summary>
    /// 初始化一个 <see cref="TemplateDefinitionContext"/> 类型的实例。
    /// </summary>
    /// <param name="templates">模板字典</param>
    public TemplateDefinitionContext(Dictionary<string, TemplateDefinition> templates) => Templates = templates;

    /// <inheritdoc />
    public virtual IReadOnlyList<TemplateDefinition> GetAll(string name) => Templates.Values.ToImmutableList();

    /// <inheritdoc />
    public virtual TemplateDefinition GetOrNull(string name) => Templates.GetOrDefault(name);

    /// <summary>
    /// 获取全部的模板定义列表
    /// </summary>
    /// <returns>当前上下文中的全部模板定义列表。</returns>
    public virtual IReadOnlyList<TemplateDefinition> GetAll() => Templates.Values.ToImmutableList();

    /// <inheritdoc />
    public void Add(params TemplateDefinition[] definitions)
    {
        if (definitions.IsNullOrEmpty())
            return;
        foreach (var definition in definitions)
            Templates[definition.Name] = definition;
    }
}